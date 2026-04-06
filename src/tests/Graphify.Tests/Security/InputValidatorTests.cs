using Graphify.Security;
using Xunit;

namespace Graphify.Tests.Security;

public class InputValidatorTests
{
    private readonly InputValidator _validator = new();

    #region URL Validation Tests

    [Theory]
    [InlineData("https://example.com")]
    [InlineData("http://example.com")]
    [InlineData("https://api.github.com/repos/owner/repo")]
    public void ValidateUrl_ValidUrls_ReturnsSuccess(string url)
    {
        // Act
        var result = _validator.ValidateUrl(url);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Theory]
    [InlineData("ftp://example.com")]
    [InlineData("file:///etc/passwd")]
    [InlineData("javascript:alert('xss')")]
    public void ValidateUrl_InvalidScheme_ReturnsFailure(string url)
    {
        // Act
        var result = _validator.ValidateUrl(url);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("scheme", result.Errors[0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateUrl_Localhost_ReturnsFailure()
    {
        // Arrange
        var url = "http://localhost";

        // Act
        var result = _validator.ValidateUrl(url);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("localhost", result.Errors[0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateUrl_LoopbackIp_ReturnsFailure()
    {
        // Arrange
        var url = "http://127.0.0.1";

        // Act
        var result = _validator.ValidateUrl(url);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("private", result.Errors[0], StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("http://10.0.0.1")]
    [InlineData("http://192.168.1.1")]
    [InlineData("http://172.16.0.1")]
    [InlineData("http://172.31.255.255")]
    public void ValidateUrl_PrivateIpUrls_ReturnsFailure(string url)
    {
        // Act
        var result = _validator.ValidateUrl(url);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("private", result.Errors[0], StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void ValidateUrl_NullOrEmpty_ReturnsFailure(string? url)
    {
        // Act
        var result = _validator.ValidateUrl(url!);

        // Assert
        Assert.False(result.IsValid);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public void ValidateUrl_InvalidFormat_ReturnsFailure()
    {
        // Arrange
        var url = "not-a-valid-url";

        // Act
        var result = _validator.ValidateUrl(url);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("Invalid URL format", result.Errors[0]);
    }

    #endregion

    #region Path Validation Tests

    [Fact]
    public void ValidatePath_ValidPath_ReturnsSuccess()
    {
        // Arrange
        var path = Path.Combine("src", "test.cs");

        // Act
        var result = _validator.ValidatePath(path);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Theory]
    [InlineData("..\\..\\etc\\passwd")]
    [InlineData("../../../etc/passwd")]
    [InlineData("test\\..\\..\\sensitive")]
    public void ValidatePath_PathTraversal_ReturnsFailure(string path)
    {
        // Act
        var result = _validator.ValidatePath(path);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("traversal", result.Errors[0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidatePath_NullBytes_ReturnsFailure()
    {
        // Arrange
        var path = "test\0file.txt";

        // Act
        var result = _validator.ValidatePath(path);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("null bytes", result.Errors[0], StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void ValidatePath_NullOrEmpty_ReturnsFailure(string? path)
    {
        // Act
        var result = _validator.ValidatePath(path!);

        // Assert
        Assert.False(result.IsValid);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public void ValidatePath_WithinBaseDirectory_ReturnsSuccess()
    {
        // Arrange
        var baseDir = Path.GetTempPath();
        var path = Path.Combine(baseDir, "test.txt");

        // Act
        var result = _validator.ValidatePath(path, baseDir);

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidatePath_OutsideBaseDirectory_ReturnsFailure()
    {
        // Arrange
        var baseDir = Path.Combine(Path.GetTempPath(), "restricted");
        Directory.CreateDirectory(baseDir);
        var path = Path.GetTempPath();

        try
        {
            // Act
            var result = _validator.ValidatePath(path, baseDir);

            // Assert
            Assert.False(result.IsValid);
            Assert.Contains("escapes", result.Errors[0], StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(baseDir))
            {
                Directory.Delete(baseDir);
            }
        }
    }

    #endregion

    #region Label Sanitization Tests

    [Fact]
    public void SanitizeLabel_CleanLabel_ReturnsUnchanged()
    {
        // Arrange
        var label = "Clean Label";

        // Act
        var result = _validator.SanitizeLabel(label);

        // Assert
        Assert.True(result.IsValid);
        Assert.Equal("Clean Label", result.SanitizedValue);
    }

    [Fact]
    public void SanitizeLabel_HtmlTags_RemovesTags()
    {
        // Arrange
        var label = "<script>alert('xss')</script>Hello";

        // Act
        var result = _validator.SanitizeLabel(label);

        // Assert
        Assert.True(result.IsValid);
        Assert.DoesNotContain("<script>", result.SanitizedValue);
        Assert.Contains("Hello", result.SanitizedValue);
    }

    [Fact]
    public void SanitizeLabel_ControlCharacters_RemovesControlChars()
    {
        // Arrange
        var label = "Hello\x01\x1fWorld";

        // Act
        var result = _validator.SanitizeLabel(label);

        // Assert
        Assert.True(result.IsValid);
        Assert.DoesNotContain("\x01", result.SanitizedValue);
        Assert.DoesNotContain("\x1f", result.SanitizedValue);
        Assert.Contains("Hello", result.SanitizedValue);
        Assert.Contains("World", result.SanitizedValue);
    }

    [Fact]
    public void SanitizeLabel_ExceedsMaxLength_Truncates()
    {
        // Arrange
        var label = new string('A', 300);
        var maxLength = 100;

        // Act
        var result = _validator.SanitizeLabel(label, maxLength);

        // Assert
        Assert.True(result.IsValid);
        Assert.True(result.SanitizedValue!.Length <= maxLength);
    }

    [Fact]
    public void SanitizeLabel_SpecialCharacters_HtmlEncodes()
    {
        // Arrange
        var label = "Test & <special> \"chars\"";

        // Act
        var result = _validator.SanitizeLabel(label);

        // Assert
        Assert.True(result.IsValid);
        Assert.Contains("&amp;", result.SanitizedValue);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void SanitizeLabel_EmptyInput_ReturnsEmptySuccess(string? label)
    {
        // Act
        var result = _validator.SanitizeLabel(label!);

        // Assert
        Assert.True(result.IsValid);
        Assert.Equal(string.Empty, result.SanitizedValue);
    }

    #endregion

    #region Input Validation Tests

    [Fact]
    public void ValidateInput_CleanInput_ReturnsSuccess()
    {
        // Arrange
        var input = "This is clean input text";

        // Act
        var result = _validator.ValidateInput(input);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void ValidateInput_ExceedsMaxLength_ReturnsFailure()
    {
        // Arrange
        var input = new string('A', 1500);

        // Act
        var result = _validator.ValidateInput(input);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("maximum length", result.Errors[0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateInput_NullBytes_ReturnsFailure()
    {
        // Arrange
        var input = "test\0input";

        // Act
        var result = _validator.ValidateInput(input);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("null bytes", result.Errors[0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateInput_ControlCharacters_ReturnsFailure()
    {
        // Arrange
        var input = "test\x01\x02input";

        // Act
        var result = _validator.ValidateInput(input);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("control characters", result.Errors[0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateInput_ManyInjectionPatterns_ReturnsFailure()
    {
        // Arrange - More than 10% special injection characters
        var input = "''''''''''''''''''''''''''''''";

        // Act
        var result = _validator.ValidateInput(input);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("injection", result.Errors[0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateInput_NormalSpecialChars_ReturnsSuccess()
    {
        // Arrange - Less than 10% special chars
        var input = "SELECT * FROM users WHERE name = 'test'";

        // Act
        var result = _validator.ValidateInput(input);

        // Assert
        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidateInput_EmptyOrWhitespace_ReturnsFailure(string input)
    {
        // Act
        var result = _validator.ValidateInput(input);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("empty", result.Errors[0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateInput_NullInput_ReturnsFailure()
    {
        // Act
        var result = _validator.ValidateInput(null!);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("null", result.Errors[0], StringComparison.OrdinalIgnoreCase);
    }

    #endregion
}
