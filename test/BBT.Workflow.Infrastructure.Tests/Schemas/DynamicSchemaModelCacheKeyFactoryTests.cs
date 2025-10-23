using System.Linq;
using BBT.Workflow.Schemas;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using NSubstitute;
using Shouldly;
using Xunit;

namespace BBT.Workflow.Infrastructure.Tests.Schemas;

/// <summary>
/// Unit tests for DynamicSchemaModelCacheKeyFactory
/// </summary>
public sealed class DynamicSchemaModelCacheKeyFactoryTests
{
    private readonly DynamicSchemaModelCacheKeyFactory _factory;

    public DynamicSchemaModelCacheKeyFactoryTests()
    {
        _factory = new DynamicSchemaModelCacheKeyFactory();
    }

    #region Create Method Tests

    [Fact]
    public void Create_Should_Return_ModelCacheKey()
    {
        // Arrange
        var mockContext = Substitute.For<DbContext>();

        // Act
        var result = _factory.Create(mockContext, false);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeAssignableTo<object>();
    }

    [Fact]
    public void Create_Should_Return_Different_Keys_For_Different_Design_Time_Flags()
    {
        // Arrange
        var mockContext = Substitute.For<DbContext>();

        // Act
        var runtimeKey = _factory.Create(mockContext, false);
        var designTimeKey = _factory.Create(mockContext, true);

        // Assert
        runtimeKey.ShouldNotBe(designTimeKey);
    }

    [Fact]
    public void Create_Should_Handle_Regular_DbContext()
    {
        // Arrange
        var mockContext = Substitute.For<DbContext>();

        // Act
        var result = _factory.Create(mockContext, false);

        // Assert
        result.ShouldNotBeNull();
    }

    #endregion

    #region CoreModelCacheKey Equality Tests

    [Fact]
    public void CoreModelCacheKey_GetHashCode_Should_Return_Consistent_Value()
    {
        // Arrange
        var mockContext = Substitute.For<DbContext>();

        // Act
        var key = _factory.Create(mockContext, false);
        var hashCode1 = key.GetHashCode();
        var hashCode2 = key.GetHashCode();

        // Assert
        hashCode1.ShouldBe(hashCode2);
    }

    [Fact]
    public void CoreModelCacheKey_GetHashCode_Should_Be_Different_For_Different_Keys()
    {
        // Arrange
        var mockContext = Substitute.For<DbContext>();

        // Act
        var runtimeKey = _factory.Create(mockContext, false);
        var designTimeKey = _factory.Create(mockContext, true);

        // Assert
        runtimeKey.GetHashCode().ShouldNotBe(designTimeKey.GetHashCode());
    }

    #endregion

    #region Schema-Aware Caching Tests

    [Fact]
    public void Create_Should_Use_Public_Schema_As_Default_For_Non_WorkflowDbContext()
    {
        // Arrange
        var mockContext = Substitute.For<DbContext>();

        // Act
        var key1 = _factory.Create(mockContext, false);
        var key2 = _factory.Create(mockContext, false);

        // Assert - Both should use "public" as default schema and be equal
        key1.ShouldBe(key2);
    }

    #endregion

    #region Constructor Tests

    [Fact]
    public void Constructor_Should_Create_Instance_Successfully()
    {
        // Act & Assert
        Should.NotThrow(() => new DynamicSchemaModelCacheKeyFactory());
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Create_Should_Handle_Multiple_Calls_With_Same_Context()
    {
        // Arrange
        var mockContext = Substitute.For<DbContext>();

        // Act
        var keys = Enumerable.Range(0, 10)
            .Select(_ => _factory.Create(mockContext, false))
            .ToList();

        // Assert - All keys should be equal
        keys.Skip(1).ShouldAllBe(key => key.Equals(keys[0]));
    }

    [Fact]
    public void Create_Should_Handle_Runtime_And_DesignTime_Keys_Separately()
    {
        // Arrange
        var mockContext = Substitute.For<DbContext>();

        // Act
        var runtimeKeys = Enumerable.Range(0, 5)
            .Select(_ => _factory.Create(mockContext, false))
            .ToList();
        
        var designTimeKeys = Enumerable.Range(0, 5)
            .Select(_ => _factory.Create(mockContext, true))
            .ToList();

        // Assert
        runtimeKeys.Skip(1).ShouldAllBe(key => key.Equals(runtimeKeys[0]));
        designTimeKeys.Skip(1).ShouldAllBe(key => key.Equals(designTimeKeys[0]));
        
        // Runtime and design-time keys should not be equal
        foreach (var runtimeKey in runtimeKeys)
        {
            foreach (var designTimeKey in designTimeKeys)
            {
                runtimeKey.ShouldNotBe(designTimeKey);
            }
        }
    }

    #endregion
}

