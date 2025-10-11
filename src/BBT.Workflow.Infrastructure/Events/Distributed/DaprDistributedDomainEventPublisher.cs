using BBT.Aether.Domain.Events.Distributed;
using Dapr.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BBT.Workflow.Events.Distributed;

public sealed class DaprDistributedDomainEventPublisher(
    DaprClient daprClient,
    IOptions<DaprEventPublisherOptions> options,
    ILogger<DaprDistributedDomainEventPublisher> logger) : IDistributedDomainEventPublisher
{
    private readonly DaprEventPublisherOptions _options = options.Value;
    
    public async Task PublishAsync(IEnumerable<IDistributedDomainEvent> events, CancellationToken cancellationToken = default)
    {
        var distributedDomainEvents = events as IDistributedDomainEvent[] ?? events.ToArray();
        if (distributedDomainEvents.Any() != true)
        {
            logger.LogDebug("No distributed events to publish");
            return;
        }
        
        logger.LogInformation("Publishing {Count} distributed domain events to Dapr pub/sub '{PubSubName}'", 
            distributedDomainEvents.Length, _options.PubSubName);

        var publishTasks = new List<Task>();
        var failedEvents = new List<(IDistributedDomainEvent Event, Exception Exception)>();

        foreach (var @event in distributedDomainEvents)
        {
            try
            {
                var topicName = GetTopicName(@event);
                var metadata = BuildMetadata(@event);

                logger.LogDebug(
                    "Publishing event {EventType} (ID: {EventId}) to topic '{TopicName}' for aggregate {AggregateType}/{AggregateId}",
                    @event.EventType, @event.EventId, topicName, @event.AggregateType, @event.AggregateId);

                var publishTask = daprClient.PublishEventAsync(
                    _options.PubSubName,
                    topicName,
                    @event,
                    metadata,
                    cancellationToken);

                if (_options.PublishInParallel)
                {
                    publishTasks.Add(publishTask);
                }
                else
                {
                    await publishTask;
                    logger.LogDebug("Successfully published event {EventType} (ID: {EventId})", 
                        @event.EventType, @event.EventId);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, 
                    "Failed to publish event {EventType} (ID: {EventId}) for aggregate {AggregateType}/{AggregateId}",
                    @event.EventType, @event.EventId, @event.AggregateType, @event.AggregateId);

                failedEvents.Add((@event, ex));

                if (!_options.ContinueOnError)
                {
                    throw new DomainEventPublishException(
                        $"Failed to publish distributed event {@event.AggregateType} (ID: {@event.EventId})", ex);
                }
            }
        }

        // Wait for all parallel publish operations to complete
        if (_options.PublishInParallel && publishTasks.Count > 0)
        {
            try
            {
                await Task.WhenAll(publishTasks);
                logger.LogInformation("Successfully published all {Count} distributed events in parallel", 
                    distributedDomainEvents.Length);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "One or more events failed to publish in parallel mode");
                
                if (!_options.ContinueOnError)
                {
                    throw new DomainEventPublishException(
                        "One or more distributed events failed to publish", ex);
                }
            }
        }

        // Report on any failures if ContinueOnError is enabled
        if (failedEvents.Count > 0)
        {
            logger.LogWarning(
                "Published {SuccessCount} out of {TotalCount} events. {FailedCount} events failed",
                distributedDomainEvents.Length - failedEvents.Count, distributedDomainEvents.Length, failedEvents.Count);

            if (_options.ThrowAggregateException && failedEvents.Count > 0)
            {
                throw new AggregateException(
                    $"Failed to publish {failedEvents.Count} out of {distributedDomainEvents.Length} distributed events",
                    failedEvents.Select(f => f.Exception));
            }
        }
        else
        {
            logger.LogInformation("Successfully published all {Count} distributed events", distributedDomainEvents.Length);
        }
    }
    
    /// <summary>
    /// Builds metadata dictionary for the event publication.
    /// </summary>
    private Dictionary<string, string> BuildMetadata(IDistributedDomainEvent @event)
    {
        var metadata = new Dictionary<string, string>
        {
            ["event-id"] = @event.EventId,
            ["event-type"] = @event.EventType,
            ["aggregate-id"] = @event.AggregateId,
            ["aggregate-type"] = @event.AggregateType,
            ["aggregate-version"] = @event.AggregateVersion.ToString(),
            ["occurred-on"] = @event.OccurredOn.ToString("O"), // ISO 8601 format
            ["content-type"] = "application/json"
        };
        
        // Add custom metadata if configured
        if (_options.CustomMetadataBuilder != null)
        {
            var customMetadata = _options.CustomMetadataBuilder(@event);
            if (customMetadata != null)
            {
                foreach (var kvp in customMetadata)
                {
                    metadata[kvp.Key] = kvp.Value;
                }
            }
        }

        return metadata;
    }
    
    /// <summary>
    /// Determines the topic name for the given event based on configuration.
    /// </summary>
    private string GetTopicName(IDistributedDomainEvent @event)
    {
        // Check if there's a custom topic mapping
        if (_options.TopicMappings?.TryGetValue(@event.EventType, out var customTopic) == true)
        {
            return customTopic;
        }

        // Use topic naming strategy
        return _options.TopicNamingStrategy switch
        {
            TopicNamingStrategy.EventType => @event.EventType,
            TopicNamingStrategy.AggregateType => @event.AggregateType,
            TopicNamingStrategy.Custom when _options.CustomTopicNameResolver != null 
                => _options.CustomTopicNameResolver(@event),
            TopicNamingStrategy.AggregateTypeAndEventType 
                => $"{@event.AggregateType}.{@event.EventType}",
            _ => _options.DefaultTopicName ?? @event.EventType
        };
    }
}

/// <summary>
/// Configuration options for Dapr distributed event publisher.
/// </summary>
public sealed class DaprEventPublisherOptions
{
    public const string SectionName = "DaprEventPublisher";
    /// <summary>
    /// Gets or sets the name of the Dapr pub/sub component.
    /// </summary>
    public string PubSubName { get; set; } = "vnext-pubsub";
    
    /// <summary>
    /// Gets or sets the default topic name to use when no specific mapping exists.
    /// </summary>
    public string? DefaultTopicName { get; set; }

    /// <summary>
    /// Gets or sets whether to publish events in parallel.
    /// Default is false for ordered publication.
    /// </summary>
    public bool PublishInParallel { get; set; } = false;

    /// <summary>
    /// Gets or sets whether to continue publishing remaining events when one fails.
    /// Default is false (fail fast).
    /// </summary>
    public bool ContinueOnError { get; set; } = false;

    /// <summary>
    /// Gets or sets whether to throw an AggregateException with all failures when ContinueOnError is true.
    /// Default is false.
    /// </summary>
    public bool ThrowAggregateException { get; set; } = false;
    
    /// <summary>
    /// Gets or sets the topic naming strategy.
    /// </summary>
    public TopicNamingStrategy TopicNamingStrategy { get; set; } = TopicNamingStrategy.EventType;
    
    /// <summary>
    /// Gets or sets custom topic mappings (EventType -> TopicName).
    /// </summary>
    public Dictionary<string, string>? TopicMappings { get; set; }
    
    /// <summary>
    /// Gets or sets a custom function to resolve topic names.
    /// Used when TopicNamingStrategy is set to Custom.
    /// </summary>
    public Func<IDistributedDomainEvent, string>? CustomTopicNameResolver { get; set; }
    
    /// <summary>
    /// Gets or sets a custom function to build additional metadata for events.
    /// </summary>
    public Func<IDistributedDomainEvent, Dictionary<string, string>?>? CustomMetadataBuilder { get; set; }
}

/// <summary>
/// Defines strategies for determining topic names from events.
/// </summary>
public enum TopicNamingStrategy
{
    /// <summary>
    /// Use the event type name as the topic name.
    /// </summary>
    EventType,

    /// <summary>
    /// Use the aggregate type name as the topic name.
    /// </summary>
    AggregateType,

    /// <summary>
    /// Combine aggregate type and event type (e.g., "Order.OrderCreated").
    /// </summary>
    AggregateTypeAndEventType,

    /// <summary>
    /// Use a custom topic name resolver function.
    /// </summary>
    Custom
}


/// <summary>
/// Exception thrown when distributed event publishing fails.
/// </summary>
public sealed class DomainEventPublishException : Exception
{
    public DomainEventPublishException(string message) : base(message)
    {
    }

    public DomainEventPublishException(string message, Exception innerException) 
        : base(message, innerException)
    {
    }
}