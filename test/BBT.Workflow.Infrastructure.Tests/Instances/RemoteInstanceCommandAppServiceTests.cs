using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using BBT.Workflow.Remote.Configuration;
using BBT.Workflow.Instances;
using BBT.Workflow.Instances.Remote;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using Shouldly;
using Xunit;

namespace BBT.Workflow.Infrastructure.Tests.Instances;

public class RemoteInstanceCommandAppServiceTests
{
    private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;
    private readonly HttpClient _httpClient;
    private readonly RemoteOptions _options;
    private readonly RemoteInstanceCommandAppService _service;

    public RemoteInstanceCommandAppServiceTests()
    {
        _httpMessageHandlerMock = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_httpMessageHandlerMock.Object);
        
        _options = new RemoteOptions
        {
            BaseUrl = "https://test-api.example.com",
            ApiVersion = "1.0",
            TimeoutSeconds = 30
        };

        var optionsMock = new Mock<IOptions<RemoteOptions>>();
        optionsMock.Setup(x => x.Value).Returns(_options);

        _service = new RemoteInstanceCommandAppService(
            _httpClient,
            optionsMock.Object);
    }

    [Fact]
    public async Task StartAsync_ShouldReturnSuccess_WhenApiReturnsValidResponse()
    {
        // Arrange
        var expectedResponse = new StartInstanceOutput
        {
            Id = Guid.NewGuid(),
            AvailableTransitions = new List<string> { "submit", "cancel" }
        };

        var responseContent = JsonSerializer.Serialize(expectedResponse, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(responseContent, Encoding.UTF8, "application/json")
            });

        var input = new StartInstanceInput("test-domain", "test-workflow", "1.0", false)
        {
            Instance = new CreateInstanceInput
            {
                Key = "test-instance-key",
                Tags = new[] { "test" },
                Attributes = JsonSerializer.SerializeToElement(new { test = "value" })
            }
        };

        // Act
        var result = await _service.StartAsync(input);

        // Assert
        result.ShouldNotBeNull();
        result.Data.ShouldNotBeNull();
        result.Data.Id.ShouldBe(expectedResponse.Id);
        result.Data.AvailableTransitions.ShouldBe(expectedResponse.AvailableTransitions);

        // Verify HTTP request was made correctly
        _httpMessageHandlerMock
            .Protected()
            .Verify(
                "SendAsync",
                Times.Once(),
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Post &&
                    req.RequestUri!.ToString().Contains("test-domain/workflows/test-workflow/instances/start") &&
                    req.RequestUri.ToString().Contains("version=1.0")),
                ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task TransitionAsync_ShouldReturnSuccess_WhenApiReturnsValidResponse()
    {
        // Arrange
        var instanceId = Guid.NewGuid();
        var expectedResponse = new TransitionOutput
        {
            Id = instanceId,
            AvailableTransitions = new List<string> { "approve", "reject" }
        };

        var responseContent = JsonSerializer.Serialize(expectedResponse, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(responseContent, Encoding.UTF8, "application/json")
            });

        var input = new TransitionInput("test-domain", "test-workflow", "1.0", 
            JsonSerializer.SerializeToElement(new { approved = true }), false);

        // Act
        var result = await _service.TransitionAsync(instanceId, "submit", input);

        // Assert
        result.ShouldNotBeNull();
        result.Data.ShouldNotBeNull();
        result.Data.Id.ShouldBe(expectedResponse.Id);
        result.Data.AvailableTransitions.ShouldBe(expectedResponse.AvailableTransitions);

        // Verify HTTP request was made correctly
        _httpMessageHandlerMock
            .Protected()
            .Verify(
                "SendAsync",
                Times.Once(),
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Patch &&
                    req.RequestUri!.ToString().Contains($"instances/{instanceId}/transitions/submit")),
                ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task StartAsync_ShouldThrowHttpRequestException_WhenApiReturnsError()
    {
        // Arrange
        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.BadRequest,
                Content = new StringContent("Bad Request", Encoding.UTF8, "application/json")
            });

        var input = new StartInstanceInput("test-domain", "test-workflow", "1.0", false)
        {
            Instance = new CreateInstanceInput
            {
                Key = "test-instance-key"
            }
        };

        // Act & Assert
        var exception = await Should.ThrowAsync<HttpRequestException>(() => _service.StartAsync(input));
        exception.Message.ShouldContain("HTTP 400");
        exception.Message.ShouldContain("Bad Request");
    }
} 