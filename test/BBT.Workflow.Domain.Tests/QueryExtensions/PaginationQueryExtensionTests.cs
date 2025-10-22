using System.Collections.Generic;
using System.Linq;
using BBT.Workflow.Definitions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;
using Xunit;

namespace BBT.Workflow.Domain.Tests.QueryExtensions;

/// <summary>
/// Unit tests for QueryExtensions.Paginate method
/// </summary>
public class PaginationQueryExtensionTests : DomainTestBase<DomainEntryPoint>
{
    private class TestEntity
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    [Fact]
    public void Paginate_ShouldReturnFirstPage()
    {
        // Arrange
        var query = CreateTestQuery(50);
        var configuration = CreateConfiguration();

        // Act
        var result = query.Paginate(1, 10, "/test", configuration);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(10, result.Data.Count);
        Assert.Equal(1, result.Data.First().Id);
        Assert.Equal(10, result.Data.Last().Id);
    }

    [Fact]
    public void Paginate_ShouldReturnSecondPage()
    {
        // Arrange
        var query = CreateTestQuery(50);
        var configuration = CreateConfiguration();

        // Act
        var result = query.Paginate(2, 10, "/test", configuration);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(10, result.Data.Count);
        Assert.Equal(11, result.Data.First().Id);
        Assert.Equal(20, result.Data.Last().Id);
    }

    [Fact]
    public void Paginate_ShouldHandleLastPageWithFewerItems()
    {
        // Arrange
        var query = CreateTestQuery(25);
        var configuration = CreateConfiguration();

        // Act
        var result = query.Paginate(3, 10, "/test", configuration);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(5, result.Data.Count);
        Assert.Equal(21, result.Data.First().Id);
        Assert.Equal(25, result.Data.Last().Id);
    }

    [Fact]
    public void Paginate_ShouldCorrectInvalidPageNumber()
    {
        // Arrange
        var query = CreateTestQuery(50);
        var configuration = CreateConfiguration();

        // Act
        var result = query.Paginate(0, 10, "/test", configuration);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(10, result.Data.Count);
        Assert.Equal(1, result.Data.First().Id);
    }

    [Fact]
    public void Paginate_ShouldCorrectNegativePageNumber()
    {
        // Arrange
        var query = CreateTestQuery(50);
        var configuration = CreateConfiguration();

        // Act
        var result = query.Paginate(-5, 10, "/test", configuration);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(10, result.Data.Count);
        Assert.Equal(1, result.Data.First().Id);
    }

    [Fact]
    public void Paginate_ShouldCorrectInvalidPageSize()
    {
        // Arrange
        var query = CreateTestQuery(50);
        var configuration = CreateConfiguration();

        // Act
        var result = query.Paginate(1, 0, "/test", configuration);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(10, result.Data.Count);
    }

    [Fact]
    public void Paginate_ShouldCorrectNegativePageSize()
    {
        // Arrange
        var query = CreateTestQuery(50);
        var configuration = CreateConfiguration();

        // Act
        var result = query.Paginate(1, -10, "/test", configuration);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(10, result.Data.Count);
    }

    [Fact]
    public void Paginate_ShouldGenerateCorrectSelfLink()
    {
        // Arrange
        var query = CreateTestQuery(50);
        var configuration = CreateConfiguration();

        // Act
        var result = query.Paginate(2, 10, "/test", configuration);

        // Assert
        Assert.Contains("page=2", result.Pagination.Self);
        Assert.Contains("pageSize=10", result.Pagination.Self);
    }

    [Fact]
    public void Paginate_ShouldGenerateCorrectFirstLink()
    {
        // Arrange
        var query = CreateTestQuery(50);
        var configuration = CreateConfiguration();

        // Act
        var result = query.Paginate(3, 10, "/test", configuration);

        // Assert
        Assert.Contains("page=1", result.Pagination.First);
        Assert.Contains("pageSize=10", result.Pagination.First);
    }

    [Fact]
    public void Paginate_ShouldGenerateNextLinkWhenHasNextPage()
    {
        // Arrange
        var query = CreateTestQuery(50);
        var configuration = CreateConfiguration();

        // Act
        var result = query.Paginate(1, 10, "/test", configuration);

        // Assert
        Assert.NotEmpty(result.Pagination.Next);
        Assert.Contains("page=2", result.Pagination.Next);
    }

    [Fact]
    public void Paginate_ShouldNotGenerateNextLinkOnLastPage()
    {
        // Arrange
        var query = CreateTestQuery(20);
        var configuration = CreateConfiguration();

        // Act
        var result = query.Paginate(2, 10, "/test", configuration);

        // Assert
        Assert.Empty(result.Pagination.Next);
    }

    [Fact]
    public void Paginate_ShouldGeneratePrevLinkWhenNotFirstPage()
    {
        // Arrange
        var query = CreateTestQuery(50);
        var configuration = CreateConfiguration();

        // Act
        var result = query.Paginate(3, 10, "/test", configuration);

        // Assert
        Assert.NotEmpty(result.Pagination.Prev);
        Assert.Contains("page=2", result.Pagination.Prev);
    }

    [Fact]
    public void Paginate_ShouldNotGeneratePrevLinkOnFirstPage()
    {
        // Arrange
        var query = CreateTestQuery(50);
        var configuration = CreateConfiguration();

        // Act
        var result = query.Paginate(1, 10, "/test", configuration);

        // Assert
        Assert.Empty(result.Pagination.Prev);
    }

    [Fact]
    public void Paginate_ShouldIncludeQueryParamsInLinks()
    {
        // Arrange
        var query = CreateTestQuery(50);
        var configuration = CreateConfiguration();
        var queryParams = CreateQueryCollection(new Dictionary<string, string>
        {
            ["status"] = "active",
            ["type"] = "workflow"
        });

        // Act
        var result = query.Paginate(1, 10, "/test", configuration, queryParams);

        // Assert
        Assert.Contains("status=active", result.Pagination.Self);
        Assert.Contains("type=workflow", result.Pagination.Self);
        Assert.Contains("status=active", result.Pagination.First);
        Assert.Contains("type=workflow", result.Pagination.First);
    }

    [Fact]
    public void Paginate_ShouldHandleEmptyQuery()
    {
        // Arrange
        var query = CreateTestQuery(0);
        var configuration = CreateConfiguration();

        // Act
        var result = query.Paginate(1, 10, "/test", configuration);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result.Data);
        Assert.Empty(result.Pagination.Next);
        Assert.Empty(result.Pagination.Prev);
    }

    [Fact]
    public void Paginate_ShouldHandlePageBeyondDataSize()
    {
        // Arrange
        var query = CreateTestQuery(5);
        var configuration = CreateConfiguration();

        // Act
        var result = query.Paginate(10, 10, "/test", configuration);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result.Data);
        Assert.Empty(result.Pagination.Next);
    }

    [Fact]
    public void Paginate_ShouldHandleLargePageSize()
    {
        // Arrange
        var query = CreateTestQuery(50);
        var configuration = CreateConfiguration();

        // Act
        var result = query.Paginate(1, 100, "/test", configuration);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(50, result.Data.Count);
        Assert.Empty(result.Pagination.Next);
    }

    [Fact]
    public void Paginate_ShouldHandleNullQueryParams()
    {
        // Arrange
        var query = CreateTestQuery(50);
        var configuration = CreateConfiguration();

        // Act
        var result = query.Paginate(1, 10, "/test", configuration, null);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(10, result.Data.Count);
        Assert.NotNull(result.Pagination);
    }

    private static IQueryable<TestEntity> CreateTestQuery(int count)
    {
        var entities = Enumerable.Range(1, count)
            .Select(i => new TestEntity { Id = i, Name = $"Entity {i}" })
            .ToList();

        return entities.AsQueryable();
    }

    private static IConfiguration CreateConfiguration()
    {
        var configDict = new Dictionary<string, string>
        {
            ["vNextApi:BaseUrl"] = "http://localhost:5000",
            ["vNextApi:ApiVersion"] = "1"
        };

        return new ConfigurationBuilder()
            .AddInMemoryCollection(configDict!)
            .Build();
    }

    private static IQueryCollection CreateQueryCollection(Dictionary<string, string> values)
    {
        var dict = values.ToDictionary(
            kvp => kvp.Key,
            kvp => new StringValues(kvp.Value)
        );
        return new QueryCollection(dict);
    }
}

