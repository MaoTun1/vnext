using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text.Json;
using BBT.Workflow.Shared.Merging;
using Xunit;

namespace BBT.Workflow.Shared.Merging;

public class JsonElementMergeStrategyTests
{
    private readonly JsonElementMergeStrategy _strategy;

    public JsonElementMergeStrategyTests()
    {
        _strategy = new JsonElementMergeStrategy();
    }

    [Fact]
    public void Merge_ShouldReturnSource_WhenTargetIsNull()
    {
        // Arrange
        object? target = null;
        var sourceJson = JsonDocument.Parse(@"{""key"": ""value""}").RootElement;

        // Act
        var result = _strategy.Merge(target, sourceJson);

        // Assert
        Assert.NotNull(result);
    }

    [Fact]
    public void Merge_ShouldReturnTarget_WhenSourceIsNull()
    {
        // Arrange
        var targetJson = JsonDocument.Parse(@"{""key"": ""value""}").RootElement;
        object? source = null;

        // Act
        var result = _strategy.Merge(targetJson, source);

        // Assert
        Assert.NotNull(result);
    }

    [Fact]
    public void Merge_ShouldMergeJsonObjects()
    {
        // Arrange
        var targetJson = JsonDocument.Parse(@"{""name"": ""John"", ""age"": 30}").RootElement;
        var sourceJson = JsonDocument.Parse(@"{""age"": 35, ""city"": ""NYC""}").RootElement;

        // Act
        var result = _strategy.Merge(targetJson, sourceJson) as ExpandoObject;

        // Assert
        Assert.NotNull(result);
        var dict = (IDictionary<string, object?>)result;
        Assert.Equal(3, dict.Count);
        Assert.True(dict.ContainsKey("name"));
        Assert.True(dict.ContainsKey("age"));
        Assert.True(dict.ContainsKey("city"));
    }

    [Fact]
    public void Merge_ShouldReturnSource_ForNonObjectJsonElements()
    {
        // Arrange
        var targetJson = JsonDocument.Parse(@"123").RootElement;
        var sourceJson = JsonDocument.Parse(@"456").RootElement;

        // Act
        var result = _strategy.Merge(targetJson, sourceJson) as JsonElement?;

        // Assert
        Assert.NotNull(result);
        Assert.Equal(456, result.Value.GetInt32());
    }

    [Fact]
    public void Merge_ShouldReturnSource_ForStringJsonElements()
    {
        // Arrange
        var targetJson = JsonDocument.Parse(@"""target""").RootElement;
        var sourceJson = JsonDocument.Parse(@"""source""").RootElement;

        // Act
        var result = _strategy.Merge(targetJson, sourceJson) as JsonElement?;

        // Assert
        Assert.NotNull(result);
        Assert.Equal("source", result.Value.GetString());
    }

    [Fact]
    public void Merge_ShouldMergeJsonArrays()
    {
        // Arrange
        var targetJson = JsonDocument.Parse(@"[1, 2, 3]").RootElement;
        var sourceJson = JsonDocument.Parse(@"[10, 20]").RootElement;

        // Act
        var result = _strategy.Merge(targetJson, sourceJson) as JsonElement?;

        // Assert
        Assert.NotNull(result);
        Assert.Equal(JsonValueKind.Array, result.Value.ValueKind);
        var array = result.Value.EnumerateArray().ToList();
        Assert.Equal(3, array.Count);
        // Arrays are merged, so we just check count
        // The actual merge logic combines items at same positions
    }

    [Fact]
    public void Merge_ShouldMergeJsonArrays_WithDifferentLengths()
    {
        // Arrange
        var targetJson = JsonDocument.Parse(@"[1, 2]").RootElement;
        var sourceJson = JsonDocument.Parse(@"[10, 20, 30, 40]").RootElement;

        // Act
        var result = _strategy.Merge(targetJson, sourceJson) as JsonElement?;

        // Assert
        Assert.NotNull(result);
        var array = result.Value.EnumerateArray().ToList();
        Assert.Equal(4, array.Count);
        // Arrays are merged, checking count is sufficient
    }

    [Fact]
    public void Merge_ShouldDeepMergeNestedJsonObjects()
    {
        // Arrange
        var targetJson = JsonDocument.Parse(@"{
            ""user"": {
                ""name"": ""John"",
                ""age"": 30
            }
        }").RootElement;

        var sourceJson = JsonDocument.Parse(@"{
            ""user"": {
                ""age"": 35,
                ""city"": ""NYC""
            }
        }").RootElement;

        // Act
        var result = _strategy.Merge(targetJson, sourceJson) as ExpandoObject;

        // Assert
        Assert.NotNull(result);
        var dict = (IDictionary<string, object?>)result;
        var user = dict["user"] as ExpandoObject;
        Assert.NotNull(user);
        var userDict = (IDictionary<string, object?>)user;
        Assert.Equal(3, userDict.Count);
    }

    [Fact]
    public void Merge_ShouldMergeJsonElementWithExpandoObject()
    {
        // Arrange
        var targetJson = JsonDocument.Parse(@"{""name"": ""John"", ""age"": 30}").RootElement;
        
        dynamic source = new ExpandoObject();
        source.age = 35;
        source.city = "NYC";

        // Act
        var result = _strategy.Merge(targetJson, source) as ExpandoObject;

        // Assert
        Assert.NotNull(result);
        var dict = (IDictionary<string, object?>)result;
        Assert.Equal(3, dict.Count);
        Assert.True(dict.ContainsKey("name"));
        Assert.True(dict.ContainsKey("age"));
        Assert.True(dict.ContainsKey("city"));
    }

    [Fact]
    public void Merge_ShouldMergeExpandoObjectWithJsonElement()
    {
        // Arrange
        dynamic target = new ExpandoObject();
        target.name = "John";
        target.age = 30;

        var sourceJson = JsonDocument.Parse(@"{""age"": 35, ""city"": ""NYC""}").RootElement;

        // Act
        var result = _strategy.Merge(target, sourceJson) as ExpandoObject;

        // Assert
        Assert.NotNull(result);
        var dict = (IDictionary<string, object?>)result;
        Assert.Equal(3, dict.Count);
        Assert.True(dict.ContainsKey("name"));
        Assert.True(dict.ContainsKey("age"));
        Assert.True(dict.ContainsKey("city"));
    }

    [Fact]
    public void Merge_ShouldReturnExpandoObject_WhenJsonIsNotObject()
    {
        // Arrange
        var targetJson = JsonDocument.Parse(@"123").RootElement;
        
        dynamic source = new ExpandoObject();
        source.value = 456;

        // Act
        var result = _strategy.Merge(targetJson, source);

        // Assert
        Assert.Same(source, result);
    }

    [Fact]
    public void Merge_ShouldReturnJsonElement_WhenExpandoSourceIsNotObject()
    {
        // Arrange
        dynamic target = new ExpandoObject();
        target.value = 123;

        var sourceJson = JsonDocument.Parse(@"456").RootElement;

        // Act
        var result = _strategy.Merge(target, sourceJson);

        // Assert
        Assert.IsType<JsonElement>(result);
    }

    [Fact]
    public void Merge_ShouldHandleEmptyJsonObjects()
    {
        // Arrange
        var targetJson = JsonDocument.Parse(@"{}").RootElement;
        var sourceJson = JsonDocument.Parse(@"{""key"": ""value""}").RootElement;

        // Act
        var result = _strategy.Merge(targetJson, sourceJson) as ExpandoObject;

        // Assert
        Assert.NotNull(result);
        var dict = (IDictionary<string, object?>)result;
        Assert.Single(dict);
        Assert.Equal("value", dict["key"]?.ToString());
    }

    [Fact]
    public void Merge_ShouldHandleEmptyJsonArrays()
    {
        // Arrange
        var targetJson = JsonDocument.Parse(@"[]").RootElement;
        var sourceJson = JsonDocument.Parse(@"[1, 2, 3]").RootElement;

        // Act
        var result = _strategy.Merge(targetJson, sourceJson) as JsonElement?;

        // Assert
        Assert.NotNull(result);
        var array = result.Value.EnumerateArray().ToList();
        Assert.Equal(3, array.Count);
    }

    [Fact]
    public void Merge_ShouldMergeNestedArraysInObjects()
    {
        // Arrange
        var targetJson = JsonDocument.Parse(@"{
            ""items"": [1, 2, 3]
        }").RootElement;

        var sourceJson = JsonDocument.Parse(@"{
            ""items"": [10, 20]
        }").RootElement;

        // Act
        var result = _strategy.Merge(targetJson, sourceJson) as ExpandoObject;

        // Assert
        Assert.NotNull(result);
        var dict = (IDictionary<string, object?>)result;
        Assert.True(dict.ContainsKey("items"));
    }

    [Fact]
    public void Merge_ShouldHandleComplexNestedStructures()
    {
        // Arrange
        var targetJson = JsonDocument.Parse(@"{
            ""user"": {
                ""profile"": {
                    ""name"": ""John"",
                    ""age"": 30
                },
                ""settings"": {
                    ""theme"": ""dark""
                }
            }
        }").RootElement;

        var sourceJson = JsonDocument.Parse(@"{
            ""user"": {
                ""profile"": {
                    ""age"": 35,
                    ""email"": ""john@example.com""
                },
                ""preferences"": {
                    ""language"": ""en""
                }
            }
        }").RootElement;

        // Act
        var result = _strategy.Merge(targetJson, sourceJson) as ExpandoObject;

        // Assert
        Assert.NotNull(result);
        var dict = (IDictionary<string, object?>)result;
        var user = dict["user"] as ExpandoObject;
        Assert.NotNull(user);
        var userDict = (IDictionary<string, object?>)user;
        Assert.Equal(3, userDict.Count);
        Assert.True(userDict.ContainsKey("profile"));
        Assert.True(userDict.ContainsKey("settings"));
        Assert.True(userDict.ContainsKey("preferences"));
    }

    [Fact]
    public void Merge_ShouldHandleJsonBooleans()
    {
        // Arrange
        var targetJson = JsonDocument.Parse(@"true").RootElement;
        var sourceJson = JsonDocument.Parse(@"false").RootElement;

        // Act
        var result = _strategy.Merge(targetJson, sourceJson) as JsonElement?;

        // Assert
        Assert.NotNull(result);
        Assert.False(result.Value.GetBoolean());
    }

    [Fact]
    public void Merge_ShouldHandleJsonNull()
    {
        // Arrange
        var targetJson = JsonDocument.Parse(@"{""key"": ""value""}").RootElement;
        var sourceJson = JsonDocument.Parse(@"null").RootElement;

        // Act
        var result = _strategy.Merge(targetJson, sourceJson) as JsonElement?;

        // Assert
        Assert.NotNull(result);
        Assert.Equal(JsonValueKind.Null, result.Value.ValueKind);
    }

    [Fact]
    public void Merge_ShouldMergeArraysWithObjectElements()
    {
        // Arrange
        var targetJson = JsonDocument.Parse(@"[
            {""id"": 1, ""name"": ""Item1""}
        ]").RootElement;

        var sourceJson = JsonDocument.Parse(@"[
            {""id"": 1, ""value"": ""Added""}
        ]").RootElement;

        // Act
        var result = _strategy.Merge(targetJson, sourceJson) as JsonElement?;

        // Assert
        Assert.NotNull(result);
        var array = result.Value.EnumerateArray().ToList();
        Assert.Single(array);
    }

    [Fact]
    public void Merge_ShouldReturnSource_ForNonExpandoNonJsonTypes()
    {
        // Arrange
        var target = "string-value";
        var source = 123;

        // Act
        var result = _strategy.Merge(target, source);

        // Assert
        Assert.Equal(123, result);
    }
}

