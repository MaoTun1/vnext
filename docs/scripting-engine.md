# Scripting Engine

## Overview

The BBT Workflow Engine includes a powerful scripting engine that enables dynamic C# script compilation and execution at runtime. This engine uses Microsoft's Roslyn compiler APIs to provide seamless integration of custom business logic within workflow definitions.

## Core Components

### 1. IScriptEngine Interface

The main interface that combines script running and compilation capabilities:

```csharp
public interface IScriptEngine : IScriptRunner, IScriptCompiler
{
}
```

### 2. Script Runner (IScriptRunner)

Provides capabilities for executing C# scripts dynamically at runtime:

```csharp
public interface IScriptRunner
{
    Task<object?> EvaluateAsync(
        string code,
        Func<ScriptOptions, ScriptOptions>? configureScriptOptions = null,
        Type? returnType = null,
        object? globals = null,
        CancellationToken cancellationToken = default);

    Task<T> EvaluateAsync<T>(
        string code,
        Func<ScriptOptions, ScriptOptions>? configureScriptOptions = null,
        Type? returnType = null,
        object? globals = null,
        CancellationToken cancellationToken = default);
}
```

### 3. Script Compiler (IScriptCompiler)

Provides capabilities for compiling C# code into executable instances:

```csharp
public interface IScriptCompiler
{
    Task<T> CompileToInstanceAsync<T>(
        string code,
        IEnumerable<MetadataReference>? extraReferences = null,
        IEnumerable<string>? usingDirectives = null,
        CancellationToken cancellationToken = default);
}
```

## Script Engine Implementation

### Core Features

- **Roslyn Integration**: Uses Microsoft.CodeAnalysis.CSharp.Scripting for compilation
- **Caching**: Scripts are cached for improved performance
- **Global Functions**: Built-in functions available to all scripts
- **DAPR Integration**: Secure access to secrets and services through DAPR
- **Automatic Assembly References**: Default .NET and workflow assemblies included

### Default References and Imports

The engine automatically includes essential references and using statements:

```csharp
private static readonly MetadataReference[] DefaultReferences = new[]
{
    MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
    MetadataReference.CreateFromFile(typeof(IMapping).Assembly.Location),
    MetadataReference.CreateFromFile(typeof(Task).Assembly.Location),
    MetadataReference.CreateFromFile(typeof(Dictionary<,>).Assembly.Location)
};

private static readonly string[] DefaultUsings =
{
    "System",
    "System.IO",
    "System.Linq",
    "System.Collections.Generic",
    "System.Threading",
    "System.Threading.Tasks",
    "BBT.Workflow.Scripting",
    "BBT.Workflow.Definitions",
    "BBT.Workflow.Instances",
    "BBT.Workflow.Runtime"
};
```

## Script Interfaces and Contracts

### 1. IMapping Interface

Used for task input/output mapping:

```csharp
public interface IMapping
{
    Task<ScriptResponse> InputHandler(WorkflowTask task, ScriptContext context);
    Task<ScriptResponse> OutputHandler(ScriptContext context);
}
```

### 2. IScriptExecution Interface

For general script execution:

```csharp
public interface IScriptExecution
{
    Task<object?> ExecuteAsync(ScriptContext context, CancellationToken cancellationToken);
}
```

### 3. IConditionMapping Interface

For conditional logic evaluation:

```csharp
public interface IConditionMapping
{
    Task<bool> Handler(ScriptContext context);
}
```

### 4. ITimerMapping Interface

For flexible timer scheduling with Dapr-compatible functionality:

```csharp
public interface ITimerMapping
{
    Task<TimerSchedule> Handler(ScriptContext context);
}
```

The enhanced `ITimerMapping` interface supports:

- **DateTime scheduling**: `TimerSchedule.FromDateTime(dateTime)`
- **Cron expressions**: `TimerSchedule.FromCronExpression("0 9 * * *")`
- **Duration-based**: `TimerSchedule.FromDuration(TimeSpan.FromHours(2))`
- **Immediate execution**: `TimerSchedule.Immediate()`

**Example Timer Implementation:**

```csharp
public class PaymentDueTimerRule : ITimerMapping
{
    public async Task<TimerSchedule> Handler(ScriptContext context)
    {
        var frequency = context.Instance.Data.paymentFrequency?.ToString() ?? "monthly";
        
        return frequency switch
        {
            "daily" => TimerSchedule.FromCronExpression("0 9 * * *"),
            "weekly" => TimerSchedule.FromCronExpression("0 9 * * 1"),
            "monthly" => TimerSchedule.FromCronExpression("0 9 1 * *"),
            "immediate" => TimerSchedule.Immediate(),
            _ => TimerSchedule.FromDuration(TimeSpan.FromDays(30))
        };
    }
}
```

## Script Context

The `ScriptContext` provides execution context to scripts:

```csharp
public sealed class ScriptContext
{
    public Instance Instance { get; private set; }
    public Workflow Workflow { get; private set; }
    public Transition? Transition { get; private set; }
    public JsonElement? Attributes { get; private set; }
    public object? Body { get; private set; }
    public IReadOnlyDictionary<string, string>? Headers { get; private set; }
    public Dictionary<string, object?> RouteValues { get; private set; }
    public Guid TransitionId { get; private set; }

    // Builder pattern for construction
    public class Builder
    {
        public Builder SetInstance(Instance instance) { /* ... */ }
        public Builder SetWorkflow(Workflow workflow) { /* ... */ }
        public Builder SetTransition(Transition transition) { /* ... */ }
        public Builder SetAttributes(JsonElement attributes) { /* ... */ }
        public Builder SetHeaders(IReadOnlyDictionary<string, string>? headers) { /* ... */ }
        public ScriptContext Build() { /* ... */ }
    }
}
```

## Global Script Functions

### DAPR Secret Functions

The engine provides built-in functions for secure secret management through DAPR:

```csharp
public class GlobalScriptFunctions
{
    public string GetSecret(string storeName, string secretStore, string secretKey);
    public Dictionary<string, string> GetSecrets(string storeName, string secretStore);
    public async Task<string> GetSecretAsync(string storeName, string secretStore, string secretKey);
    public async Task<Dictionary<string, string>> GetSecretsAsync(string storeName, string secretStore);
}
```

### Usage Examples

**1. Basic Script Evaluation:**
```csharp
// Simple expression evaluation
var result = await scriptEngine.EvaluateAsync("1 + 2");
// Returns: 3

// String manipulation
var greeting = await scriptEngine.EvaluateAsync<string>("\"Hello, \" + \"World!\"");
// Returns: "Hello, World!"
```

**2. Using Global Functions:**
```csharp
string secretScript = @"
    var apiKey = GetSecret(""dapr_store"", ""secret_store"", ""Asgard_ApiKey"");
    return ""API Key: "" + apiKey;
";

var result = await scriptEngine.EvaluateAsync<string>(secretScript);
```

**3. Compiling Task Mapping:**
```csharp
var mappingCode = @"
    using System.Threading.Tasks;
    using BBT.Workflow.Scripting;
    using BBT.Workflow.Definitions;

    public class HttpTaskMapping : IMapping
    {
        public Task<ScriptResponse> InputHandler(WorkflowTask task, ScriptContext context)
        {
            var httpTask = (task as HttpTask)!;
            
            // Set dynamic URL based on context
            httpTask.Url = ""https://api.example.com/customers/"" + context.Attributes?.GetProperty(""customerId"").GetString();
            
            // Prepare request data
            var requestData = new
            {
                customerId = context.Attributes?.GetProperty(""customerId"").GetString(),
                action = ""verification""
            };

            return Task.FromResult(new ScriptResponse
            {
                Data = requestData,
                Headers = new Dictionary<string, string>
                {
                    { ""Authorization"", ""Bearer "" + GetSecret(""dapr_store"", ""api_store"", ""auth_token"") }
                }
            });
        }

        public Task<ScriptResponse> OutputHandler(ScriptContext context)
        {
            // Transform response data
            var responseData = new
            {
                verified = context.Body?.verified ?? false,
                timestamp = DateTime.UtcNow,
                customerId = context.Attributes?.GetProperty(""customerId"").GetString()
            };

            return Task.FromResult(new ScriptResponse
            {
                Data = responseData
            });
        }
    }
";

var mappingInstance = await scriptEngine.CompileToInstanceAsync<IMapping>(mappingCode);
```

## Script Execution Patterns

### 1. Simple Script Tasks

For basic data transformation:

```csharp
var scriptTask = new ScriptTask
{
    Key = "calculate-interest",
    Script = new ScriptCode
    {
        Code = @"
            var principal = context.attributes.loanAmount;
            var rate = context.attributes.interestRate;
            var years = context.attributes.termYears;
            
            var monthlyRate = rate / 12 / 100;
            var payments = years * 12;
            
            var monthlyPayment = principal * (monthlyRate * Math.Pow(1 + monthlyRate, payments)) / 
                               (Math.Pow(1 + monthlyRate, payments) - 1);
            
            return new { 
                monthlyPayment = Math.Round(monthlyPayment, 2),
                totalPayment = Math.Round(monthlyPayment * payments, 2)
            };
        ",
        Language = ScriptLanguage.CSharp
    }
};
```

### 2. Conditional Scripts

For workflow branching logic:

```csharp
var conditionCode = @"
    public class CreditCheckCondition : IConditionMapping
    {
        public Task<bool> Handler(ScriptContext context)
        {
            var creditScore = context.Attributes?.creditScore ?? 0;
            var loanAmount = context.Attributes?.loanAmount ?? 0;
            
            // Approve if credit score > 700 and loan amount < 100000
            var approved = creditScore > 700 && loanAmount < 100000;
            
            return Task.FromResult(approved);
        }
    }
";

var conditionInstance = await scriptEngine.CompileToInstanceAsync<IConditionMapping>(conditionCode);
var isApproved = await conditionInstance.Handler(scriptContext);
```

### 3. Advanced Data Processing

Using LINQ and complex transformations:

```csharp
var dataProcessingScript = @"
    var customers = context.attributes.customers.EnumerateArray()
        .Select(c => new {
            Id = c.GetProperty(""id"").GetString(),
            Name = c.GetProperty(""name"").GetString(),
            Score = c.GetProperty(""creditScore"").GetInt32()
        })
        .Where(c => c.Score > 650)
        .OrderByDescending(c => c.Score)
        .Take(10)
        .ToList();
    
    return new { 
        eligibleCustomers = customers,
        count = customers.Count,
        averageScore = customers.Any() ? customers.Average(c => c.Score) : 0
    };
";

var result = await scriptEngine.EvaluateAsync(dataProcessingScript);
```

## Script Base Classes

### ScriptBase Class

`ScriptBase` provides convenient access to various global functions including secret management, logging, and configuration access. All scripts can inherit from this class to access these capabilities.

```csharp
public abstract class ScriptBase
{
    // Secret Management Functions
    protected string GetSecret(string storeName, string secretStore, string secretKey);
    protected async Task<string> GetSecretAsync(string storeName, string secretStore, string secretKey);
    protected Dictionary<string, string> GetSecrets(string storeName, string secretStore);
    protected async Task<Dictionary<string, string>> GetSecretsAsync(string storeName, string secretStore);
    
    // Logging Functions
    protected void LogTrace(string message);
    protected void LogDebug(string message);
    protected void LogInformation(string message);
    protected void LogWarning(string message);
    protected void LogError(string message);
    protected void LogCritical(string message);
    
    // Configuration Functions
    protected string? GetConfigValue(string key);
    protected string GetConfigValue(string key, string defaultValue);
    protected T? GetConfigValue<T>(string key);
    
    // Dynamic Object Helpers
    protected bool HasProperty(object obj, string propertyName);
    protected object? GetPropertyValue(object obj, string propertyName);
}
```

### Logging Functions

Scripts can now use structured logging functions to output diagnostic information:

```csharp
public class DiagnosticMapping : ScriptBase, IMapping
{
    public Task<ScriptResponse> InputHandler(WorkflowTask task, ScriptContext context)
    {
        LogDebug($"Starting task execution for {task.Key}");
        
        try
        {
            LogInformation($"Processing instance {context.Instance.Id}");
            
            var data = PrepareData(context);
            
            LogTrace($"Data prepared: {JsonSerializer.Serialize(data)}");
            
            return Task.FromResult(new ScriptResponse { Data = data });
        }
        catch (Exception ex)
        {
            LogError($"Task execution failed: {ex.Message}");
            throw;
        }
    }
}
```

**Log Levels:**
- `LogTrace`: Detailed debugging information (rarely used in production)
- `LogDebug`: Diagnostic information useful during development
- `LogInformation`: General information about workflow execution
- `LogWarning`: Warning messages for unexpected but recoverable situations
- `LogError`: Error messages for failures
- `LogCritical`: Critical failures requiring immediate attention

**Best Practices:**
- Use appropriate log levels based on importance
- Include contextual information (instance ID, task key, etc.)
- Avoid logging sensitive data (passwords, API keys, PII)
- Keep messages concise and meaningful
- Log performance-critical operations with timing information

### Configuration Functions

Scripts can access application configuration values at runtime:

```csharp
public class ConfigurableMapping : ScriptBase, IMapping
{
    public Task<ScriptResponse> InputHandler(WorkflowTask task, ScriptContext context)
    {
        // Get configuration values with different approaches
        var apiEndpoint = GetConfigValue("ExternalApi:Endpoint");
        var timeout = GetConfigValue("ExternalApi:Timeout", "30"); // with default
        var maxRetries = GetConfigValue<int>("ExternalApi:MaxRetries"); // typed
        
        LogInformation($"Using endpoint: {apiEndpoint}");
        
        var httpTask = (task as HttpTask)!;
        httpTask.Url = apiEndpoint;
        httpTask.Timeout = TimeSpan.FromSeconds(int.Parse(timeout));
        
        return Task.FromResult(new ScriptResponse 
        { 
            Data = new { maxRetries = maxRetries }
        });
    }
}
```

**Configuration Key Format:**
- Supports hierarchical keys with `:` separator (e.g., `"Section:SubSection:Key"`)
- Case-insensitive key matching
- Returns `null` if key not found (unless default value provided)

**Common Configuration Patterns:**
```csharp
// External service endpoints
var apiUrl = GetConfigValue("Services:PaymentApi:Url");

// Feature flags
var featureEnabled = GetConfigValue<bool>("Features:NewWorkflowEngine");

// Retry policies
var maxRetries = GetConfigValue<int>("Resilience:MaxRetries");
var backoffSeconds = GetConfigValue<double>("Resilience:BackoffSeconds");

// Business rules
var creditLimit = GetConfigValue<decimal>("BusinessRules:MaxCreditLimit");
```

### Dynamic Object Helper Functions

Work safely with dynamic objects and JSON data:

```csharp
public class DynamicDataMapping : ScriptBase, IMapping
{
    public Task<ScriptResponse> InputHandler(WorkflowTask task, ScriptContext context)
    {
        var attributes = context.Attributes;
        
        // Check if properties exist before accessing
        if (HasProperty(attributes, "customerId"))
        {
            var customerId = GetPropertyValue(attributes, "customerId");
            LogInformation($"Processing customer: {customerId}");
        }
        
        // Safe navigation through nested objects
        if (HasProperty(attributes, "address") && 
            HasProperty(GetPropertyValue(attributes, "address"), "city"))
        {
            var city = GetPropertyValue(
                GetPropertyValue(attributes, "address"), 
                "city");
            LogDebug($"Customer city: {city}");
        }
        
        return Task.FromResult(new ScriptResponse { Data = "processed" });
    }
}
```

### Complete Example with All ScriptBase Features

```csharp
var scriptWithBase = @"
    using System;
    using System.Threading.Tasks;
    using System.Collections.Generic;
    
    public class AdvancedMapping : ScriptBase, IMapping
    {
        public Task<ScriptResponse> InputHandler(WorkflowTask task, ScriptContext context)
        {
            // Log execution start
            LogInformation($""Starting task execution: {task.Key}"");
            
            try
            {
                // Get configuration values
                var apiEndpoint = GetConfigValue(""ExternalApi:Endpoint"");
                var timeout = GetConfigValue<int>(""ExternalApi:Timeout"");
                
                LogDebug($""Using endpoint: {apiEndpoint}, timeout: {timeout}s"");
                
                // Get secret for authentication
                var apiKey = GetSecret(""dapr_store"", ""secret_store"", ""api_key"");
                
                // Check dynamic properties safely
                if (HasProperty(context.Attributes, ""customerId""))
                {
                    var customerId = GetPropertyValue(context.Attributes, ""customerId"");
                    LogTrace($""Processing for customer: {customerId}"");
                }
                
                var httpTask = (task as HttpTask)!;
                httpTask.Url = apiEndpoint;
                httpTask.Timeout = TimeSpan.FromSeconds(timeout);
                
                LogInformation(""Task prepared successfully"");
                
                return Task.FromResult(new ScriptResponse
                {
                    Data = new { status = ""prepared"" },
                    Headers = new Dictionary<string, string>
                    {
                        { ""Authorization"", ""Bearer "" + apiKey }
                    }
                });
            }
            catch (Exception ex)
            {
                LogError($""Task preparation failed: {ex.Message}"");
                throw;
            }
        }

        public Task<ScriptResponse> OutputHandler(ScriptContext context)
        {
            LogInformation(""Processing task output"");
            
            // Process response data
            var result = new
            {
                success = true,
                timestamp = DateTime.UtcNow,
                instanceId = context.Instance.Id
            };
            
            LogDebug($""Output processed: {result}"");
            
            return Task.FromResult(new ScriptResponse
            {
                Data = result,
                Headers = null
            });
        }
    }
";

var mappingInstance = await scriptEngine.CompileToInstanceAsync<IMapping>(scriptWithBase);
```

## Performance Optimization

### 1. Script Caching

The engine automatically caches compiled scripts:

```csharp
private static readonly ConcurrentDictionary<string, Script<object>> ScriptCache = new();

public async Task<object?> EvaluateAsync(string code, ...)
{
    if (!ScriptCache.TryGetValue(code, out var cachedScript))
    {
        cachedScript = CSharpScript.Create(code, scriptOptions);
        ScriptCache[code] = cachedScript;
    }
    
    var state = await cachedScript.RunAsync(globals, cancellationToken);
    return state.ReturnValue;
}
```

### 2. Lazy Reference Loading

Default references are loaded lazily:

```csharp
private static readonly Lazy<MetadataReference[]> DefaultReferences = new(() => new[]
{
    MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
    // ... other references
});
```

### 3. Optimized Compilation

Scripts are compiled with optimization enabled:

```csharp
public static ScriptOptions Default => ScriptOptions.Default
    .WithOptimizationLevel(OptimizationLevel.Release)
    .AddReferences(/* default references */)
    .AddImports(/* default usings */);
```

## Error Handling

### Compilation Errors

```csharp
try
{
    var instance = await scriptEngine.CompileToInstanceAsync<IMapping>(code);
}
catch (CompilationErrorException ex)
{
    // Handle compilation errors
    logger.LogError(ex, "Script compilation failed: {Errors}", ex.Diagnostics);
}
```

### Runtime Errors

```csharp
try
{
    var result = await scriptEngine.EvaluateAsync(code);
}
catch (Exception ex)
{
    // Handle runtime execution errors
    logger.LogError(ex, "Script execution failed");
}
```

## Integration with Task Executors

Script tasks integrate seamlessly with the task execution system:

```csharp
public sealed class ScriptTaskExecutor : ITaskExecutor
{
    public async Task<object?> ExecuteAsync(
        WorkflowTask task,
        string scriptCode,
        ScriptContext context,
        CancellationToken cancellationToken = default)
    {
        var scriptRunner = await ScriptEngine.CompileToInstanceAsync<IMapping>(
            scriptCode, 
            cancellationToken: cancellationToken);

        var response = await scriptRunner.OutputHandler(context);
        return response.Data;
    }
}
```

## Best Practices

### 1. Script Organization

- Keep scripts focused and single-purpose
- Use meaningful class and method names
- Include error handling in complex scripts

### 2. Performance Considerations

- Minimize heavy computations in scripts
- Use async methods for I/O operations
- Cache expensive lookups

### 3. Security Guidelines

- Always use DAPR secret store for sensitive data
- Validate input data in scripts
- Avoid hardcoded credentials

### 4. Testing Scripts

```csharp
[Test]
public async Task TestScriptExecution()
{
    var scriptCode = @"
        public class TestMapping : IMapping
        {
            public Task<ScriptResponse> InputHandler(WorkflowTask task, ScriptContext context)
            {
                return Task.FromResult(new ScriptResponse { Data = ""test"" });
            }

            public Task<ScriptResponse> OutputHandler(ScriptContext context)
            {
                return Task.FromResult(new ScriptResponse { Data = ""output"" });
            }
        }
    ";

    var mapping = await scriptEngine.CompileToInstanceAsync<IMapping>(scriptCode);
    var result = await mapping.InputHandler(mockTask, mockContext);
    
    Assert.Equal("test", result.Data);
}
```

## Configuration

### Module Registration

```csharp
public static IServiceCollection AddApplicationModule(this IServiceCollection services)
{
    // Scripting service
    services.AddScoped<IScriptEngine, ScriptEngine>();
    
    // DAPR client for script functions
    services.AddDapr();
    
    return services;
}
```

### Script Options Configuration

```csharp
services.Configure<ScriptOptions>(options =>
{
    options.WithOptimizationLevel(OptimizationLevel.Release);
    options.AddReferences(additionalReferences);
    options.AddImports(additionalUsings);
});
```

The scripting engine provides a powerful and flexible foundation for implementing dynamic business logic within workflows, enabling developers to create sophisticated workflow behaviors without requiring code recompilation or deployment.

## Development Tools

For efficient development of CSX scripts, use the automated development tools:

- **Workflow Development Automation**: Automatically converts CSX files to base64 encoded JSON
- **File Watching**: Real-time updates when CSX files change
- **VS Code Integration**: Keyboard shortcuts and tasks for rapid development

See [Workflow Development Automation](./workflow-development-automation.md) for detailed setup and usage instructions. 