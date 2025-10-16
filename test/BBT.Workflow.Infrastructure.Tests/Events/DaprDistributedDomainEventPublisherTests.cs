using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BBT.Aether.Domain.Events.Distributed;
using BBT.Workflow.Events.Distributed;
using Dapr.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;

namespace BBT.Workflow.Infrastructure.Tests.Events;

/// <summary>
/// Unit tests for DaprDistributedDomainEventPublisher
/// </summary>
public sealed class DaprDistributedDomainEventPublisherTests
{
    private readonly DaprClient _mockDaprClient;
    private readonly ILogger<DaprDistributedDomainEventPublisher> _mockLogger;
    private readonly DaprEventPublisherOptions _options;
    private readonly DaprDistributedDomainEventPublisher _publisher;

    public DaprDistributedDomainEventPublisherTests()
    {
        _mockDaprClient = Substitute.For<DaprClient>();
        _mockLogger = Substitute.For<ILogger<DaprDistributedDomainEventPublisher>>();
        _options = new DaprEventPublisherOptions
        {
            PubSubName = "test-pubsub",
            DefaultTopicName = "default-topic",
            PublishInParallel = false,
            ContinueOnError = false,
            TopicNamingStrategy = TopicNamingStrategy.EventType
        };
        
        var mockOptions = Substitute.For<IOptions<DaprEventPublisherOptions>>();
        mockOptions.Value.Returns(_options);
        
        _publisher = new DaprDistributedDomainEventPublisher(
            _mockDaprClient,
            mockOptions,
            _mockLogger
        );
    }

    #region PublishAsync Tests

    [Fact]
    public async Task PublishAsync_Should_Not_Publish_When_No_Events_Provided()
    {
        // Arrange
        var events = Array.Empty<IDistributedDomainEvent>();

        // Act
        await _publisher.PublishAsync(events);

        // Assert
        await _mockDaprClient.DidNotReceive().PublishEventAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<object>(),
            Arg.Any<Dictionary<string, string>>(),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task PublishAsync_Should_Publish_Single_Event()
    {
        // Arrange
        var testEvent = CreateTestEvent();
        var events = new[] { testEvent };

        // Act
        await _publisher.PublishAsync(events);

        // Assert
        await _mockDaprClient.Received(1).PublishEventAsync(
            _options.PubSubName,
            testEvent.EventType,
            testEvent,
            Arg.Any<Dictionary<string, string>>(),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task PublishAsync_Should_Publish_Multiple_Events()
    {
        // Arrange
        var event1 = CreateTestEvent("Event1");
        var event2 = CreateTestEvent("Event2");
        var events = new[] { event1, event2 };

        // Act
        await _publisher.PublishAsync(events);

        // Assert
        await _mockDaprClient.Received(1).PublishEventAsync(
            _options.PubSubName,
            event1.EventType,
            event1,
            Arg.Any<Dictionary<string, string>>(),
            Arg.Any<CancellationToken>()
        );
        
        await _mockDaprClient.Received(1).PublishEventAsync(
            _options.PubSubName,
            event2.EventType,
            event2,
            Arg.Any<Dictionary<string, string>>(),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task PublishAsync_Should_Include_Metadata_In_Published_Event()
    {
        // Arrange
        var testEvent = CreateTestEvent();
        var events = new[] { testEvent };

        // Act
        await _publisher.PublishAsync(events);

        // Assert
        await _mockDaprClient.Received(1).PublishEventAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<object>(),
            Arg.Is<Dictionary<string, string>>(m => 
                m.ContainsKey("event-id") &&
                m.ContainsKey("event-type") &&
                m.ContainsKey("aggregate-id") &&
                m.ContainsKey("aggregate-type") &&
                m.ContainsKey("aggregate-version") &&
                m.ContainsKey("occurred-on") &&
                m.ContainsKey("content-type")
            ),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task PublishAsync_Should_Use_CancellationToken()
    {
        // Arrange
        var testEvent = CreateTestEvent();
        var events = new[] { testEvent };
        var cts = new CancellationTokenSource();
        var cancellationToken = cts.Token;

        // Act
        await _publisher.PublishAsync(events, cancellationToken);

        // Assert
        await _mockDaprClient.Received(1).PublishEventAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<object>(),
            Arg.Any<Dictionary<string, string>>(),
            cancellationToken
        );
    }

    #endregion

    #region Topic Naming Strategy Tests

    [Fact]
    public async Task PublishAsync_Should_Use_EventType_For_Topic_Name_When_Strategy_Is_EventType()
    {
        // Arrange
        _options.TopicNamingStrategy = TopicNamingStrategy.EventType;
        var testEvent = CreateTestEvent();
        var events = new[] { testEvent };

        // Act
        await _publisher.PublishAsync(events);

        // Assert
        await _mockDaprClient.Received(1).PublishEventAsync(
            _options.PubSubName,
            testEvent.EventType,
            Arg.Any<object>(),
            Arg.Any<Dictionary<string, string>>(),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task PublishAsync_Should_Use_AggregateType_For_Topic_Name_When_Strategy_Is_AggregateType()
    {
        // Arrange
        _options.TopicNamingStrategy = TopicNamingStrategy.AggregateType;
        var testEvent = CreateTestEvent();
        var events = new[] { testEvent };

        // Act
        await _publisher.PublishAsync(events);

        // Assert
        await _mockDaprClient.Received(1).PublishEventAsync(
            _options.PubSubName,
            testEvent.AggregateType,
            Arg.Any<object>(),
            Arg.Any<Dictionary<string, string>>(),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task PublishAsync_Should_Use_Combined_Name_When_Strategy_Is_AggregateTypeAndEventType()
    {
        // Arrange
        _options.TopicNamingStrategy = TopicNamingStrategy.AggregateTypeAndEventType;
        var testEvent = CreateTestEvent();
        var events = new[] { testEvent };

        // Act
        await _publisher.PublishAsync(events);

        // Assert
        await _mockDaprClient.Received(1).PublishEventAsync(
            _options.PubSubName,
            $"{testEvent.AggregateType}.{testEvent.EventType}",
            Arg.Any<object>(),
            Arg.Any<Dictionary<string, string>>(),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task PublishAsync_Should_Use_Custom_Resolver_When_Strategy_Is_Custom()
    {
        // Arrange
        const string customTopicName = "custom-topic";
        _options.TopicNamingStrategy = TopicNamingStrategy.Custom;
        _options.CustomTopicNameResolver = _ => customTopicName;
        var testEvent = CreateTestEvent();
        var events = new[] { testEvent };

        // Act
        await _publisher.PublishAsync(events);

        // Assert
        await _mockDaprClient.Received(1).PublishEventAsync(
            _options.PubSubName,
            customTopicName,
            Arg.Any<object>(),
            Arg.Any<Dictionary<string, string>>(),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task PublishAsync_Should_Use_Topic_Mapping_When_Configured()
    {
        // Arrange
        const string mappedTopic = "mapped-topic";
        var testEvent = CreateTestEvent();
        _options.TopicMappings = new Dictionary<string, string>
        {
            { testEvent.EventType, mappedTopic }
        };
        var events = new[] { testEvent };

        // Act
        await _publisher.PublishAsync(events);

        // Assert
        await _mockDaprClient.Received(1).PublishEventAsync(
            _options.PubSubName,
            mappedTopic,
            Arg.Any<object>(),
            Arg.Any<Dictionary<string, string>>(),
            Arg.Any<CancellationToken>()
        );
    }

    #endregion

    #region Parallel Publishing Tests

    [Fact]
    public async Task PublishAsync_Should_Publish_Sequentially_When_PublishInParallel_Is_False()
    {
        // Arrange
        _options.PublishInParallel = false;
        var events = new[]
        {
            CreateTestEvent("Event1"),
            CreateTestEvent("Event2"),
            CreateTestEvent("Event3")
        };

        // Act
        await _publisher.PublishAsync(events);

        // Assert - Sequential calls should complete successfully
        await _mockDaprClient.Received(3).PublishEventAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<object>(),
            Arg.Any<Dictionary<string, string>>(),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task PublishAsync_Should_Publish_In_Parallel_When_PublishInParallel_Is_True()
    {
        // Arrange
        _options.PublishInParallel = true;
        var events = new[]
        {
            CreateTestEvent("Event1"),
            CreateTestEvent("Event2"),
            CreateTestEvent("Event3")
        };

        // Act
        await _publisher.PublishAsync(events);

        // Assert - All events should be published
        await _mockDaprClient.Received(3).PublishEventAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<object>(),
            Arg.Any<Dictionary<string, string>>(),
            Arg.Any<CancellationToken>()
        );
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task PublishAsync_Should_Throw_On_First_Error_When_ContinueOnError_Is_False()
    {
        // Arrange
        _options.ContinueOnError = false;
        var events = new[]
        {
            CreateTestEvent("Event1"),
            CreateTestEvent("Event2")
        };

        _mockDaprClient.PublishEventAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<object>(),
            Arg.Any<Dictionary<string, string>>(),
            Arg.Any<CancellationToken>()
        ).Returns(
            Task.CompletedTask,
            Task.FromException(new Exception("Publish failed"))
        );

        // Act & Assert
        await Should.ThrowAsync<DomainEventPublishException>(
            async () => await _publisher.PublishAsync(events)
        );
    }

    [Fact]
    public async Task PublishAsync_Should_Continue_On_Error_When_ContinueOnError_Is_True()
    {
        // Arrange
        _options.ContinueOnError = true;
        _options.PublishInParallel = false;
        var events = new[]
        {
            CreateTestEvent("Event1"),
            CreateTestEvent("Event2"),
            CreateTestEvent("Event3")
        };

        var callCount = 0;
        _mockDaprClient.PublishEventAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<object>(),
            Arg.Any<Dictionary<string, string>>(),
            Arg.Any<CancellationToken>()
        ).Returns(_ =>
        {
            callCount++;
            return callCount == 2 
                ? Task.FromException(new Exception("Publish failed")) 
                : Task.CompletedTask;
        });

        // Act & Assert - Should not throw
        Should.NotThrow(async () => await _publisher.PublishAsync(events));
    }

    [Fact]
    public async Task PublishAsync_Should_Throw_AggregateException_When_ContinueOnError_And_ThrowAggregateException()
    {
        // Arrange
        _options.ContinueOnError = true;
        _options.ThrowAggregateException = true;
        _options.PublishInParallel = false;
        var events = new[]
        {
            CreateTestEvent("Event1"),
            CreateTestEvent("Event2")
        };

        var callCount = 0;
        _mockDaprClient.PublishEventAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<object>(),
            Arg.Any<Dictionary<string, string>>(),
            Arg.Any<CancellationToken>()
        ).Returns(_ =>
        {
            callCount++;
            return callCount == 1 
                ? Task.FromException(new Exception("Publish failed")) 
                : Task.CompletedTask;
        });

        // Act & Assert
        await Should.ThrowAsync<AggregateException>(
            async () => await _publisher.PublishAsync(events)
        );
    }

    #endregion

    #region Custom Metadata Tests

    [Fact]
    public async Task PublishAsync_Should_Include_Custom_Metadata_When_Builder_Is_Configured()
    {
        // Arrange
        _options.CustomMetadataBuilder = _ => new Dictionary<string, string>
        {
            { "custom-key", "custom-value" }
        };
        var testEvent = CreateTestEvent();
        var events = new[] { testEvent };

        // Act
        await _publisher.PublishAsync(events);

        // Assert
        await _mockDaprClient.Received(1).PublishEventAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<object>(),
            Arg.Is<Dictionary<string, string>>(m => 
                m.ContainsKey("custom-key") && m["custom-key"] == "custom-value"
            ),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task PublishAsync_Should_Handle_Null_Custom_Metadata()
    {
        // Arrange
        _options.CustomMetadataBuilder = _ => null;
        var testEvent = CreateTestEvent();
        var events = new[] { testEvent };

        // Act & Assert
        Should.NotThrow(async () => await _publisher.PublishAsync(events));
    }

    #endregion

    #region Metadata Content Tests

    [Fact]
    public async Task PublishAsync_Should_Include_All_Standard_Metadata_Fields()
    {
        // Arrange
        var testEvent = CreateTestEvent();
        var events = new[] { testEvent };

        // Act
        await _publisher.PublishAsync(events);

        // Assert
        await _mockDaprClient.Received(1).PublishEventAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<object>(),
            Arg.Is<Dictionary<string, string>>(m => 
                m["event-id"] == testEvent.EventId &&
                m["event-type"] == testEvent.EventType &&
                m["aggregate-id"] == testEvent.AggregateId &&
                m["aggregate-type"] == testEvent.AggregateType &&
                m["aggregate-version"] == testEvent.AggregateVersion.ToString() &&
                m.ContainsKey("occurred-on") &&
                m["content-type"] == "application/json"
            ),
            Arg.Any<CancellationToken>()
        );
    }

    #endregion

    #region Test Helper Methods

    private static TestDistributedDomainEvent CreateTestEvent(string? eventTypeSuffix = null)
    {
        var eventType = eventTypeSuffix != null ? $"TestEvent{eventTypeSuffix}" : "TestEvent";
        return new TestDistributedDomainEvent
        {
            EventId = Guid.NewGuid().ToString(),
            EventType = eventType,
            AggregateId = Guid.NewGuid().ToString(),
            AggregateType = "TestAggregate",
            AggregateVersion = 1,
            OccurredOn = DateTime.UtcNow
        };
    }

    private class TestDistributedDomainEvent : IDistributedDomainEvent
    {
        public string EventId { get; set; } = string.Empty;
        public string EventType { get; set; } = string.Empty;
        public string AggregateId { get; set; } = string.Empty;
        public string AggregateType { get; set; } = string.Empty;
        public long AggregateVersion { get; set; }
        public DateTime OccurredOn { get; set; }
    }

    #endregion
}

