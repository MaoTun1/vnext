using System;
using System.Text.Json;
using Xunit;

namespace BBT.Workflow;

public class JsonSerializerConstantsTests
{
    [Fact]
    public void JsonOptions_ShouldNotBeNull()
    {
        // Act
        var options = JsonSerializerConstants.JsonOptions;

        // Assert
        Assert.NotNull(options);
    }

    [Fact]
    public void JsonOptions_WriteIndented_ShouldBeFalse()
    {
        // Act
        var options = JsonSerializerConstants.JsonOptions;

        // Assert
        Assert.False(options.WriteIndented);
    }

    [Fact]
    public void JsonOptions_PropertyNamingPolicy_ShouldBeCamelCase()
    {
        // Act
        var options = JsonSerializerConstants.JsonOptions;

        // Assert
        Assert.NotNull(options.PropertyNamingPolicy);
        Assert.Equal(JsonNamingPolicy.CamelCase, options.PropertyNamingPolicy);
    }

    [Fact]
    public void JsonOptions_IncludeFields_ShouldBeTrue()
    {
        // Act
        var options = JsonSerializerConstants.JsonOptions;

        // Assert
        Assert.True(options.IncludeFields);
    }

    [Fact]
    public void JsonOptions_ShouldSerializeWithCamelCase()
    {
        // Arrange
        var obj = new { FirstName = "John", LastName = "Doe" };
        var options = JsonSerializerConstants.JsonOptions;

        // Act
        var json = JsonSerializer.Serialize(obj, options);

        // Assert
        Assert.Contains("\"firstName\"", json);
        Assert.Contains("\"lastName\"", json);
        Assert.DoesNotContain("\"FirstName\"", json);
        Assert.DoesNotContain("\"LastName\"", json);
    }

    [Fact]
    public void JsonOptions_ShouldSerializeCompact()
    {
        // Arrange
        var obj = new { Name = "Test", Value = 123 };
        var options = JsonSerializerConstants.JsonOptions;

        // Act
        var json = JsonSerializer.Serialize(obj, options);

        // Assert
        Assert.DoesNotContain("\n", json);
        Assert.DoesNotContain("  ", json);
    }

    [Fact]
    public void JsonOptions_ShouldIncludeFields_InSerialization()
    {
        // Arrange
        var obj = new TestClassWithFields { PublicField = "test" };
        var options = JsonSerializerConstants.JsonOptions;

        // Act
        var json = JsonSerializer.Serialize(obj, options);

        // Assert
        Assert.Contains("\"publicField\"", json);
        Assert.Contains("test", json);
    }

    [Fact]
    public void JsonOptions_ShouldBeReusable()
    {
        // Arrange
        var obj1 = new { Name = "Test1" };
        var obj2 = new { Name = "Test2" };
        var options = JsonSerializerConstants.JsonOptions;

        // Act
        var json1 = JsonSerializer.Serialize(obj1, options);
        var json2 = JsonSerializer.Serialize(obj2, options);

        // Assert
        Assert.Contains("\"name\":\"Test1\"", json1);
        Assert.Contains("\"name\":\"Test2\"", json2);
    }

    [Fact]
    public void JsonOptions_ShouldBeSameInstance()
    {
        // Arrange & Act
        var options1 = JsonSerializerConstants.JsonOptions;
        var options2 = JsonSerializerConstants.JsonOptions;

        // Assert
        Assert.Same(options1, options2);
    }

    private class TestClassWithFields
    {
        public string PublicField;
    }
}

