using Anthropic;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenAI;
using OpenAI.Chat;
using OpenAI.Embeddings;
using OptimizeAll.Application.Abstractions.Ai;
using OptimizeAll.Application.Abstractions.Persistence;
using OptimizeAll.Application.Abstractions.Platform;
using OptimizeAll.Application.Abstractions.Security;
using OptimizeAll.Application.Abstractions.Tools;
using OptimizeAll.Infrastructure.Ai;
using OptimizeAll.Infrastructure.Ai.Providers;
using OptimizeAll.Infrastructure.Caching;
using OptimizeAll.Infrastructure.Persistence;
using OptimizeAll.Infrastructure.Persistence.Repositories;
using OptimizeAll.Infrastructure.Scheduling;
using OptimizeAll.Infrastructure.Security;
using OptimizeAll.Infrastructure.Tools;
using OptimizeAll.SharedKernel.Time;
using StackExchange.Redis;

namespace OptimizeAll.Infrastructure;

public sealed class InfrastructureOptions
{
    public const string SectionName = "Infrastructure";

    public required string PostgresConnectionString { get; init; }

    public required string RedisConnectionString { get; init; }

    /// <summary>Null in development, where secrets come from user-secrets instead of a vault.</summary>
    public string? KeyVaultUri { get; init; }

    public string? OpenAiApiKey { get; init; }

    public string? AnthropicApiKey { get; init; }

    public string? GoogleApiKey { get; init; }

    public string GeminiBaseUrl { get; init; } = "https://generativelanguage.googleapis.com/";

    public string EmbeddingModel { get; init; } = "text-embedding-3-small";
}

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        InfrastructureOptions options = configuration
            .GetSection(InfrastructureOptions.SectionName)
            .Get<InfrastructureOptions>()
            ?? throw new InvalidOperationException(
                $"The '{InfrastructureOptions.SectionName}' configuration section is missing.");

        services.Configure<ModelPricingOptions>(configuration.GetSection(ModelPricingOptions.SectionName));

        services.AddSingleton<IClock>(SystemClock.Instance);
        services.AddMemoryCache();

        AddPersistence(services, options);
        AddCaching(services, options);
        AddAiProviders(services, options);
        AddPlatformServices(services, options);
        AddTools(services);

        return services;
    }

    private static void AddPersistence(IServiceCollection services, InfrastructureOptions options)
    {
        services.AddDbContext<OptimizeAllDbContext>((provider, builder) =>
            builder.UseNpgsql(options.PostgresConnectionString, npgsql =>
            {
                npgsql.UseVector();

                // Transient faults are retried inside the provider so a brief failover does not
                // surface as a request failure. Retries are safe because every command runs inside
                // an explicit transaction that either commits whole or not at all.
                npgsql.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(5),
                    errorCodesToAdd: null);
            }));

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
        services.AddScoped<IAuditTrail, AuditTrail>();

        services.AddScoped<ITenantRepository, TenantRepository>();
        services.AddScoped<IWorkspaceRepository, WorkspaceRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<IAgentDefinitionRepository, AgentDefinitionRepository>();
        services.AddScoped<IAgentRunRepository, AgentRunRepository>();
        services.AddScoped<IApprovalRepository, ApprovalRepository>();
        services.AddScoped<IWorkflowRepository, WorkflowRepository>();
        services.AddScoped<IKnowledgeRepository, KnowledgeRepository>();
        services.AddScoped<IMemoryRepository, MemoryRepository>();
        services.AddScoped<IScheduleRepository, ScheduleRepository>();
        services.AddScoped<IOutboxRepository, OutboxRepository>();
        services.AddScoped<INotificationRepository, NotificationRepository>();
    }

    private static void AddCaching(IServiceCollection services, InfrastructureOptions options)
    {
        services.AddSingleton<IConnectionMultiplexer>(_ =>
        {
            ConfigurationOptions redisOptions = ConfigurationOptions.Parse(options.RedisConnectionString);

            // The platform stays up when Redis is briefly unavailable: queue operations fail and are
            // retried rather than the process failing to start.
            redisOptions.AbortOnConnectFail = false;
            redisOptions.ConnectRetry = 5;

            return ConnectionMultiplexer.Connect(redisOptions);
        });

        services.AddSingleton<IRunQueue, RedisRunQueue>();
        services.AddSingleton<IDistributedLock, RedisDistributedLock>();
    }

    private static void AddAiProviders(IServiceCollection services, InfrastructureOptions options)
    {
        services.AddSingleton<IModelPricing, ModelPricing>();

        // Each provider registers only when configured. An unconfigured provider is simply absent
        // from the router's rotation rather than present and failing on every call.
        if (!string.IsNullOrWhiteSpace(options.AnthropicApiKey))
        {
            services.AddSingleton(new AnthropicClient { ApiKey = options.AnthropicApiKey });
            services.AddSingleton<IChatCompletionService, AnthropicChatCompletionService>();
        }

        if (!string.IsNullOrWhiteSpace(options.OpenAiApiKey))
        {
            services.AddSingleton(new OpenAIClient(options.OpenAiApiKey));

            services.AddSingleton<Func<string, ChatClient>>(provider =>
            {
                OpenAIClient client = provider.GetRequiredService<OpenAIClient>();
                return model => client.GetChatClient(model);
            });

            services.AddSingleton<IChatCompletionService, OpenAiChatCompletionService>();

            services.AddSingleton(provider =>
                provider.GetRequiredService<OpenAIClient>().GetEmbeddingClient(options.EmbeddingModel));

            services.AddSingleton<IEmbeddingService, OpenAiEmbeddingService>();
        }

        if (!string.IsNullOrWhiteSpace(options.GoogleApiKey))
        {
            services.AddHttpClient<GeminiChatCompletionService>(client =>
            {
                client.BaseAddress = new Uri(options.GeminiBaseUrl);
                client.DefaultRequestHeaders.Add("x-goog-api-key", options.GoogleApiKey);
                client.Timeout = TimeSpan.FromMinutes(3);
            })
            .AddStandardResilienceHandler();

            services.AddSingleton<IChatCompletionService>(provider =>
                provider.GetRequiredService<GeminiChatCompletionService>());
        }

        services.AddSingleton<IChatCompletionRouter, ChatCompletionRouter>();
    }

    private static void AddPlatformServices(IServiceCollection services, InfrastructureOptions options)
    {
        services.AddScoped<IPermissionEvaluator, PermissionEvaluator>();
        services.AddSingleton<StepUpVerifier>();
        services.AddSingleton<IStepUpVerifier>(provider => provider.GetRequiredService<StepUpVerifier>());
        services.AddSingleton<IDataRedactor, DataRedactor>();
        services.AddSingleton<ICronEvaluator, CronosEvaluator>();

        if (!string.IsNullOrWhiteSpace(options.KeyVaultUri))
        {
            // Workload identity: no client secret is stored anywhere, and the credential is
            // scoped to the container app's managed identity.
            services.AddSingleton(new SecretClient(new Uri(options.KeyVaultUri), new DefaultAzureCredential()));
            services.AddScoped<ISecretResolver, KeyVaultSecretResolver>();
        }
    }

    private static void AddTools(IServiceCollection services)
    {
        services.AddScoped<IToolExecutor, KnowledgeSearchTool>();
        services.AddScoped<IToolExecutor, KnowledgeWriteTool>();
        services.AddScoped<IToolExecutor, AgentDelegateTool>();
        services.AddScoped<IToolExecutor, ContentDraftTool>();
        services.AddScoped<IToolExecutor, AnalyticsQueryTool>();

        services.AddScoped<IToolCatalog, ToolCatalog>();
    }
}
