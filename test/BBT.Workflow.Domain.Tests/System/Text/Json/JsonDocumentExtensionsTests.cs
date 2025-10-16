using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using Xunit;

namespace System.Text.Json;

/// <summary>
/// Unit tests for JsonDocumentExtensions
/// </summary>
public class JsonDocumentExtensionsTests
{
    [Fact]
    public void ToDynamic_WithSimpleObject_ShouldReturnExpandoObject()
    {
        // Arrange
        var json = "{\"name\":\"test\",\"age\":30}";
        var element = JsonDocument.Parse(json).RootElement;

        // Act
        var result = element.ToDynamic();

        // Assert
        Assert.NotNull(result);
        dynamic dynamicResult = result!;
        Assert.Equal("test", dynamicResult.name);
        Assert.Equal(30L, dynamicResult.age);
    }

    [Fact]
    public void ToDynamic_WithNestedObject_ShouldReturnNestedExpandoObject()
    {
        // Arrange
        var json = "{\"user\":{\"name\":\"test\",\"age\":30}}";
        var element = JsonDocument.Parse(json).RootElement;

        // Act
        var result = element.ToDynamic();

        // Assert
        Assert.NotNull(result);
        dynamic dynamicResult = result!;
        Assert.Equal("test", dynamicResult.user.name);
        Assert.Equal(30L, dynamicResult.user.age);
    }

    [Fact]
    public void ToDynamic_WithArray_ShouldReturnList()
    {
        // Arrange
        var json = "[1,2,3,4,5]";
        var element = JsonDocument.Parse(json).RootElement;

        // Act
        var result = element.ToDynamic();

        // Assert
        Assert.NotNull(result);
        var list = result as List<object?>;
        Assert.NotNull(list);
        Assert.Equal(5, list!.Count);
        Assert.Equal(1L, list[0]);
        Assert.Equal(5L, list[4]);
    }

    [Fact]
    public void ToDynamic_WithArrayOfObjects_ShouldReturnListOfExpandoObjects()
    {
        // Arrange
        var json = "[{\"name\":\"user1\"},{\"name\":\"user2\"}]";
        var element = JsonDocument.Parse(json).RootElement;

        // Act
        var result = element.ToDynamic();

        // Assert
        Assert.NotNull(result);
        var list = result as List<object?>;
        Assert.NotNull(list);
        Assert.Equal(2, list!.Count);

        dynamic first = list[0]!;
        dynamic second = list[1]!;
        Assert.Equal("user1", first.name);
        Assert.Equal("user2", second.name);
    }

    [Fact]
    public void ToDynamic_WithString_ShouldReturnString()
    {
        // Arrange
        var json = "\"test string\"";
        var element = JsonDocument.Parse(json).RootElement;

        // Act
        var result = element.ToDynamic();

        // Assert
        Assert.Equal("test string", result);
    }

    [Fact]
    public void ToDynamic_WithNumber_ShouldReturnNumber()
    {
        // Arrange
        var json = "42";
        var element = JsonDocument.Parse(json).RootElement;

        // Act
        var result = element.ToDynamic();

        // Assert
        Assert.Equal(42L, result);
    }

    [Fact]
    public void ToDynamic_WithDouble_ShouldReturnDouble()
    {
        // Arrange
        var json = "42.5";
        var element = JsonDocument.Parse(json).RootElement;

        // Act
        var result = element.ToDynamic();

        // Assert
        Assert.Equal(42.5, result);
    }

    [Fact]
    public void ToDynamic_WithBoolean_ShouldReturnBoolean()
    {
        // Arrange
        var jsonTrue = "true";
        var jsonFalse = "false";
        var elementTrue = JsonDocument.Parse(jsonTrue).RootElement;
        var elementFalse = JsonDocument.Parse(jsonFalse).RootElement;

        // Act
        var resultTrue = elementTrue.ToDynamic();
        var resultFalse = elementFalse.ToDynamic();

        // Assert
        Assert.True((bool)resultTrue!);
        Assert.False((bool)resultFalse!);
    }

    [Fact]
    public void ToDynamic_WithNull_ShouldReturnNull()
    {
        // Arrange
        var json = "null";
        var element = JsonDocument.Parse(json).RootElement;

        // Act
        var result = element.ToDynamic();

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void ToDynamic_WithEmptyObject_ShouldReturnEmptyExpandoObject()
    {
        // Arrange
        var json = "{}";
        var element = JsonDocument.Parse(json).RootElement;

        // Act
        var result = element.ToDynamic();

        // Assert
        Assert.NotNull(result);
        var dict = result as IDictionary<string, object?>;
        Assert.NotNull(dict);
        Assert.Empty(dict!);
    }

    [Fact]
    public void ToDynamic_WithEmptyArray_ShouldReturnEmptyList()
    {
        // Arrange
        var json = "[]";
        var element = JsonDocument.Parse(json).RootElement;

        // Act
        var result = element.ToDynamic();

        // Assert
        Assert.NotNull(result);
        var list = result as List<object?>;
        Assert.NotNull(list);
        Assert.Empty(list!);
    }

    [Fact]
    public void ToDynamic_WithComplexNestedStructure_ShouldConvertCorrectly()
    {
        // Arrange
        var json = @"{
            ""name"": ""test"",
            ""data"": {
                ""items"": [1, 2, 3],
                ""metadata"": {
                    ""created"": ""2023-01-01"",
                    ""tags"": [""tag1"", ""tag2""]
                }
            }
        }";
        var element = JsonDocument.Parse(json).RootElement;

        // Act
        var result = element.ToDynamic();

        // Assert
        Assert.NotNull(result);
        dynamic dynamicResult = result!;
        Assert.Equal("test", dynamicResult.name);

        var items = dynamicResult.data.items as List<object?>;
        Assert.NotNull(items);
        Assert.Equal(3, items!.Count);

        Assert.Equal("2023-01-01", dynamicResult.data.metadata.created);

        var tags = dynamicResult.data.metadata.tags as List<object?>;
        Assert.NotNull(tags);
        Assert.Equal(2, tags!.Count);
    }

    [Fact]
    public void ToDynamic_WithMixedArray_ShouldHandleMultipleTypes()
    {
        // Arrange
        var json = "[1, \"text\", true, null, {\"key\":\"value\"}]";
        var element = JsonDocument.Parse(json).RootElement;

        // Act
        var result = element.ToDynamic();

        // Assert
        Assert.NotNull(result);
        var list = result as List<object?>;
        Assert.NotNull(list);
        Assert.Equal(5, list!.Count);
        Assert.Equal(1L, list[0]);
        Assert.Equal("text", list[1]);
        Assert.True((bool)list[2]!);
        Assert.Null(list[3]);

        dynamic obj = list[4]!;
        Assert.Equal("value", obj.key);
    }

    [Fact]
    public void ToDynamic_WithEmptyString_ShouldReturnEmptyString()
    {
        // Arrange
        var json = "\"\"";
        var element = JsonDocument.Parse(json).RootElement;

        // Act
        var result = element.ToDynamic();

        // Assert
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void ToDynamic_WithLargeNumber_ShouldHandleCorrectly()
    {
        // Arrange
        var json = "9223372036854775807"; // long.MaxValue
        var element = JsonDocument.Parse(json).RootElement;

        // Act
        var result = element.ToDynamic();

        // Assert
        Assert.Equal(long.MaxValue, result);
    }

    [Fact]
    public void ToDynamic_WithVeryLargeNumber_ShouldFallbackToDouble()
    {
        // Arrange
        var json = "9223372036854775808.5"; // Beyond long.MaxValue
        var element = JsonDocument.Parse(json).RootElement;

        // Act
        var result = element.ToDynamic();

        // Assert
        Assert.IsType<double>(result);
    }

    [Fact]
    public void JsonOptions_ShouldHaveCorrectSettings()
    {
        // Assert
        Assert.False(JsonDocumentExtensions.JsonOptions.WriteIndented);
        Assert.NotNull(JsonDocumentExtensions.JsonOptions.PropertyNamingPolicy);
        Assert.Equal(JsonNamingPolicy.CamelCase, JsonDocumentExtensions.JsonOptions.PropertyNamingPolicy);
    }
}

