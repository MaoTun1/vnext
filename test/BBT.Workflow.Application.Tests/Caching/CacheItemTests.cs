using System;
using System.Collections.Generic;
using System.Threading;
using BBT.Workflow.Caching;
using Shouldly;
using Xunit;

namespace BBT.Workflow.Caching;

/// <summary>
/// Unit tests for CacheItem
/// Tests cache item access tracking and usage metrics
/// </summary>
public class CacheItemTests
{
    [Fact]
    public void Constructor_ShouldInitializeWithValue()
    {
        // Arrange
        var testValue = "test-value";

        // Act
        var cacheItem = new CacheItem<string>(testValue);

        // Assert
        cacheItem.Value.ShouldBe(testValue);
        cacheItem.AccessCount.ShouldBe(1);
        cacheItem.LastAccessTime.ShouldNotBe(default);
        cacheItem.CreatedTime.ShouldNotBe(default);
    }

    [Fact]
    public void Constructor_ShouldSetCreatedTimeAndLastAccessTime()
    {
        // Arrange
        var testValue = 42;
        var beforeCreation = DateTime.UtcNow;

        // Act
        var cacheItem = new CacheItem<int>(testValue);

        // Assert
        var afterCreation = DateTime.UtcNow;
        cacheItem.CreatedTime.ShouldBeGreaterThanOrEqualTo(beforeCreation);
        cacheItem.CreatedTime.ShouldBeLessThanOrEqualTo(afterCreation);
        cacheItem.LastAccessTime.ShouldBeGreaterThanOrEqualTo(beforeCreation);
        cacheItem.LastAccessTime.ShouldBeLessThanOrEqualTo(afterCreation);
    }

    [Fact]
    public void Constructor_ShouldInitializeAccessCountToOne()
    {
        // Arrange & Act
        var cacheItem = new CacheItem<string>("test");

        // Assert
        cacheItem.AccessCount.ShouldBe(1);
    }

    [Fact]
    public void UpdateAccess_ShouldIncrementAccessCount()
    {
        // Arrange
        var cacheItem = new CacheItem<string>("test");
        var initialAccessCount = cacheItem.AccessCount;

        // Act
        cacheItem.UpdateAccess();

        // Assert
        cacheItem.AccessCount.ShouldBe(initialAccessCount + 1);
    }

    [Fact]
    public void UpdateAccess_ShouldUpdateLastAccessTime()
    {
        // Arrange
        var cacheItem = new CacheItem<string>("test");
        var initialLastAccessTime = cacheItem.LastAccessTime;
        
        // Wait a small amount to ensure time difference
        Thread.Sleep(10);

        // Act
        cacheItem.UpdateAccess();

        // Assert
        cacheItem.LastAccessTime.ShouldBeGreaterThan(initialLastAccessTime);
    }

    [Fact]
    public void UpdateAccess_CalledMultipleTimes_ShouldIncrementAccessCountCorrectly()
    {
        // Arrange
        var cacheItem = new CacheItem<string>("test");
        var initialAccessCount = cacheItem.AccessCount;

        // Act
        cacheItem.UpdateAccess();
        cacheItem.UpdateAccess();
        cacheItem.UpdateAccess();

        // Assert
        cacheItem.AccessCount.ShouldBe(initialAccessCount + 3);
    }

    [Fact]
    public void CreatedTime_ShouldNotChange_AfterUpdateAccess()
    {
        // Arrange
        var cacheItem = new CacheItem<string>("test");
        var initialCreatedTime = cacheItem.CreatedTime;
        
        // Wait a small amount to ensure time difference
        Thread.Sleep(10);

        // Act
        cacheItem.UpdateAccess();
        cacheItem.UpdateAccess();

        // Assert
        cacheItem.CreatedTime.ShouldBe(initialCreatedTime);
    }

    [Fact]
    public void Value_ShouldBeReadOnly()
    {
        // Arrange
        var initialValue = "initial-value";
        var cacheItem = new CacheItem<string>(initialValue);

        // Act & Assert
        cacheItem.Value.ShouldBe(initialValue);
        
        // Verify that Value property doesn't have a setter
        var propertyInfo = typeof(CacheItem<string>).GetProperty(nameof(CacheItem<string>.Value));
        propertyInfo.ShouldNotBeNull();
        propertyInfo.CanWrite.ShouldBeFalse();
    }

    [Fact]
    public void CacheItem_WithComplexType_ShouldWorkCorrectly()
    {
        // Arrange
        var testObject = new TestComplexObject
        {
            Id = 1,
            Name = "Test",
            Data = new List<string> { "item1", "item2" }
        };

        // Act
        var cacheItem = new CacheItem<TestComplexObject>(testObject);

        // Assert
        cacheItem.Value.ShouldBe(testObject);
        cacheItem.Value.Id.ShouldBe(1);
        cacheItem.Value.Name.ShouldBe("Test");
        cacheItem.Value.Data.ShouldContain("item1");
    }

    [Fact]
    public void CacheItem_WithNullValue_ShouldAllowNull()
    {
        // Arrange & Act
        var cacheItem = new CacheItem<string?>(null);

        // Assert
        cacheItem.Value.ShouldBeNull();
        cacheItem.AccessCount.ShouldBe(1);
    }

    private class TestComplexObject
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public List<string> Data { get; set; } = new();
    }
}

