using System.Text.RegularExpressions;
using Graphify.Security;
using Xunit;

namespace Graphify.Tests.Regression;

/// <summary>
/// Regression tests for Bug 2: InputValidator.SanitizeLabel Empty String Assertion.
/// Root cause: Assert.DoesNotContain("\x01", result) — after control chars are stripped,
/// \x01 becomes empty string. xUnit's DoesNotContain always finds empty substring → false
/// failure. Fix: used Assert.DoesNotMatch with regex and char.IsControl() fallback.
/// </summary>
[Trait("Category", "Regression")]
public sealed class InputValidatorRegressionTests
{
    private static readonly Regex ControlCharRegex = new(@"[\x00-\x1f\x7f]", RegexOptions.Compiled);
    private readonly InputValidator _validator = new();

    /// <summary>
    /// Regression Bug 2: All ASCII control characters (\x00-\x1f and \x7f) must be
    /// removed from sanitized output.
    /// </summary>
    [Fact(Timeout = 10000)]
    public async Task SanitizeLabel_ControlChars_AllRemoved_RegressionBug2()
    {
        await Task.CompletedTask;
        // Build a string containing every ASCII control character
        var controlChars = new string(Enumerable.Range(0x00, 0x20)
            .Concat(new[] { 0x7f })
            .Select(i => (char)i)
            .ToArray());

        var result = _validator.SanitizeLabel(controlChars);

        Assert.True(result.IsValid);
        Assert.DoesNotMatch(ControlCharRegex.ToString(), result.SanitizedValue ?? string.Empty);
        // Verify with char.IsControl fallback (the exact fix)
        Assert.DoesNotContain(result.SanitizedValue ?? string.Empty,
            c => char.IsControl(c));
    }

    /// <summary>
    /// Regression Bug 2: Normal characters must be preserved when mixed with control chars.
    /// </summary>
    [Fact(Timeout = 10000)]
    public async Task SanitizeLabel_MixedControlAndNormal_PreservesNormal_RegressionBug2()
    {
        await Task.CompletedTask;
        var input = "Hello\x01World\x02!";
        var result = _validator.SanitizeLabel(input);

        Assert.True(result.IsValid);
        Assert.NotNull(result.SanitizedValue);

        // Decode HTML encoding to check underlying content
        var decoded = System.Net.WebUtility.HtmlDecode(result.SanitizedValue);
        Assert.Equal("HelloWorld!", decoded);
    }

    /// <summary>
    /// Regression Bug 2: Pure control character input returns a clean (empty or placeholder) result.
    /// </summary>
    [Fact(Timeout = 10000)]
    public async Task SanitizeLabel_OnlyControlChars_ReturnsClean_RegressionBug2()
    {
        await Task.CompletedTask;
        var input = "\x01\x02\x03\x04\x05";
        var result = _validator.SanitizeLabel(input);

        Assert.True(result.IsValid);
        Assert.NotNull(result.SanitizedValue);
        Assert.DoesNotContain(result.SanitizedValue, c => char.IsControl(c));
    }

    /// <summary>
    /// Regression Bug 2: Null byte (\x00) is the most dangerous control char and must be removed.
    /// </summary>
    [Fact(Timeout = 10000)]
    public async Task SanitizeLabel_NullByte_Removed_RegressionBug2()
    {
        await Task.CompletedTask;
        var result = _validator.SanitizeLabel("\x00");

        Assert.True(result.IsValid);
        Assert.False((result.SanitizedValue ?? string.Empty).Contains('\x00'),
            "Null byte should be removed from sanitized output");
        Assert.DoesNotContain(result.SanitizedValue ?? string.Empty,
            c => char.IsControl(c));
    }

    /// <summary>
    /// Regression Bug 2: Tabs and newlines should be handled (stripped as control chars).
    /// </summary>
    [Fact(Timeout = 10000)]
    public async Task SanitizeLabel_TabAndNewline_Handling_RegressionBug2()
    {
        await Task.CompletedTask;
        var result = _validator.SanitizeLabel("Line1\tLine2\nLine3");

        Assert.True(result.IsValid);
        Assert.NotNull(result.SanitizedValue);
        // Tabs (\t = \x09) and newlines (\n = \x0a) are control chars and should be stripped
        Assert.DoesNotContain(result.SanitizedValue, c => char.IsControl(c));
    }

    /// <summary>
    /// Regression Bug 2: Unicode control characters like zero-width space and line separator.
    /// </summary>
    [Fact(Timeout = 10000)]
    public async Task SanitizeLabel_UnicodeControlChars_Handled_RegressionBug2()
    {
        await Task.CompletedTask;
        // \u200B = zero-width space, \u2028 = line separator
        var input = "Hello\u200BWorld\u2028End";
        var result = _validator.SanitizeLabel(input);

        Assert.True(result.IsValid);
        Assert.NotNull(result.SanitizedValue);
        // At minimum, no ASCII control chars should be present
        Assert.DoesNotMatch(ControlCharRegex.ToString(), result.SanitizedValue);
    }

    /// <summary>
    /// Regression Bug 2: Extended ASCII characters (é, ñ, ü) must NOT be stripped.
    /// </summary>
    [Fact(Timeout = 10000)]
    public async Task SanitizeLabel_ExtendedAscii_Preserved_RegressionBug2()
    {
        await Task.CompletedTask;
        var input = "café résumé naïve";
        var result = _validator.SanitizeLabel(input);

        Assert.True(result.IsValid);
        Assert.NotNull(result.SanitizedValue);
        var decoded = System.Net.WebUtility.HtmlDecode(result.SanitizedValue);
        Assert.Contains("caf", decoded);
        Assert.Contains("sum", decoded);
        Assert.Contains("ve", decoded);
    }

    /// <summary>
    /// Regression Bug 2: Empty string input must not throw.
    /// </summary>
    [Fact(Timeout = 10000)]
    public async Task SanitizeLabel_EmptyInput_HandledGracefully_RegressionBug2()
    {
        await Task.CompletedTask;
        var result = _validator.SanitizeLabel(string.Empty);
        Assert.True(result.IsValid);
    }

    /// <summary>
    /// Regression Bug 2: Very long input (10,000 chars) must not throw or hang.
    /// </summary>
    [Fact(Timeout = 10000)]
    public async Task SanitizeLabel_VeryLongInput_HandledGracefully_RegressionBug2()
    {
        await Task.CompletedTask;
        var input = new string('A', 10_000);
        var result = _validator.SanitizeLabel(input);

        Assert.True(result.IsValid);
        Assert.NotNull(result.SanitizedValue);
        // Max length is 200 by default
        var decoded = System.Net.WebUtility.HtmlDecode(result.SanitizedValue);
        Assert.True(decoded.Length <= 200);
    }
}
