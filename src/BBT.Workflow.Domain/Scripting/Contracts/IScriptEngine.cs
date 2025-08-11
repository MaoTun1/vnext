using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Scripting;

namespace BBT.Workflow.Scripting;

/// <summary>
/// Represents a script engine that combines script running and compilation capabilities.
/// Provides a unified interface for executing C# scripts and compiling them to instances.
/// </summary>
public interface IScriptEngine : IScriptRunner, IScriptCompiler
{
}

/// <summary>
/// Provides capabilities for executing C# scripts dynamically at runtime.
/// Supports both generic and non-generic evaluation with customizable script options and global variables.
/// </summary>
public interface IScriptRunner
{
    /// <summary>
    /// Evaluates a C# script asynchronously and returns the result as an object.
    /// </summary>
    /// <param name="code">The C# code to evaluate</param>
    /// <param name="configureScriptOptions">Optional function to configure script compilation options</param>
    /// <param name="returnType">Optional expected return type for the script evaluation</param>
    /// <param name="globals">Optional global variables accessible within the script</param>
    /// <param name="cancellationToken">Token to cancel the operation</param>
    /// <returns>A task containing the evaluation result as an object, or null if no result</returns>
    /// <exception cref="CompilationErrorException">Thrown when the script contains compilation errors</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled</exception>
    Task<object?> EvaluateAsync(
        string code,
        Func<ScriptOptions, ScriptOptions>? configureScriptOptions = null,
        Type? returnType = null,
        object? globals = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Evaluates a C# script asynchronously and returns the result as a strongly-typed value.
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
    Task<T> EvaluateAsync<T>(
        string code,
        Func<ScriptOptions, ScriptOptions>? configureScriptOptions = null,
        Type? returnType = null,
        object? globals = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Provides capabilities for compiling C# code into executable instances.
/// Supports compilation with custom metadata references and using directives.
/// </summary>
public interface IScriptCompiler
{
    /// <summary>
    /// Compiles C# code into an instance of the specified type asynchronously.
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
    Task<T> CompileToInstanceAsync<T>(
        string code,
        IEnumerable<MetadataReference>? extraReferences = null,
        IEnumerable<string>? usingDirectives = null,
        CancellationToken cancellationToken = default);
}