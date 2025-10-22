using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text.Json;
using BBT.Workflow.Shared.Merging;
using Xunit;

namespace BBT.Workflow.Shared.Merging;

public class MergeStrategyFactoryTests
{
    [Fact]
    public void GetStrategy_ShouldReturnDefaultStrategy_WhenTargetIsNull()
    {
        // Arrange
        object? target = null;
        var source = "value";

        // Act
        var strategy = MergeStrategyFactory.GetStrategy(target, source);

        // Assert
        Assert.IsType<DefaultMergeStrategy>(strategy);
    }

    [Fact]
    public void GetStrategy_ShouldReturnDefaultStrategy_WhenSourceIsNull()
    {
        // Arrange
        var target = "value";
        object? source = null;

        // Act
        var strategy = MergeStrategyFactory.GetStrategy(target, source);

        // Assert
        Assert.IsType<DefaultMergeStrategy>(strategy);
    }

    [Fact]
    public void GetStrategy_ShouldReturnDefaultStrategy_WhenBothAreNull()
    {
        // Arrange
        object? target = null;
        object? source = null;

        // Act
        var strategy = MergeStrategyFactory.GetStrategy(target, source);

        // Assert
        Assert.IsType<DefaultMergeStrategy>(strategy);
    }

    [Fact]
    public void GetStrategy_ShouldReturnExpandoObjectStrategy_WhenBothAreExpandoObjects()
    {
        // Arrange
        var target = new ExpandoObject();
        var source = new ExpandoObject();

        // Act
        var strategy = MergeStrategyFactory.GetStrategy(target, source);

        // Assert
        Assert.IsType<ExpandoObjectMergeStrategy>(strategy);
    }

    [Fact]
    public void GetStrategy_ShouldReturnJsonElementStrategy_WhenBothAreJsonElements()
    {
        // Arrange
        var target = JsonDocument.Parse(@"{""key"": ""value""}").RootElement;
        var source = JsonDocument.Parse(@"{""key2"": ""value2""}").RootElement;

        // Act
        var strategy = MergeStrategyFactory.GetStrategy(target, source);

        // Assert
        Assert.IsType<JsonElementMergeStrategy>(strategy);
    }

    [Fact]
    public void GetStrategy_ShouldReturnJsonElementStrategy_WhenTargetIsJsonAndSourceIsExpando()
    {
        // Arrange
        var target = JsonDocument.Parse(@"{""key"": ""value""}").RootElement;
        var source = new ExpandoObject();

        // Act
        var strategy = MergeStrategyFactory.GetStrategy(target, source);

        // Assert
        Assert.IsType<JsonElementMergeStrategy>(strategy);
    }

    [Fact]
    public void GetStrategy_ShouldReturnJsonElementStrategy_WhenTargetIsExpandoAndSourceIsJson()
    {
        // Arrange
        var target = new ExpandoObject();
        var source = JsonDocument.Parse(@"{""key"": ""value""}").RootElement;

        // Act
        var strategy = MergeStrategyFactory.GetStrategy(target, source);

        // Assert
        Assert.IsType<JsonElementMergeStrategy>(strategy);
    }

    [Fact]
    public void GetStrategy_ShouldReturnCollectionStrategy_WhenBothAreLists()
    {
        // Arrange
        var target = new List<int> { 1, 2, 3 };
        var source = new List<int> { 4, 5, 6 };

        // Act
        var strategy = MergeStrategyFactory.GetStrategy(target, source);

        // Assert
        Assert.IsType<CollectionMergeStrategy>(strategy);
    }

    [Fact]
    public void GetStrategy_ShouldReturnCollectionStrategy_WhenBothAreDictionaries()
    {
        // Arrange
        var target = new Dictionary<string, object> { { "key1", "value1" } };
        var source = new Dictionary<string, object> { { "key2", "value2" } };

        // Act
        var strategy = MergeStrategyFactory.GetStrategy(target, source);

        // Assert
        Assert.IsType<CollectionMergeStrategy>(strategy);
    }

    [Fact]
    public void GetStrategy_ShouldReturnCollectionStrategy_WhenBothAreArrays()
    {
        // Arrange
        var target = new[] { 1, 2, 3 };
        var source = new[] { 4, 5, 6 };

        // Act
        var strategy = MergeStrategyFactory.GetStrategy(target, source);

        // Assert
        Assert.IsType<CollectionMergeStrategy>(strategy);
    }

    [Fact]
    public void GetStrategy_ShouldReturnDefaultStrategy_WhenTargetIsString()
    {
        // Arrange
        var target = "string-value";
        var source = "another-string";

        // Act
        var strategy = MergeStrategyFactory.GetStrategy(target, source);

        // Assert
        Assert.IsType<DefaultMergeStrategy>(strategy);
    }

    [Fact]
    public void GetStrategy_ShouldReturnDefaultStrategy_ForSimpleTypes()
    {
        // Arrange
        var target = 123;
        var source = 456;

        // Act
        var strategy = MergeStrategyFactory.GetStrategy(target, source);

        // Assert
        Assert.IsType<DefaultMergeStrategy>(strategy);
    }

    [Fact]
    public void GetStrategy_ShouldReturnDefaultStrategy_ForBooleans()
    {
        // Arrange
        var target = true;
        var source = false;

        // Act
        var strategy = MergeStrategyFactory.GetStrategy(target, source);

        // Assert
        Assert.IsType<DefaultMergeStrategy>(strategy);
    }

    [Fact]
    public void GetStrategy_ShouldReturnDefaultStrategy_ForMixedSimpleTypes()
    {
        // Arrange
        var target = 123;
        var source = "string";

        // Act
        var strategy = MergeStrategyFactory.GetStrategy(target, source);

        // Assert
        Assert.IsType<DefaultMergeStrategy>(strategy);
    }

    [Fact]
    public void GetStrategy_ShouldReturnSameInstance_ForMultipleCalls()
    {
        // Arrange
        var target1 = new ExpandoObject();
        var source1 = new ExpandoObject();
        var target2 = new ExpandoObject();
        var source2 = new ExpandoObject();

        // Act
        var strategy1 = MergeStrategyFactory.GetStrategy(target1, source1);
        var strategy2 = MergeStrategyFactory.GetStrategy(target2, source2);

        // Assert
        Assert.Same(strategy1, strategy2);
    }

    [Fact]
    public void GetStrategy_ShouldUseSingletonInstances()
    {
        // Arrange & Act
        var expandoStrategy1 = MergeStrategyFactory.GetStrategy(new ExpandoObject(), new ExpandoObject());
        var expandoStrategy2 = MergeStrategyFactory.GetStrategy(new ExpandoObject(), new ExpandoObject());

        var jsonStrategy1 = MergeStrategyFactory.GetStrategy(
            JsonDocument.Parse(@"{}").RootElement, 
            JsonDocument.Parse(@"{}").RootElement);
        var jsonStrategy2 = MergeStrategyFactory.GetStrategy(
            JsonDocument.Parse(@"{}").RootElement, 
            JsonDocument.Parse(@"{}").RootElement);

        var collectionStrategy1 = MergeStrategyFactory.GetStrategy(new List<int>(), new List<int>());
        var collectionStrategy2 = MergeStrategyFactory.GetStrategy(new List<int>(), new List<int>());

        var defaultStrategy1 = MergeStrategyFactory.GetStrategy(123, 456);
        var defaultStrategy2 = MergeStrategyFactory.GetStrategy("a", "b");

        // Assert
        Assert.Same(expandoStrategy1, expandoStrategy2);
        Assert.Same(jsonStrategy1, jsonStrategy2);
        Assert.Same(collectionStrategy1, collectionStrategy2);
        Assert.Same(defaultStrategy1, defaultStrategy2);
    }

    [Fact]
    public void GetStrategy_ShouldPrioritizeExpandoObject_OverOtherStrategies()
    {
        // Arrange
        var target = new ExpandoObject();
        var source = new ExpandoObject();

        // Act
        var strategy = MergeStrategyFactory.GetStrategy(target, source);

        // Assert
        Assert.IsType<ExpandoObjectMergeStrategy>(strategy);
    }

    [Fact]
    public void GetStrategy_ShouldHandleMixedListTypes()
    {
        // Arrange
        var target = new List<object> { 1, 2, 3 };
        var source = new[] { "a", "b", "c" };

        // Act
        var strategy = MergeStrategyFactory.GetStrategy(target, source);

        // Assert
        Assert.IsType<CollectionMergeStrategy>(strategy);
    }

    [Fact]
    public void GetStrategy_ShouldReturnDefaultStrategy_ForCustomObjects()
    {
        // Arrange
        var target = new CustomClass { Value = "target" };
        var source = new CustomClass { Value = "source" };

        // Act
        var strategy = MergeStrategyFactory.GetStrategy(target, source);

        // Assert
        Assert.IsType<DefaultMergeStrategy>(strategy);
    }

    [Fact]
    public void GetStrategy_ShouldHandleEnumerableOfDifferentTypes()
    {
        // Arrange
        var target = Enumerable.Range(1, 5);
        var source = Enumerable.Range(6, 5);

        // Act
        var strategy = MergeStrategyFactory.GetStrategy(target, source);

        // Assert
        Assert.IsType<CollectionMergeStrategy>(strategy);
    }

    private class CustomClass
    {
        public string Value { get; set; } = string.Empty;
    }
}

