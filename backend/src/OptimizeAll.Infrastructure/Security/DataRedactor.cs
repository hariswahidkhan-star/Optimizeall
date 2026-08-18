using System.Text.RegularExpressions;
using OptimizeAll.Application.Abstractions.Platform;

namespace OptimizeAll.Infrastructure.Security;

/// <summary>
/// Removes obvious personal and secret data before content is written to durable storage.
/// <para>
/// This is data minimisation, not a guarantee. Pattern matching cannot recognise a person's name in
/// free text, so redaction reduces exposure rather than eliminating it. The controls that actually
/// bound the risk are retention limits, the per-agent <c>RedactPersonalData</c> memory policy, and
/// the erasure path — this makes the common cases cheap to get right.
/// </para>
/// <para>
/// Applied by default rather than opt-in: an agent's trace is a durable copy of everything that
/// passed through it, and an unredacted trace becomes an unmanaged personal-data store sitting
/// outside the erasure path.
/// </para>
/// </summary>
public sealed partial class DataRedactor : IDataRedactor
{
    private const string Placeholder = "[redacted]";

    public string Redact(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return content;
        }

        string redacted = EmailPattern().Replace(content, Placeholder);
        redacted = PhonePattern().Replace(redacted, Placeholder);
        redacted = CreditCardPattern().Replace(redacted, Placeholder);
        redacted = IbanPattern().Replace(redacted, Placeholder);
        redacted = BearerTokenPattern().Replace(redacted, $"Bearer {Placeholder}");
        redacted = ApiKeyPattern().Replace(redacted, $"$1{Placeholder}");

        return redacted;
    }

    public bool ContainsPersonalData(string content)
        => !string.IsNullOrWhiteSpace(content)
            && (EmailPattern().IsMatch(content)
                || PhonePattern().IsMatch(content)
                || CreditCardPattern().IsMatch(content)
                || IbanPattern().IsMatch(content));

    [GeneratedRegex(@"[\w.+-]+@[\w-]+\.[\w.-]+", RegexOptions.CultureInvariant)]
    private static partial Regex EmailPattern();

    // Deliberately conservative: requires an international prefix or a long digit run with
    // separators, so ordinary numbers in analytics output are not mangled into placeholders.
    [GeneratedRegex(@"\+\d{1,3}[\s.-]?\(?\d{2,4}\)?[\s.-]?\d{3,4}[\s.-]?\d{3,4}", RegexOptions.CultureInvariant)]
    private static partial Regex PhonePattern();

    [GeneratedRegex(@"\b(?:\d[ -]*?){13,19}\b", RegexOptions.CultureInvariant)]
    private static partial Regex CreditCardPattern();

    [GeneratedRegex(@"\b[A-Z]{2}\d{2}[A-Z0-9]{10,30}\b", RegexOptions.CultureInvariant)]
    private static partial Regex IbanPattern();

    [GeneratedRegex(@"Bearer\s+[A-Za-z0-9._~+/-]+=*", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex BearerTokenPattern();

    [GeneratedRegex(
        @"((?:api[_-]?key|secret|password|token)\s*[:=]\s*[""']?)[^\s""',}]+",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ApiKeyPattern();
}
