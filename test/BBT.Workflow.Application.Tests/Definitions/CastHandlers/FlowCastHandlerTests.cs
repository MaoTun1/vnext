using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using BBT.Workflow.Caching;
using BBT.Workflow.Definitions;
using BBT.Workflow.Definitions.CastHandlers;
using Moq;
using Shouldly;
using Xunit;

namespace BBT.Workflow.Definitions.CastHandlers;

/// <summary>
/// Unit tests for FlowCastHandler
/// Tests flow workflow casting operations
/// </summary>
public class FlowCastHandlerTests
{
    private readonly Mock<IDomainCacheContext> _mockCacheContext;
    private readonly Mock<ICacheSet<Workflow>> _mockWorkflowsCacheSet;
    private readonly FlowCastHandler _handler;

    public FlowCastHandlerTests()
    {
        _mockCacheContext = new Mock<IDomainCacheContext>();
        _mockWorkflowsCacheSet = new Mock<ICacheSet<Workflow>>();
        
        _mockCacheContext.Setup(x => x.Workflows).Returns(_mockWorkflowsCacheSet.Object);
        
        _handler = new FlowCastHandler(_mockCacheContext.Object);
    }

    [Fact]
    public void CanHandle_ShouldReturnTrue_ForSysFlows()
    {
        // Act
        var result = _handler.CanHandle("sys-flows");

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void CanHandle_ShouldReturnFalse_ForOtherWorkflowTypes()
    {
        // Arrange & Act & Assert
        _handler.CanHandle("sys-tasks").ShouldBeFalse();
        _handler.CanHandle("sys-views").ShouldBeFalse();
        _handler.CanHandle("sys-schemas").ShouldBeFalse();
        _handler.CanHandle("sys-functions").ShouldBeFalse();
        _handler.CanHandle("sys-extensions").ShouldBeFalse();
        _handler.CanHandle("unknown").ShouldBeFalse();
    }

    [Fact]
    public async Task HandleAsync_ShouldDeserializeAndCacheWorkflow()
    {
        // Arrange
        var reference = new Reference("test-flow", "test-domain", "sys-flows", "1.0.0");
        
        var workflowJson = """
        {
            "key": "test-flow",
            "domain": "test-domain",
            "version": "1.0.0",
            "flow": "sys-flows",
            "type": "F",
            "timeout": null,
            "labels": [],
            "functions": [],
            "features": [],
            "states": [],
            "sharedTransitions": [],
            "extensions": [],
            "startTransition": {
                "key": "start",
                "target": "initial"
            }
        }
        """;
        
        var attributes = JsonDocument.Parse(workflowJson).RootElement;

        _mockWorkflowsCacheSet
            .Setup(x => x.SetAsync(It.IsAny<Workflow>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _handler.HandleAsync(reference, attributes, CancellationToken.None);

        // Assert
        _mockWorkflowsCacheSet.Verify(
            x => x.SetAsync(It.Is<Workflow>(w => 
                w.Key == "test-flow" && 
                w.Domain == "test-domain" && 
                w.Version == "1.0.0"), 
                It.IsAny<CancellationToken>()), 
            Times.Once);
    }
}

