using OptimizeAll.Domain.Governance;
using Xunit;

namespace OptimizeAll.Domain.Tests.Governance;

public sealed class CanonicalJsonTests
{
    [Fact]
    public void Object_keys_are_ordered_deterministically()
    {
        Assert.Equal(
            CanonicalJson.Canonicalise("""{"b":1,"a":2}"""),
            CanonicalJson.Canonicalise("""{"a":2,"b":1}"""));
    }

    [Fact]
    public void Nested_object_keys_are_ordered_too()
    {
        Assert.Equal(
            CanonicalJson.Canonicalise("""{"outer":{"z":1,"a":2}}"""),
            CanonicalJson.Canonicalise("""{"outer":{"a":2,"z":1}}"""));
    }

    [Fact]
    public void Array_order_is_preserved_because_it_is_semantically_significant()
    {
        Assert.NotEqual(
            CanonicalJson.Canonicalise("""{"recipients":["a","b"]}"""),
            CanonicalJson.Canonicalise("""{"recipients":["b","a"]}"""));
    }

    [Fact]
    public void Insignificant_whitespace_is_removed()
    {
        Assert.Equal(
            CanonicalJson.Canonicalise("""{"a":1}"""),
            CanonicalJson.Canonicalise("  {  \"a\" :  1  }  "));
    }

    [Fact]
    public void Equivalent_decimal_representations_converge()
    {
        Assert.Equal(
            CanonicalJson.Canonicalise("""{"amount":1.50}"""),
            CanonicalJson.Canonicalise("""{"amount":1.5}"""));
    }

    [Fact]
    public void Control_characters_in_strings_are_escaped()
    {
        string canonical = CanonicalJson.Canonicalise("{\"a\":\"line1\\nline2\"}");

        Assert.Contains("\\n", canonical, StringComparison.Ordinal);
        Assert.DoesNotContain('\n', canonical);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("{unclosed:")]
    public void Invalid_documents_are_rejected(string input)
        => Assert.False(CanonicalJson.IsValidJson(input));

    [Fact]
    public void Fingerprints_of_reordered_but_identical_payloads_match()
    {
        PayloadFingerprint left = PayloadFingerprint.Compute("""{"x":1,"y":[1,2],"z":{"b":2,"a":1}}""");
        PayloadFingerprint right = PayloadFingerprint.Compute("""{"z":{"a":1,"b":2},"y":[1,2],"x":1}""");

        Assert.True(left.Matches(right));
    }

    [Fact]
    public void Fingerprints_differ_when_a_single_character_changes()
    {
        PayloadFingerprint left = PayloadFingerprint.Compute("""{"body":"Send now"}""");
        PayloadFingerprint right = PayloadFingerprint.Compute("""{"body":"Send now."}""");

        Assert.False(left.Matches(right));
    }

    [Fact]
    public void A_fingerprint_round_trips_through_its_hexadecimal_form()
    {
        PayloadFingerprint original = PayloadFingerprint.Compute("""{"a":1}""");
        PayloadFingerprint restored = PayloadFingerprint.FromHex(original.Value);

        Assert.True(original.Matches(restored));
    }
}
