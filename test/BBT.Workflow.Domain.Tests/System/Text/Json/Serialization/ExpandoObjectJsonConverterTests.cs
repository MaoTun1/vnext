using System.Collections.Generic;
using System.Dynamic;
using Xunit;

namespace System.Text.Json.Serialization;

/// <summary>
/// Unit tests for ExpandoObjectJsonConverter
/// </summary>
public class ExpandoObjectJsonConverterTests
{
    private readonly JsonSerializerOptions _options;

    public ExpandoObjectJsonConverterTests()
    {
        _options = new JsonSerializerOptions();
        _options.Converters.Add(new ExpandoObjectJsonConverter());
    }

    #region Read Tests - Simple Values

    [Fact]
    public void Read_SimpleObject_ShouldDeserializeCorrectly()
    {
        // Arrange
        var json = "{\"name\":\"test\",\"age\":30}";

        // Act
        var result = JsonSerializer.Deserialize<ExpandoObject>(json, _options);

        // Assert
        Assert.NotNull(result);
        dynamic dynamicResult = result!;
        Assert.Equal("test", dynamicResult.name);
        Assert.Equal(30, dynamicResult.age);
    }

    [Fact]
    public void Read_EmptyObject_ShouldReturnEmptyExpando()
    {
        // Arrange
        var json = "{}";

        // Act
        var result = JsonSerializer.Deserialize<ExpandoObject>(json, _options);

        // Assert
        Assert.NotNull(result);
        var dict = result as IDictionary<string, object?>;
        Assert.NotNull(dict);
        Assert.Empty(dict!);
    }

    [Fact]
    public void Read_StringProperty_ShouldDeserialize()
    {
        // Arrange
        var json = "{\"text\":\"hello world\"}";

        // Act
        var result = JsonSerializer.Deserialize<ExpandoObject>(json, _options);

        // Assert
        Assert.NotNull(result);
        dynamic dynamicResult = result!;
        Assert.Equal("hello world", dynamicResult.text);
    }

    [Fact]
    public void Read_NumberProperty_ShouldDeserializeAsInt()
    {
        // Arrange
        var json = "{\"count\":42}";

        // Act
        var result = JsonSerializer.Deserialize<ExpandoObject>(json, _options);

        // Assert
        Assert.NotNull(result);
        dynamic dynamicResult = result!;
        Assert.Equal(42, dynamicResult.count);
    }

    [Fact]
    public void Read_DoubleProperty_ShouldDeserializeAsDouble()
    {
        // Arrange
        var json = "{\"price\":99.99}";

        // Act
        var result = JsonSerializer.Deserialize<ExpandoObject>(json, _options);

        // Assert
        Assert.NotNull(result);
        dynamic dynamicResult = result!;
        Assert.Equal(99.99, dynamicResult.price);
    }

    [Fact]
    public void Read_BooleanProperties_ShouldDeserialize()
    {
        // Arrange
        var json = "{\"isActive\":true,\"isDeleted\":false}";

        // Act
        var result = JsonSerializer.Deserialize<ExpandoObject>(json, _options);

        // Assert
        Assert.NotNull(result);
        dynamic dynamicResult = result!;
        Assert.True(dynamicResult.isActive);
        Assert.False(dynamicResult.isDeleted);
    }

    [Fact]
    public void Read_NullProperty_ShouldDeserialize()
    {
        // Arrange
        var json = "{\"value\":null}";

        // Act
        var result = JsonSerializer.Deserialize<ExpandoObject>(json, _options);

        // Assert
        Assert.NotNull(result);
        dynamic dynamicResult = result!;
        Assert.Null(dynamicResult.value);
    }

    #endregion

    #region Read Tests - Nested Objects

    [Fact]
    public void Read_NestedObject_ShouldDeserializeRecursively()
    {
        // Arrange
        var json = "{\"user\":{\"name\":\"test\",\"age\":30}}";

        // Act
        var result = JsonSerializer.Deserialize<ExpandoObject>(json, _options);

        // Assert
        Assert.NotNull(result);
        dynamic dynamicResult = result!;
        Assert.Equal("test", dynamicResult.user.name);
        Assert.Equal(30, dynamicResult.user.age);
    }

    [Fact]
    public void Read_DeeplyNestedObject_ShouldDeserialize()
    {
        // Arrange
        var json = "{\"level1\":{\"level2\":{\"level3\":{\"value\":\"deep\"}}}}";

        // Act
        var result = JsonSerializer.Deserialize<ExpandoObject>(json, _options);

        // Assert
        Assert.NotNull(result);
        dynamic dynamicResult = result!;
        Assert.Equal("deep", dynamicResult.level1.level2.level3.value);
    }

    #endregion

    #region Read Tests - Arrays

    [Fact]
    public void Read_ArrayProperty_ShouldDeserializeAsArray()
    {
        // Arrange
        var json = "{\"items\":[1,2,3,4,5]}";

        // Act
        var result = JsonSerializer.Deserialize<ExpandoObject>(json, _options);

        // Assert
        Assert.NotNull(result);
        dynamic dynamicResult = result!;
        var items = dynamicResult.items as object?[];
        Assert.NotNull(items);
        Assert.Equal(5, items!.Length);
        // Numbers are deserialized as int or double
        Assert.True(items[0] is int or double);
        Assert.True(items[4] is int or double);
    }

    [Fact]
    public void Read_ArrayOfObjects_ShouldDeserialize()
    {
        // Arrange
        var json = "{\"users\":[{\"name\":\"user1\"},{\"name\":\"user2\"}]}";

        // Act
        var result = JsonSerializer.Deserialize<ExpandoObject>(json, _options);

        // Assert
        Assert.NotNull(result);
        dynamic dynamicResult = result!;
        var users = dynamicResult.users as object?[];
        Assert.NotNull(users);
        Assert.Equal(2, users!.Length);

        dynamic first = users[0]!;
        Assert.Equal("user1", first.name);
    }

    [Fact]
    public void Read_EmptyArray_ShouldDeserialize()
    {
        // Arrange
        var json = "{\"items\":[]}";

        // Act
        var result = JsonSerializer.Deserialize<ExpandoObject>(json, _options);

        // Assert
        Assert.NotNull(result);
        dynamic dynamicResult = result!;
        var items = dynamicResult.items as object?[];
        Assert.NotNull(items);
        Assert.Empty(items!);
    }

    [Fact]
    public void Read_MixedTypeArray_ShouldDeserialize()
    {
        // Arrange
        var json = "{\"mixed\":[1,\"text\",true,null,{\"key\":\"value\"}]}";

        // Act
        var result = JsonSerializer.Deserialize<ExpandoObject>(json, _options);

        // Assert
        Assert.NotNull(result);
        dynamic dynamicResult = result!;
        var mixed = dynamicResult.mixed as object?[];
        Assert.NotNull(mixed);
        Assert.Equal(5, mixed!.Length);
        // Number can be int or double
        Assert.True(mixed[0] is int or double);
        Assert.Equal("text", mixed[1]);
        Assert.True((bool)mixed[2]!);
        Assert.Null(mixed[3]);
    }

    #endregion

    #region Read Tests - Complex Scenarios

    [Fact]
    public void Read_ComplexNestedStructure_ShouldDeserialize()
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

        // Act
        var result = JsonSerializer.Deserialize<ExpandoObject>(json, _options);

        // Assert
        Assert.NotNull(result);
        dynamic dynamicResult = result!;
        Assert.Equal("test", dynamicResult.name);

        var items = dynamicResult.data.items as object?[];
        Assert.Equal(3, items!.Length);

        Assert.Equal("2023-01-01", dynamicResult.data.metadata.created);

        var tags = dynamicResult.data.metadata.tags as object?[];
        Assert.Equal(2, tags!.Length);
    }

    #endregion

    #region Read Tests - Error Cases

    [Fact]
    public void Read_InvalidJsonToken_ShouldThrowJsonException()
    {
        // Arrange
        var json = "[1,2,3]"; // Array instead of object

        // Act & Assert
        Assert.Throws<JsonException>(() => 
            JsonSerializer.Deserialize<ExpandoObject>(json, _options));
    }

    [Fact]
    public void Read_PrimitiveValue_ShouldThrowJsonException()
    {
        // Arrange
        var json = "42";

        // Act & Assert
        Assert.Throws<JsonException>(() => 
            JsonSerializer.Deserialize<ExpandoObject>(json, _options));
    }

    [Fact]
    public void Read_StringValue_ShouldThrowJsonException()
    {
        // Arrange
        var json = "\"test\"";

        // Act & Assert
        Assert.Throws<JsonException>(() => 
            JsonSerializer.Deserialize<ExpandoObject>(json, _options));
    }

    #endregion

    #region Write Tests

    [Fact]
    public void Write_SimpleExpandoObject_ShouldSerialize()
    {
        // Arrange
        dynamic expando = new ExpandoObject();
        expando.name = "test";
        expando.age = 30;

        // Act
        var json = JsonSerializer.Serialize(expando, _options);

        // Assert
        Assert.Contains("\"name\"", json);
        Assert.Contains("\"test\"", json);
        Assert.Contains("\"age\"", json);
        Assert.Contains("30", json);
    }

    [Fact]
    public void Write_EmptyExpandoObject_ShouldSerializeAsEmptyObject()
    {
        // Arrange
        var expando = new ExpandoObject();

        // Act
        var json = JsonSerializer.Serialize(expando, _options);

        // Assert
        Assert.Equal("{}", json);
    }

    [Fact]
    public void Write_NestedExpandoObject_ShouldSerialize()
    {
        // Arrange
        dynamic expando = new ExpandoObject();
        expando.user = new ExpandoObject();
        expando.user.name = "test";
        expando.user.age = 30;

        // Act
        var json = JsonSerializer.Serialize(expando, _options);

        // Assert
        Assert.Contains("\"user\"", json);
        Assert.Contains("\"name\"", json);
        Assert.Contains("\"test\"", json);
    }

    [Fact]
    public void Write_WithNullProperty_ShouldSerialize()
    {
        // Arrange
        dynamic expando = new ExpandoObject();
        expando.name = "test";
        expando.value = null;

        // Act
        var json = JsonSerializer.Serialize(expando, _options);

        // Assert
        Assert.Contains("\"name\"", json);
        Assert.Contains("null", json);
    }

    #endregion

    #region Round-trip Tests

    [Fact]
    public void RoundTrip_SimpleObject_ShouldMaintainData()
    {
        // Arrange
        dynamic original = new ExpandoObject();
        original.name = "test";
        original.age = 30;
        original.isActive = true;

        // Act
        var json = JsonSerializer.Serialize(original, _options);
        var result = JsonSerializer.Deserialize<ExpandoObject>(json, _options);

        // Assert
        Assert.NotNull(result);
        dynamic dynamicResult = result!;
        Assert.Equal("test", dynamicResult.name);
        Assert.Equal(30, dynamicResult.age);
        Assert.True(dynamicResult.isActive);
    }

    [Fact]
    public void RoundTrip_ComplexNestedObject_ShouldMaintainData()
    {
        // Arrange
        var json = @"{
            ""name"": ""test"",
            ""data"": {
                ""items"": [1, 2, 3],
                ""metadata"": {
                    ""created"": ""2023-01-01""
                }
            }
        }";

        // Act
        var deserialized = JsonSerializer.Deserialize<ExpandoObject>(json, _options);
        var reserialized = JsonSerializer.Serialize(deserialized, _options);
        var result = JsonSerializer.Deserialize<ExpandoObject>(reserialized, _options);

        // Assert
        Assert.NotNull(result);
        dynamic dynamicResult = result!;
        Assert.Equal("test", dynamicResult.name);
        Assert.Equal("2023-01-01", dynamicResult.data.metadata.created);
    }

    #endregion
}

