using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BBT.Workflow.BackgroundJobs;
using BBT.Workflow.Monitoring;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace BBT.Workflow.Domain.Tests.BackgroundJobs;

/// <summary>
/// Unit tests for JobDispatcher
/// </summary>
public class JobDispatcherTests : DomainTestBase<DomainEntryPoint>
{
    private readonly IWorkflowMetrics _mockMetrics;
    private readonly ILogger<JobDispatcher> _mockLogger;
    private readonly List<IJobHandler> _handlers;

    public JobDispatcherTests()
    {
        _mockMetrics = Substitute.For<IWorkflowMetrics>();
        _mockLogger = Substitute.For<ILogger<JobDispatcher>>();
        _handlers = new List<IJobHandler>();
    }

    [Fact]
    public async Task DispatchAsync_ShouldLogWarning_WhenNoHandlerFound()
    {
        // Arrange
        var dispatcher = new JobDispatcher(_handlers, _mockMetrics, _mockLogger);
        var jobName = "unknown-job";
        var jobPayload = new ReadOnlyMemory<byte>(new byte[] { 1, 2, 3 });

        // Act
        await dispatcher.DispatchAsync(jobName, jobPayload, CancellationToken.None);

        // Assert
        _mockLogger.Received(1).Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("No handler found for job")),
            null,
            Arg.Any<Func<object, Exception?, string>>());

        _mockMetrics.Received(1).RecordBackgroundJobFailed("Unknown", jobName, "NoHandlerFound");
    }

    [Fact]
    public async Task DispatchAsync_ShouldCallHandler_WhenHandlerFound()
    {
        // Arrange
        var handler = Substitute.For<IJobHandler>();
        handler.JobName.Returns("test-job");
        _handlers.Add(handler);

        var dispatcher = new JobDispatcher(_handlers, _mockMetrics, _mockLogger);
        var jobPayload = new ReadOnlyMemory<byte>(new byte[] { 1, 2, 3 });

        // Act
        await dispatcher.DispatchAsync("test-job", jobPayload, CancellationToken.None);

        // Assert
        await handler.Received(1).HandleAsync(jobPayload, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DispatchAsync_ShouldRecordSuccessMetrics_WhenHandlerSucceeds()
    {
        // Arrange
        var handler = Substitute.For<IJobHandler>();
        handler.JobName.Returns("test-job");
        _handlers.Add(handler);

        var dispatcher = new JobDispatcher(_handlers, _mockMetrics, _mockLogger);
        var jobPayload = new ReadOnlyMemory<byte>(new byte[] { 1, 2, 3 });

        // Act
        await dispatcher.DispatchAsync("test-job", jobPayload, CancellationToken.None);

        // Assert
        var handlerType = handler.GetType().Name;
        _mockMetrics.Received(1).SetBackgroundJobsRunning(handlerType, 1);
        _mockMetrics.Received(1).RecordBackgroundJobExecuted(handlerType, "test-job", "success");
        _mockMetrics.Received(1).RecordBackgroundJobDuration(handlerType, "test-job", "success", Arg.Any<double>());
        _mockMetrics.Received(1).SetBackgroundJobsRunning(handlerType, 0);
    }

    [Fact]
    public async Task DispatchAsync_ShouldLogSuccess_WhenHandlerSucceeds()
    {
        // Arrange
        var handler = Substitute.For<IJobHandler>();
        handler.JobName.Returns("test-job");
        _handlers.Add(handler);

        var dispatcher = new JobDispatcher(_handlers, _mockMetrics, _mockLogger);
        var jobPayload = new ReadOnlyMemory<byte>(new byte[] { 1, 2, 3 });

        // Act
        await dispatcher.DispatchAsync("test-job", jobPayload, CancellationToken.None);

        // Assert
        _mockLogger.Received(1).Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Dispatching job")),
            null,
            Arg.Any<Func<object, Exception?, string>>());

        _mockLogger.Received(1).Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Successfully completed job")),
            null,
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task DispatchAsync_ShouldRecordFailureMetrics_WhenHandlerThrows()
    {
        // Arrange
        var handler = Substitute.For<IJobHandler>();
        handler.JobName.Returns("test-job");
        handler.HandleAsync(Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Test exception"));
        _handlers.Add(handler);

        var dispatcher = new JobDispatcher(_handlers, _mockMetrics, _mockLogger);
        var jobPayload = new ReadOnlyMemory<byte>(new byte[] { 1, 2, 3 });

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await dispatcher.DispatchAsync("test-job", jobPayload, CancellationToken.None));

        var handlerType = handler.GetType().Name;
        _mockMetrics.Received(1).SetBackgroundJobsRunning(handlerType, 1);
        _mockMetrics.Received(1).RecordBackgroundJobExecuted(handlerType, "test-job", "failed");
        _mockMetrics.Received(1).RecordBackgroundJobFailed(handlerType, "test-job", "InvalidOperationException");
        _mockMetrics.Received(1).RecordBackgroundJobDuration(handlerType, "test-job", "failed", Arg.Any<double>());
        _mockMetrics.Received(1).SetBackgroundJobsRunning(handlerType, 0);
    }

    [Fact]
    public async Task DispatchAsync_ShouldLogError_WhenHandlerThrows()
    {
        // Arrange
        var handler = Substitute.For<IJobHandler>();
        handler.JobName.Returns("test-job");
        var expectedException = new InvalidOperationException("Test exception");
        handler.HandleAsync(Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(expectedException);
        _handlers.Add(handler);

        var dispatcher = new JobDispatcher(_handlers, _mockMetrics, _mockLogger);
        var jobPayload = new ReadOnlyMemory<byte>(new byte[] { 1, 2, 3 });

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await dispatcher.DispatchAsync("test-job", jobPayload, CancellationToken.None));

        _mockLogger.Received(1).Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Job") && o.ToString()!.Contains("failed")),
            expectedException,
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task DispatchAsync_ShouldRecordCancellationMetrics_WhenCancellationRequested()
    {
        // Arrange
        var handler = Substitute.For<IJobHandler>();
        handler.JobName.Returns("test-job");
        handler.HandleAsync(Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException());
        _handlers.Add(handler);

        var dispatcher = new JobDispatcher(_handlers, _mockMetrics, _mockLogger);
        var jobPayload = new ReadOnlyMemory<byte>(new byte[] { 1, 2, 3 });
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await dispatcher.DispatchAsync("test-job", jobPayload, cts.Token));

        var handlerType = handler.GetType().Name;
        _mockMetrics.Received(1).SetBackgroundJobsRunning(handlerType, 1);
        _mockMetrics.Received(1).RecordBackgroundJobExecuted(handlerType, "test-job", "cancelled");
        _mockMetrics.Received(1).RecordBackgroundJobDuration(handlerType, "test-job", "cancelled", Arg.Any<double>());
        _mockMetrics.Received(1).SetBackgroundJobsRunning(handlerType, 0);
    }

    [Fact]
    public async Task DispatchAsync_ShouldLogWarning_WhenCancellationRequested()
    {
        // Arrange
        var handler = Substitute.For<IJobHandler>();
        handler.JobName.Returns("test-job");
        handler.HandleAsync(Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException());
        _handlers.Add(handler);

        var dispatcher = new JobDispatcher(_handlers, _mockMetrics, _mockLogger);
        var jobPayload = new ReadOnlyMemory<byte>(new byte[] { 1, 2, 3 });
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await dispatcher.DispatchAsync("test-job", jobPayload, cts.Token));

        _mockLogger.Received(1).Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("was cancelled")),
            null,
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task DispatchAsync_ShouldUseFirstMatchingHandler_WhenMultipleHandlersExist()
    {
        // Arrange
        var handler1 = Substitute.For<IJobHandler>();
        handler1.JobName.Returns("test-job");
        var handler2 = Substitute.For<IJobHandler>();
        handler2.JobName.Returns("test-job");
        _handlers.Add(handler1);
        _handlers.Add(handler2);

        var dispatcher = new JobDispatcher(_handlers, _mockMetrics, _mockLogger);
        var jobPayload = new ReadOnlyMemory<byte>(new byte[] { 1, 2, 3 });

        // Act
        await dispatcher.DispatchAsync("test-job", jobPayload, CancellationToken.None);

        // Assert
        await handler1.Received(1).HandleAsync(jobPayload, Arg.Any<CancellationToken>());
        await handler2.DidNotReceive().HandleAsync(Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DispatchAsync_ShouldMatchCorrectHandler_WhenMultipleDifferentHandlersExist()
    {
        // Arrange
        var handler1 = Substitute.For<IJobHandler>();
        handler1.JobName.Returns("job-1");
        var handler2 = Substitute.For<IJobHandler>();
        handler2.JobName.Returns("job-2");
        _handlers.Add(handler1);
        _handlers.Add(handler2);

        var dispatcher = new JobDispatcher(_handlers, _mockMetrics, _mockLogger);
        var jobPayload = new ReadOnlyMemory<byte>(new byte[] { 1, 2, 3 });

        // Act
        await dispatcher.DispatchAsync("job-2", jobPayload, CancellationToken.None);

        // Assert
        await handler1.DidNotReceive().HandleAsync(Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<CancellationToken>());
        await handler2.Received(1).HandleAsync(jobPayload, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DispatchAsync_ShouldAlwaysCallSetRunningInFinally_WhenHandlerThrows()
    {
        // Arrange
        var handler = Substitute.For<IJobHandler>();
        handler.JobName.Returns("test-job");
        handler.HandleAsync(Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new Exception("Test exception"));
        _handlers.Add(handler);

        var dispatcher = new JobDispatcher(_handlers, _mockMetrics, _mockLogger);
        var jobPayload = new ReadOnlyMemory<byte>(new byte[] { 1, 2, 3 });

        // Act & Assert
        await Assert.ThrowsAsync<Exception>(async () =>
            await dispatcher.DispatchAsync("test-job", jobPayload, CancellationToken.None));

        var handlerType = handler.GetType().Name;
        // Verify SetBackgroundJobsRunning was called twice: once to increment, once to decrement
        _mockMetrics.Received(1).SetBackgroundJobsRunning(handlerType, 1);
        _mockMetrics.Received(1).SetBackgroundJobsRunning(handlerType, 0);
    }

    [Fact]
    public async Task DispatchAsync_ShouldPassCancellationToken_ToHandler()
    {
        // Arrange
        var handler = Substitute.For<IJobHandler>();
        handler.JobName.Returns("test-job");
        _handlers.Add(handler);

        var dispatcher = new JobDispatcher(_handlers, _mockMetrics, _mockLogger);
        var jobPayload = new ReadOnlyMemory<byte>(new byte[] { 1, 2, 3 });
        var cts = new CancellationTokenSource();

        // Act
        await dispatcher.DispatchAsync("test-job", jobPayload, cts.Token);

        // Assert
        await handler.Received(1).HandleAsync(jobPayload, cts.Token);
    }

    [Fact]
    public async Task DispatchAsync_ShouldHandleEmptyPayload()
    {
        // Arrange
        var handler = Substitute.For<IJobHandler>();
        handler.JobName.Returns("test-job");
        _handlers.Add(handler);

        var dispatcher = new JobDispatcher(_handlers, _mockMetrics, _mockLogger);
        var jobPayload = ReadOnlyMemory<byte>.Empty;

        // Act
        await dispatcher.DispatchAsync("test-job", jobPayload, CancellationToken.None);

        // Assert
        await handler.Received(1).HandleAsync(jobPayload, Arg.Any<CancellationToken>());
    }
}

