using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OptimizeAll.Api.Endpoints;
using OptimizeAll.Api.Middleware;
using OptimizeAll.Api.Security;
using OptimizeAll.Application;
using OptimizeAll.Application.Abstractions.Security;
using OptimizeAll.Infrastructure;
using Serilog;
using Serilog.Formatting.Compact;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Structured JSON logs from the first line, so a start-up failure is as diagnosable as a
// steady-state one. Request payloads are never logged — see LoggingBehaviour for why.
builder.Host.UseSerilog((context, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("service", "optimizeall-api")
    .WriteTo.Console(new CompactJsonFormatter()));

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ITenantContext, HttpTenantContext>();
builder.Services.AddScoped<ICurrentPrincipal, HttpCurrentPrincipal>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = builder.Configuration["Authentication:Authority"];
        options.Audience = builder.Configuration["Authentication:Audience"];
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,

            // Default clock skew is five minutes, which keeps a revoked or expired token usable for
            // that long. Thirty seconds tolerates real clock drift without extending a token's life
            // in any way an operator would find surprising.
            ClockSkew = TimeSpan.FromSeconds(30),
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "OptimizeAll API",
        Version = "v1",
        Description =
            "Enterprise AI operating system. Every external, financial or irreversible action " +
            "performed through this API is gated on human approval and recorded in an immutable " +
            "audit trail.",
    });

    options.AddSecurityDefinition("bearer", new()
    {
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "OAuth 2.0 bearer token issued by the configured identity provider.",
    });

    options.AddSecurityRequirement(new()
    {
        [new() { Reference = new() { Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme, Id = "bearer" } }] = [],
    });
});

// Partitioned per authenticated principal rather than per IP: several users behind one corporate
// egress address must not share a bucket, and one noisy tenant must not throttle another.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetTokenBucketLimiter(
            context.User.FindFirst("sub")?.Value
                ?? context.Connection.RemoteIpAddress?.ToString()
                ?? "anonymous",
            _ => new TokenBucketRateLimiterOptions
            {
                TokenLimit = 200,
                TokensPerPeriod = 100,
                ReplenishmentPeriod = TimeSpan.FromSeconds(10),
                QueueLimit = 0,
                AutoReplenishment = true,
            }));
});

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("optimizeall-api"))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddOtlpExporter())
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddRuntimeInstrumentation()
        .AddOtlpExporter());

builder.Services.AddHealthChecks();

builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [])
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()));

WebApplication app = builder.Build();

app.UseExceptionHandler();
app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

// Liveness answers "is the process up"; readiness answers "should traffic be sent here". Keeping
// them distinct stops a dependency outage from triggering a pointless restart loop.
app.MapHealthChecks("/health/live").AllowAnonymous();
app.MapHealthChecks("/health/ready").AllowAnonymous();

app.MapApprovalEndpoints();
app.MapRunEndpoints();
app.MapWorkspaceEndpoints();
app.MapWorkflowEndpoints();

await app.RunAsync();

/// <summary>Exposed so the integration test host can reference this entry point.</summary>
public partial class Program;
