using BBT.Workflow.Monitoring;
using BBT.Workflow.Scripting.Evaluators;
using BBT.Workflow.Scripting.Functions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Scripting;
using Dapr.Client;
using System.Diagnostics;
using BBT.Workflow.Definitions.Timer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BBT.Workflow.Scripting;

/// <summary>
/// Implementation of the script engine that provides C# script evaluation and compilation capabilities.
/// Integrates with Dapr for distributed computing scenarios and provides global functions for scripts.
/// Uses Roslyn's scripting APIs for dynamic C# code execution.
/// </summary>
/// <param name="daprClient">The Dapr client for distributed computing operations</param>
/// <param name="workflowMetrics">The workflow metrics service for recording script engine metrics</param>
/// <param name="logger">The logger for script logging</param>
/// <param name="configuration">The configuration for script configuration access</param>
public sealed class ScriptEngine(
    DaprClient daprClient, 
    IWorkflowMetrics workflowMetrics,
    ILogger<ScriptEngine> logger,
    IConfiguration configuration) : IScriptEngine
{
    /// <summary>
    /// The underlying C# evaluator responsible for script compilation and execution
    /// </summary>
    private readonly IEvaluator _evaluator = new CSharpEvaluator();
    
    /// <summary>
    /// Global functions available to all scripts, providing access to Dapr services, logging, and configuration
    /// </summary>
    private readonly GlobalScriptFunctions _globalFunctions = new(daprClient, logger, configuration);

    /// <summary>
    /// Lazily-initialized default metadata references used for script compilation.
    /// Includes core .NET types, collections, and workflow-specific assemblies.
    /// </summary>
    private static readonly Lazy<MetadataReference[]> DefaultReferences = new(() => new[]
    {
        MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
        MetadataReference.CreateFromFile(typeof(IMapping).Assembly.Location),
        MetadataReference.CreateFromFile(typeof(TimerSchedule).Assembly.Location),
        MetadataReference.CreateFromFile(typeof(Task).Assembly.Location),
        MetadataReference.CreateFromFile(typeof(Dictionary<,>).Assembly.Location),
        MetadataReference.CreateFromFile(typeof(Microsoft.CSharp.RuntimeBinder.CSharpArgumentInfo).Assembly.Location),
        MetadataReference.CreateFromFile(typeof(ScriptHelper).Assembly.Location)
    });

    /// <summary>
    /// Default using directives automatically included in all scripts.
    /// Provides access to common .NET namespaces and workflow-specific types.
    /// </summary>
    private static readonly string[] DefaultUsings =
    {
        "System",
        "System.IO",
        "System.Linq",
        "System.Collections.Generic",
        "System.Threading",
        "System.Threading.Tasks",
        "System.Dynamic",
        "BBT.Workflow.Scripting",
        "BBT.Workflow.Definitions",
        "BBT.Workflow.Instances",
        "BBT.Workflow.Runtime",
        "BBT.Workflow.Scripting.Functions",
        "BBT.Workflow.Definitions.Timer"
    };

    /// <summary>
    /// Evaluates a C# script asynchronously and returns the result as an object.
    /// Automatically merges provided globals with the engine's global functions.
    /// </summary>
    /// <param name="code">The C# code to evaluate</param>
    /// <param name="configureScriptOptions">Optional function to configure script compilation options</param>
    /// <param name="returnType">Optional expected return type for the script evaluation</param>
    /// <param name="globals">Optional global variables accessible within the script</param>
    /// <param name="cancellationToken">Token to cancel the operation</param>
    /// <returns>A task containing the evaluation result as an object, or null if no result</returns>
    /// <exception cref="CompilationErrorException">Thrown when the script contains compilation errors</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled</exception>
    public async Task<object?> EvaluateAsync(string code,
        Func<ScriptOptions, ScriptOptions>? configureScriptOptions = null,
        Type? returnType = null,
        object? globals = null,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        const string scriptType = "evaluation";
        const string language = "csharp";

        try
        {
            // Merge provided globals with our global functions
            object mergedGlobals = globals != null
                ? new ScriptGlobals { Functions = _globalFunctions, Globals = globals }
                : new ScriptGlobals { Functions = _globalFunctions };

            var result = await _evaluator.EvaluateAsync(code, returnType ?? typeof(ScriptGlobals), configureScriptOptions,
                mergedGlobals, cancellationToken);

            stopwatch.Stop();
            var durationSeconds = stopwatch.Elapsed.TotalSeconds;

            // Record successful script execution
            workflowMetrics.RecordScriptExecution(scriptType, language, "success");
            workflowMetrics.RecordScriptExecutionDuration(scriptType, language, "success", durationSeconds);

            return result;
        }
        catch (CompilationErrorException ex)
        {
            stopwatch.Stop();
            var durationSeconds = stopwatch.Elapsed.TotalSeconds;

            // Record compilation error
            workflowMetrics.RecordScriptExecution(scriptType, language, "compilation_error");
            workflowMetrics.RecordScriptCompilationError(scriptType, language, ex.GetType().Name);
            workflowMetrics.RecordScriptExecutionDuration(scriptType, language, "compilation_error", durationSeconds);

            throw;
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            var durationSeconds = stopwatch.Elapsed.TotalSeconds;

            // Record cancelled execution
            workflowMetrics.RecordScriptExecution(scriptType, language, "cancelled");
            workflowMetrics.RecordScriptExecutionDuration(scriptType, language, "cancelled", durationSeconds);

            throw;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            var durationSeconds = stopwatch.Elapsed.TotalSeconds;

            // Record runtime error
            workflowMetrics.RecordScriptExecution(scriptType, language, "runtime_error");
            workflowMetrics.RecordScriptRuntimeError(scriptType, language, ex.GetType().Name);
            workflowMetrics.RecordScriptExecutionDuration(scriptType, language, "runtime_error", durationSeconds);

            throw;
        }
    }

    /// <summary>
    /// Evaluates a C# script asynchronously and returns the result as a strongly-typed value.
    /// Automatically merges provided globals with the engine's global functions.
    /// </summary>
    /// <typeparam name="T">The expected return type of the script evaluation</typeparam>
    /// <param name="code">The C# code to evaluate</param>
    /// <param name="configureScriptOptions">Optional function to configure script compilation options</param>
    /// <param name="returnType">Optional expected return type for the script evaluation</param>
    /// <param name="globals">Optional global variables accessible within the script</param>
    /// <param name="cancellationToken">Token to cancel the operation</param>
    /// <returns>A task containing the evaluation result cast to type T</returns>
    /// <exception cref="CompilationErrorException">Thrown when the script contains compilation errors</exception>
    /// <exception cref="InvalidCastException">Thrown when the result cannot be cast to type T</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled</exception>
    public async Task<T> EvaluateAsync<T>(string code, Func<ScriptOptions, ScriptOptions>? configureScriptOptions = null,
        Type? returnType = null, object? globals = null, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        const string scriptType = "evaluation_generic";
        const string language = "csharp";

        try
        {
            // Merge provided globals with our global functions
            object mergedGlobals = globals != null
                ? new ScriptGlobals { Functions = _globalFunctions, Globals = globals }
                : new ScriptGlobals { Functions = _globalFunctions };

            var result = await _evaluator.EvaluateAsync<T>(code, configureScriptOptions, returnType ?? typeof(ScriptGlobals),
                mergedGlobals, cancellationToken);

            stopwatch.Stop();
            var durationSeconds = stopwatch.Elapsed.TotalSeconds;

            // Record successful script execution
            workflowMetrics.RecordScriptExecution(scriptType, language, "success");
            workflowMetrics.RecordScriptExecutionDuration(scriptType, language, "success", durationSeconds);

            return result;
        }
        catch (CompilationErrorException ex)
        {
            stopwatch.Stop();
            var durationSeconds = stopwatch.Elapsed.TotalSeconds;

            // Record compilation error
            workflowMetrics.RecordScriptExecution(scriptType, language, "compilation_error");
            workflowMetrics.RecordScriptCompilationError(scriptType, language, ex.GetType().Name);
            workflowMetrics.RecordScriptExecutionDuration(scriptType, language, "compilation_error", durationSeconds);

            throw;
        }
        catch (InvalidCastException ex)
        {
            stopwatch.Stop();
            var durationSeconds = stopwatch.Elapsed.TotalSeconds;

            // Record cast error as runtime error
            workflowMetrics.RecordScriptExecution(scriptType, language, "cast_error");
            workflowMetrics.RecordScriptRuntimeError(scriptType, language, ex.GetType().Name);
            workflowMetrics.RecordScriptExecutionDuration(scriptType, language, "cast_error", durationSeconds);

            throw;
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            var durationSeconds = stopwatch.Elapsed.TotalSeconds;

            // Record cancelled execution
            workflowMetrics.RecordScriptExecution(scriptType, language, "cancelled");
            workflowMetrics.RecordScriptExecutionDuration(scriptType, language, "cancelled", durationSeconds);

            throw;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            var durationSeconds = stopwatch.Elapsed.TotalSeconds;

            // Record runtime error
            workflowMetrics.RecordScriptExecution(scriptType, language, "runtime_error");
            workflowMetrics.RecordScriptRuntimeError(scriptType, language, ex.GetType().Name);
            workflowMetrics.RecordScriptExecutionDuration(scriptType, language, "runtime_error", durationSeconds);

            throw;
        }
    }

    /// <summary>
    /// Compiles C# code into an instance of the specified type asynchronously.
    /// Automatically includes default metadata references and using directives,
    /// merging them with any additional references and usings provided.
    /// </summary>
    /// <typeparam name="T">The target type to compile the code into</typeparam>
    /// <param name="code">The C# code to compile</param>
    /// <param name="extraReferences">Optional additional metadata references for compilation</param>
    /// <param name="usingDirectives">Optional additional using directives to include</param>
    /// <param name="cancellationToken">Token to cancel the operation</param>
    /// <returns>A task containing the compiled instance of type T</returns>
    /// <exception cref="CompilationErrorException">Thrown when the code contains compilation errors</exception>
    /// <exception cref="InvalidOperationException">Thrown when the code cannot be compiled to the target type</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled</exception>
    public async Task<T> CompileToInstanceAsync<T>(
        string code,
        IEnumerable<MetadataReference>? extraReferences = null,
        IEnumerable<string>? usingDirectives = null,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        const string scriptType = "compilation";
        const string language = "csharp";

        try
        {
            // Use cached default references
            var mergedReferences = (extraReferences ?? Enumerable.Empty<MetadataReference>())
                .Concat(DefaultReferences.Value)
                .Distinct();

            // Use cached default usings
            var mergedUsings = (usingDirectives ?? Enumerable.Empty<string>())
                .Concat(DefaultUsings)
                .Distinct();

            var result = await _evaluator.CompileToInstanceAsync<T>(code, mergedReferences, mergedUsings, cancellationToken);

            stopwatch.Stop();
            var durationSeconds = stopwatch.Elapsed.TotalSeconds;

            // Record successful script compilation
            workflowMetrics.RecordScriptExecution(scriptType, language, "success");
            workflowMetrics.RecordScriptCompilationDuration(scriptType, language, "success", durationSeconds);

            return result;
        }
        catch (CompilationErrorException ex)
        {
            stopwatch.Stop();
            var durationSeconds = stopwatch.Elapsed.TotalSeconds;

            // Record compilation error
            workflowMetrics.RecordScriptExecution(scriptType, language, "compilation_error");
            workflowMetrics.RecordScriptCompilationError(scriptType, language, ex.GetType().Name);
            workflowMetrics.RecordScriptCompilationDuration(scriptType, language, "compilation_error", durationSeconds);

            throw;
        }
        catch (InvalidOperationException ex)
        {
            stopwatch.Stop();
            var durationSeconds = stopwatch.Elapsed.TotalSeconds;

            // Record invalid operation as compilation error
            workflowMetrics.RecordScriptExecution(scriptType, language, "invalid_operation");
            workflowMetrics.RecordScriptCompilationError(scriptType, language, ex.GetType().Name);
            workflowMetrics.RecordScriptCompilationDuration(scriptType, language, "invalid_operation", durationSeconds);

            throw;
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            var durationSeconds = stopwatch.Elapsed.TotalSeconds;

            // Record cancelled compilation
            workflowMetrics.RecordScriptExecution(scriptType, language, "cancelled");
            workflowMetrics.RecordScriptCompilationDuration(scriptType, language, "cancelled", durationSeconds);

            throw;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            var durationSeconds = stopwatch.Elapsed.TotalSeconds;

            // Record unexpected compilation error
            workflowMetrics.RecordScriptExecution(scriptType, language, "unexpected_error");
            workflowMetrics.RecordScriptCompilationError(scriptType, language, ex.GetType().Name);
            workflowMetrics.RecordScriptCompilationDuration(scriptType, language, "unexpected_error", durationSeconds);

            throw;
        }
    }
}