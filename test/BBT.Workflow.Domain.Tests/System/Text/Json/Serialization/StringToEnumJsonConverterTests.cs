using Xunit;

namespace System.Text.Json.Serialization;

/// <summary>
/// Unit tests for StringToEnumJsonConverter
/// </summary>
public class StringToEnumJsonConverterTests
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

    private readonly JsonSerializerOptions _options;

    public StringToEnumJsonConverterTests()
    {
        _options = new JsonSerializerOptions();
        _options.Converters.Add(new StringToEnumJsonConverter<TestEnum>());
    }

    #region Read Tests - Valid Cases

    [Fact]
    public void Read_ValidIntegerString_ShouldDeserialize()
    {
        // Arrange
        var json = "\"1\"";

        // Act
        var result = JsonSerializer.Deserialize<TestEnum>(json, _options);

        // Assert
        Assert.Equal(TestEnum.Value1, result);
    }

    [Fact]
    public void Read_ZeroString_ShouldDeserialize()
    {
        // Arrange
        var json = "\"0\"";

        // Act
        var result = JsonSerializer.Deserialize<TestEnum>(json, _options);

        // Assert
        Assert.Equal(TestEnum.None, result);
    }

    [Fact]
    public void Read_AllEnumValues_ShouldDeserializeCorrectly()
    {
        // Arrange & Act & Assert
        Assert.Equal(TestEnum.None, JsonSerializer.Deserialize<TestEnum>("\"0\"", _options));
        Assert.Equal(TestEnum.Value1, JsonSerializer.Deserialize<TestEnum>("\"1\"", _options));
        Assert.Equal(TestEnum.Value2, JsonSerializer.Deserialize<TestEnum>("\"2\"", _options));
        Assert.Equal(TestEnum.Value3, JsonSerializer.Deserialize<TestEnum>("\"3\"", _options));
    }

    [Fact]
    public void Read_LargeIntegerString_ShouldDeserialize()
    {
        // Arrange
        var json = "\"100\"";

        // Act
        var result = JsonSerializer.Deserialize<TestEnum>(json, _options);

        // Assert
        Assert.Equal((TestEnum)100, result);
    }

    [Fact]
    public void Read_NegativeIntegerString_ShouldDeserialize()
    {
        // Arrange
        var json = "\"-1\"";

        // Act
        var result = JsonSerializer.Deserialize<TestEnum>(json, _options);

        // Assert
        Assert.Equal((TestEnum)(-1), result);
    }

    #endregion

    #region Read Tests - Invalid Cases

    [Fact]
    public void Read_NonIntegerString_ShouldThrowJsonException()
    {
        // Arrange
        var json = "\"Value1\""; // Name instead of integer

        // Act & Assert
        var exception = Assert.Throws<JsonException>(() => 
            JsonSerializer.Deserialize<TestEnum>(json, _options));

        Assert.Contains("Cannot convert", exception.Message);
    }

    [Fact]
    public void Read_EmptyString_ShouldThrowJsonException()
    {
        // Arrange
        var json = "\"\"";

        // Act & Assert
        Assert.Throws<JsonException>(() => 
            JsonSerializer.Deserialize<TestEnum>(json, _options));
    }

    [Fact]
    public void Read_DecimalString_ShouldThrowJsonException()
    {
        // Arrange
        var json = "\"1.5\"";

        // Act & Assert
        Assert.Throws<JsonException>(() => 
            JsonSerializer.Deserialize<TestEnum>(json, _options));
    }

    [Fact]
    public void Read_NullString_ShouldThrowJsonException()
    {
        // Arrange
        var json = "null";

        // Act & Assert
        Assert.Throws<JsonException>(() => 
            JsonSerializer.Deserialize<TestEnum>(json, _options));
    }

    [Fact]
    public void Read_WhitespaceString_ShouldThrowJsonException()
    {
        // Arrange
        var json = "\"   \"";

        // Act & Assert
        Assert.Throws<JsonException>(() => 
            JsonSerializer.Deserialize<TestEnum>(json, _options));
    }

    [Fact]
    public void Read_InvalidFormatString_ShouldThrowJsonException()
    {
        // Arrange
        var json = "\"abc\"";

        // Act & Assert
        var exception = Assert.Throws<JsonException>(() => 
            JsonSerializer.Deserialize<TestEnum>(json, _options));

        Assert.Contains("Cannot convert", exception.Message);
    }

    #endregion

    #region Write Tests

    [Fact]
    public void Write_ValidEnumValue_ShouldSerializeAsIntegerString()
    {
        // Arrange
        var value = TestEnum.Value1;

        // Act
        var json = JsonSerializer.Serialize(value, _options);

        // Assert
        Assert.Equal("\"1\"", json);
    }

    [Fact]
    public void Write_ZeroValue_ShouldSerializeAsZeroString()
    {
        // Arrange
        var value = TestEnum.None;

        // Act
        var json = JsonSerializer.Serialize(value, _options);

        // Assert
        Assert.Equal("\"0\"", json);
    }

    [Fact]
    public void Write_AllEnumValues_ShouldSerializeCorrectly()
    {
        // Arrange & Act & Assert
        Assert.Equal("\"0\"", JsonSerializer.Serialize(TestEnum.None, _options));
        Assert.Equal("\"1\"", JsonSerializer.Serialize(TestEnum.Value1, _options));
        Assert.Equal("\"2\"", JsonSerializer.Serialize(TestEnum.Value2, _options));
        Assert.Equal("\"3\"", JsonSerializer.Serialize(TestEnum.Value3, _options));
    }

    [Fact]
    public void Write_UndefinedEnumValue_ShouldSerializeAsIntegerString()
    {
        // Arrange
        var value = (TestEnum)99;

        // Act
        var json = JsonSerializer.Serialize(value, _options);

        // Assert
        Assert.Equal("\"99\"", json);
    }

    [Fact]
    public void Write_NegativeEnumValue_ShouldSerializeAsIntegerString()
    {
        // Arrange
        var value = (TestEnum)(-1);

        // Act
        var json = JsonSerializer.Serialize(value, _options);

        // Assert
        Assert.Equal("\"-1\"", json);
    }

    #endregion

    #region Round-trip Tests

    [Fact]
    public void RoundTrip_ValidEnumValue_ShouldMaintainValue()
    {
        // Arrange
        var original = TestEnum.Value2;

        // Act
        var json = JsonSerializer.Serialize(original, _options);
        var result = JsonSerializer.Deserialize<TestEnum>(json, _options);

        // Assert
        Assert.Equal(original, result);
    }

    [Fact]
    public void RoundTrip_AllEnumValues_ShouldMaintainValues()
    {
        // Arrange
        var values = new[] { TestEnum.None, TestEnum.Value1, TestEnum.Value2, TestEnum.Value3 };

        foreach (var original in values)
        {
            // Act
            var json = JsonSerializer.Serialize(original, _options);
            var result = JsonSerializer.Deserialize<TestEnum>(json, _options);

            // Assert
            Assert.Equal(original, result);
        }
    }

    [Fact]
    public void RoundTrip_UndefinedEnumValue_ShouldMaintainValue()
    {
        // Arrange
        var original = (TestEnum)999;

        // Act
        var json = JsonSerializer.Serialize(original, _options);
        var result = JsonSerializer.Deserialize<TestEnum>(json, _options);

        // Assert
        Assert.Equal(original, result);
    }

    #endregion

    #region Flags Enum Tests

    [Fact]
    public void Read_FlagsEnumValue_ShouldDeserialize()
    {
        // Arrange
        var options = new JsonSerializerOptions();
        options.Converters.Add(new StringToEnumJsonConverter<FlagsTestEnum>());
        var json = "\"1\"";

        // Act
        var result = JsonSerializer.Deserialize<FlagsTestEnum>(json, options);

        // Assert
        Assert.Equal(FlagsTestEnum.Read, result);
    }

    [Fact]
    public void Write_FlagsEnumValue_ShouldSerialize()
    {
        // Arrange
        var options = new JsonSerializerOptions();
        options.Converters.Add(new StringToEnumJsonConverter<FlagsTestEnum>());
        var value = FlagsTestEnum.Write;

        // Act
        var json = JsonSerializer.Serialize(value, options);

        // Assert
        Assert.Equal("\"2\"", json);
    }

    [Fact]
    public void RoundTrip_FlagsEnumCombinedValue_ShouldMaintainValue()
    {
        // Arrange
        var options = new JsonSerializerOptions();
        options.Converters.Add(new StringToEnumJsonConverter<FlagsTestEnum>());
        var original = FlagsTestEnum.All;

        // Act
        var json = JsonSerializer.Serialize(original, options);
        var result = JsonSerializer.Deserialize<FlagsTestEnum>(json, options);

        // Assert
        Assert.Equal(original, result);
        Assert.Equal("\"7\"", json); // 1 | 2 | 4 = 7
    }

    [Fact]
    public void RoundTrip_FlagsEnumCustomCombination_ShouldMaintainValue()
    {
        // Arrange
        var options = new JsonSerializerOptions();
        options.Converters.Add(new StringToEnumJsonConverter<FlagsTestEnum>());
        var original = FlagsTestEnum.Read | FlagsTestEnum.Execute;

        // Act
        var json = JsonSerializer.Serialize(original, options);
        var result = JsonSerializer.Deserialize<FlagsTestEnum>(json, options);

        // Assert
        Assert.Equal(original, result);
        Assert.Equal("\"5\"", json); // 1 | 4 = 5
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Read_MaxIntValue_ShouldDeserialize()
    {
        // Arrange
        var json = $"\"{int.MaxValue}\"";

        // Act
        var result = JsonSerializer.Deserialize<TestEnum>(json, _options);

        // Assert
        Assert.Equal((TestEnum)int.MaxValue, result);
    }

    [Fact]
    public void Read_MinIntValue_ShouldDeserialize()
    {
        // Arrange
        var json = $"\"{int.MinValue}\"";

        // Act
        var result = JsonSerializer.Deserialize<TestEnum>(json, _options);

        // Assert
        Assert.Equal((TestEnum)int.MinValue, result);
    }

    [Fact]
    public void Write_MaxIntValue_ShouldSerialize()
    {
        // Arrange
        var value = (TestEnum)int.MaxValue;

        // Act
        var json = JsonSerializer.Serialize(value, _options);

        // Assert
        Assert.Equal($"\"{int.MaxValue}\"", json);
    }

    [Fact]
    public void Write_MinIntValue_ShouldSerialize()
    {
        // Arrange
        var value = (TestEnum)int.MinValue;

        // Act
        var json = JsonSerializer.Serialize(value, _options);

        // Assert
        Assert.Equal($"\"{int.MinValue}\"", json);
    }

    #endregion

    #region Array and Collection Tests

    [Fact]
    public void Read_ArrayOfEnumStrings_ShouldDeserialize()
    {
        // Arrange
        var json = "[\"1\",\"2\",\"3\"]";

        // Act
        var result = JsonSerializer.Deserialize<TestEnum[]>(json, _options);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result!.Length);
        Assert.Equal(TestEnum.Value1, result[0]);
        Assert.Equal(TestEnum.Value2, result[1]);
        Assert.Equal(TestEnum.Value3, result[2]);
    }

    [Fact]
    public void Write_ArrayOfEnums_ShouldSerialize()
    {
        // Arrange
        var values = new[] { TestEnum.Value1, TestEnum.Value2, TestEnum.Value3 };

        // Act
        var json = JsonSerializer.Serialize(values, _options);

        // Assert
        Assert.Contains("\"1\"", json);
        Assert.Contains("\"2\"", json);
        Assert.Contains("\"3\"", json);
    }

    [Fact]
    public void RoundTrip_ArrayOfEnums_ShouldMaintainValues()
    {
        // Arrange
        var original = new[] { TestEnum.None, TestEnum.Value1, TestEnum.Value2, TestEnum.Value3 };

        // Act
        var json = JsonSerializer.Serialize(original, _options);
        var result = JsonSerializer.Deserialize<TestEnum[]>(json, _options);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(original.Length, result!.Length);
        for (int i = 0; i < original.Length; i++)
        {
            Assert.Equal(original[i], result[i]);
        }
    }

    #endregion

    #region Complex Object Tests

    private class TestObject
    {
        public string Name { get; set; } = string.Empty;
        public TestEnum Status { get; set; }
    }

    [Fact]
    public void Read_ObjectWithEnumProperty_ShouldDeserialize()
    {
        // Arrange
        var json = "{\"Name\":\"Test\",\"Status\":\"2\"}";

        // Act
        var result = JsonSerializer.Deserialize<TestObject>(json, _options);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Test", result!.Name);
        Assert.Equal(TestEnum.Value2, result.Status);
    }

    [Fact]
    public void Write_ObjectWithEnumProperty_ShouldSerialize()
    {
        // Arrange
        var obj = new TestObject { Name = "Test", Status = TestEnum.Value3 };

        // Act
        var json = JsonSerializer.Serialize(obj, _options);

        // Assert
        Assert.Contains("\"Test\"", json);
        Assert.Contains("\"3\"", json);
    }

    [Fact]
    public void RoundTrip_ObjectWithEnumProperty_ShouldMaintainData()
    {
        // Arrange
        var original = new TestObject { Name = "Test", Status = TestEnum.Value1 };

        // Act
        var json = JsonSerializer.Serialize(original, _options);
        var result = JsonSerializer.Deserialize<TestObject>(json, _options);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(original.Name, result!.Name);
        Assert.Equal(original.Status, result.Status);
    }

    #endregion
}

