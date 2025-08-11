using System.Runtime.Serialization;
using Microsoft.Extensions.Logging;

namespace BBT.Workflow.Tasks.Factory;

/// <summary>
/// Provides metrics and sizing calculations for task factory object pools.
/// Helps determine optimal pool sizes based on memory usage and performance requirements.
/// </summary>
public static class TaskFactoryMetrics
{
    /// <summary>
    /// Calculates approximate memory usage per task instance in bytes.
    /// </summary>
    /// <param name="taskType">The task type to calculate memory usage for.</param>
    /// <returns>Estimated memory usage in bytes per instance.</returns>
    public static long CalculateApproximateMemoryPerTask(Type taskType)
    {
        return taskType.Name switch
        {
            "DaprServiceTask" => 2048,      // ~2KB (strings + JsonElement)
            "HttpTask" => 3072,             // ~3KB (URL + headers + body)
            "ScriptTask" => 1024,           // ~1KB (minimal data)
            "DaprHttpEndpointTask" => 2048, // ~2KB
            "DaprBindingTask" => 1536,      // ~1.5KB
            "DaprPubSubTask" => 1536,       // ~1.5KB
            _ => 1024                       // Default ~1KB
        };
    }

    /// <summary>
    /// Recommends optimal pool sizes based on system characteristics.
    /// </summary>
    /// <param name="maxConcurrentRequests">Maximum expected concurrent requests.</param>
    /// <param name="taskExecutionRatePerSecond">Expected task executions per second.</param>
    /// <param name="averageTaskLifetimeMs">Average lifetime of a task in milliseconds.</param>
    /// <param name="availableMemoryMB">Available memory for pooling in MB.</param>
    /// <returns>Recommended pool sizing configuration.</returns>
    public static PoolSizingRecommendation CalculateOptimalPoolSizes(
        int maxConcurrentRequests,
        int taskExecutionRatePerSecond, 
        int averageTaskLifetimeMs,
        int availableMemoryMB)
    {
        // Calculate concurrent task instances needed
        var concurrentTasks = (taskExecutionRatePerSecond * averageTaskLifetimeMs) / 1000.0;
        
        // Add buffer for spikes (20% safety margin)
        var bufferedConcurrentTasks = (int)(concurrentTasks * 1.2);
        
        // Consider request concurrency
        var requestBasedPoolSize = Math.Max(maxConcurrentRequests / 2, 10);
        
        // Take the higher of the two calculations
        var recommendedMaxSize = Math.Max(bufferedConcurrentTasks, requestBasedPoolSize);
        
        // Memory constraint check
        var memoryLimitedSize = CalculateMemoryConstrainedPoolSize(availableMemoryMB);
        recommendedMaxSize = Math.Min(recommendedMaxSize, memoryLimitedSize);
        
        // Initial size should be 10-20% of max size
        var recommendedInitialSize = Math.Max(recommendedMaxSize / 10, 5);
        
        return new PoolSizingRecommendation
        {
            RecommendedMaxPoolSize = recommendedMaxSize,
            RecommendedInitialPoolSize = recommendedInitialSize,
            EstimatedMemoryUsageMB = CalculateEstimatedMemoryUsage(recommendedMaxSize),
            ConcurrentTasksCalculated = (int)concurrentTasks,
            MemoryConstrainedSize = memoryLimitedSize
        };
    }

    /// <summary>
    /// Calculates maximum pool size based on available memory constraints.
    /// </summary>
    private static int CalculateMemoryConstrainedPoolSize(int availableMemoryMB)
    {
        // Reserve memory for each task type (using largest task size for safety)
        var maxTaskSizeBytes = 3072; // HttpTask size
        var availableBytes = availableMemoryMB * 1024 * 1024;
        
        // Use only 10% of available memory for object pooling
        var poolingBudgetBytes = availableBytes * 0.1;
        
        return (int)(poolingBudgetBytes / maxTaskSizeBytes);
    }

    /// <summary>
    /// Estimates total memory usage for a given pool size.
    /// </summary>
    private static double CalculateEstimatedMemoryUsage(int poolSize)
    {
        // Average task size across all types
        var avgTaskSizeBytes = 2048;
        var totalBytes = poolSize * avgTaskSizeBytes;
        return totalBytes / (1024.0 * 1024.0); // Convert to MB
    }

    /// <summary>
    /// Validates pool configuration against system constraints.
    /// </summary>
    public static PoolValidationResult ValidatePoolConfiguration(
        TaskFactoryOptions options, 
        int availableMemoryMB)
    {
        var warnings = new List<string>();
        var errors = new List<string>();

        // Check memory usage
        var estimatedMemoryMB = CalculateEstimatedMemoryUsage(options.MaxPoolSize);
        var memoryPercentage = (estimatedMemoryMB / availableMemoryMB) * 100;

        if (memoryPercentage > 20)
        {
            errors.Add($"Pool configuration may use {memoryPercentage:F1}% of available memory ({estimatedMemoryMB:F1}MB). Consider reducing MaxPoolSize.");
        }
        else if (memoryPercentage > 10)
        {
            warnings.Add($"Pool configuration will use {memoryPercentage:F1}% of available memory ({estimatedMemoryMB:F1}MB).");
        }

        // Check initial vs max ratio
        var initialRatio = (double)options.InitialPoolSize / options.MaxPoolSize;
        if (initialRatio > 0.5)
        {
            warnings.Add($"InitialPoolSize ({options.InitialPoolSize}) is {initialRatio:P0} of MaxPoolSize. Consider reducing for better memory efficiency.");
        }
        else if (initialRatio < 0.05)
        {
            warnings.Add($"InitialPoolSize ({options.InitialPoolSize}) is very low ({initialRatio:P0} of MaxPoolSize). May cause initial allocation delays.");
        }

        return new PoolValidationResult
        {
            IsValid = !errors.Any(),
            Warnings = warnings,
            Errors = errors,
            EstimatedMemoryUsageMB = estimatedMemoryMB,
            MemoryUsagePercentage = memoryPercentage
        };
    }
}

/// <summary>
/// Pool sizing recommendation result.
/// </summary>
public sealed record PoolSizingRecommendation
{
    public int RecommendedMaxPoolSize { get; init; }
    public int RecommendedInitialPoolSize { get; init; }
    public double EstimatedMemoryUsageMB { get; init; }
    public int ConcurrentTasksCalculated { get; init; }
    public int MemoryConstrainedSize { get; init; }
}

/// <summary>
/// Pool configuration validation result.
/// </summary>
public sealed record PoolValidationResult
{
    public bool IsValid { get; init; }
    public List<string> Warnings { get; init; } = new();
    public List<string> Errors { get; init; } = new();
    public double EstimatedMemoryUsageMB { get; init; }
    public double MemoryUsagePercentage { get; init; }
} 