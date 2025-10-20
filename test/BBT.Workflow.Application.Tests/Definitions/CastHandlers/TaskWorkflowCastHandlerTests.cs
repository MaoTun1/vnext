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
/// Unit tests for TaskWorkflowCastHandler
/// Tests task workflow casting operations
/// </summary>
public class TaskWorkflowCastHandlerTests
{
    private readonly Mock<IDomainCacheContext> _mockCacheContext;
    private readonly Mock<ICacheSet<WorkflowTask>> _mockTasksCacheSet;
    private readonly TaskWorkflowCastHandler _handler;

    public TaskWorkflowCastHandlerTests()
    {
        _mockCacheContext = new Mock<IDomainCacheContext>();
        _mockTasksCacheSet = new Mock<ICacheSet<WorkflowTask>>();
        
        _mockCacheContext.Setup(x => x.Tasks).Returns(_mockTasksCacheSet.Object);
        
        _handler = new TaskWorkflowCastHandler(_mockCacheContext.Object);
    }

    [Fact]
    public void CanHandle_ShouldReturnTrue_ForSysTasks()
    {
        // Act
        var result = _handler.CanHandle("sys-tasks");

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void CanHandle_ShouldReturnFalse_ForOtherWorkflowTypes()
    {
        // Arrange & Act & Assert
        _handler.CanHandle("sys-flows").ShouldBeFalse();
        _handler.CanHandle("sys-views").ShouldBeFalse();
        _handler.CanHandle("sys-schemas").ShouldBeFalse();
        _handler.CanHandle("sys-functions").ShouldBeFalse();
        _handler.CanHandle("sys-extensions").ShouldBeFalse();
        _handler.CanHandle("unknown").ShouldBeFalse();
    }

    [Fact]
    public async Task HandleAsync_ShouldDeserializeAndCacheTask()
    {
        // Arrange
        var reference = new Reference("test-key", "test-domain", "sys-tasks", "1.0.0");
        
        var taskJson = """
        {
            "type": "6",
            "config": {
                "validateSSL": false,
                "timeoutSeconds": 30,
                "url": "https://example.com",
                "method": "GET"
            }
        }
        """;
        
        var attributes = JsonDocument.Parse(taskJson).RootElement;

        _mockTasksCacheSet
            .Setup(x => x.SetAsync(It.IsAny<WorkflowTask>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _handler.HandleAsync(reference, attributes, CancellationToken.None);

        // Assert
        _mockTasksCacheSet.Verify(
            x => x.SetAsync(It.Is<WorkflowTask>(t => 
                t.Key == "test-key" && 
                t.Domain == "test-domain" && 
                t.Version == "1.0.0"), 
                It.IsAny<CancellationToken>()), 
            Times.Once);
    }
}

