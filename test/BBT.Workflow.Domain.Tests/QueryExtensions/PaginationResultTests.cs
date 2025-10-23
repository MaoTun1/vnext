using System;
using System.Collections.Generic;
using System.Linq;
using BBT.Workflow.Definitions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace BBT.Workflow.Domain.Tests.QueryExtensions;

/// <summary>
/// Unit tests for PaginationResult and PaginationLinks models
/// </summary>
public class PaginationResultTests : DomainTestBase<DomainEntryPoint>
{
    [Fact]
    public void PaginationResult_ShouldCreateWithData()
    {
        // Arrange
        var data = new List<string> { "item1", "item2", "item3" };
        var links = new PaginationLinks
        {
            Self = "http://api/items?page=1&pageSize=10",
            First = "http://api/items?page=1&pageSize=10",
            Next = "http://api/items?page=2&pageSize=10",
            Prev = string.Empty
        };

        // Act
        var result = new PaginationResult<string>
        {
            Data = data,
            Pagination = links
        };

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.Data.Count);
        Assert.Equal(data, result.Data);
        Assert.Equal(links, result.Pagination);
    }

    [Fact]
    public void PaginationResult_ShouldHandleEmptyData()
    {
        // Arrange & Act
        var result = new PaginationResult<string>
        {
            Data = new List<string>(),
            Pagination = new PaginationLinks()
        };

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result.Data);
        Assert.NotNull(result.Pagination);
    }

    [Fact]
    public void PaginationLinks_ShouldStoreAllLinks()
    {
        // Arrange & Act
        var links = new PaginationLinks
        {
            Self = "http://api/items?page=2&pageSize=10",
            First = "http://api/items?page=1&pageSize=10",
            Next = "http://api/items?page=3&pageSize=10",
            Prev = "http://api/items?page=1&pageSize=10"
        };

        // Assert
        Assert.Equal("http://api/items?page=2&pageSize=10", links.Self);
        Assert.Equal("http://api/items?page=1&pageSize=10", links.First);
        Assert.Equal("http://api/items?page=3&pageSize=10", links.Next);
        Assert.Equal("http://api/items?page=1&pageSize=10", links.Prev);
    }

    [Fact]
    public void PaginationLinks_ShouldHandleEmptyStrings()
    {
        // Arrange & Act
        var links = new PaginationLinks
        {
            Self = "http://api/items?page=1&pageSize=10",
            First = "http://api/items?page=1&pageSize=10",
            Next = string.Empty,
            Prev = string.Empty
        };

        // Assert
        Assert.Equal(string.Empty, links.Next);
        Assert.Equal(string.Empty, links.Prev);
    }
}

/// <summary>
/// Unit tests for LinkBuilder
/// </summary>
public class LinkBuilderTests : DomainTestBase<DomainEntryPoint>
{
    [Fact]
    public void LinkBuilder_ShouldBuildBasicLink()
    {
        // Arrange
        var configuration = CreateConfiguration("http://localhost:5000", "1");
        var linkBuilder = new LinkBuilder(configuration, "/workflows");

        // Act
        var link = linkBuilder.BuildPageLink(1, 10);

        // Assert
        Assert.Equal("http://localhost:5000/api/v1/workflows?page=1&pageSize=10", link);
    }

    [Fact]
    public void LinkBuilder_ShouldBuildLinkWithMultiplePages()
    {
        // Arrange
        var configuration = CreateConfiguration("http://localhost:5000", "1");
        var linkBuilder = new LinkBuilder(configuration, "/workflows");

        // Act
        var link = linkBuilder.BuildPageLink(5, 20);

        // Assert
        Assert.Equal("http://localhost:5000/api/v1/workflows?page=5&pageSize=20", link);
    }

    [Fact]
    public void LinkBuilder_ShouldIncludeAdditionalQueryParams()
    {
        // Arrange
        var configuration = CreateConfiguration("http://localhost:5000", "1");
        var linkBuilder = new LinkBuilder(configuration, "/workflows");
        var queryParams = new Dictionary<string, string>
        {
            ["status"] = "active",
            ["type"] = "standard"
        };

        // Act
        var link = linkBuilder.BuildPageLink(1, 10, queryParams);

        // Assert
        Assert.Contains("page=1", link);
        Assert.Contains("pageSize=10", link);
        Assert.Contains("status=active", link);
        Assert.Contains("type=standard", link);
    }

    [Fact]
    public void LinkBuilder_ShouldExcludePageAndPageSizeFromQueryParams()
    {
        // Arrange
        var configuration = CreateConfiguration("http://localhost:5000", "1");
        var linkBuilder = new LinkBuilder(configuration, "/workflows");
        var queryParams = new Dictionary<string, string>
        {
            ["page"] = "999",
            ["pageSize"] = "999",
            ["status"] = "active"
        };

        // Act
        var link = linkBuilder.BuildPageLink(1, 10, queryParams);

        // Assert
        Assert.Contains("page=1", link);
        Assert.Contains("pageSize=10", link);
        Assert.DoesNotContain("page=999", link);
        Assert.DoesNotContain("pageSize=999", link);
        Assert.Contains("status=active", link);
    }

    [Fact]
    public void LinkBuilder_ShouldHandleTrailingSlashInBaseUrl()
    {
        // Arrange
        var configuration = CreateConfiguration("http://localhost:5000/", "1");
        var linkBuilder = new LinkBuilder(configuration, "/workflows");

        // Act
        var link = linkBuilder.BuildPageLink(1, 10);

        // Assert
        Assert.Equal("http://localhost:5000/api/v1/workflows?page=1&pageSize=10", link);
        Assert.DoesNotContain("//api", link);
    }

    [Fact]
    public void LinkBuilder_ShouldHandleLeadingSlashInRoute()
    {
        // Arrange
        var configuration = CreateConfiguration("http://localhost:5000", "1");
        var linkBuilder = new LinkBuilder(configuration, "/workflows");

        // Act
        var link = linkBuilder.BuildPageLink(1, 10);

        // Assert
        Assert.Equal("http://localhost:5000/api/v1/workflows?page=1&pageSize=10", link);
    }

    [Fact]
    public void LinkBuilder_ShouldHandleRouteWithoutLeadingSlash()
    {
        // Arrange
        var configuration = CreateConfiguration("http://localhost:5000", "1");
        var linkBuilder = new LinkBuilder(configuration, "workflows");

        // Act
        var link = linkBuilder.BuildPageLink(1, 10);

        // Assert
        Assert.Equal("http://localhost:5000/api/v1/workflows?page=1&pageSize=10", link);
    }

    [Fact]
    public void LinkBuilder_ShouldHandleDifferentApiVersions()
    {
        // Arrange
        var configuration = CreateConfiguration("http://localhost:5000", "2");
        var linkBuilder = new LinkBuilder(configuration, "/workflows");

        // Act
        var link = linkBuilder.BuildPageLink(1, 10);

        // Assert
        Assert.Contains("/api/v2/", link);
    }

    [Fact]
    public void LinkBuilder_ShouldHandleEmptyBaseUrl()
    {
        // Arrange
        var configuration = CreateConfiguration("", "1");
        var linkBuilder = new LinkBuilder(configuration, "/workflows");

        // Act
        var link = linkBuilder.BuildPageLink(1, 10);

        // Assert
        Assert.Equal("/api/v1/workflows?page=1&pageSize=10", link);
    }

    [Fact]
    public void LinkBuilder_ShouldHandleNullQueryParams()
    {
        // Arrange
        var configuration = CreateConfiguration("http://localhost:5000", "1");
        var linkBuilder = new LinkBuilder(configuration, "/workflows");

        // Act
        var link = linkBuilder.BuildPageLink(1, 10, null);

        // Assert
        Assert.Equal("http://localhost:5000/api/v1/workflows?page=1&pageSize=10", link);
    }

    private static IConfiguration CreateConfiguration(string baseUrl, string apiVersion)
    {
        var configDict = new Dictionary<string, string>
        {
            ["vNextApi:BaseUrl"] = baseUrl,
            ["vNextApi:ApiVersion"] = apiVersion
        };

        return new ConfigurationBuilder()
            .AddInMemoryCollection(configDict!)
            .Build();
    }
}

