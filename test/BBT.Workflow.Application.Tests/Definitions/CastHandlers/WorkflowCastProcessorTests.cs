using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using BBT.Workflow.Definitions.CastHandlers;
using Moq;
using Shouldly;
using Xunit;

namespace BBT.Workflow.Definitions.CastHandlers;

/// <summary>
/// Unit tests for WorkflowCastProcessor
/// Tests the workflow casting coordination and handler selection
/// </summary>
public class WorkflowCastProcessorTests
{
    private readonly Mock<IWorkflowCastHandler> _mockHandler1;
    private readonly Mock<IWorkflowCastHandler> _mockHandler2;
    private readonly WorkflowCastProcessor _processor;

    public WorkflowCastProcessorTests()
    {
        _mockHandler1 = new Mock<IWorkflowCastHandler>();
        _mockHandler2 = new Mock<IWorkflowCastHandler>();

        var handlers = new List<IWorkflowCastHandler>
        {
            _mockHandler1.Object,
            _mockHandler2.Object
        };

        _processor = new WorkflowCastProcessor(handlers);
    }

    [Fact]
    public async Task ProcessAsync_ShouldSelectCorrectHandler_WhenHandlerCanHandle()
    {
        // Arrange
        var workflow = "sys-tasks";
        var reference = Mock.Of<IReference>();
        var attributes = JsonDocument.Parse("{}").RootElement;

        _mockHandler1.Setup(x => x.CanHandle(workflow)).Returns(false);
        _mockHandler2.Setup(x => x.CanHandle(workflow)).Returns(true);
        _mockHandler2.Setup(x => x.HandleAsync(reference, attributes, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _processor.ProcessAsync(workflow, reference, attributes, CancellationToken.None);

        // Assert
        _mockHandler1.Verify(x => x.CanHandle(workflow), Times.Once);
        _mockHandler2.Verify(x => x.CanHandle(workflow), Times.Once);
        _mockHandler2.Verify(x => x.HandleAsync(reference, attributes, It.IsAny<CancellationToken>()), Times.Once);
        _mockHandler1.Verify(x => x.HandleAsync(It.IsAny<IReference>(), It.IsAny<JsonElement>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessAsync_ShouldThrowException_WhenNoHandlerFound()
    {
        // Arrange
        var workflow = "unknown-workflow";
        var reference = Mock.Of<IReference>();
        var attributes = JsonDocument.Parse("{}").RootElement;

        _mockHandler1.Setup(x => x.CanHandle(workflow)).Returns(false);
        _mockHandler2.Setup(x => x.CanHandle(workflow)).Returns(false);

        // Act & Assert
        var exception = await Should.ThrowAsync<NotSupportedException>(
            async () => await _processor.ProcessAsync(workflow, reference, attributes, CancellationToken.None)
        );

        exception.Message.ShouldBe("No handler found for component 'unknown-workflow'.");
    }

    [Fact]
    public async Task ProcessAsync_ShouldSelectFirstMatchingHandler_WhenMultipleHandlersCanHandle()
    {
        // Arrange
        var workflow = "sys-flows";
        var reference = Mock.Of<IReference>();
        var attributes = JsonDocument.Parse("{}").RootElement;

        _mockHandler1.Setup(x => x.CanHandle(workflow)).Returns(true);
        _mockHandler1.Setup(x => x.HandleAsync(reference, attributes, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockHandler2.Setup(x => x.CanHandle(workflow)).Returns(true);

        // Act
        await _processor.ProcessAsync(workflow, reference, attributes, CancellationToken.None);

        // Assert
        _mockHandler1.Verify(x => x.HandleAsync(reference, attributes, It.IsAny<CancellationToken>()), Times.Once);
        _mockHandler2.Verify(x => x.HandleAsync(It.IsAny<IReference>(), It.IsAny<JsonElement>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessAsync_ShouldPassCancellationToken_ToHandler()
    {
        // Arrange
        var workflow = "sys-views";
        var reference = Mock.Of<IReference>();
        var attributes = JsonDocument.Parse("{}").RootElement;
        var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;

        _mockHandler1.Setup(x => x.CanHandle(workflow)).Returns(true);
        _mockHandler1.Setup(x => x.HandleAsync(reference, attributes, cancellationToken))
            .Returns(Task.CompletedTask);

        // Act
        await _processor.ProcessAsync(workflow, reference, attributes, cancellationToken);

        // Assert
        _mockHandler1.Verify(x => x.HandleAsync(reference, attributes, cancellationToken), Times.Once);
    }
}

