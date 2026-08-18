using System.Buffers;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace OptimizeAll.Domain.Governance;

/// <summary>
/// Produces a byte-stable serialisation of a JSON value so that two structurally identical payloads
/// always hash to the same fingerprint.
/// <para>
/// This is the foundation of the approval guarantee. Without a canonical form, re-serialising an
/// approved payload with a different property order — which JSON libraries are free to do — would
/// change its hash and either break every execution or, worse, tempt someone to compare payloads
/// loosely instead.
/// </para>
/// <para>
/// The form follows RFC 8785 (JSON Canonicalisation Scheme) in the ways that matter here: object
/// members sorted by key in UTF-16 code-unit order, no insignificant whitespace, and shortest
/// round-trippable number formatting. It is not a certified RFC 8785 implementation; correctness
/// here depends only on both sides of the comparison using this same function, which they do.
/// </para>
/// </summary>
public static class CanonicalJson
{
    /// <summary>Canonicalises a JSON document supplied as text. Throws when the input is not valid JSON.</summary>
    public static string Canonicalise(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        using JsonDocument document = JsonDocument.Parse(json, new JsonDocumentOptions
        {
            CommentHandling = JsonCommentHandling.Disallow,
            AllowTrailingCommas = false,
        });

        StringBuilder builder = new(json.Length);
        WriteElement(document.RootElement, builder);
        return builder.ToString();
    }

    public static bool IsValidJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        try
        {
            using JsonDocument _ = JsonDocument.Parse(json);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static void WriteElement(JsonElement element, StringBuilder builder)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                WriteObject(element, builder);
                break;

            case JsonValueKind.Array:
                WriteArray(element, builder);
                break;

            case JsonValueKind.String:
                WriteString(element.GetString()!, builder);
                break;

            case JsonValueKind.Number:
                WriteNumber(element, builder);
                break;

            case JsonValueKind.True:
                builder.Append("true");
                break;

            case JsonValueKind.False:
                builder.Append("false");
                break;

            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
                builder.Append("null");
                break;

            default:
                throw new JsonException($"Unsupported JSON value kind '{element.ValueKind}'.");
        }
    }

    private static void WriteObject(JsonElement element, StringBuilder builder)
    {
        // Ordinal ordering on the UTF-16 representation, which is what RFC 8785 specifies.
        List<JsonProperty> properties = [.. element.EnumerateObject()];
        properties.Sort(static (left, right) => string.CompareOrdinal(left.Name, right.Name));

        builder.Append('{');

        for (int index = 0; index < properties.Count; index++)
        {
            if (index > 0)
            {
                builder.Append(',');
            }

            WriteString(properties[index].Name, builder);
            builder.Append(':');
            WriteElement(properties[index].Value, builder);
        }

        builder.Append('}');
    }

    private static void WriteArray(JsonElement element, StringBuilder builder)
    {
        // Array order is significant and is preserved: [1,2] and [2,1] are different payloads.
        builder.Append('[');

        bool first = true;

        foreach (JsonElement item in element.EnumerateArray())
        {
            if (!first)
            {
                builder.Append(',');
            }

            WriteElement(item, builder);
            first = false;
        }

        builder.Append(']');
    }

    private static void WriteNumber(JsonElement element, StringBuilder builder)
    {
        // Integers keep their exact representation; anything else uses the shortest round-trip form
        // so that 1.0 and 1.00 converge rather than producing two hashes for one value.
        if (element.TryGetInt64(out long integral))
        {
            builder.Append(integral.ToString(CultureInfo.InvariantCulture));
            return;
        }

        if (element.TryGetDecimal(out decimal exact))
        {
            // decimal preserves trailing zeros (1.50m and 1.5m are distinct representations of the
            // same value), so they are stripped here. Without this, a payload re-serialised with a
            // different scale would fingerprint differently and fail an otherwise valid approval.
            string text = exact.ToString(CultureInfo.InvariantCulture);

            if (text.Contains('.', StringComparison.Ordinal))
            {
                text = text.TrimEnd('0').TrimEnd('.');
            }

            builder.Append(text.Length == 0 ? "0" : text);
            return;
        }

        double approximate = element.GetDouble();
        builder.Append(approximate.ToString("R", CultureInfo.InvariantCulture));
    }

    private static void WriteString(string value, StringBuilder builder)
    {
        builder.Append('"');

        foreach (char character in value)
        {
            switch (character)
            {
                case '"':
                    builder.Append("\\\"");
                    break;

                case '\\':
                    builder.Append("\\\\");
                    break;

                case '\b':
                    builder.Append("\\b");
                    break;

                case '\f':
                    builder.Append("\\f");
                    break;

                case '\n':
                    builder.Append("\\n");
                    break;

                case '\r':
                    builder.Append("\\r");
                    break;

                case '\t':
                    builder.Append("\\t");
                    break;

                default:
                    if (character < 0x20)
                    {
                        builder.Append(CultureInfo.InvariantCulture, $"\\u{(int)character:x4}");
                    }
                    else
                    {
                        builder.Append(character);
                    }

                    break;
            }
        }

        builder.Append('"');
    }
}
