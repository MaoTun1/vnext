using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BBT.Workflow.Definitions;
using BBT.Workflow.Domain;
using BBT.Workflow.Execution.Handlers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;
using Xunit;

namespace BBT.Workflow.Execution.Handlers;

/// <summary>
/// Unit tests for TransitionHandlerFactory
/// Tests transition handler resolution based on trigger type
/// </summary>
public class TransitionHandlerFactoryTests
{
    private readonly Mock<ILogger<TransitionHandlerFactory>> _mockLogger;

    public TransitionHandlerFactoryTests()
    {
        _mockLogger = new Mock<ILogger<TransitionHandlerFactory>>();
    }

    #region Get Tests

    [Fact]
    public void Get_WithUserTrigger_ShouldReturnUserTransitionHandler()
    {
        // Arrange
        var userHandler = new MockTransitionHandler(TriggerType.Manual);
        var serviceProvider = CreateServiceProviderWithHandlers(userHandler);
        var factory = new TransitionHandlerFactory(serviceProvider, _mockLogger.Object);

        // Act
        var result = factory.Get(TriggerType.Manual);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBe(userHandler);
        result.CanHandle(TriggerType.Manual).ShouldBeTrue();
    }

    [Fact]
    public void Get_WithTimerTrigger_ShouldReturnTimerTransitionHandler()
    {
        // Arrange
        var timerHandler = new MockTransitionHandler(TriggerType.Scheduled);
        var serviceProvider = CreateServiceProviderWithHandlers(timerHandler);
        var factory = new TransitionHandlerFactory(serviceProvider, _mockLogger.Object);

        // Act
        var result = factory.Get(TriggerType.Scheduled);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBe(timerHandler);
        result.CanHandle(TriggerType.Scheduled).ShouldBeTrue();
    }

    [Fact]
    public void Get_WithSystemTrigger_ShouldReturnSystemTransitionHandler()
    {
        // Arrange
        var systemHandler = new MockTransitionHandler(TriggerType.Automatic);
        var serviceProvider = CreateServiceProviderWithHandlers(systemHandler);
        var factory = new TransitionHandlerFactory(serviceProvider, _mockLogger.Object);

        // Act
        var result = factory.Get(TriggerType.Automatic);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBe(systemHandler);
        result.CanHandle(TriggerType.Automatic).ShouldBeTrue();
    }

    [Fact]
    public void Get_WithMultipleHandlers_ShouldReturnCorrectHandler()
    {
        // Arrange
        var userHandler = new MockTransitionHandler(TriggerType.Manual);
        var timerHandler = new MockTransitionHandler(TriggerType.Scheduled);
        var systemHandler = new MockTransitionHandler(TriggerType.Automatic);
        
        var serviceProvider = CreateServiceProviderWithHandlers(userHandler, timerHandler, systemHandler);
        var factory = new TransitionHandlerFactory(serviceProvider, _mockLogger.Object);

        // Act
        var userResult = factory.Get(TriggerType.Manual);
        var timerResult = factory.Get(TriggerType.Scheduled);
        var systemResult = factory.Get(TriggerType.Automatic);

        // Assert
        userResult.ShouldBe(userHandler);
        timerResult.ShouldBe(timerHandler);
        systemResult.ShouldBe(systemHandler);
    }

    [Fact]
    public void Get_WhenNoHandlerFound_ShouldThrowNotSupportedException()
    {
        // Arrange
        var serviceProvider = CreateServiceProviderWithHandlers(); // No handlers
        var factory = new TransitionHandlerFactory(serviceProvider, _mockLogger.Object);

        // Act & Assert
        var exception = Should.Throw<NotSupportedException>(() => factory.Get(TriggerType.Manual));
        exception.Message.ShouldContain("No transition handler found");
        exception.Message.ShouldContain(TriggerType.Manual.ToString());
    }

    [Fact]
    public void Get_WhenHandlerDoesNotSupportTriggerType_ShouldThrowNotSupportedException()
    {
        // Arrange
        var userHandler = new MockTransitionHandler(TriggerType.Manual);
        var serviceProvider = CreateServiceProviderWithHandlers(userHandler);
        var factory = new TransitionHandlerFactory(serviceProvider, _mockLogger.Object);

        // Act & Assert
        var exception = Should.Throw<NotSupportedException>(() => factory.Get(TriggerType.Scheduled));
        exception.Message.ShouldContain("No transition handler found");
        exception.Message.ShouldContain(TriggerType.Scheduled.ToString());
    }

    [Fact]
    public void Get_ShouldLogDebugMessages()
    {
        // Arrange
        var userHandler = new MockTransitionHandler(TriggerType.Manual);
        var serviceProvider = CreateServiceProviderWithHandlers(userHandler);
        var factory = new TransitionHandlerFactory(serviceProvider, _mockLogger.Object);

        // Act
        factory.Get(TriggerType.Manual);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Resolving transition handler")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public void Get_WhenNoHandlerFound_ShouldLogError()
    {
        // Arrange
        var serviceProvider = CreateServiceProviderWithHandlers();
        var factory = new TransitionHandlerFactory(serviceProvider, _mockLogger.Object);

        // Act & Assert
        Should.Throw<NotSupportedException>(() => factory.Get(TriggerType.Manual));

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("No transition handler found")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void Get_ShouldReturnFirstMatchingHandler()
    {
        // Arrange - Create two handlers that can handle the same trigger type
        var firstHandler = new MockTransitionHandler(TriggerType.Manual, name: "First");
        var secondHandler = new MockTransitionHandler(TriggerType.Manual, name: "Second");
        
        var serviceProvider = CreateServiceProviderWithHandlers(firstHandler, secondHandler);
        var factory = new TransitionHandlerFactory(serviceProvider, _mockLogger.Object);

        // Act
        var result = factory.Get(TriggerType.Manual);

        // Assert
        result.ShouldBe(firstHandler);
    }

    #endregion

    #region Helper Methods

    private IServiceProvider CreateServiceProviderWithHandlers(params ITransitionHandler[] handlers)
    {
        var services = new ServiceCollection();
        
        foreach (var handler in handlers)
        {
            services.AddSingleton(handler);
        }

        return services.BuildServiceProvider();
    }

    private class MockTransitionHandler : ITransitionHandler
    {
        private readonly TriggerType _supportedTriggerType;
        private readonly string _name;

        public MockTransitionHandler(TriggerType supportedTriggerType, string name = "MockHandler")
        {
            _supportedTriggerType = supportedTriggerType;
            _name = name;
        }

        public bool CanHandle(TriggerType triggerType) => triggerType == _supportedTriggerType;

        public Task PreHandleAsync(TransitionExecutionContext context, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task PostHandleAsync(TransitionExecutionContext context, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public override string ToString() => _name;
    }

    #endregion
}

