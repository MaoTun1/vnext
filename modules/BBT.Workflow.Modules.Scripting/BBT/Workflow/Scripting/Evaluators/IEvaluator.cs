using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Scripting;

namespace BBT.Workflow.Scripting.Evaluators;

public interface IEvaluator
{
    Task<object?> EvaluateAsync(
        string code, 
        Type? returnType = null, 
        Func<ScriptOptions, ScriptOptions>? configureScriptOptions = null, 
        object? globals = null, 
        CancellationToken cancellationToken = default);

    Task<T> EvaluateAsync<T>(
        string code, 
        Func<ScriptOptions, ScriptOptions>? configureScriptOptions = null, 
        Type? returnType = null,
        object? globals = null, 
        CancellationToken cancellationToken = default);

    Task<T> CompileToInstanceAsync<T>(
        string code,
        IEnumerable<MetadataReference>? extraReferences = null,
        IEnumerable<string>? usingDirectives = null,
        CancellationToken cancellationToken = default);
}