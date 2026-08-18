using OptimizeAll.SharedKernel.Guards;
using OptimizeAll.SharedKernel.Primitives;

namespace OptimizeAll.Domain.Common;

/// <summary>
/// An amount in a specific currency. Stored as <c>numeric(19,4)</c>; never as a floating-point
/// value, because binary floating point cannot represent 0.1 exactly and money that drifts by a
/// cent per operation is a defect that surfaces at audit time rather than at test time.
/// </summary>
public sealed class Money : ValueObject
{
    public const int Scale = 4;

    private Money(decimal amount, string currency)
    {
        Amount = decimal.Round(amount, Scale, MidpointRounding.ToEven);
        Currency = currency;
    }

    public decimal Amount { get; }

    /// <summary>ISO-4217 alphabetic code, upper case.</summary>
    public string Currency { get; }

    public bool IsZero => Amount == 0m;

    public static Money Zero(string currency) => new(0m, NormaliseCurrency(currency));

    public static Money Of(decimal amount, string currency) => new(amount, NormaliseCurrency(currency));

    public Money Add(Money other)
    {
        EnsureSameCurrency(other);
        return new Money(Amount + other.Amount, Currency);
    }

    public Money Subtract(Money other)
    {
        EnsureSameCurrency(other);
        return new Money(Amount - other.Amount, Currency);
    }

    public Money Multiply(decimal factor) => new(Amount * factor, Currency);

    public bool IsGreaterThan(Money other)
    {
        EnsureSameCurrency(other);
        return Amount > other.Amount;
    }

    public bool IsGreaterThanOrEqual(Money other)
    {
        EnsureSameCurrency(other);
        return Amount >= other.Amount;
    }

    public override string ToString() => $"{Amount.ToString("0.0000", System.Globalization.CultureInfo.InvariantCulture)} {Currency}";

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Amount;
        yield return Currency;
    }

    private static string NormaliseCurrency(string currency)
    {
        Ensure.NotNullOrWhiteSpace(currency);

        string normalised = currency.Trim().ToUpperInvariant();

        if (normalised.Length != 3 || !normalised.All(char.IsAsciiLetterUpper))
        {
            throw new ArgumentException(
                $"Currency must be a three-letter ISO-4217 alphabetic code; received '{currency}'.",
                nameof(currency));
        }

        return normalised;
    }

    private void EnsureSameCurrency(Money other)
    {
        ArgumentNullException.ThrowIfNull(other);

        if (!string.Equals(Currency, other.Currency, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Cannot combine amounts in different currencies ({Currency} and {other.Currency}). " +
                "Convert explicitly at a known rate first.");
        }
    }
}
