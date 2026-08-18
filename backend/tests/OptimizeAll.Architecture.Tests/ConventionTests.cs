using System.Reflection;
using NetArchTest.Rules;
using OptimizeAll.Application.Abstractions.Messaging;
using OptimizeAll.SharedKernel.Primitives;
using Xunit;

namespace OptimizeAll.Architecture.Tests;

/// <summary>
/// Conventions that carry a correctness or security consequence, not merely a stylistic one.
/// </summary>
public sealed class ConventionTests
{
    private static readonly Assembly Domain = typeof(Domain.Common.TenantId).Assembly;
    private static readonly Assembly Application = typeof(Application.DependencyInjection).Assembly;

    [Fact]
    public void Every_command_declares_the_permission_it_requires()
    {
        // A command without a declared permission passes through the authorisation behaviour
        // untouched. That is correct for a handful of unauthenticated endpoints and catastrophic
        // anywhere else, so the exemptions are listed explicitly rather than inferred.
        HashSet<string> exempt = new(StringComparer.Ordinal);

        List<Type> commands =
        [
            .. Application.GetTypes().Where(type =>
                type is { IsAbstract: false, IsInterface: false }
                && type.GetInterfaces().Any(i =>
                    i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICommand<>))),
        ];

        Assert.NotEmpty(commands);

        List<string> undeclared =
        [
            .. commands
                .Where(type => !typeof(IRequirePermission).IsAssignableFrom(type))
                .Select(type => type.Name)
                .Where(name => !exempt.Contains(name)),
        ];

        Assert.True(
            undeclared.Count == 0,
            $"These commands do not declare a required permission: {string.Join(", ", undeclared)}");
    }

    [Fact]
    public void Every_state_changing_command_is_auditable()
    {
        List<string> unaudited =
        [
            .. Application.GetTypes()
                .Where(type => type is { IsAbstract: false, IsInterface: false }
                    && type.GetInterfaces().Any(i =>
                        i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICommand<>))
                    && !typeof(IAuditableRequest).IsAssignableFrom(type))
                .Select(type => type.Name),
        ];

        Assert.True(
            unaudited.Count == 0,
            $"These commands change state without producing an audit event: {string.Join(", ", unaudited)}");
    }

    [Fact]
    public void Aggregate_roots_are_sealed()
    {
        // An inheritable aggregate root invites a subclass that bypasses the parent's invariants.
        TestResult result = Types.InAssembly(Domain)
            .That()
            .Inherit(typeof(AggregateRoot<>))
            .Should()
            .BeSealed()
            .GetResult();

        Assert.True(
            result.IsSuccessful,
            $"Aggregate roots must be sealed: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void Domain_events_are_immutable_records()
    {
        List<string> mutable =
        [
            .. Domain.GetTypes()
                .Where(type => typeof(IDomainEvent).IsAssignableFrom(type)
                    && type is { IsAbstract: false, IsInterface: false })
                .Where(type => type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Any(IsSettableAfterConstruction))
                .Select(type => type.Name),
        ];

        Assert.True(
            mutable.Count == 0,
            $"Domain events record what already happened and must be immutable: {string.Join(", ", mutable)}");
    }

    /// <summary>
    /// Distinguishes a real setter from an <c>init</c> accessor.
    /// <para>
    /// Through reflection both look like a public setter; the compiler marks an init accessor with a
    /// required <c>IsExternalInit</c> modifier on its return type. Without this distinction the
    /// check would reject every positional record, which is the very shape it exists to encourage.
    /// </para>
    /// </summary>
    private static bool IsSettableAfterConstruction(PropertyInfo property)
    {
        if (!property.CanWrite || property.SetMethod is not { IsPublic: true } setter)
        {
            return false;
        }

        return !setter.ReturnParameter
            .GetRequiredCustomModifiers()
            .Any(modifier => modifier.FullName == "System.Runtime.CompilerServices.IsExternalInit");
    }
}
