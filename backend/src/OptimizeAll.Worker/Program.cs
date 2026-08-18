using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OptimizeAll.Application;
using OptimizeAll.Application.Abstractions.Security;
using OptimizeAll.Infrastructure;
using OptimizeAll.Worker;
using OptimizeAll.Worker.Workers;
using Serilog;
using Serilog.Formatting.Compact;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSerilog((services, configuration) => configuration
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("service", "optimizeall-worker")
    .WriteTo.Console(new CompactJsonFormatter()));

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// Scoped, so each unit of work gets its own instance and one item's tenant scope can never bleed
// into the next.
builder.Services.AddScoped<WorkerTenantContext>();
builder.Services.AddScoped<ITenantContext>(provider => provider.GetRequiredService<WorkerTenantContext>());
builder.Services.TryAddSingleton<ICurrentPrincipal, SystemPrincipal>();

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("optimizeall-worker"))
    .WithTracing(tracing => tracing.AddHttpClientInstrumentation().AddOtlpExporter())
    .WithMetrics(metrics => metrics
        .AddHttpClientInstrumentation()
        .AddRuntimeInstrumentation()
        .AddOtlpExporter());

builder.Services.AddHostedService<AgentRunWorker>();
builder.Services.AddHostedService<LeaseReclaimWorker>();
builder.Services.AddHostedService<ApprovalExpiryWorker>();
builder.Services.AddHostedService<SchedulerWorker>();

IHost host = builder.Build();
await host.RunAsync();
