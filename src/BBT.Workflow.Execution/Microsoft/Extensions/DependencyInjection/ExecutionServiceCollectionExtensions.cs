using BBT.Workflow.Execution.Invokers;
using BBT.Workflow.Execution.Metrics;
using BBT.Workflow.Execution.Services;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering Execution services.
/// </summary>
public static class ExecutionServiceCollectionExtensions
{
    /// <summary>
    /// Adds the task invoker registry and all built-in invokers to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddTaskInvokers(this IServiceCollection services)
    {
        // Register the registry
        services.AddSingleton<ITaskInvokerRegistry, TaskInvokerRegistry>();
        
        // Register null metrics as default (can be overridden)
        services.TryAddSingleton<ITaskMetrics>(NullTaskMetrics.Instance);
        
        // Register all built-in remote execution invokers
        services.AddSingleton<ITaskInvoker, HttpTaskInvoker>();
        services.AddSingleton<ITaskInvoker, DaprServiceTaskInvoker>();
        services.AddSingleton<ITaskInvoker, DaprBindingTaskInvoker>();
        services.AddSingleton<ITaskInvoker, DaprHttpEndpointTaskInvoker>();
        services.AddSingleton<ITaskInvoker, DaprPubSubTaskInvoker>();
        services.AddSingleton<ITaskInvoker, NotificationTaskInvoker>();
        
        // Register trigger task remote invokers (for cross-domain execution)
        services.AddSingleton<ITaskInvoker, StartTriggerRemoteInvoker>();
        services.AddSingleton<ITaskInvoker, DirectTriggerRemoteInvoker>();
        services.AddSingleton<ITaskInvoker, SubProcessRemoteInvoker>();
        services.AddSingleton<ITaskInvoker, GetInstanceDataRemoteInvoker>();
        
        return services;
    }
    
    /// <summary>
    /// Adds custom task metrics implementation.
    /// </summary>
    /// <typeparam name="TMetrics">The metrics implementation type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddTaskMetrics<TMetrics>(this IServiceCollection services)
        where TMetrics : class, ITaskMetrics
    {
        services.AddSingleton<ITaskMetrics, TMetrics>();
        return services;
    }
}
