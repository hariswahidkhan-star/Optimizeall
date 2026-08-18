using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using OptimizeAll.Application.Abstractions.Security;
using OptimizeAll.Domain.Common;
using OptimizeAll.SharedKernel.Time;

namespace OptimizeAll.Infrastructure.Persistence;

/// <summary>
/// Used only by the EF Core tooling to build the model for migration scaffolding.
/// <para>
/// The stub context it supplies never reaches a running application: at runtime the real tenant
/// context is resolved per request from the validated token. The connection string is a placeholder
/// because migration scaffolding builds the model without connecting.
/// </para>
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<OptimizeAllDbContext>
{
    public OptimizeAllDbContext CreateDbContext(string[] args)
    {
        DbContextOptionsBuilder<OptimizeAllDbContext> builder = new();

        builder.UseNpgsql(
            Environment.GetEnvironmentVariable("OPTIMIZEALL_DESIGN_CONNECTION")
                ?? "Host=localhost;Database=optimizeall_design;Username=postgres;Password=postgres",
            npgsql => npgsql.UseVector());

        return new OptimizeAllDbContext(
            builder.Options,
            new DesignTimeTenantContext(),
            new DesignTimePrincipal(),
            SystemClock.Instance);
    }

    private sealed class DesignTimeTenantContext : ITenantContext
    {
        public TenantId TenantId { get; } = TenantId.From(Guid.Empty);

        public WorkspaceId? WorkspaceId => null;

        public EnvironmentTier Environment => EnvironmentTier.Development;

        public bool IsPlatformEscalation => false;

        public string CorrelationId { get; } = Guid.Empty.ToString();
    }

    private sealed class DesignTimePrincipal : ICurrentPrincipal
    {
        public PrincipalRef Principal { get; } = PrincipalRef.System;

        public bool IsAuthenticated => false;

        public IReadOnlySet<string> Permissions { get; } = new HashSet<string>(StringComparer.Ordinal);

        public bool StepUpVerified => false;

        public string? IpAddress => null;
    }
}
