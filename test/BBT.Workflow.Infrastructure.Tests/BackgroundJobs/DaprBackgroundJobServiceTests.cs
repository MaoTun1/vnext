using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BBT.Workflow.BackgroundJobs;
using BBT.Workflow.Monitoring;
using Dapr.Jobs;
using Dapr.Jobs.Models;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Xunit;

namespace BBT.Workflow.Infrastructure.Tests.BackgroundJobs;

/// <summary>
/// Unit tests for DaprBackgroundJobService
/// </summary>
public sealed class DaprBackgroundJobServiceTests
{
    private readonly ILogger<DaprBackgroundJobService> _mockLogger;
    private readonly DaprJobsClient _mockDaprJobsClient;
    private readonly IJobStore _mockJobStore;
    private readonly IWorkflowMetrics _mockMetrics;
    private readonly DaprBackgroundJobService _service;

    public DaprBackgroundJobServiceTests()
    {
        _mockLogger = Substitute.For<ILogger<DaprBackgroundJobService>>();
        _mockDaprJobsClient = Substitute.For<DaprJobsClient>();
        _mockJobStore = Substitute.For<IJobStore>();
        _mockMetrics = Substitute.For<IWorkflowMetrics>();
        
        _service = new DaprBackgroundJobService(
            _mockLogger,
            _mockDaprJobsClient,
            _mockJobStore,
            _mockMetrics
        );
    }

    #region EnqueueAsync Tests

    [Fact]
    public async Task EnqueueAsync_Should_Save_Job_To_Store()
    {
        // Arrange
        const string jobName = "TestJob";
        const string jobId = "test-job-123";
        var schedule = DaprJobSchedule.FromDateTime(DateTime.UtcNow.AddMinutes(5));
        var payload = new TestJobPayload { Data = "Test" };
        var metadata = new Dictionary<string, string>
        {
            { "domain", "TestDomain" },
            { "flowName", "TestFlow" },
            { "instanceId", Guid.NewGuid().ToString() }
        };

        // Act
        await _service.EnqueueAsync(jobName, jobId, schedule, payload, metadata);

        // Assert
        await _mockJobStore.Received(1).SaveAsync(
            jobId,
            Arg.Is<BackgroundJobInfo<TestJobPayload>>(j => 
                j.JobName == jobName &&
                j.JobId == jobId &&
                j.Payload == payload &&
                j.Metadata == metadata
            ),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task EnqueueAsync_Should_Schedule_Job_With_Dapr()
    {
        // Arrange
        const string jobName = "TestJob";
        const string jobId = "test-job-123";
        var schedule = DaprJobSchedule.FromDateTime(DateTime.UtcNow.AddMinutes(5));
        var payload = new TestJobPayload { Data = "Test" };
        var metadata = new Dictionary<string, string>
        {
            { "domain", "TestDomain" },
            { "flowName", "TestFlow" }
        };

        // Act
        await _service.EnqueueAsync(jobName, jobId, schedule, payload, metadata);

        // Assert
#pragma warning disable CS0618
        await _mockDaprJobsClient.Received(1).ScheduleJobAsync(
            jobName,
            schedule,
            Arg.Any<ReadOnlyMemory<byte>?>(),
            null,
            null,
            null,
            Arg.Any<CancellationToken>()
        );
#pragma warning restore CS0618
    }

    [Fact]
    public async Task EnqueueAsync_Should_Record_Metrics_On_Success()
    {
        // Arrange
        const string jobName = "TestJob";
        const string jobId = "test-job-123";
        var schedule = DaprJobSchedule.FromDateTime(DateTime.UtcNow.AddMinutes(5));
        var payload = new TestJobPayload { Data = "Test" };

        // Act
        await _service.EnqueueAsync(jobName, jobId, schedule, payload);

        // Assert
        _mockMetrics.Received(1).RecordBackgroundJobScheduled(
            Arg.Is<string>(x => x == "TestJobPayload"),
            jobName
        );
    }

    [Fact]
    public async Task EnqueueAsync_Should_Use_CancellationToken()
    {
        // Arrange
        const string jobName = "TestJob";
        const string jobId = "test-job-123";
        var schedule = DaprJobSchedule.FromDateTime(DateTime.UtcNow.AddMinutes(5));
        var payload = new TestJobPayload { Data = "Test" };
        var cts = new CancellationTokenSource();
        var cancellationToken = cts.Token;

        // Act
        await _service.EnqueueAsync(jobName, jobId, schedule, payload, null, cancellationToken);

        // Assert
        await _mockJobStore.Received(1).SaveAsync(
            jobId,
            Arg.Any<BackgroundJobInfo<TestJobPayload>>(),
            cancellationToken
        );
    }

    [Fact]
    public async Task EnqueueAsync_Should_Set_IsTriggered_To_False()
    {
        // Arrange
        const string jobName = "TestJob";
        const string jobId = "test-job-123";
        var schedule = DaprJobSchedule.FromDateTime(DateTime.UtcNow.AddMinutes(5));
        var payload = new TestJobPayload { Data = "Test" };

        // Act
        await _service.EnqueueAsync(jobName, jobId, schedule, payload);

        // Assert
        await _mockJobStore.Received(1).SaveAsync(
            jobId,
            Arg.Is<BackgroundJobInfo<TestJobPayload>>(j => j.IsTriggered == false),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task EnqueueAsync_Should_Store_ExpressionValue_From_Schedule()
    {
        // Arrange
        const string jobName = "TestJob";
        const string jobId = "test-job-123";
        var targetTime = DateTime.UtcNow.AddMinutes(5);
        var schedule = DaprJobSchedule.FromDateTime(targetTime);
        var payload = new TestJobPayload { Data = "Test" };

        // Act
        await _service.EnqueueAsync(jobName, jobId, schedule, payload);

        // Assert
        await _mockJobStore.Received(1).SaveAsync(
            jobId,
            Arg.Is<BackgroundJobInfo<TestJobPayload>>(j => 
                j.ExpressionValue == schedule.ExpressionValue
            ),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task EnqueueAsync_Should_Handle_Null_Metadata()
    {
        // Arrange
        const string jobName = "TestJob";
        const string jobId = "test-job-123";
        var schedule = DaprJobSchedule.FromDateTime(DateTime.UtcNow.AddMinutes(5));
        var payload = new TestJobPayload { Data = "Test" };

        // Act
        await _service.EnqueueAsync(jobName, jobId, schedule, payload, null);

        // Assert
        await _mockJobStore.Received(1).SaveAsync(
            jobId,
            Arg.Is<BackgroundJobInfo<TestJobPayload>>(j => j.Metadata == null),
            Arg.Any<CancellationToken>()
        );
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task EnqueueAsync_Should_Log_Error_On_JobStore_Failure()
    {
        // Arrange
        const string jobName = "TestJob";
        const string jobId = "test-job-123";
        var schedule = DaprJobSchedule.FromDateTime(DateTime.UtcNow.AddMinutes(5));
        var payload = new TestJobPayload { Data = "Test" };
        
        _mockJobStore.SaveAsync(
            Arg.Any<string>(),
            Arg.Any<BackgroundJobInfo<TestJobPayload>>(),
            Arg.Any<CancellationToken>()
        ).Returns(Task.FromException(new InvalidOperationException("Store error")));

        // Act & Assert
        await Should.ThrowAsync<InvalidOperationException>(
            async () => await _service.EnqueueAsync(jobName, jobId, schedule, payload)
        );
    }

    [Fact]
    public async Task EnqueueAsync_Should_Log_Error_On_Dapr_Failure()
    {
        // Arrange
        const string jobName = "TestJob";
        const string jobId = "test-job-123";
        var schedule = DaprJobSchedule.FromDateTime(DateTime.UtcNow.AddMinutes(5));
        var payload = new TestJobPayload { Data = "Test" };
        
#pragma warning disable CS0618
        _mockDaprJobsClient.ScheduleJobAsync(
            Arg.Any<string>(),
            Arg.Any<DaprJobSchedule>(),
            Arg.Any<ReadOnlyMemory<byte>?>(),
            null,
            null,
            null,
            Arg.Any<CancellationToken>()
        ).Returns(Task.FromException(new InvalidOperationException("Dapr error")));
#pragma warning restore CS0618

        // Act & Assert
        await Should.ThrowAsync<InvalidOperationException>(
            async () => await _service.EnqueueAsync(jobName, jobId, schedule, payload)
        );
    }

    [Fact]
    public async Task EnqueueAsync_Should_Not_Record_Metrics_On_Failure()
    {
        // Arrange
        const string jobName = "TestJob";
        const string jobId = "test-job-123";
        var schedule = DaprJobSchedule.FromDateTime(DateTime.UtcNow.AddMinutes(5));
        var payload = new TestJobPayload { Data = "Test" };
        
        _mockJobStore.SaveAsync(
            Arg.Any<string>(),
            Arg.Any<BackgroundJobInfo<TestJobPayload>>(),
            Arg.Any<CancellationToken>()
        ).Returns(Task.FromException(new InvalidOperationException("Store error")));

        // Act
        try
        {
            await _service.EnqueueAsync(jobName, jobId, schedule, payload);
        }
        catch
        {
            // Expected exception
        }

        // Assert
        _mockMetrics.DidNotReceive().RecordBackgroundJobScheduled(
            Arg.Any<string>(),
            Arg.Any<string>()
        );
    }

    #endregion

    #region Schedule Format Tests

    [Fact]
    public async Task EnqueueAsync_Should_Handle_Cron_Schedule()
    {
        // Arrange
        const string jobName = "TestJob";
        const string jobId = "test-job-123";
        var schedule = DaprJobSchedule.FromExpression("0 */5 * * *");
        var payload = new TestJobPayload { Data = "Test" };

        // Act & Assert
        Should.NotThrow(async () => await _service.EnqueueAsync(jobName, jobId, schedule, payload));
    }
    
    [Fact]
    public async Task EnqueueAsync_Should_Handle_TimeSpan_Schedule()
    {
        // Arrange
        const string jobName = "TestJob";
        const string jobId = "test-job-123";
        var schedule = DaprJobSchedule.FromDuration(TimeSpan.FromMinutes(5));
        var payload = new TestJobPayload { Data = "Test" };

        // Act & Assert
        Should.NotThrow(async () => await _service.EnqueueAsync(jobName, jobId, schedule, payload));
    }

    #endregion

    #region Integration Tests

    [Fact]
    public async Task EnqueueAsync_Should_Execute_Full_Flow_Successfully()
    {
        // Arrange
        const string jobName = "TestJob";
        const string jobId = "test-job-123";
        var schedule = DaprJobSchedule.FromDateTime(DateTime.UtcNow.AddMinutes(5));
        var payload = new TestJobPayload { Data = "Test" };
        var metadata = new Dictionary<string, string>
        {
            { "domain", "TestDomain" },
            { "flowName", "TestFlow" },
            { "instanceId", Guid.NewGuid().ToString() }
        };

        // Act
        await _service.EnqueueAsync(jobName, jobId, schedule, payload, metadata);

        // Assert - Verify complete flow
        await _mockJobStore.Received(1).SaveAsync(
            Arg.Any<string>(),
            Arg.Any<BackgroundJobInfo<TestJobPayload>>(),
            Arg.Any<CancellationToken>()
        );
        
#pragma warning disable CS0618
        await _mockDaprJobsClient.Received(1).ScheduleJobAsync(
            Arg.Any<string>(),
            Arg.Any<DaprJobSchedule>(),
            Arg.Any<ReadOnlyMemory<byte>?>(),
            null,
            null,
            null,
            Arg.Any<CancellationToken>()
        );
#pragma warning restore CS0618
        
        _mockMetrics.Received(1).RecordBackgroundJobScheduled(
            Arg.Any<string>(),
            Arg.Any<string>()
        );
    }

    #endregion

    #region Test Helper Classes

    private class TestJobPayload
    {
        public string Data { get; set; } = string.Empty;
    }

    #endregion
}

