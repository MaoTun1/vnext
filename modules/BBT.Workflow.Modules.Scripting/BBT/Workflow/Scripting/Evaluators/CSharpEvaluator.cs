using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Scripting;

namespace BBT.Workflow.Scripting.Evaluators;

public class CSharpEvaluator : IEvaluator
{
    private static readonly ConcurrentDictionary<string, Script<object>> ScriptCache = new();

    public Task<T> CompileToInstanceAsync<T>(
        string code, 
        IEnumerable<MetadataReference>? extraReferences = null,
        IEnumerable<string>? usingDirectives = null,
        CancellationToken cancellationToken = default)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(code, cancellationToken: cancellationToken);

        // Eğer usingDirectives varsa root'a ekle
        if (usingDirectives != null && usingDirectives.Any())
        {
            var root = syntaxTree.GetRoot(cancellationToken);
            var usings = usingDirectives.Select(u => SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(u)));
            var newRoot = ((CompilationUnitSyntax)root).WithUsings(SyntaxFactory.List(usings));
            syntaxTree = syntaxTree.WithRootAndOptions(newRoot, syntaxTree.Options);
        }

        // Gerekli tüm assembly referanslarını al
        var defaultReferences = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrWhiteSpace(a.Location))
            .Select(a => MetadataReference.CreateFromFile(a.Location));

        var references = defaultReferences
            .Concat(extraReferences ?? Enumerable.Empty<MetadataReference>())
            .Distinct();

        var compilation = CSharpCompilation.Create(
            assemblyName: Path.GetRandomFileName(),
            syntaxTrees: [syntaxTree],
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        
        using var ms = new MemoryStream();
        var emitResult = compilation.Emit(ms, cancellationToken: cancellationToken);

        if (!emitResult.Success)
        {
            var errors = string.Join("\n", emitResult.Diagnostics
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .Select(d => d.ToString()));

            throw new InvalidOperationException($"Compilation failed:\n{errors}");
        }

        ms.Seek(0, SeekOrigin.Begin);
        var assembly = Assembly.Load(ms.ToArray());

        // Daha detaylı type arama: hem interface hem de sınıf adı üzerinden
        var types = assembly.GetTypes();

        var matchedType = types.FirstOrDefault(t =>
            typeof(T).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

        if (matchedType == null)
        {
            var available = string.Join(", ", types.Select(t => t.FullName));
            throw new InvalidOperationException(
                $"No type implementing {typeof(T).FullName} found.\nAvailable types: {available}");
        }

        return Task.FromResult((T)Activator.CreateInstance(matchedType)!);
    }

    private static ScriptOptions CreateDefaultOptions(Func<ScriptOptions, ScriptOptions>? configureScriptOptions)
    {
        var defaultOptions = CSharpScriptOptionProvider.Default;
        return configureScriptOptions?.Invoke(defaultOptions) ?? defaultOptions;
    }
}