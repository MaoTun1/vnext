using Xunit;

namespace System.ComponentModel.DataAnnotations;

/// <summary>
/// Unit tests for EnumValueValidationAttribute
/// </summary>
public class EnumValueValidationAttributeTests
{
    private enum TestEnum
    {
        None = 0,
        Value1 = 1,
        Value2 = 2,
        Value3 = 3
    }

    [Flags]
    private enum FlagsTestEnum
    {
        None = 0,
        Read = 1,
        Write = 2,
        Execute = 4,
        All = Read | Write | Execute
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidEnumType_ShouldNotThrow()
    {
        // Act & Assert
        var exception = Record.Exception(() => new EnumValueValidationAttribute(typeof(TestEnum)));
        Assert.Null(exception);
    }

    [Fact]
    public void Constructor_WithNonEnumType_ShouldThrowArgumentException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => 
            new EnumValueValidationAttribute(typeof(string)));

        Assert.Contains("Provided type must be an enum", exception.Message);
    }

    [Fact]
    public void Constructor_WithClassType_ShouldThrowArgumentException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => 
            new EnumValueValidationAttribute(typeof(EnumValueValidationAttributeTests)));

        Assert.Contains("Provided type must be an enum", exception.Message);
    }

    #endregion

    #region IsValid Tests - Valid Values

    [Fact]
    public void IsValid_WithValidEnumValue_ShouldReturnSuccess()
    {
        // Arrange
        var attribute = new EnumValueValidationAttribute(typeof(TestEnum));
        var context = new ValidationContext(new object());

        // Act
        var result = attribute.GetValidationResult(TestEnum.Value1, context);

        // Assert
        Assert.Equal(ValidationResult.Success, result);
    }

    [Fact]
    public void IsValid_WithValidEnumIntValue_ShouldReturnSuccess()
    {
        // Arrange
        var attribute = new EnumValueValidationAttribute(typeof(TestEnum));
        var context = new ValidationContext(new object());

        // Act
        var result = attribute.GetValidationResult(1, context);

        // Assert
        Assert.Equal(ValidationResult.Success, result);
    }

    [Fact]
    public void IsValid_WithZeroValue_ShouldReturnSuccess()
    {
        // Arrange
        var attribute = new EnumValueValidationAttribute(typeof(TestEnum));
        var context = new ValidationContext(new object());

        // Act
        var result = attribute.GetValidationResult(TestEnum.None, context);

        // Assert
        Assert.Equal(ValidationResult.Success, result);
    }

    [Fact]
    public void IsValid_WithAllEnumValues_ShouldReturnSuccess()
    {
        // Arrange
        var attribute = new EnumValueValidationAttribute(typeof(TestEnum));
        var context = new ValidationContext(new object());

        // Act & Assert
        Assert.Equal(ValidationResult.Success, attribute.GetValidationResult(TestEnum.None, context));
        Assert.Equal(ValidationResult.Success, attribute.GetValidationResult(TestEnum.Value1, context));
        Assert.Equal(ValidationResult.Success, attribute.GetValidationResult(TestEnum.Value2, context));
        Assert.Equal(ValidationResult.Success, attribute.GetValidationResult(TestEnum.Value3, context));
    }

    #endregion

    #region IsValid Tests - Invalid Values

    [Fact]
    public void IsValid_WithInvalidEnumValue_ShouldReturnError()
    {
        // Arrange
        var attribute = new EnumValueValidationAttribute(typeof(TestEnum));
        var context = new ValidationContext(new object());

        // Act
        var result = attribute.GetValidationResult(999, context);

        // Assert
        Assert.NotEqual(ValidationResult.Success, result);
        Assert.NotNull(result);
        Assert.Contains("not valid for type TestEnum", result!.ErrorMessage);
    }

    [Fact]
    public void IsValid_WithNullValue_ShouldReturnError()
    {
        // Arrange
        var attribute = new EnumValueValidationAttribute(typeof(TestEnum));
        var context = new ValidationContext(new object());

        // Act
        var result = attribute.GetValidationResult(null, context);

        // Assert
        Assert.NotEqual(ValidationResult.Success, result);
        Assert.NotNull(result);
    }

    [Fact]
    public void IsValid_WithStringValue_ShouldReturnError()
    {
        // Arrange
        var attribute = new EnumValueValidationAttribute(typeof(TestEnum));
        var context = new ValidationContext(new object());

        // Act
        var result = attribute.GetValidationResult("InvalidValue", context);

        // Assert
        Assert.NotEqual(ValidationResult.Success, result);
        Assert.NotNull(result);
    }

    [Fact]
    public void IsValid_WithNegativeValue_ShouldReturnError()
    {
        // Arrange
        var attribute = new EnumValueValidationAttribute(typeof(TestEnum));
        var context = new ValidationContext(new object());

        // Act
        var result = attribute.GetValidationResult(-1, context);

        // Assert
        Assert.NotEqual(ValidationResult.Success, result);
        Assert.NotNull(result);
    }

    #endregion

    #region Error Message Tests

    [Fact]
    public void IsValid_WithInvalidValue_ShouldIncludeEnumNameInErrorMessage()
    {
        // Arrange
        var attribute = new EnumValueValidationAttribute(typeof(TestEnum));
        var context = new ValidationContext(new object());

        // Act
        var result = attribute.GetValidationResult(99, context);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("TestEnum", result!.ErrorMessage);
    }

    [Fact]
    public void IsValid_WithInvalidValue_ShouldIncludeInvalidValueInErrorMessage()
    {
        // Arrange
        var attribute = new EnumValueValidationAttribute(typeof(TestEnum));
        var context = new ValidationContext(new object());

        // Act
        var result = attribute.GetValidationResult(99, context);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("99", result!.ErrorMessage);
    }

    [Fact]
    public void IsValid_WithInvalidValue_ShouldIncludeAllowedValuesInErrorMessage()
    {
        // Arrange
        var attribute = new EnumValueValidationAttribute(typeof(TestEnum));
        var context = new ValidationContext(new object());

        // Act
        var result = attribute.GetValidationResult(99, context);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("Allowed values are:", result!.ErrorMessage);
    }

    #endregion

    #region Flags Enum Tests

    [Fact]
    public void IsValid_WithFlagsEnum_ValidValue_ShouldReturnSuccess()
    {
        // Arrange
        var attribute = new EnumValueValidationAttribute(typeof(FlagsTestEnum));
        var context = new ValidationContext(new object());

        // Act
        var result = attribute.GetValidationResult(FlagsTestEnum.Read, context);

        // Assert
        Assert.Equal(ValidationResult.Success, result);
    }

    [Fact]
    public void IsValid_WithFlagsEnum_CombinedValue_ShouldReturnSuccess()
    {
        // Arrange
        var attribute = new EnumValueValidationAttribute(typeof(FlagsTestEnum));
        var context = new ValidationContext(new object());

        // Act
        var result = attribute.GetValidationResult(FlagsTestEnum.All, context);

        // Assert
        Assert.Equal(ValidationResult.Success, result);
    }

    [Fact]
    public void IsValid_WithFlagsEnum_InvalidCombinedValue_ShouldReturnError()
    {
        // Arrange
        var attribute = new EnumValueValidationAttribute(typeof(FlagsTestEnum));
        var context = new ValidationContext(new object());

        // Act - Combined value not explicitly defined in enum
        var result = attribute.GetValidationResult((FlagsTestEnum)3, context);

        // Assert
        // Note: Enum.IsDefined doesn't validate flag combinations
        // It only checks if the exact value is defined
        Assert.NotEqual(ValidationResult.Success, result);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void IsValid_WithMaxIntValue_ShouldReturnError()
    {
        // Arrange
        var attribute = new EnumValueValidationAttribute(typeof(TestEnum));
        var context = new ValidationContext(new object());

        // Act
        var result = attribute.GetValidationResult(int.MaxValue, context);

        // Assert
        Assert.NotEqual(ValidationResult.Success, result);
    }

    [Fact]
    public void IsValid_WithMinIntValue_ShouldReturnError()
    {
        // Arrange
        var attribute = new EnumValueValidationAttribute(typeof(TestEnum));
        var context = new ValidationContext(new object());

        // Act
        var result = attribute.GetValidationResult(int.MinValue, context);

        // Assert
        Assert.NotEqual(ValidationResult.Success, result);
    }

    [Fact]
    public void IsValid_WithWrongEnumType_ShouldThrowArgumentException()
    {
        // Arrange
        var attribute = new EnumValueValidationAttribute(typeof(TestEnum));
        var context = new ValidationContext(new object());

        // Act & Assert
        // Enum.IsDefined throws ArgumentException when enum types don't match
        Assert.Throws<ArgumentException>(() => 
            attribute.GetValidationResult(DayOfWeek.Monday, context));
    }

    #endregion

    #region Multiple Validation Scenarios

    [Fact]
    public void IsValid_MultipleInstances_ShouldWorkIndependently()
    {
        // Arrange
        var attribute1 = new EnumValueValidationAttribute(typeof(TestEnum));
        var attribute2 = new EnumValueValidationAttribute(typeof(FlagsTestEnum));
        var context = new ValidationContext(new object());

        // Act
        var result1 = attribute1.GetValidationResult(TestEnum.Value1, context);
        var result2 = attribute2.GetValidationResult(FlagsTestEnum.Read, context);

        // Assert
        Assert.Equal(ValidationResult.Success, result1);
        Assert.Equal(ValidationResult.Success, result2);
    }

    [Fact]
    public void IsValid_RepeatedCalls_ShouldReturnConsistentResults()
    {
        // Arrange
        var attribute = new EnumValueValidationAttribute(typeof(TestEnum));
        var context = new ValidationContext(new object());

        // Act
        var result1 = attribute.GetValidationResult(TestEnum.Value1, context);
        var result2 = attribute.GetValidationResult(TestEnum.Value1, context);
        var result3 = attribute.GetValidationResult(999, context);
        var result4 = attribute.GetValidationResult(999, context);

        // Assert
        Assert.Equal(ValidationResult.Success, result1);
        Assert.Equal(ValidationResult.Success, result2);
        Assert.NotEqual(ValidationResult.Success, result3);
        Assert.NotEqual(ValidationResult.Success, result4);
    }

    #endregion
}

