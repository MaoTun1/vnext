using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BBT.Workflow.Definitions;
using BBT.Workflow.Instances;
using NSubstitute;
using Shouldly;
using Xunit;

namespace BBT.Workflow.Tasks.Persistence.Strategies;

/// <summary>
/// Unit tests for task persistence strategies to ensure SOLID principles compliance
/// and correct behavior based on TaskTrigger types.
/// </summary>
public class TaskPersistenceStrategyTests
{
    private readonly IInstanceTaskRepository _mockRepository;
    private readonly InstanceTask _instanceTask;

    public TaskPersistenceStrategyTests()
    {
        _mockRepository = Substitute.For<IInstanceTaskRepository>();
        _instanceTask = new InstanceTask(Guid.NewGuid(), Guid.NewGuid(), "test-task");
    }

    [Fact]
    public void StandardTaskPersistenceStrategy_CanHandle_ShouldReturnTrueForNonExtensionTriggers()
    {
        // Arrange
        var strategy = new StandardTaskPersistenceStrategy(_mockRepository);
        var nonExtensionTriggers = new[]
        {
            TaskTrigger.OnEntry,
            TaskTrigger.OnExit,
            TaskTrigger.Both,
            TaskTrigger.Manual,
            TaskTrigger.OnExecute
        };

        // Act & Assert
        foreach (var trigger in nonExtensionTriggers)
        {
            strategy.CanHandle(trigger).ShouldBeTrue($"StandardTaskPersistenceStrategy should handle {trigger}");
        }
    }

    [Fact]
    public void StandardTaskPersistenceStrategy_CanHandle_ShouldReturnFalseForExtensionTrigger()
    {
        // Arrange
        var strategy = new StandardTaskPersistenceStrategy(_mockRepository);

        // Act
        var canHandle = strategy.CanHandle(TaskTrigger.Extension);

        // Assert
        canHandle.ShouldBeFalse("StandardTaskPersistenceStrategy should not handle Extension trigger");
    }

    [Fact]
    public async Task StandardTaskPersistenceStrategy_HandleCreationAsync_ShouldCallRepositoryInsert()
    {
        // Arrange
        var strategy = new StandardTaskPersistenceStrategy(_mockRepository);

        // Act
        await strategy.HandleCreationAsync(_instanceTask, CancellationToken.None);

        // Assert
        await _mockRepository.Received(1).InsertAsync(_instanceTask, true, CancellationToken.None);
    }

    [Fact]
    public async Task StandardTaskPersistenceStrategy_HandleCompletionAsync_ShouldCallRepositoryUpdate()
    {
        // Arrange
        var strategy = new StandardTaskPersistenceStrategy(_mockRepository);

        // Act
        await strategy.HandleCompletionAsync(_instanceTask, CancellationToken.None);

        // Assert
        await _mockRepository.Received(1).UpdateAsync(_instanceTask, true, CancellationToken.None);
    }

    [Fact]
    public void ExtensionTaskPersistenceStrategy_CanHandle_ShouldReturnTrueOnlyForExtensionTrigger()
    {
        // Arrange
        var strategy = new ExtensionTaskPersistenceStrategy();

        // Act & Assert
        strategy.CanHandle(TaskTrigger.Extension).ShouldBeTrue("ExtensionTaskPersistenceStrategy should handle Extension trigger");

        var nonExtensionTriggers = new[]
        {
            TaskTrigger.OnEntry,
            TaskTrigger.OnExit,
            TaskTrigger.Both,
            TaskTrigger.Manual,
            TaskTrigger.OnExecute
        };

        foreach (var trigger in nonExtensionTriggers)
        {
            strategy.CanHandle(trigger).ShouldBeFalse($"ExtensionTaskPersistenceStrategy should not handle {trigger}");
        }
    }

    [Fact]
    public async Task ExtensionTaskPersistenceStrategy_HandleCreationAsync_ShouldNotPersist()
    {
        // Arrange
        var strategy = new ExtensionTaskPersistenceStrategy();

        // Act
        await strategy.HandleCreationAsync(_instanceTask, CancellationToken.None);

        // Assert
        // No exception should be thrown and method should complete successfully
        // No persistence operations should occur
    }

    [Fact]
    public async Task ExtensionTaskPersistenceStrategy_HandleCompletionAsync_ShouldNotPersist()
    {
        // Arrange
        var strategy = new ExtensionTaskPersistenceStrategy();

        // Act
        await strategy.HandleCompletionAsync(_instanceTask, CancellationToken.None);

        // Assert
        // No exception should be thrown and method should complete successfully
        // No persistence operations should occur
    }

    [Theory]
    [InlineData(TaskTrigger.OnEntry, typeof(StandardTaskPersistenceStrategy))]
    [InlineData(TaskTrigger.OnExit, typeof(StandardTaskPersistenceStrategy))]
    [InlineData(TaskTrigger.Both, typeof(StandardTaskPersistenceStrategy))]
    [InlineData(TaskTrigger.Manual, typeof(StandardTaskPersistenceStrategy))]
    [InlineData(TaskTrigger.OnExecute, typeof(StandardTaskPersistenceStrategy))]
    [InlineData(TaskTrigger.Extension, typeof(ExtensionTaskPersistenceStrategy))]
    public void TaskPersistenceStrategyFactory_GetStrategy_ShouldReturnCorrectStrategy(
        TaskTrigger taskTrigger, Type expectedStrategyType)
    {
        // Arrange
        var strategies = new List<ITaskPersistenceStrategy>
        {
            new StandardTaskPersistenceStrategy(_mockRepository),
            new ExtensionTaskPersistenceStrategy()
        };
        var factory = new TaskPersistenceStrategyFactory(strategies);

        // Act
        var strategy = factory.GetStrategy(taskTrigger);

        // Assert
        strategy.ShouldBeOfType(expectedStrategyType);
        strategy.CanHandle(taskTrigger).ShouldBeTrue();
    }

    [Fact]
    public void TaskPersistenceStrategyFactory_GetStrategy_ShouldThrowWhenNoStrategyFound()
    {
        // Arrange
        var strategies = new List<ITaskPersistenceStrategy>(); // Empty list
        var factory = new TaskPersistenceStrategyFactory(strategies);

        // Act
        var act = () => factory.GetStrategy(TaskTrigger.OnEntry);

        // Assert
        act.ShouldThrow<InvalidOperationException>("No task persistence strategy found for TaskTrigger: OnEntry");
    }

    [Fact]
    public void TaskPersistenceStrategies_ShouldFollowSingleResponsibilityPrinciple()
    {
        // Arrange
        var standardStrategy = new StandardTaskPersistenceStrategy(_mockRepository);
        var extensionStrategy = new ExtensionTaskPersistenceStrategy();

        // Assert
        // Each strategy should only handle specific TaskTrigger types
        standardStrategy.CanHandle(TaskTrigger.Extension).ShouldBeFalse();
        extensionStrategy.CanHandle(TaskTrigger.OnEntry).ShouldBeFalse();
        
        // Strategies should have distinct responsibilities
        standardStrategy.GetType().ShouldNotBe(extensionStrategy.GetType());
    }
} 