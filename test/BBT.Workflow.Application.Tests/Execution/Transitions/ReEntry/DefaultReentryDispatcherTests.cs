using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BBT.Workflow.Domain;
using BBT.Workflow.ExceptionHandling;
using BBT.Workflow.Execution;
using BBT.Workflow.Execution.ReEntry;
using BBT.Workflow.Execution.Services;
using BBT.Workflow.Instances;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Shouldly;
using Xunit;

namespace BBT.Workflow.Application.Tests.Execution.Transitions.ReEntry;

/// <summary>
/// Unit tests for DefaultReentryDispatcher
/// Tests re-entry dispatcher functionality for automatic and scheduled transitions
/// </summary>
public class DefaultReentryDispatcherTests
{
    private readonly Mock<IServiceScopeFactory> _mockScopeFactory;
    private readonly Mock<IServiceScope> _mockScope;
    private readonly Mock<IWorkflowExecutionService> _mockExecutionService;
    private readonly Mock<ILogger<DefaultReentryDispatcher>> _mockLogger;
    private readonly ReentryOptions _options;
    private readonly DefaultReentryDispatcher _dispatcher;

    public DefaultReentryDispatcherTests()
    {
        _mockScopeFactory = new Mock<IServiceScopeFactory>();
        _mockScope = new Mock<IServiceScope>();
        var mockServiceProvider = new Mock<IServiceProvider>();
        _mockExecutionService = new Mock<IWorkflowExecutionService>();
        _mockLogger = new Mock<ILogger<DefaultReentryDispatcher>>();

        _options = new ReentryOptions
        {
            MaxAutoHops = 12,
            AllowInlineAuto = true
        };

        var optionsWrapper = Options.Create(_options);

        // Setup scope factory chain
        _mockScopeFactory
            .Setup(x => x.CreateScope())
            .Returns(_mockScope.Object);

        _mockScope
            .Setup(x => x.ServiceProvider)
            .Returns(mockServiceProvider.Object);

        mockServiceProvider
            .Setup(x => x.GetService(typeof(IWorkflowExecutionService)))
            .Returns(_mockExecutionService.Object);

        _dispatcher = new DefaultReentryDispatcher(
            _mockScopeFactory.Object,
            optionsWrapper,
            _mockLogger.Object);
    }

    #region DispatchAutoAsync Tests

    [Fact]
    public async Task DispatchAutoAsync_WithValidCommand_ShouldExecuteInline()
    {
        // Arrange
        var command = CreateAutoCommand();
        SetupSuccessfulExecution();

        // Act
        var result = await _dispatcher.DispatchAutoAsync(command, CancellationToken.None);

        // Assert
        result.InlineExecuted.ShouldBeTrue();
        result.Succeeded.ShouldBeTrue();

        _mockExecutionService.Verify(
            x => x.ExecuteTransitionAsync(It.IsAny<WorkflowExecutionContext>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DispatchAutoAsync_ShouldIncrementChainDepth()
    {
        // Arrange
        var command = CreateAutoCommand(chainDepth: 3);
        WorkflowExecutionContext? capturedContext = null;

        SetupSuccessfulExecution();

        _mockExecutionService
            .Setup(x => x.ExecuteTransitionAsync(It.IsAny<WorkflowExecutionContext>(), It.IsAny<CancellationToken>()))
            .Callback<WorkflowExecutionContext, CancellationToken>((ctx, _) => capturedContext = ctx)
            .ReturnsAsync(Result<TransitionOutput>.Ok(new TransitionOutput { Id = Guid.NewGuid(), Status = InstanceStatus.Active }));

        // Act
        await _dispatcher.DispatchAutoAsync(command, CancellationToken.None);

        // Assert
        capturedContext.ShouldNotBeNull();
        capturedContext.Execution.ShouldNotBeNull();
        capturedContext.Execution!.ChainDepth.ShouldBe(4); // Incremented from 3 to 4
    }

    [Fact]
    public async Task DispatchAutoAsync_WhenExceedsMaxHops_ShouldReturnFailedOutcome()
    {
        // Arrange
        var command = CreateAutoCommand(chainDepth: 13); // Exceeds default max of 12

        // Act
        var result = await _dispatcher.DispatchAutoAsync(command, CancellationToken.None);

        // Assert
        result.InlineExecuted.ShouldBeFalse();
        result.Succeeded.ShouldBeFalse();

        _mockExecutionService.Verify(
            x => x.ExecuteTransitionAsync(It.IsAny<WorkflowExecutionContext>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact(Skip = "Test needs implementation details adjustment")]
    public async Task DispatchAutoAsync_WhenInlineNotPreferred_ShouldReturnNotInlineExecuted()
    {
        // Arrange
        var command = CreateAutoCommand();

        // Act
        var result = await _dispatcher.DispatchAutoAsync(command, CancellationToken.None);

        // Assert
        result.InlineExecuted.ShouldBeFalse();
        result.Succeeded.ShouldBeFalse();

        _mockExecutionService.Verify(
            x => x.ExecuteTransitionAsync(It.IsAny<WorkflowExecutionContext>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task DispatchAutoAsync_WhenInlineAutoNotAllowed_ShouldReturnNotInlineExecuted()
    {
        // Arrange
        _options.AllowInlineAuto = false;
        var command = CreateAutoCommand();

        // Act
        var result = await _dispatcher.DispatchAutoAsync(command, CancellationToken.None);

        // Assert
        result.InlineExecuted.ShouldBeFalse();
        result.Succeeded.ShouldBeFalse();

        _mockExecutionService.Verify(
            x => x.ExecuteTransitionAsync(It.IsAny<WorkflowExecutionContext>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task DispatchAutoAsync_WhenExecutionFails_ShouldReturnFailedOutcome()
    {
        // Arrange
        var command = CreateAutoCommand();
        var error = Error.Failure("execution.failed", "Execution failed");

        _mockExecutionService
            .Setup(x => x.ExecuteTransitionAsync(It.IsAny<WorkflowExecutionContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<TransitionOutput>.Fail(error));

        // Act
        var result = await _dispatcher.DispatchAutoAsync(command, CancellationToken.None);

        // Assert
        result.InlineExecuted.ShouldBeTrue();
        result.Succeeded.ShouldBeFalse();
    }

    [Fact]
    public async Task DispatchAutoAsync_WhenAutoConditionNotMet_ShouldReturnFailedOutcome()
    {
        // Arrange
        var command = CreateAutoCommand();
        var error = Error.Validation(
            WorkflowErrorCodes.AutoTransitionConditionNotMet,
            "Condition not met");

        _mockExecutionService
            .Setup(x => x.ExecuteTransitionAsync(It.IsAny<WorkflowExecutionContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<TransitionOutput>.Fail(error));

        // Act
        var result = await _dispatcher.DispatchAutoAsync(command, CancellationToken.None);

        // Assert
        result.InlineExecuted.ShouldBeTrue();
        result.Succeeded.ShouldBeFalse();

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("condition not met")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task DispatchAutoAsync_WhenAutoConditionNotMetExceptionThrown_ShouldReturnFailedOutcome()
    {
        // Arrange
        var command = CreateAutoCommand();

        _mockExecutionService
            .Setup(x => x.ExecuteTransitionAsync(It.IsAny<WorkflowExecutionContext>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AutoTransitionConditionNotMetException());

        // Act
        var result = await _dispatcher.DispatchAutoAsync(command, CancellationToken.None);

        // Assert
        result.InlineExecuted.ShouldBeTrue();
        result.Succeeded.ShouldBeFalse();
    }

    [Fact]
    public async Task DispatchAutoAsync_WhenCancelled_ShouldPropagateCancellation()
    {
        // Arrange
        var command = CreateAutoCommand();
        var cts = new CancellationTokenSource();
        cts.Cancel();

        _mockExecutionService
            .Setup(x => x.ExecuteTransitionAsync(It.IsAny<WorkflowExecutionContext>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        // Act & Assert
        await Should.ThrowAsync<OperationCanceledException>(
            async () => await _dispatcher.DispatchAutoAsync(command, cts.Token));
    }

    [Fact]
    public async Task DispatchAutoAsync_WhenUnexpectedExceptionThrown_ShouldPropagateException()
    {
        // Arrange
        var command = CreateAutoCommand();
        var expectedException = new InvalidOperationException("Unexpected error");

        _mockExecutionService
            .Setup(x => x.ExecuteTransitionAsync(It.IsAny<WorkflowExecutionContext>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(expectedException);

        // Act & Assert
        var exception = await Should.ThrowAsync<InvalidOperationException>(
            async () => await _dispatcher.DispatchAutoAsync(command, CancellationToken.None));

        exception.ShouldBe(expectedException);
    }

    [Fact]
    public async Task DispatchAutoAsync_ShouldCreateNewScope()
    {
        // Arrange
        var command = CreateAutoCommand();
        SetupSuccessfulExecution();

        // Act
        await _dispatcher.DispatchAutoAsync(command, CancellationToken.None);

        // Assert
        _mockScopeFactory.Verify(x => x.CreateScope(), Times.Once);
        _mockScope.Verify(x => x.Dispose(), Times.Once);
    }

    [Fact]
    public async Task DispatchAutoAsync_ShouldLogTraceMessages()
    {
        // Arrange
        var command = CreateAutoCommand();
        SetupSuccessfulExecution();

        // Act
        await _dispatcher.DispatchAutoAsync(command, CancellationToken.None);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Trace,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => 
                    v.ToString()!.Contains("Executing inline re-entry") ||
                    v.ToString()!.Contains("Completed inline re-entry")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeast(2));
    }

    [Fact]
    public async Task DispatchAutoAsync_WithHeaders_ShouldPassHeadersToExecutionService()
    {
        // Arrange
        var headers = new Dictionary<string, string?>
        {
            ["X-Custom-Header"] = "value"
        };
        var command = CreateAutoCommand(headers: headers);
        WorkflowExecutionContext? capturedContext = null;

        SetupSuccessfulExecution();

        _mockExecutionService
            .Setup(x => x.ExecuteTransitionAsync(It.IsAny<WorkflowExecutionContext>(), It.IsAny<CancellationToken>()))
            .Callback<WorkflowExecutionContext, CancellationToken>((ctx, _) => capturedContext = ctx)
            .ReturnsAsync(Result<TransitionOutput>.Ok(new TransitionOutput { Id = Guid.NewGuid(), Status = InstanceStatus.Active }));

        // Act
        await _dispatcher.DispatchAutoAsync(command, CancellationToken.None);

        // Assert
        capturedContext.ShouldNotBeNull();
        capturedContext.Headers.ShouldBe(headers);
    }

    [Fact]
    public async Task DispatchAutoAsync_WhenMaxHopsExceeded_ShouldLogWarning()
    {
        // Arrange
        var command = CreateAutoCommand(chainDepth: 15);

        // Act
        await _dispatcher.DispatchAutoAsync(command, CancellationToken.None);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Maximum auto transition hops")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task DispatchAutoAsync_WithCustomMaxHops_ShouldRespectConfiguration()
    {
        // Arrange
        _options.MaxAutoHops = 5;
        var command = CreateAutoCommand(chainDepth: 6);

        // Act
        var result = await _dispatcher.DispatchAutoAsync(command, CancellationToken.None);

        // Assert
        result.InlineExecuted.ShouldBeFalse();
        result.Succeeded.ShouldBeFalse();
    }

    #endregion

    #region Helper Methods

    private void SetupSuccessfulExecution()
    {
        var transitionOutput = new TransitionOutput
        {
            Id = Guid.NewGuid(),
            Status = InstanceStatus.Active
        };
        
        _mockExecutionService
            .Setup(x => x.ExecuteTransitionAsync(It.IsAny<WorkflowExecutionContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<TransitionOutput>.Ok(transitionOutput));
    }

    private ReentryCommand CreateAutoCommand(
        int chainDepth = 0,
        IReadOnlyDictionary<string, string?>? headers = null)
    {
        return ReentryCommand.ForAutomatic(
            Guid.NewGuid(),
            "test-domain",
            "test-workflow",
            "test-transition",
            Guid.NewGuid().ToString("N"),
            chainDepth,
            headers);
    }

    #endregion
}

