using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using BBT.Workflow.Scripting.Functions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Scripting;

namespace BBT.Workflow.Scripting.Evaluators;

public static class CSharpScriptOptionProvider
{
    public static ScriptOptions Default => ScriptOptions.Default
        .WithOptimizationLevel(OptimizationLevel.Release)
        .AddReferences(
            typeof(CSharpEvaluator).Assembly,
            typeof(Enumerable).Assembly, // System.Linq
            typeof(Guid).Assembly, // System.Runtime
            typeof(JsonSerializer).Assembly, // System.Text.Json
            typeof(IDictionary<string, object>).Assembly, // System.Collections
            typeof(Dapr.Client.DaprClient).Assembly // Dapr.Client
        )
        .AddImports(
            typeof(CSharpEvaluator).Namespace!, // BBT.Workflow.Scripting.Evaluators
            typeof(Enumerable).Namespace!, // System.Linq
            typeof(Guid).Namespace!, // System
            typeof(JsonSerializer).Namespace!, // System.Text.Json
            typeof(JsonConverter).Namespace!, // System.Text.Json.Serialization
            typeof(JsonNode).Namespace!, // System.Text.Json.Nodes
            typeof(IDictionary<string, object>).Namespace!, // System.Collections.Generic
            typeof(ScriptHelper).Namespace // Custom functions namespace
        );
}