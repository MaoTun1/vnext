using System.Text.Json;
using System.Net.Http;
using BBT.Workflow.Definitions;
using BBT.Workflow.Scripting;
using BBT.Workflow.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace BBT.Workflow.Application.Tests.Tasks;

/// <summary>
/// Unit tests for HttpTaskExecutor SSL validation and HttpClientFactory integration
/// </summary>
public class HttpTaskExecutorTests : ApplicationTestBase<ApplicationEntryPoint>
{
    private readonly Mock<ILogger<HttpTaskExecutor>> _loggerMock;
    private readonly Mock<IScriptEngine> _scriptEngineMock;
    private readonly IHttpClientFactory _httpClientFactory;

    public HttpTaskExecutorTests()
    {
        _loggerMock = new Mock<ILogger<HttpTaskExecutor>>();
        _scriptEngineMock = new Mock<IScriptEngine>();
        
        // Get HttpClientFactory from the test service provider
        _httpClientFactory = GetRequiredService<IHttpClientFactory>();
    }

    [Fact]
    public void HttpTaskExecutor_Should_Have_Correct_Named_Client_Constants()
    {
        // Arrange & Assert
        Assert.Equal("WorkflowHttpClient", HttpTaskExecutor.DefaultHttpClientName);
        Assert.Equal("WorkflowHttpClient.NoSslValidation", HttpTaskExecutor.NoSslValidationHttpClientName);
    }

    [Fact]
    public void HttpClientFactory_Should_Create_Named_Clients_Successfully()
    {
        // Arrange & Act
        var defaultClient = _httpClientFactory.CreateClient(HttpTaskExecutor.DefaultHttpClientName);
        var noSslClient = _httpClientFactory.CreateClient(HttpTaskExecutor.NoSslValidationHttpClientName);

        // Assert
        Assert.NotNull(defaultClient);
        Assert.NotNull(noSslClient);
        Assert.NotSame(defaultClient, noSslClient);
    }

    [Fact]
    public void HttpTask_ValidateSSL_Should_Default_To_True()
    {
        // Arrange
        var config = """
                     {
                       "url": "https://example.com/api",
                       "method": "GET"
                     }
                     """;

        // Act
        var httpTask = HttpTask.Create(config.ToJsonElement());

        // Assert
        Assert.True(httpTask.ValidateSSL);
    }

    [Fact]
    public void HttpTask_ValidateSSL_Should_Be_Configurable()
    {
        // Arrange
        var config = """
                     {
                       "url": "https://example.com/api",
                       "method": "GET",
                       "validateSsl": false
                     }
                     """;

        // Act
        var httpTask = HttpTask.Create(config.ToJsonElement());

        // Assert
        Assert.False(httpTask.ValidateSSL);
    }

    [Fact]
    public void HttpTask_TimeoutSeconds_Should_Default_To_30()
    {
        // Arrange
        var config = """
                     {
                       "url": "https://example.com/api",
                       "method": "GET"
                     }
                     """;

        // Act
        var httpTask = HttpTask.Create(config.ToJsonElement());

        // Assert
        Assert.Equal(30, httpTask.TimeoutSeconds);
    }

    [Fact]
    public void HttpTask_TimeoutSeconds_Should_Be_Configurable()
    {
        // Arrange
        var config = """
                     {
                       "url": "https://example.com/api",
                       "method": "GET",
                       "timeoutSeconds": 60
                     }
                     """;

        // Act
        var httpTask = HttpTask.Create(config.ToJsonElement());

        // Assert
        Assert.Equal(60, httpTask.TimeoutSeconds);
    }

    // Integration test that would require actual HTTP client behavior
    // This is more of a documentation test showing how the executor would be used
    [Fact]
    public void HttpTaskExecutor_Should_Be_Creatable_With_Dependencies()
    {
        // Arrange & Act
        var executor = new HttpTaskExecutor(_scriptEngineMock.Object, _httpClientFactory, _loggerMock.Object);

        // Assert
        Assert.NotNull(executor);
    }
}
