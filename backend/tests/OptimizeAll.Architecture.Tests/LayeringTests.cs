using System.Reflection;
using NetArchTest.Rules;
using Xunit;

namespace OptimizeAll.Architecture.Tests;

/// <summary>
/// Enforces the dependency rule.
/// <para>
/// A README saying "dependencies point inward" describes an intention; a failing build enforces
/// one. Layering decays through single, locally reasonable exceptions — one handler that reaches
/// for a DbContext because it is faster — and each one is invisible in review until the layer no
/// longer means anything.
/// </para>
/// </summary>
public sealed class LayeringTests
{
    private static readonly Assembly SharedKernel = typeof(SharedKernel.Results.Result).Assembly;
    private static readonly Assembly Domain = typeof(Domain.Common.TenantId).Assembly;
    private static readonly Assembly Application = typeof(Application.DependencyInjection).Assembly;
    private static readonly Assembly Infrastructure = typeof(Infrastructure.DependencyInjection).Assembly;

    [Fact]
    public void Domain_does_not_depend_on_any_other_layer()
    {
        TestResult result = Types.InAssembly(Domain)
            .Should()
            .NotHaveDependencyOnAny(
                "OptimizeAll.Application",
                "OptimizeAll.Infrastructure",
                "OptimizeAll.Api",
                "OptimizeAll.Worker")
            .GetResult();

        AssertPasses(result, "Domain must not depend on an outer layer.");
    }

    [Fact]
    public void Domain_does_not_depend_on_infrastructure_technologies()
    {
        // The domain is where the business rules live, and they must be testable without a database,
        // a cache, or an HTTP client anywhere in sight.
        TestResult result = Types.InAssembly(Domain)
            .Should()
            .NotHaveDependencyOnAny(
                "Microsoft.EntityFrameworkCore",
                "Npgsql",
                "StackExchange.Redis",
                "Microsoft.AspNetCore",
                "Azure",
                "Anthropic",
                "OpenAI")
            .GetResult();

        AssertPasses(result, "Domain must stay free of infrastructure dependencies.");
    }

    [Fact]
    public void SharedKernel_depends_on_nothing_in_the_platform()
    {
        TestResult result = Types.InAssembly(SharedKernel)
            .Should()
            .NotHaveDependencyOnAny(
                "OptimizeAll.Domain",
                "OptimizeAll.Application",
                "OptimizeAll.Infrastructure",
                "OptimizeAll.Api",
                "OptimizeAll.Worker")
            .GetResult();

        AssertPasses(result, "SharedKernel must not depend on any platform layer.");
    }

    [Fact]
    public void Application_does_not_depend_on_infrastructure()
    {
        TestResult result = Types.InAssembly(Application)
            .Should()
            .NotHaveDependencyOnAny("OptimizeAll.Infrastructure", "OptimizeAll.Api", "OptimizeAll.Worker")
            .GetResult();

        AssertPasses(result, "Application must depend only on Domain and SharedKernel.");
    }

    [Fact]
    public void Application_does_not_reference_a_database_provider()
    {
        // If the Application layer could see EF Core, a handler could write a query — and with it,
        // one that forgets the tenant filter.
        TestResult result = Types.InAssembly(Application)
            .Should()
            .NotHaveDependencyOnAny("Microsoft.EntityFrameworkCore", "Npgsql", "StackExchange.Redis")
            .GetResult();

        AssertPasses(result, "Application must reach persistence only through its ports.");
    }

    [Fact]
    public void No_provider_sdk_type_escapes_the_infrastructure_layer()
    {
        // The load-bearing property behind "providers are interchangeable". If an Anthropic or
        // OpenAI type appeared in Application, swapping vendors would stop being configuration.
        TestResult result = Types.InAssembly(Application)
            .Should()
            .NotHaveDependencyOnAny("Anthropic", "OpenAI", "Azure")
            .GetResult();

        AssertPasses(result, "No AI provider SDK type may cross the Infrastructure boundary.");
    }

    [Fact]
    public void Infrastructure_does_not_depend_on_the_hosts()
    {
        TestResult result = Types.InAssembly(Infrastructure)
            .Should()
            .NotHaveDependencyOnAny("OptimizeAll.Api", "OptimizeAll.Worker")
            .GetResult();

        AssertPasses(result, "Infrastructure must not depend on a composition root.");
    }

    private static void AssertPasses(TestResult result, string because)
    {
        string offenders = result.FailingTypeNames is null
            ? string.Empty
            : string.Join(", ", result.FailingTypeNames);

        Assert.True(result.IsSuccessful, $"{because} Offending types: {offenders}");
    }
}
