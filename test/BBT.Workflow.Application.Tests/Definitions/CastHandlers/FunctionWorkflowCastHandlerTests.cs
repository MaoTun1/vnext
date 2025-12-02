using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using BBT.Aether.Results;
using BBT.Workflow.Caching;
using BBT.Workflow.Definitions;
using BBT.Workflow.Definitions.CastHandlers;
using Moq;
using Shouldly;
using Xunit;

namespace BBT.Workflow.Definitions.CastHandlers;

/// <summary>
/// Unit tests for FunctionWorkflowCastHandler
/// Tests function workflow casting operations
/// </summary>
public class FunctionWorkflowCastHandlerTests
{
    private readonly Mock<IDomainCacheContext> _mockCacheContext;
    private readonly Mock<ICacheSet<Function>> _mockFunctionsCacheSet;
    private readonly FunctionWorkflowCastHandler _handler;

    public FunctionWorkflowCastHandlerTests()
    {
        _mockCacheContext = new Mock<IDomainCacheContext>();
        _mockFunctionsCacheSet = new Mock<ICacheSet<Function>>();
        
        _mockCacheContext.Setup(x => x.Functions).Returns(_mockFunctionsCacheSet.Object);
        
        _handler = new FunctionWorkflowCastHandler(_mockCacheContext.Object);
    }

    [Fact]
    public void CanHandle_ShouldReturnTrue_ForSysFunctions()
    {
        // Act
        var result = _handler.CanHandle("sys-functions");

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void CanHandle_ShouldReturnFalse_ForOtherWorkflowTypes()
    {
        // Arrange & Act & Assert
        _handler.CanHandle("sys-tasks").ShouldBeFalse();
        _handler.CanHandle("sys-flows").ShouldBeFalse();
        _handler.CanHandle("sys-views").ShouldBeFalse();
        _handler.CanHandle("sys-schemas").ShouldBeFalse();
        _handler.CanHandle("sys-extensions").ShouldBeFalse();
        _handler.CanHandle("unknown").ShouldBeFalse();
    }

    [Fact]
    public async Task HandleAsync_ShouldDeserializeAndCacheFunction()
    {
        // Arrange
        var reference = new Reference("test-function", "test-domain", "sys-functions", "1.0.0");
        
        var functionJson = """
        {
            "name": "Test Function",
            "description": "Test Description",
            "script": "return true;"
        }
        """;
        
        var attributes = JsonDocument.Parse(functionJson).RootElement;

        _mockFunctionsCacheSet
            .Setup(x => x.SetAsync(It.IsAny<Function>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok());

        // Act
        await _handler.HandleAsync(reference, attributes, CancellationToken.None);

        // Assert
        _mockFunctionsCacheSet.Verify(
            x => x.SetAsync(It.Is<Function>(f => 
                f.Key == "test-function" && 
                f.Domain == "test-domain" && 
                f.Version == "1.0.0"), 
                It.IsAny<CancellationToken>()), 
            Times.Once);
    }
}
