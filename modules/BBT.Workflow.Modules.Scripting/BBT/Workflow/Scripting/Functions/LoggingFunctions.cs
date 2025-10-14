using System;
using Microsoft.Extensions.Logging;

namespace BBT.Workflow.Scripting.Functions;

/// <summary>
/// Custom functions for logging from scripts
/// </summary>
public class LoggingFunctions
{
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="LoggingFunctions"/> class
    /// </summary>
    /// <param name="logger">The logger instance</param>
    /// <exception cref="ArgumentNullException">Thrown when logger is null</exception>
    public LoggingFunctions(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Logs a trace message
    /// </summary>
    /// <param name="message">The message to log</param>
    public void LogTrace(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        _logger.LogTrace("{Message}", message);
    }

    /// <summary>
    /// Logs a debug message
    /// </summary>
    /// <param name="message">The message to log</param>
    public void LogDebug(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        _logger.LogDebug("{Message}", message);
    }

    /// <summary>
    /// Logs an informational message
    /// </summary>
    /// <param name="message">The message to log</param>
    public void LogInformation(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        _logger.LogInformation("{Message}", message);
    }

    /// <summary>
    /// Logs a warning message
    /// </summary>
    /// <param name="message">The message to log</param>
    public void LogWarning(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        _logger.LogWarning("{Message}", message);
    }

    /// <summary>
    /// Logs an error message
    /// </summary>
    /// <param name="message">The message to log</param>
    public void LogError(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        _logger.LogError("{Message}", message);
    }

    /// <summary>
    /// Logs an error message with exception
    /// </summary>
    /// <param name="message">The message to log</param>
    /// <param name="exception">The exception to log</param>
    public void LogError(string message, Exception exception)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        _logger.LogError(exception, "{Message}", message);
    }

    /// <summary>
    /// Logs a critical message
    /// </summary>
    /// <param name="message">The message to log</param>
    public void LogCritical(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        _logger.LogCritical("{Message}", message);
    }

    /// <summary>
    /// Logs a critical message with exception
    /// </summary>
    /// <param name="message">The message to log</param>
    /// <param name="exception">The exception to log</param>
    public void LogCritical(string message, Exception exception)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        _logger.LogCritical(exception, "{Message}", message);
    }
}

