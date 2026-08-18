using OptimizeAll.Domain.Common;
using Xunit;

namespace OptimizeAll.Domain.Tests.Common;

public sealed class MoneyTests
{
    [Fact]
    public void Combining_different_currencies_throws_rather_than_guessing_a_rate()
        => Assert.Throws<InvalidOperationException>(
            () => Money.Of(10m, "USD").Add(Money.Of(10m, "EUR")));

    [Fact]
    public void Amounts_are_rounded_to_four_decimal_places_using_bankers_rounding()
        => Assert.Equal(1.2346m, Money.Of(1.23456m, "USD").Amount);

    [Fact]
    public void Currency_codes_are_normalised_to_upper_case()
        => Assert.Equal("USD", Money.Of(1m, "usd").Currency);

    [Theory]
    [InlineData("US")]
    [InlineData("USDD")]
    [InlineData("12A")]
    public void Malformed_currency_codes_are_rejected(string code)
        => Assert.Throws<ArgumentException>(() => Money.Of(1m, code));

    [Fact]
    public void Equal_amounts_in_the_same_currency_are_equal_values()
        => Assert.Equal(Money.Of(5m, "GBP"), Money.Of(5m, "GBP"));

    [Fact]
    public void Repeated_addition_does_not_drift()
    {
        Money total = Money.Zero("USD");

        for (int i = 0; i < 10; i++)
        {
            total = total.Add(Money.Of(0.10m, "USD"));
        }

        Assert.Equal(1.00m, total.Amount);
    }
}

public sealed class PermissionTests
{
    [Fact]
    public void An_exact_permission_satisfies_itself()
        => Assert.True(Permission.Parse("approval:decide").Satisfies(Permission.Parse("approval:decide")));

    [Fact]
    public void A_different_action_on_the_same_resource_is_not_satisfied()
        => Assert.False(Permission.Parse("approval:read").Satisfies(Permission.Parse("approval:decide")));

    [Fact]
    public void An_action_wildcard_satisfies_any_action_on_that_resource()
        => Assert.True(Permission.Parse("approval:*").Satisfies(Permission.Parse("approval:decide")));

    [Fact]
    public void An_action_wildcard_does_not_leak_across_resources()
        => Assert.False(Permission.Parse("approval:*").Satisfies(Permission.Parse("tenant:delete")));

    [Fact]
    public void A_full_wildcard_satisfies_everything()
        => Assert.True(Permission.Parse("*:*").Satisfies(Permission.Parse("tenant:delete")));

    [Theory]
    [InlineData("noseparator")]
    [InlineData("too:many:parts")]
    [InlineData(":action")]
    [InlineData("resource:")]
    public void Malformed_permissions_are_rejected(string value)
        => Assert.Throws<ArgumentException>(() => Permission.Parse(value));
}

public sealed class ActionRiskClassTests
{
    [Theory]
    [InlineData(ActionRiskClass.Read, false)]
    [InlineData(ActionRiskClass.Write, false)]
    [InlineData(ActionRiskClass.External, true)]
    [InlineData(ActionRiskClass.Financial, true)]
    [InlineData(ActionRiskClass.Irreversible, true)]
    public void Gating_is_a_property_of_the_risk_class_with_no_configuration_input(
        ActionRiskClass risk,
        bool expected)
        => Assert.Equal(expected, risk.RequiresHumanApproval());

    [Fact]
    public void Irreversible_actions_need_more_approvers_than_external_ones()
        => Assert.True(
            ActionRiskClass.Irreversible.MinimumApproverCount()
            > ActionRiskClass.External.MinimumApproverCount());
}
