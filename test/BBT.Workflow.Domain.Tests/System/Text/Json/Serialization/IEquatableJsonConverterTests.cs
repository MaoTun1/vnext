using BBT.Workflow.Instances;
using Xunit;

namespace System.Text.Json.Serialization;

/// <summary>
/// Unit tests for IEquatableJsonConverter
/// </summary>
public class IEquatableJsonConverterTests
{
    // Test class that implements IEquatable with FromCode method and Code property
    private class TestValueObject : IEquatable<TestValueObject>
    {
        public string Code { get; }

        private TestValueObject(string code)
        {
            Code = code;
        }

        public static TestValueObject FromCode(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
                throw new ArgumentException("Code cannot be null or empty");

            return new TestValueObject(code);
        }

        public bool Equals(TestValueObject? other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;
            return Code == other.Code;
        }

        public override bool Equals(object? obj)
        {
            return Equals(obj as TestValueObject);
        }

        public override int GetHashCode()
        {
            return Code.GetHashCode();
        }
    }

    private readonly JsonSerializerOptions _options;

    public IEquatableJsonConverterTests()
    {
        _options = new JsonSerializerOptions();
        _options.Converters.Add(new IEquatableJsonConverter<TestValueObject>());
    }

    #region Read Tests - Success Cases

    [Fact]
    public void Read_ValidCode_ShouldDeserialize()
    {
        // Arrange
        var json = "\"TEST_CODE\"";

        // Act
        var result = JsonSerializer.Deserialize<TestValueObject>(json, _options);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("TEST_CODE", result!.Code);
    }

    [Fact]
    public void Read_SimpleString_ShouldDeserialize()
    {
        // Arrange
        var json = "\"active\"";

        // Act
        var result = JsonSerializer.Deserialize<TestValueObject>(json, _options);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("active", result!.Code);
    }

    [Fact]
    public void Read_NumericString_ShouldDeserialize()
    {
        // Arrange
        var json = "\"123\"";

        // Act
        var result = JsonSerializer.Deserialize<TestValueObject>(json, _options);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("123", result!.Code);
    }

    [Fact]
    public void Read_WithSpecialCharacters_ShouldDeserialize()
    {
        // Arrange
        var json = "\"test-code_123\"";

        // Act
        var result = JsonSerializer.Deserialize<TestValueObject>(json, _options);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("test-code_123", result!.Code);
    }

    #endregion

    #region Read Tests - Error Cases

    [Fact]
    public void Read_NonStringValue_ShouldThrowJsonException()
    {
        // Arrange
        var json = "123"; // Number instead of string

        // Act & Assert
        var exception = Assert.Throws<JsonException>(() => 
            JsonSerializer.Deserialize<TestValueObject>(json, _options));

        Assert.Contains("Expected a JSON string", exception.Message);
    }

    [Fact]
    public void Read_ObjectValue_ShouldThrowJsonException()
    {
        // Arrange
        var json = "{\"code\":\"test\"}"; // Object instead of string

        // Act & Assert
        var exception = Assert.Throws<JsonException>(() => 
            JsonSerializer.Deserialize<TestValueObject>(json, _options));

        Assert.Contains("Expected a JSON string", exception.Message);
    }

    [Fact]
    public void Read_ArrayValue_ShouldThrowJsonException()
    {
        // Arrange
        var json = "[\"test\"]"; // Array instead of string

        // Act & Assert
        var exception = Assert.Throws<JsonException>(() => 
            JsonSerializer.Deserialize<TestValueObject>(json, _options));

        Assert.Contains("Expected a JSON string", exception.Message);
    }

    [Fact]
    public void Read_EmptyString_ShouldThrowJsonException()
    {
        // Arrange
        var json = "\"\"";

        // Act & Assert
        var exception = Assert.Throws<JsonException>(() => 
            JsonSerializer.Deserialize<TestValueObject>(json, _options));

        Assert.Contains("Invalid or empty code", exception.Message);
    }

    [Fact]
    public void Read_WhitespaceString_ShouldThrowJsonException()
    {
        // Arrange
        var json = "\"   \"";

        // Act & Assert
        var exception = Assert.Throws<JsonException>(() => 
            JsonSerializer.Deserialize<TestValueObject>(json, _options));

        Assert.Contains("Invalid or empty code", exception.Message);
    }

    [Fact]
    public void Read_NullValue_ShouldReturnNull()
    {
        // Arrange
        var json = "null";

        // Act
        var result = JsonSerializer.Deserialize<TestValueObject>(json, _options);

        // Assert
        // Nullable reference types allow null deserialization
        Assert.Null(result);
    }

    #endregion

    #region Write Tests

    [Fact]
    public void Write_ValidObject_ShouldSerializeCodeProperty()
    {
        // Arrange
        var obj = TestValueObject.FromCode("TEST_CODE");

        // Act
        var json = JsonSerializer.Serialize(obj, _options);

        // Assert
        Assert.Equal("\"TEST_CODE\"", json);
    }

    [Fact]
    public void Write_SimpleCode_ShouldSerialize()
    {
        // Arrange
        var obj = TestValueObject.FromCode("active");

        // Act
        var json = JsonSerializer.Serialize(obj, _options);

        // Assert
        Assert.Equal("\"active\"", json);
    }

    [Fact]
    public void Write_NumericCode_ShouldSerializeAsString()
    {
        // Arrange
        var obj = TestValueObject.FromCode("123");

        // Act
        var json = JsonSerializer.Serialize(obj, _options);

        // Assert
        Assert.Equal("\"123\"", json);
    }

    [Fact]
    public void Write_WithSpecialCharacters_ShouldSerialize()
    {
        // Arrange
        var obj = TestValueObject.FromCode("test-code_123");

        // Act
        var json = JsonSerializer.Serialize(obj, _options);

        // Assert
        Assert.Equal("\"test-code_123\"", json);
    }

    #endregion

    #region Round-trip Tests

    [Fact]
    public void RoundTrip_SimpleValue_ShouldMaintainData()
    {
        // Arrange
        var original = TestValueObject.FromCode("TEST_CODE");

        // Act
        var json = JsonSerializer.Serialize(original, _options);
        var result = JsonSerializer.Deserialize<TestValueObject>(json, _options);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(original.Code, result!.Code);
        Assert.True(original.Equals(result));
    }

    [Fact]
    public void RoundTrip_MultipleValues_ShouldMaintainData()
    {
        // Arrange
        var codes = new[] { "active", "passive", "completed", "faulted" };

        foreach (var code in codes)
        {
            var original = TestValueObject.FromCode(code);

            // Act
            var json = JsonSerializer.Serialize(original, _options);
            var result = JsonSerializer.Deserialize<TestValueObject>(json, _options);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(code, result!.Code);
            Assert.True(original.Equals(result));
        }
    }

    #endregion

    #region Integration Tests with Real Domain Objects

    [Fact]
    public void Read_InstanceStatus_ShouldDeserialize()
    {
        // Arrange
        var options = new JsonSerializerOptions();
        options.Converters.Add(new IEquatableJsonConverter<InstanceStatus>());
        var json = "\"A\""; // InstanceStatus uses single letter codes

        // Act
        var result = JsonSerializer.Deserialize<InstanceStatus>(json, options);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(InstanceStatus.Active, result);
    }

    [Fact]
    public void Write_InstanceStatus_ShouldSerialize()
    {
        // Arrange
        var options = new JsonSerializerOptions();
        options.Converters.Add(new IEquatableJsonConverter<InstanceStatus>());
        var status = InstanceStatus.Active;

        // Act
        var json = JsonSerializer.Serialize(status, options);

        // Assert
        Assert.Equal("\"A\"", json); // InstanceStatus uses single letter codes
    }

    [Fact]
    public void RoundTrip_InstanceStatus_ShouldMaintainData()
    {
        // Arrange
        var options = new JsonSerializerOptions();
        options.Converters.Add(new IEquatableJsonConverter<InstanceStatus>());
        var original = InstanceStatus.Completed;

        // Act
        var json = JsonSerializer.Serialize(original, options);
        var result = JsonSerializer.Deserialize<InstanceStatus>(json, options);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(original, result);
    }

    [Fact]
    public void Read_AllInstanceStatuses_ShouldDeserializeCorrectly()
    {
        // Arrange
        var options = new JsonSerializerOptions();
        options.Converters.Add(new IEquatableJsonConverter<InstanceStatus>());

        // InstanceStatus uses single letter codes: B, A, P, C, F
        var statuses = new[]
        {
            ("\"B\"", InstanceStatus.Busy),
            ("\"A\"", InstanceStatus.Active),
            ("\"P\"", InstanceStatus.Passive),
            ("\"C\"", InstanceStatus.Completed),
            ("\"F\"", InstanceStatus.Faulted)
        };

        foreach (var (json, expected) in statuses)
        {
            // Act
            var result = JsonSerializer.Deserialize<InstanceStatus>(json, options);

            // Assert
            Assert.Equal(expected, result);
        }
    }

    #endregion

    #region Type Without FromCode Method

    // This test class lacks the required FromCode method
    private class InvalidValueObject : IEquatable<InvalidValueObject>
    {
        public string Code { get; set; } = string.Empty;

        public bool Equals(InvalidValueObject? other) => false;
    }

    [Fact]
    public void Read_TypeWithoutFromCodeMethod_ShouldThrowJsonException()
    {
        // Arrange
        var options = new JsonSerializerOptions();
        options.Converters.Add(new IEquatableJsonConverter<InvalidValueObject>());
        var json = "\"test\"";

        // Act & Assert
        var exception = Assert.Throws<JsonException>(() => 
            JsonSerializer.Deserialize<InvalidValueObject>(json, options));

        Assert.Contains("must have a static FromCode(string) method", exception.Message);
    }

    #endregion

    #region Type Without Code Property

    // This test class lacks the required Code property
    private class NoCodePropertyValueObject : IEquatable<NoCodePropertyValueObject>
    {
        public string Value { get; set; } = string.Empty;

        public static NoCodePropertyValueObject FromCode(string code)
        {
            return new NoCodePropertyValueObject { Value = code };
        }

        public bool Equals(NoCodePropertyValueObject? other) => false;
    }

    [Fact]
    public void Write_TypeWithoutCodeProperty_ShouldThrowJsonException()
    {
        // Arrange
        var options = new JsonSerializerOptions();
        options.Converters.Add(new IEquatableJsonConverter<NoCodePropertyValueObject>());
        var obj = NoCodePropertyValueObject.FromCode("test");

        // Act & Assert
        var exception = Assert.Throws<JsonException>(() => 
            JsonSerializer.Serialize(obj, options));

        Assert.Contains("must have a public 'Code' property", exception.Message);
    }

    #endregion
}

