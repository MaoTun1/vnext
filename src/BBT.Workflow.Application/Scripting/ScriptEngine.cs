using System.Dynamic;
using BBT.Workflow.Scripting.Evaluators;
using BBT.Workflow.Scripting.Functions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Scripting;
using Dapr.Client;

namespace BBT.Workflow.Scripting;

/// <summary>
/// Implementation of the script engine that provides C# script evaluation and compilation capabilities.
/// Integrates with Dapr for distributed computing scenarios and provides global functions for scripts.
/// Uses Roslyn's scripting APIs for dynamic C# code execution.
/// </summary>
/// <param name="daprClient">The Dapr client for distributed computing operations</param>
public sealed class ScriptEngine(DaprClient daprClient) : IScriptEngine
{
    /// <summary>
    /// The underlying C# evaluator responsible for script compilation and execution
    /// </summary>
    private readonly IEvaluator _evaluator = new CSharpEvaluator();
    
    /// <summary>
    /// Global functions available to all scripts, providing access to Dapr services
    /// </summary>
    private readonly GlobalScriptFunctions _globalFunctions = new(daprClient);

    /// <summary>
    /// Lazily-initialized default metadata references used for script compilation.
    /// Includes core .NET types, collections, and workflow-specific assemblies.
    /// </summary>
    private static readonly Lazy<MetadataReference[]> DefaultReferences = new(() => new[]
    {
        MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
        MetadataReference.CreateFromFile(typeof(IMapping).Assembly.Location),
        MetadataReference.CreateFromFile(typeof(Task).Assembly.Location),
        MetadataReference.CreateFromFile(typeof(Dictionary<,>).Assembly.Location),
        MetadataReference.CreateFromFile(typeof(Microsoft.CSharp.RuntimeBinder.CSharpArgumentInfo).Assembly.Location),
        MetadataReference.CreateFromFile(typeof(ScriptHelper).Assembly.Location),
        MetadataReference.CreateFromFile(typeof(ExpandoExtensions).Assembly.Location),
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
        "BBT.Workflow.Scripting.Functions"
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
    public Task<object?> EvaluateAsync(string code,
        Func<ScriptOptions, ScriptOptions>? configureScriptOptions = null,
        Type? returnType = null,
        object? globals = null,
        CancellationToken cancellationToken = default)
    {
        // Merge provided globals with our global functions
        object mergedGlobals = globals != null
            ? new ScriptGlobals { Functions = _globalFunctions, Globals = globals }
            : new ScriptGlobals { Functions = _globalFunctions };

        return _evaluator.EvaluateAsync(code, returnType ?? typeof(ScriptGlobals), configureScriptOptions,
            mergedGlobals, cancellationToken);
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
    public Task<T> EvaluateAsync<T>(string code, Func<ScriptOptions, ScriptOptions>? configureScriptOptions = null,
        Type? returnType = null, object? globals = null, CancellationToken cancellationToken = default)
    {
        // Merge provided globals with our global functions
        object mergedGlobals = globals != null
            ? new ScriptGlobals { Functions = _globalFunctions, Globals = globals }
            : new ScriptGlobals { Functions = _globalFunctions };

        return _evaluator.EvaluateAsync<T>(code, configureScriptOptions, returnType ?? typeof(ScriptGlobals),
            mergedGlobals, cancellationToken);
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
    public Task<T> CompileToInstanceAsync<T>(
        string code,
        IEnumerable<MetadataReference>? extraReferences = null,
        IEnumerable<string>? usingDirectives = null,
        CancellationToken cancellationToken = default)
    {
        // Use cached default references
        var mergedReferences = (extraReferences ?? Enumerable.Empty<MetadataReference>())
            .Concat(DefaultReferences.Value)
            .Distinct();

        // Use cached default usings
        var mergedUsings = (usingDirectives ?? Enumerable.Empty<string>())
            .Concat(DefaultUsings)
            .Distinct();

        return _evaluator.CompileToInstanceAsync<T>(code, mergedReferences, mergedUsings, cancellationToken);
    }
}