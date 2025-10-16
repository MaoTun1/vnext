using System;
using System.Collections.Generic;
using System.Linq;
using BBT.Aether.Guids;
using BBT.Workflow.BackgroundJobs;
using BBT.Workflow.Instances;
using BBT.Workflow.Schemas;
using NSubstitute;
using Shouldly;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace BBT.Workflow.Infrastructure.Tests.BackgroundJobs;

/// <summary>
/// Unit tests for EfCoreJobStore
/// </summary>
public sealed class EfCoreJobStoreTests
{
    private readonly ICurrentSchema _mockCurrentSchema;
    private readonly IInstanceJobRepository _mockJobRepository;
    private readonly IGuidGenerator _mockGuidGenerator;
    private readonly EfCoreJobStore _jobStore;

    public EfCoreJobStoreTests()
    {
        _mockCurrentSchema = Substitute.For<ICurrentSchema>();
        _mockJobRepository = Substitute.For<IInstanceJobRepository>();
        _mockGuidGenerator = Substitute.For<IGuidGenerator>();
        
        _jobStore = new EfCoreJobStore(
            _mockCurrentSchema,
            _mockJobRepository,
            _mockGuidGenerator
        );
    }

    #region SaveAsync Tests

    [Fact]
    public async Task SaveAsync_Should_Create_New_Job_When_Not_Exists()
    {
        // Arrange
        const string jobId = "test-job-123";
        var payload = new TestJobPayload { Data = "Test" };
        var metadata = new Dictionary<string, string>
        {
            { "domain", "TestDomain" },
            { "flowName", "TestFlow" },
            { "instanceId", Guid.NewGuid().ToString() }
        };
        var job = new BackgroundJobInfo<TestJobPayload>
        {
            JobName = "TestJob",
            JobId = jobId,
            ExpressionValue = "0 */5 * * *",
            Payload = payload,
            IsTriggered = false,
            Metadata = metadata
        };

        var newGuid = Guid.NewGuid();
        _mockGuidGenerator.Create().Returns(newGuid);
        _mockJobRepository.FindByNameAsync(jobId, Arg.Any<CancellationToken>())
            .Returns((InstanceJob?)null);

        var disposable = Substitute.For<IDisposable>();
        _mockCurrentSchema.Change(Arg.Any<string>()).Returns(disposable);

        // Act
        await _jobStore.SaveAsync(jobId, job);

        // Assert
        await _mockJobRepository.Received(1).InsertAsync(
            Arg.Is<InstanceJob>(j => 
                j.JobName == job.JobName &&
                j.JobId == jobId &&
                j.Domain == "TestDomain" &&
                j.FlowName == "TestFlow"
            ),
            true,
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task SaveAsync_Should_Update_Existing_Job_When_Exists()
    {
        // Arrange
        const string jobId = "test-job-123";
        var payload = new TestJobPayload { Data = "Test" };
        var metadata = new Dictionary<string, string>
        {
            { "domain", "TestDomain" },
            { "flowName", "TestFlow" },
            { "instanceId", Guid.NewGuid().ToString() }
        };
        var job = new BackgroundJobInfo<TestJobPayload>
        {
            JobName = "TestJob",
            JobId = jobId,
            ExpressionValue = "0 */5 * * *",
            Payload = payload,
            IsTriggered = false,
            Metadata = metadata
        };

        var payloadJson = JsonSerializer.Serialize(payload);
        var payloadElement = JsonSerializer.Deserialize<JsonElement>(payloadJson);
        var existingJob = InstanceJob.Create(
            Guid.NewGuid(),
            "TestJob",
            jobId,
            "TestDomain",
            "TestFlow",
            Guid.NewGuid(),
            "0 */10 * * *",
            payloadElement
        );

        _mockJobRepository.FindByNameAsync(jobId, Arg.Any<CancellationToken>())
            .Returns(existingJob);

        var disposable = Substitute.For<IDisposable>();
        _mockCurrentSchema.Change(Arg.Any<string>()).Returns(disposable);

        // Act
        await _jobStore.SaveAsync(jobId, job);

        // Assert
        await _mockJobRepository.Received(1).UpdateAsync(
            existingJob,
            true,
            Arg.Any<CancellationToken>()
        );
        
        // Verify the expression value was updated
        existingJob.ExpressionValue.ShouldBe(job.ExpressionValue);
    }

    [Fact]
    public async Task SaveAsync_Should_Mark_Job_As_Triggered_When_IsTriggered_Is_True()
    {
        // Arrange
        const string jobId = "test-job-123";
        var payload = new TestJobPayload { Data = "Test" };
        var metadata = new Dictionary<string, string>
        {
            { "domain", "TestDomain" },
            { "flowName", "TestFlow" }
        };
        var job = new BackgroundJobInfo<TestJobPayload>
        {
            JobName = "TestJob",
            JobId = jobId,
            ExpressionValue = "0 */5 * * *",
            Payload = payload,
            IsTriggered = true,
            Metadata = metadata
        };

        var payloadJson = JsonSerializer.Serialize(payload);
        var payloadElement = JsonSerializer.Deserialize<JsonElement>(payloadJson);
        var existingJob = InstanceJob.Create(
            Guid.NewGuid(),
            "TestJob",
            jobId,
            "TestDomain",
            "TestFlow",
            Guid.NewGuid(),
            "0 */10 * * *",
            payloadElement
        );

        _mockJobRepository.FindByNameAsync(jobId, Arg.Any<CancellationToken>())
            .Returns(existingJob);

        var disposable = Substitute.For<IDisposable>();
        _mockCurrentSchema.Change(Arg.Any<string>()).Returns(disposable);

        // Act
        await _jobStore.SaveAsync(jobId, job);

        // Assert
        existingJob.IsTriggered.ShouldBeTrue();
    }

    [Fact]
    public async Task SaveAsync_Should_Use_Schema_Context_From_FlowName()
    {
        // Arrange
        const string jobId = "test-job-123";
        const string flowName = "TestFlow";
        var payload = new TestJobPayload { Data = "Test" };
        var metadata = new Dictionary<string, string>
        {
            { "domain", "TestDomain" },
            { "flowName", flowName },
            { "instanceId", Guid.NewGuid().ToString() }
        };
        var job = new BackgroundJobInfo<TestJobPayload>
        {
            JobName = "TestJob",
            JobId = jobId,
            ExpressionValue = "0 */5 * * *",
            Payload = payload,
            Metadata = metadata
        };

        _mockJobRepository.FindByNameAsync(jobId, Arg.Any<CancellationToken>())
            .Returns((InstanceJob?)null);

        var disposable = Substitute.For<IDisposable>();
        _mockCurrentSchema.Change(flowName).Returns(disposable);
        _mockGuidGenerator.Create().Returns(Guid.NewGuid());

        // Act
        await _jobStore.SaveAsync(jobId, job);

        // Assert
        _mockCurrentSchema.Received(1).Change(flowName);
        disposable.Received(1).Dispose();
    }

    [Fact]
    public async Task SaveAsync_Should_Return_Early_When_FlowName_Is_Empty()
    {
        // Arrange
        const string jobId = "test-job-123";
        var payload = new TestJobPayload { Data = "Test" };
        var metadata = new Dictionary<string, string>
        {
            { "domain", "TestDomain" }
        };
        var job = new BackgroundJobInfo<TestJobPayload>
        {
            JobName = "TestJob",
            JobId = jobId,
            ExpressionValue = "0 */5 * * *",
            Payload = payload,
            Metadata = metadata
        };

        // Act
        await _jobStore.SaveAsync(jobId, job);

        // Assert
        await _mockJobRepository.DidNotReceive().FindByNameAsync(
            Arg.Any<string>(),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task SaveAsync_Should_Use_CancellationToken()
    {
        // Arrange
        const string jobId = "test-job-123";
        var payload = new TestJobPayload { Data = "Test" };
        var metadata = new Dictionary<string, string>
        {
            { "domain", "TestDomain" },
            { "flowName", "TestFlow" },
            { "instanceId", Guid.NewGuid().ToString() }
        };
        var job = new BackgroundJobInfo<TestJobPayload>
        {
            JobName = "TestJob",
            JobId = jobId,
            ExpressionValue = "0 */5 * * *",
            Payload = payload,
            Metadata = metadata
        };

        var cts = new CancellationTokenSource();
        var cancellationToken = cts.Token;
        
        _mockJobRepository.FindByNameAsync(jobId, cancellationToken)
            .Returns((InstanceJob?)null);

        var disposable = Substitute.For<IDisposable>();
        _mockCurrentSchema.Change(Arg.Any<string>()).Returns(disposable);
        _mockGuidGenerator.Create().Returns(Guid.NewGuid());

        // Act
        await _jobStore.SaveAsync(jobId, job, cancellationToken);

        // Assert
        await _mockJobRepository.Received(1).FindByNameAsync(jobId, cancellationToken);
    }

    #endregion

    #region GetAsync Tests

    [Fact]
    public async Task GetAsync_Should_Return_Null_When_Job_Not_Found()
    {
        // Arrange
        const string jobId = "non-existent-job";
        _mockJobRepository.FindByNameAsync(jobId, Arg.Any<CancellationToken>())
            .Returns((InstanceJob?)null);

        // Act
        var result = await _jobStore.GetAsync<TestJobPayload>(jobId);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetAsync_Should_Return_Job_When_Found()
    {
        // Arrange
        const string jobId = "test-job-123";
        var instanceId = Guid.NewGuid();
        var payload = new TestJobPayload { Data = "Test" };
        var payloadJson = JsonSerializer.Serialize(payload);
        var payloadElement = JsonSerializer.Deserialize<JsonElement>(payloadJson);

        var instanceJob = InstanceJob.Create(
            Guid.NewGuid(),
            "TestJob",
            jobId,
            "TestDomain",
            "TestFlow",
            instanceId,
            "0 */5 * * *",
            payloadElement
        );

        _mockJobRepository.FindByNameAsync(jobId, Arg.Any<CancellationToken>())
            .Returns(instanceJob);

        // Act
        var result = await _jobStore.GetAsync<TestJobPayload>(jobId);

        // Assert
        result.ShouldNotBeNull();
        result.JobId.ShouldBe(jobId);
        result.JobName.ShouldBe("TestJob");
        result.Payload.ShouldNotBeNull();
        result.Payload.Data.ShouldBe("Test");
        result.Metadata.ShouldNotBeNull();
        result.Metadata["domain"].ShouldBe("TestDomain");
        result.Metadata["flowName"].ShouldBe("TestFlow");
        result.Metadata["instanceId"].ShouldBe(instanceId.ToString());
    }

    [Fact]
    public async Task GetAsync_Should_Use_CancellationToken()
    {
        // Arrange
        const string jobId = "test-job-123";
        var cts = new CancellationTokenSource();
        var cancellationToken = cts.Token;

        _mockJobRepository.FindByNameAsync(jobId, cancellationToken)
            .Returns((InstanceJob?)null);

        // Act
        await _jobStore.GetAsync<TestJobPayload>(jobId, cancellationToken);

        // Assert
        await _mockJobRepository.Received(1).FindByNameAsync(jobId, cancellationToken);
    }

    #endregion

    #region GetListByActiveAsync Tests

    [Fact]
    public async Task GetListByActiveAsync_Should_Return_All_Untriggered_Jobs()
    {
        // Arrange
        var instanceId1 = Guid.NewGuid();
        var instanceId2 = Guid.NewGuid();
        var payload1 = new TestJobPayload { Data = "Test1" };
        var payload2 = new TestJobPayload { Data = "Test2" };
        var payloadJson1 = JsonSerializer.Serialize(payload1);
        var payloadJson2 = JsonSerializer.Serialize(payload2);
        var payloadElement1 = JsonSerializer.Deserialize<JsonElement>(payloadJson1);
        var payloadElement2 = JsonSerializer.Deserialize<JsonElement>(payloadJson2);

        var instanceJob1 = InstanceJob.Create(
            Guid.NewGuid(),
            "TestJob1",
            "job-1",
            "TestDomain",
            "TestFlow",
            instanceId1,
            "0 */5 * * *",
            payloadElement1
        );

        var instanceJob2 = InstanceJob.Create(
            Guid.NewGuid(),
            "TestJob2",
            "job-2",
            "TestDomain",
            "TestFlow",
            instanceId2,
            "0 */10 * * *",
            payloadElement2
        );

        _mockJobRepository.GetListUntriggeredAsync(Arg.Any<CancellationToken>())
            .Returns(new List<InstanceJob> { instanceJob1, instanceJob2 });

        // Act
        var result = await _jobStore.GetListByActiveAsync<TestJobPayload>();

        // Assert
        var resultList = result.ToList();
        resultList.Count.ShouldBe(2);
        resultList[0].JobId.ShouldBe("job-1");
        resultList[0].JobName.ShouldBe("TestJob1");
        resultList[1].JobId.ShouldBe("job-2");
        resultList[1].JobName.ShouldBe("TestJob2");
    }

    [Fact]
    public async Task GetListByActiveAsync_Should_Return_Empty_When_No_Active_Jobs()
    {
        // Arrange
        _mockJobRepository.GetListUntriggeredAsync(Arg.Any<CancellationToken>())
            .Returns(new List<InstanceJob>());

        // Act
        var result = await _jobStore.GetListByActiveAsync<TestJobPayload>();

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetListByActiveAsync_Should_Use_CancellationToken()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        var cancellationToken = cts.Token;

        _mockJobRepository.GetListUntriggeredAsync(cancellationToken)
            .Returns(new List<InstanceJob>());

        // Act
        await _jobStore.GetListByActiveAsync<TestJobPayload>(cancellationToken);

        // Assert
        await _mockJobRepository.Received(1).GetListUntriggeredAsync(cancellationToken);
    }

    #endregion

    #region Test Helper Classes

    private class TestJobPayload
    {
        public string Data { get; set; } = string.Empty;
    }

    #endregion
}

