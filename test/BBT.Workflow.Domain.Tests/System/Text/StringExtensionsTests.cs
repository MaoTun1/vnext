using Xunit;

namespace System.Text;

/// <summary>
/// Unit tests for StringExtensions
/// </summary>
public class StringExtensionsTests
{
    [Fact]
    public void ToVariableName_WithHyphenatedString_ShouldConvertToCamelCase()
    {
        // Arrange
        var input = "user-info";

        // Act
        var result = input.ToVariableName();

        // Assert
        Assert.Equal("userInfo", result);
    }

    [Fact]
    public void ToVariableName_WithMultipleHyphens_ShouldConvertToCamelCase()
    {
        // Arrange
        var input = "create-user-task";

        // Act
        var result = input.ToVariableName();

        // Assert
        Assert.Equal("createUserTask", result);
    }

    [Fact]
    public void ToVariableName_WithUnderscores_ShouldConvertToCamelCase()
    {
        // Arrange
        var input = "user_info_data";

        // Act
        var result = input.ToVariableName();

        // Assert
        Assert.Equal("userInfoData", result);
    }

    [Fact]
    public void ToVariableName_WithMixedSeparators_ShouldConvertToCamelCase()
    {
        // Arrange
        var input = "user-info_data task";

        // Act
        var result = input.ToVariableName();

        // Assert
        Assert.Equal("userInfoDataTask", result);
    }

    [Fact]
    public void ToVariableName_WithSingleWord_ShouldReturnLowercase()
    {
        // Arrange
        var input = "UserInfo";

        // Act
        var result = input.ToVariableName();

        // Assert
        Assert.Equal("userinfo", result);
    }

    [Fact]
    public void ToVariableName_WithUppercaseWords_ShouldConvertToCamelCase()
    {
        // Arrange
        var input = "USER-INFO-DATA";

        // Act
        var result = input.ToVariableName();

        // Assert
        Assert.Equal("userInfoData", result);
    }

    [Fact]
    public void ToVariableName_WithLeadingNumber_ShouldPrependUnderscore()
    {
        // Arrange
        var input = "123-user-info";

        // Act
        var result = input.ToVariableName();

        // Assert
        Assert.StartsWith("_", result);
    }

    [Fact]
    public void ToVariableName_WithValidVariableName_ShouldKeepFirstWordLowercase()
    {
        // Arrange
        var input = "userName";

        // Act
        var result = input.ToVariableName();

        // Assert
        Assert.Equal("username", result);
    }

    [Fact]
    public void ToVariableName_WithEmptyString_ShouldReturnEmpty()
    {
        // Arrange
        var input = "";

        // Act
        var result = input.ToVariableName();

        // Assert
        Assert.Equal("", result);
    }

    [Fact]
    public void ToVariableName_WithWhitespaceOnly_ShouldReturnOriginal()
    {
        // Arrange
        var input = "   ";

        // Act
        var result = input.ToVariableName();

        // Assert
        Assert.Equal("   ", result);
    }

    [Fact]
    public void ToVariableName_WithMultipleConsecutiveSeparators_ShouldHandleCorrectly()
    {
        // Arrange
        var input = "user---info___data";

        // Act
        var result = input.ToVariableName();

        // Assert
        Assert.Equal("userInfoData", result);
    }

    [Fact]
    public void ToVariableName_WithTrailingSeparators_ShouldIgnore()
    {
        // Arrange
        var input = "user-info---";

        // Act
        var result = input.ToVariableName();

        // Assert
        Assert.Equal("userInfo", result);
    }

    [Fact]
    public void ToVariableName_WithLeadingSeparators_ShouldIgnore()
    {
        // Arrange
        var input = "---user-info";

        // Act
        var result = input.ToVariableName();

        // Assert
        Assert.Equal("userInfo", result);
    }

    [Fact]
    public void ToVariableName_WithOnlyUnderscore_ShouldReturnOriginal()
    {
        // Arrange
        var input = "_";

        // Act
        var result = input.ToVariableName();

        // Assert
        Assert.Equal("_", result);
    }

    [Fact]
    public void ToVariableName_WithSpecialCharactersAtStart_ShouldPrependUnderscore()
    {
        // Arrange
        var input = "@user-info";

        // Act
        var result = input.ToVariableName();

        // Assert
        Assert.StartsWith("_", result);
    }

    [Fact]
    public void ToVariableName_WithCamelCaseInput_ShouldConvertToLowerFirst()
    {
        // Arrange
        var input = "UserInfoData";

        // Act
        var result = input.ToVariableName();

        // Assert
        Assert.Equal("userinfodata", result);
    }

    [Fact]
    public void ToVariableName_WithSpaces_ShouldConvertToCamelCase()
    {
        // Arrange
        var input = "user info data";

        // Act
        var result = input.ToVariableName();

        // Assert
        Assert.Equal("userInfoData", result);
    }

    [Fact]
    public void ToVariableName_WithTabsAndNewlines_ShouldConvertToCamelCase()
    {
        // Arrange
        var input = "user\tinfo\ndata";

        // Act
        var result = input.ToVariableName();

        // Assert
        Assert.Equal("userInfoData", result);
    }

    [Fact]
    public void ToVariableName_WithSingleLetterWords_ShouldHandleCorrectly()
    {
        // Arrange
        var input = "a-b-c-d";

        // Act
        var result = input.ToVariableName();

        // Assert
        Assert.Equal("aBCD", result);
    }

    [Fact]
    public void ToVariableName_WithNumbersInMiddle_ShouldPreserve()
    {
        // Arrange
        var input = "user-123-info";

        // Act
        var result = input.ToVariableName();

        // Assert
        Assert.Equal("user123Info", result);
    }
}

