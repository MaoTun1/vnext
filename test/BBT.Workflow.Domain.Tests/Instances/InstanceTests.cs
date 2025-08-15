using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BBT.Workflow.Definitions;
using BBT.Workflow.Instances.Policies;
using Xunit;

namespace BBT.Workflow.Instances;

public class InstanceTests : DomainTestBase<DomainEntryPoint>
{
    private readonly StateTransitionPolicy _stateTransitionPolicy;

    public InstanceTests()
    {
        _stateTransitionPolicy = GetRequiredService<StateTransitionPolicy>();
    }

    [Fact]
    public void Constructor_ShouldInitializeProperties()
    {
        // Arrange
        var id = Guid.NewGuid();
        var key = "TestKey";
        var flow = "sys-flows";

        // Act
        var instance = Instance.Create(id, flow, key);

        // Assert
        Assert.Equal(id, instance.Id);
        Assert.Equal(key, instance.Key);
        Assert.Equal(InstanceStatus.Active, instance.Status);
        Assert.NotNull(instance.Tags);
        Assert.Empty(instance.Tags);
    }

    [Fact]
    public void ChangeState_ShouldUpdateCurrentState_FromState()
    {
        // Arrange
        var instance = InstanceFactory.CreateDefault();
        var state = StateFactory.CreateDefault("next-state");

        // Act
        instance.ChangeState(state);

        // Assert
        Assert.Equal("next-state", instance.CurrentState);
    }

    [Fact]
    public void ChangeState_ShouldUpdateCurrentState_FromTransition()
    {
        // Arrange
        var instance = InstanceFactory.CreateDefault();
        var transition = TransitionFactory.CreateDefault("submit", "from-step", "to-step");

        // Act
        instance.ChangeState(transition);

        // Assert
        Assert.Equal("to-step", instance.CurrentState);
    }

    [Fact]
    public void CanExecuteTransition_ShouldDelegateToTransition()
    {
        // Arrange
        var instance = InstanceFactory.CreateDefault();
        var transition = TransitionFactory.CreateDefault();
        var state = StateFactory.CreateDefault("from-state");

        // Act
        var result = instance.CanExecuteTransition(transition, state, _stateTransitionPolicy);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void Complete_ShouldSetCompletedAtAndStatus()
    {
        // Arrange
        var instance = InstanceFactory.CreateDefault();
        instance.CreatedAt = DateTime.UtcNow.AddMinutes(-5);

        // Act
        instance.Complete();

        // Assert
        Assert.Equal(InstanceStatus.Completed, instance.Status);
        Assert.NotNull(instance.CompletedAt);
        Assert.Equal(instance.CompletedAt - instance.CreatedAt, instance.Duration);
    }

    [Fact]
    public void AddTags_ShouldReplaceAndAddNewTags()
    {
        // Arrange
        var instance = InstanceFactory.CreateDefault();
        instance.AddTags(new[] { "tag1", "tag2" });

        // Act
        instance.AddTags(new[] { "tag2", "tag3" });

        // Assert
        Assert.Equal(2, instance.Tags.Count);
        Assert.Contains("tag2", instance.Tags);
        Assert.Contains("tag3", instance.Tags);
        Assert.DoesNotContain("tag1", instance.Tags);
    }

    [Fact]
    public void AddData_ShouldAddInitialData_WhenNoPreviousData()
    {
        // Arrange
        var instance = InstanceFactory.CreateDefault();
        var dataId = Guid.NewGuid();
        var input = JsonData.CreateFrom("{}");

        // Act
        var result = instance.AddData(dataId, input);

        // Assert
        Assert.Single(instance.DataList);
        Assert.Equal(dataId, result.Id);
        Assert.Equal(WorkflowConstants.DefaultVersion, result.Version);
        Assert.Equal(input.JsonElement.ToString(), result.Data.JsonElement.ToString());
    }

    [Fact]
    public void AddData_ShouldCreateNewVersion_WhenPreviousDataExists()
    {
        // Arrange
        var instance = InstanceFactory.CreateDefault();
        var data1 = JsonData.CreateFrom("{\"key\":\"value1\"}");
        var data2 = JsonData.CreateFrom("{\"key\":\"value2\"}");

        instance.AddData(Guid.NewGuid(), data1);
        var newData = instance.AddData(Guid.NewGuid(), data2, VersionStrategy.IncreaseMinor);

        // Assert
        Assert.Equal(2, instance.DataList.Count);
        Assert.StartsWith("1.1.", newData.Version); // increase minor
    }

    [Fact]
    public void FindData_ShouldReturnExactMatch_WhenVersionExists()
    {
        // Arrange
        var instance = InstanceFactory.CreateDefault();
        var input = JsonData.CreateFrom("{}");
        var version = "1.0.0";

        instance.AddData(Guid.NewGuid(), input);
        var result = instance.FindData(version);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(version, result.Version);
    }

    [Fact]
    public void FindData_ShouldReturnLatestPartialMatch_WhenPartialVersionGiven()
    {
        // Arrange
        var instance = InstanceFactory.CreateDefault();

        instance.AddData(Guid.NewGuid(), JsonData.CreateFrom("{}")); // 1.0.0
        instance.AddData(Guid.NewGuid(), JsonData.CreateFrom("{}"), VersionStrategy.IncreasePatch); // 1.0.1
        instance.AddData(Guid.NewGuid(), JsonData.CreateFrom("{}"), VersionStrategy.IncreasePatch); // 1.0.2
        instance.AddData(Guid.NewGuid(), JsonData.CreateFrom("{}"), VersionStrategy.IncreaseMinor); // 1.1.0

        // Act
        var result = instance.FindData("1.0");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("1.0.2", result.Version);
    }

    [Fact]
    public void SetKey_ShouldUpdateKey_WhenValid()
    {
        // Arrange
        var instance = InstanceFactory.CreateDefault();
        var newKey = "updated-key";

        // Act
        instance.SetKey(newKey);

        // Assert
        Assert.Equal(newKey, instance.Key);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void SetKey_ShouldThrow_WhenInvalid(string? key)
    {
        // Arrange
        var instance = InstanceFactory.CreateDefault();

        // Act & Assert
        Assert.Throws<ArgumentException>(() => instance.SetKey(key!));
    }

    [Fact]
    public void LatestData_ShouldReturnMostRecentVersion()
    {
        // Arrange
        var instance = InstanceFactory.CreateDefault();

        instance.AddData(Guid.NewGuid(), JsonData.CreateFrom("{}")); // 1.0.0
        instance.AddData(Guid.NewGuid(), JsonData.CreateFrom("{}"), VersionStrategy.IncreasePatch); // 1.0.1
        instance.AddData(Guid.NewGuid(), JsonData.CreateFrom("{}"), VersionStrategy.IncreasePatch); // 1.0.2

        // Act
        var latest = instance.LatestData;

        // Assert
        Assert.NotNull(latest);
        Assert.Equal("1.0.2", latest.Version);
    }

    [Fact]
    public void CanExecuteTransition_ShouldReturnTransitionResult()
    {
        // Arrange
        var instance = InstanceFactory.CreateDefault();
        var state = StateFactory.CreateDefault("from-state");

        var transition = TransitionFactory.CreateDefault();

        transition.CanExecute(state, _stateTransitionPolicy);

        // Act
        var result = instance.CanExecuteTransition(transition, state, _stateTransitionPolicy);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void LatestData_ShouldReturnCorrectResult_DuringConcurrentAccess()
    {
        // Arrange
        var instance = InstanceFactory.CreateDefault();
        const int threadCount = 5;
        var barrier = new Barrier(threadCount);
        var tasks = new List<Task>();
        var latestDataResults = new ConcurrentBag<InstanceData?>();

        // Act - Add data and read LatestData concurrently
        for (int i = 0; i < threadCount; i++)
        {
            int threadIndex = i;
            var task = Task.Run(() =>
            {
                // Add some data first
                var data = JsonData.CreateFrom($"{{\"thread\":{threadIndex}}}");
                instance.AddData(Guid.NewGuid(), data, VersionStrategy.IncreasePatch);
                
                // Synchronize all threads
                barrier.SignalAndWait();
                
                // All threads read LatestData simultaneously
                var latestData = instance.LatestData;
                latestDataResults.Add(latestData);
            });
            tasks.Add(task);
        }

        Task.WaitAll(tasks.ToArray());

        // Assert
        Assert.Equal(threadCount, instance.DataList.Count);
        Assert.Equal(threadCount, latestDataResults.Count);
        
        // All results should be valid (not null)
        Assert.All(latestDataResults, result => Assert.NotNull(result));
        
        // Latest data should be consistently returned
        var actualLatest = instance.LatestData;
        Assert.NotNull(actualLatest);
    }

    [Fact]
    public void FindData_ShouldWorkCorrectly_DuringConcurrentAddOperations()
    {
        // Arrange
        var instance = InstanceFactory.CreateDefault();
        var addTasks = new List<Task>();
        var findTasks = new List<Task<InstanceData?>>();
        
        // Add initial data
        instance.AddData(Guid.NewGuid(), JsonData.CreateFrom("{}"), VersionStrategy.IncreasePatch); // 1.0.1

        // Act - Concurrent add and find operations
        for (int i = 0; i < 5; i++)
        {
            // Add operations
            var addTask = Task.Run(() =>
            {
                for (int j = 0; j < 3; j++)
                {
                    var data = JsonData.CreateFrom($"{{\"operation\":{j}}}");
                    instance.AddData(Guid.NewGuid(), data, VersionStrategy.IncreasePatch);
                }
            });
            addTasks.Add(addTask);

            // Find operations
            var findTask = Task.Run(() => instance.FindData("1.0"));
            findTasks.Add(findTask);
        }

        Task.WaitAll(addTasks.Concat(findTasks).ToArray());

        // Assert
        Assert.Equal(16, instance.DataList.Count); // 1 initial + 5*3 added = 16
        
        // All find operations should return a valid result
        foreach (var findTask in findTasks)
        {
            var result = findTask.Result;
            Assert.NotNull(result);
            Assert.True(result.Version.StartsWith("1.0."));
        }
    }
}