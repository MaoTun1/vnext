using System.Linq;
using Xunit;

namespace System.Text.Json;

/// <summary>
/// Unit tests for JsonElementExtensions
/// </summary>
public class JsonElementExtensionsTests
{
    #region ToDictionary Tests

    [Fact]
    public void ToDictionary_WithSimpleObject_ShouldConvertCorrectly()
    {
        // Arrange
        var json = "{\"name\":\"test\",\"age\":\"30\"}";
        var element = JsonDocument.Parse(json).RootElement;

        // Act
        var result = element.ToDictionary();

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal("test", result["name"]);
        Assert.Equal("30", result["age"]);
    }

    [Fact]
    public void ToDictionary_WithEmptyObject_ShouldReturnEmptyDictionary()
    {
        // Arrange
        var json = "{}";
        var element = JsonDocument.Parse(json).RootElement;

        // Act
        var result = element.ToDictionary();

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void ToDictionary_WithNestedObject_ShouldConvertToStringValues()
    {
        // Arrange
        var json = "{\"user\":{\"name\":\"test\"}}";
        var element = JsonDocument.Parse(json).RootElement;

        // Act
        var result = element.ToDictionary();

        // Assert
        Assert.Single(result);
        Assert.Contains("user", result.Keys);
        // Nested object should be converted to JSON string representation
        Assert.NotNull(result["user"]);
    }

    [Fact]
    public void ToDictionary_WithArray_ShouldReturnEmptyDictionary()
    {
        // Arrange
        var json = "[1,2,3]";
        var element = JsonDocument.Parse(json).RootElement;

        // Act
        var result = element.ToDictionary();

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void ToDictionary_WithString_ShouldReturnEmptyDictionary()
    {
        // Arrange
        var json = "\"test\"";
        var element = JsonDocument.Parse(json).RootElement;

        // Act
        var result = element.ToDictionary();

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void ToDictionary_WithNumber_ShouldReturnEmptyDictionary()
    {
        // Arrange
        var json = "42";
        var element = JsonDocument.Parse(json).RootElement;

        // Act
        var result = element.ToDictionary();

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void ToDictionary_WithNull_ShouldReturnEmptyDictionary()
    {
        // Arrange
        var json = "null";
        var element = JsonDocument.Parse(json).RootElement;

        // Act
        var result = element.ToDictionary();

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void ToDictionary_WithBooleanValues_ShouldConvertToString()
    {
        // Arrange
        var json = "{\"isActive\":true,\"isDeleted\":false}";
        var element = JsonDocument.Parse(json).RootElement;

        // Act
        var result = element.ToDictionary();

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal("True", result["isActive"]);
        Assert.Equal("False", result["isDeleted"]);
    }

    [Fact]
    public void ToDictionary_WithNumberValues_ShouldConvertToString()
    {
        // Arrange
        var json = "{\"age\":30,\"price\":99.99}";
        var element = JsonDocument.Parse(json).RootElement;

        // Act
        var result = element.ToDictionary();

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal("30", result["age"]);
        Assert.Equal("99.99", result["price"]);
    }

    [Fact]
    public void ToDictionary_WithNullValues_ShouldIncludeAsString()
    {
        // Arrange
        var json = "{\"name\":\"test\",\"middleName\":null}";
        var element = JsonDocument.Parse(json).RootElement;

        // Act
        var result = element.ToDictionary();

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal("test", result["name"]);
        Assert.NotNull(result["middleName"]);
    }

    [Fact]
    public void ToDictionary_WithSpecialCharactersInKeys_ShouldPreserve()
    {
        // Arrange
        var json = "{\"user-name\":\"test\",\"user_age\":\"30\"}";
        var element = JsonDocument.Parse(json).RootElement;

        // Act
        var result = element.ToDictionary();

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal("test", result["user-name"]);
        Assert.Equal("30", result["user_age"]);
    }

    [Fact]
    public void ToDictionary_WithArrayValue_ShouldConvertToString()
    {
        // Arrange
        var json = "{\"items\":[1,2,3]}";
        var element = JsonDocument.Parse(json).RootElement;

        // Act
        var result = element.ToDictionary();

        // Assert
        Assert.Single(result);
        Assert.Contains("items", result.Keys);
        Assert.NotNull(result["items"]);
    }

    #endregion

    #region ToJsonElement Tests

    [Fact]
    public void ToJsonElement_WithValidJson_ShouldParseCorrectly()
    {
        // Arrange
        var json = "{\"name\":\"test\",\"age\":30}";

        // Act
        var result = json.ToJsonElement();

        // Assert
        Assert.Equal(JsonValueKind.Object, result.ValueKind);
        Assert.Equal("test", result.GetProperty("name").GetString());
        Assert.Equal(30, result.GetProperty("age").GetInt32());
    }

    [Fact]
    public void ToJsonElement_WithArray_ShouldParseCorrectly()
    {
        // Arrange
        var json = "[1,2,3]";

        // Act
        var result = json.ToJsonElement();

        // Assert
        Assert.Equal(JsonValueKind.Array, result.ValueKind);
        Assert.Equal(3, result.GetArrayLength());
    }

    [Fact]
    public void ToJsonElement_WithString_ShouldParseCorrectly()
    {
        // Arrange
        var json = "\"test\"";

        // Act
        var result = json.ToJsonElement();

        // Assert
        Assert.Equal(JsonValueKind.String, result.ValueKind);
        Assert.Equal("test", result.GetString());
    }

    [Fact]
    public void ToJsonElement_WithNumber_ShouldParseCorrectly()
    {
        // Arrange
        var json = "42";

        // Act
        var result = json.ToJsonElement();

        // Assert
        Assert.Equal(JsonValueKind.Number, result.ValueKind);
        Assert.Equal(42, result.GetInt32());
    }

    [Fact]
    public void ToJsonElement_WithBoolean_ShouldParseCorrectly()
    {
        // Arrange
        var jsonTrue = "true";
        var jsonFalse = "false";

        // Act
        var resultTrue = jsonTrue.ToJsonElement();
        var resultFalse = jsonFalse.ToJsonElement();

        // Assert
        Assert.Equal(JsonValueKind.True, resultTrue.ValueKind);
        Assert.Equal(JsonValueKind.False, resultFalse.ValueKind);
    }

    [Fact]
    public void ToJsonElement_WithNull_ShouldParseCorrectly()
    {
        // Arrange
        var json = "null";

        // Act
        var result = json.ToJsonElement();

        // Assert
        Assert.Equal(JsonValueKind.Null, result.ValueKind);
    }

    [Fact]
    public void ToJsonElement_WithEmptyObject_ShouldParseCorrectly()
    {
        // Arrange
        var json = "{}";

        // Act
        var result = json.ToJsonElement();

        // Assert
        Assert.Equal(JsonValueKind.Object, result.ValueKind);
        Assert.Equal(0, result.EnumerateObject().Count());
    }

    [Fact]
    public void ToJsonElement_WithComplexNested_ShouldParseCorrectly()
    {
        // Arrange
        var json = "{\"user\":{\"name\":\"test\",\"tags\":[\"tag1\",\"tag2\"]}}";

        // Act
        var result = json.ToJsonElement();

        // Assert
        Assert.Equal(JsonValueKind.Object, result.ValueKind);
        var user = result.GetProperty("user");
        Assert.Equal("test", user.GetProperty("name").GetString());
        Assert.Equal(2, user.GetProperty("tags").GetArrayLength());
    }

    [Fact]
    public void ToJsonElement_WithInvalidJson_ShouldThrow()
    {
        // Arrange
        var invalidJson = "{invalid json}";

        // Act & Assert
        // JsonDocument.Parse throws JsonReaderException (derives from JsonException)
        Assert.ThrowsAny<JsonException>(() => invalidJson.ToJsonElement());
    }

    [Fact]
    public void ToJsonElement_WithEmptyString_ShouldThrow()
    {
        // Arrange
        var emptyJson = "";

        // Act & Assert
        // JsonDocument.Parse throws JsonReaderException (derives from JsonException)
        Assert.ThrowsAny<JsonException>(() => emptyJson.ToJsonElement());
    }

    #endregion

    #region Integration Tests

    [Fact]
    public void ToJsonElement_ThenToDictionary_ShouldWorkTogether()
    {
        // Arrange
        var json = "{\"name\":\"test\",\"age\":\"30\"}";

        // Act
        var element = json.ToJsonElement();
        var result = element.ToDictionary();

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal("test", result["name"]);
        Assert.Equal("30", result["age"]);
    }

    [Fact]
    public void ComplexJsonWorkflow_ShouldMaintainDataIntegrity()
    {
        // Arrange
        var originalJson = "{\"user\":{\"name\":\"test\"},\"count\":42}";

        // Act
        var element = originalJson.ToJsonElement();
        var dict = element.ToDictionary();

        // Assert
        Assert.Equal(2, dict.Count);
        Assert.Contains("user", dict.Keys);
        Assert.Contains("count", dict.Keys);
    }

    #endregion
}

