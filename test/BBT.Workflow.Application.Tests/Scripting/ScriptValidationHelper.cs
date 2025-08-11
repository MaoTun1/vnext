using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BBT.Workflow.Definitions;
using BBT.Workflow.Instances;
using BBT.Workflow.Runtime;
using BBT.Workflow.Scripting.Functions;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using IRuntimeInfoProvider = BBT.Workflow.Runtime.IRuntimeInfoProvider;
using Dapr.Client;

namespace BBT.Workflow.Scripting;

/// <summary>
/// Utility class for validating workflow scripts quickly and easily.
/// This helper can be used in console applications, interactive environments,
/// or anywhere you need to validate scripts outside of formal testing.
/// </summary>
public static class ScriptValidationHelper
{
    private static IScriptEngine? _scriptEngine;
    private static readonly object _lockObject = new();

    /// <summary>
    /// Initialize the script validation helper with necessary dependencies
    /// </summary>
    public static void Initialize(IServiceProvider serviceProvider)
    {
        lock (_lockObject)
        {
            _scriptEngine = serviceProvider.GetRequiredService<IScriptEngine>();
            
            // Setup mock DaprClient if not already configured
            var mockDaprClient = new Mock<DaprClient>();
            mockDaprClient
                .Setup(x => x.GetSecretAsync(
                    It.IsAny<string>(), 
                    It.IsAny<string>(), 
                    It.IsAny<IReadOnlyDictionary<string, string>>(), 
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Dictionary<string, string> { { "test_key", "mock_secret_value" } });

            ScriptHelper.SetDaprClient(mockDaprClient.Object);
        }
    }

    /// <summary>
    /// Quick validation of a script file with console output
    /// </summary>
    /// <param name="scriptFilePath">Path to the script file</param>
    /// <param name="verbose">Enable verbose output</param>
    /// <returns>True if validation passed, false otherwise</returns>
    public static async Task<bool> ValidateScriptFileAsync(string scriptFilePath, bool verbose = true)
    {
        EnsureInitialized();

        try
        {
            if (!File.Exists(scriptFilePath))
            {
                Console.WriteLine($"❌ Script file not found: {scriptFilePath}");
                return false;
            }

            var scriptCode = await File.ReadAllTextAsync(scriptFilePath);
            var fileName = Path.GetFileName(scriptFilePath);
            
            // Clean script for testing
            scriptCode = CleanScriptForTesting(scriptCode);
            
            if (verbose)
            {
                Console.WriteLine($"🔍 Validating script: {fileName}");
                Console.WriteLine($"📁 Path: {scriptFilePath}");
                Console.WriteLine("─────────────────────────────────────");
            }

            var result = await ValidateScriptCodeInternalAsync(scriptCode, fileName, verbose);

            if (verbose)
            {
                Console.WriteLine("─────────────────────────────────────");
                Console.WriteLine(result.IsValid ? 
                    $"✅ Overall result: {fileName} is valid!" : 
                    $"❌ Overall result: {fileName} has issues.");
            }

            return result.IsValid;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Unexpected error validating {scriptFilePath}: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Quick validation of script code string with console output
    /// </summary>
    /// <param name="scriptCode">The script code to validate</param>
    /// <param name="scriptName">Optional name for the script</param>
    /// <param name="verbose">Enable verbose output</param>
    /// <returns>True if validation passed, false otherwise</returns>
    public static async Task<bool> ValidateScriptCodeAsync(string scriptCode, string scriptName = "InlineScript", bool verbose = true)
    {
        EnsureInitialized();

        try
        {
            // Clean script for testing
            scriptCode = CleanScriptForTesting(scriptCode);
            
            if (verbose)
            {
                Console.WriteLine($"🔍 Validating script: {scriptName}");
                Console.WriteLine("─────────────────────────────────────");
            }

            var result = await ValidateScriptCodeInternalAsync(scriptCode, scriptName, verbose);

            if (verbose)
            {
                Console.WriteLine("─────────────────────────────────────");
                Console.WriteLine(result.IsValid ? 
                    $"✅ Overall result: {scriptName} is valid!" : 
                    $"❌ Overall result: {scriptName} has issues.");
            }

            return result.IsValid;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Unexpected error validating {scriptName}: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Validate all scripts in a directory
    /// </summary>
    /// <param name="directoryPath">Directory containing script files</param>
    /// <param name="pattern">File pattern (default: *.csx)</param>
    /// <param name="verbose">Enable verbose output</param>
    /// <returns>Dictionary with script names and their validation results</returns>
    public static async Task<Dictionary<string, bool>> ValidateDirectoryAsync(
        string directoryPath, 
        string pattern = "*.csx", 
        bool verbose = true)
    {
        EnsureInitialized();

        var results = new Dictionary<string, bool>();

        if (!Directory.Exists(directoryPath))
        {
            Console.WriteLine($"❌ Directory not found: {directoryPath}");
            return results;
        }

        var scriptFiles = Directory.GetFiles(directoryPath, pattern, SearchOption.AllDirectories);

        if (verbose)
        {
            Console.WriteLine($"🔍 Found {scriptFiles.Length} script files in {directoryPath}");
            Console.WriteLine("═════════════════════════════════════");
        }

        foreach (var scriptFile in scriptFiles)
        {
            var fileName = Path.GetFileName(scriptFile);
            var isValid = await ValidateScriptFileAsync(scriptFile, verbose);
            results[fileName] = isValid;

            if (verbose)
            {
                Console.WriteLine();
            }
        }

        if (verbose)
        {
            Console.WriteLine("═════════════════════════════════════");
            Console.WriteLine($"📊 Summary: {results.Values.Count(v => v)}/{results.Count} scripts passed validation");
            
            var failedScripts = results.Where(kvp => !kvp.Value).Select(kvp => kvp.Key).ToList();
            if (failedScripts.Any())
            {
                Console.WriteLine($"❌ Failed scripts: {string.Join(", ", failedScripts)}");
            }
        }

        return results;
    }

    private static void EnsureInitialized()
    {
        if (_scriptEngine == null)
        {
            throw new InvalidOperationException(
                "ScriptValidationHelper is not initialized. " +
                "Call ScriptValidationHelper.Initialize(serviceProvider) first.");
        }
    }

    private static async Task<ScriptValidationResult> ValidateScriptCodeInternalAsync(
        string scriptCode, 
        string scriptName, 
        bool verbose)
    {
        try
        {
            // Basic compilation test
            var references = new List<MetadataReference>
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(IMapping).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(ScriptHelper).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Text.Json.JsonElement).Assembly.Location)
            };

            var usings = new[]
            {
                "System",
                "System.Threading.Tasks",
                "System.Collections.Generic",
                "System.Text.Json",
                "BBT.Workflow.Scripting",
                "BBT.Workflow.Definitions",
                "BBT.Workflow.Scripting.Functions"
            };

            if (verbose)
            {
                Console.WriteLine("🔧 Attempting compilation as IMapping...");
            }

            // Try to compile as IMapping first
            try
            {
                var mappingInstance = await _scriptEngine!.CompileToInstanceAsync<IMapping>(
                    scriptCode, references, usings);

                if (verbose)
                {
                    Console.WriteLine("✅ IMapping compilation successful!");
                    Console.WriteLine("🧪 Testing execution with mock data...");
                }

                // Test basic execution
                var testResult = await TestMappingExecutionInternal(mappingInstance, scriptName, verbose);
                
                return ScriptValidationResult.Success(
                    $"✅ {scriptName} compiled and executed successfully as IMapping", 
                    mappingInstance, 
                    testResult);
            }
            catch (Exception mappingEx)
            {
                if (verbose)
                {
                    Console.WriteLine($"⚠️ IMapping compilation failed: {mappingEx.Message}");
                    Console.WriteLine("🔧 Attempting general script compilation...");
                }

                // If IMapping fails, try as general compilation
                try
                {
                    var generalResult = await _scriptEngine!.EvaluateAsync(scriptCode);
                    
                    if (verbose)
                    {
                        Console.WriteLine("✅ General script compilation successful!");
                    }

                    return ScriptValidationResult.Success(
                        $"✅ {scriptName} compiled as general script", 
                        executionResult: generalResult);
                }
                catch (Exception generalEx)
                {
                    var errorMessage = $"❌ {scriptName} compilation failed:\n" +
                                     $"IMapping Error: {mappingEx.Message}\n" +
                                     $"General Error: {generalEx.Message}";

                    if (verbose)
                    {
                        Console.WriteLine(errorMessage);
                    }

                    return ScriptValidationResult.Failed(errorMessage);
                }
            }
        }
        catch (Exception ex)
        {
            var errorMessage = $"❌ {scriptName} validation error: {ex.Message}";
            
            if (verbose)
            {
                Console.WriteLine(errorMessage);
            }

            return ScriptValidationResult.Failed(errorMessage);
        }
    }

    private static async Task<object?> TestMappingExecutionInternal(
        IMapping mapping, 
        string scriptName, 
        bool verbose)
    {
        try
        {
            // Create mock HTTP task
            var httpTask = HttpTask.CreateEmpty();
            httpTask.Url = "https://api.example.com/test/{contract_code}";
            httpTask.Method = "POST";

            // Create mock headers
            var mockHeaders = new Dictionary<string, string>
            {
                ["Accept-Language"] = "tr-TR",
                ["X-Request-Id"] = "test-request-123",
                ["user_reference"] = "test-user",
                ["ClientId"] = "test-client",
                ["business_line"] = "retail"
            };

            // Create mock instance data
            var mockInstanceData = new
            {
                entityData = new
                {
                    subProductCode = "VDLGLDR",
                    customerNumber = "12345",
                    accountType = "savings"
                }
            };

            // Build script context
            var context = new ScriptContext.Builder()
                .SetWorkflow(CreateMockWorkflow())
                .SetInstance(CreateMockInstance(mockInstanceData))
                .SetTransition(CreateMockTransition())
                .SetRuntime(Mock.Of<IRuntimeInfoProvider>())
                .SetHeaders(mockHeaders)
                .SetBody(mockInstanceData)
                .SetDefinitions(new Dictionary<string, object>())
                .Build();

            // Test InputHandler
            if (verbose) Console.WriteLine("🧪 Testing InputHandler...");
            var inputResponse = await mapping.InputHandler(httpTask, context);
            if (verbose) Console.WriteLine("✅ InputHandler executed successfully");

            // Test OutputHandler with mock response
            if (verbose) Console.WriteLine("🧪 Testing OutputHandler...");
            context.SetBody(new { StatusCode = 200, Data = new { result = "success" } });
            var outputResponse = await mapping.OutputHandler(context);
            if (verbose) Console.WriteLine("✅ OutputHandler executed successfully");

            return new { Input = inputResponse, Output = outputResponse };
        }
        catch (Exception ex)
        {
            if (verbose)
            {
                Console.WriteLine($"⚠️ {scriptName} execution test failed: {ex.Message}");
            }
            return null;
        }
    }

    /// <summary>
    /// Cleans script code for testing by commenting out #load directives
    /// and adding necessary using statements
    /// </summary>
    /// <param name="scriptCode">Original script code</param>
    /// <returns>Cleaned script code ready for testing</returns>
    private static string CleanScriptForTesting(string scriptCode)
    {
        if (string.IsNullOrWhiteSpace(scriptCode))
            return scriptCode;

        var lines = scriptCode.Split(new[] { '\r', '\n' }, StringSplitOptions.None);
        var cleanedLines = new List<string>();
        
        // Add necessary using statements at the top
        var hasSystemUsing = false;
        var hasJsonUsing = false;
        
        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();
            
            // Comment out #load directives
            if (trimmedLine.StartsWith("#load"))
            {
                cleanedLines.Add("//" + line);
                continue;
            }
            
            // Check for existing using statements
            if (trimmedLine.StartsWith("using System;"))
                hasSystemUsing = true;
            if (trimmedLine.StartsWith("using System.Text.Json;"))
                hasJsonUsing = true;
                
            cleanedLines.Add(line);
        }
        
        // Insert missing using statements after the first commented #load line
        var insertIndex = 0;
        for (int i = 0; i < cleanedLines.Count; i++)
        {
            if (cleanedLines[i].TrimStart().StartsWith("//#load"))
            {
                insertIndex = i + 1;
                break;
            }
        }
        
        var usingsToAdd = new List<string>();
        if (!hasSystemUsing)
            usingsToAdd.Add("using System;");
        if (!hasJsonUsing)
            usingsToAdd.Add("using System.Text.Json;");
            
        if (usingsToAdd.Any())
        {
            // Add empty line after #load comments
            if (insertIndex > 0 && !string.IsNullOrWhiteSpace(cleanedLines[insertIndex]))
                usingsToAdd.Insert(0, "");
                
            cleanedLines.InsertRange(insertIndex, usingsToAdd);
        }
        
        return string.Join(Environment.NewLine, cleanedLines);
    }

    // Helper methods to create mock objects
    private static Definitions.Workflow CreateMockWorkflow()
    {
        // This would need to be implemented based on your Workflow factory
        // For now, return a basic mock
        return Mock.Of<Definitions.Workflow>();
    }

    private static Instance CreateMockInstance(object data)
    {
        // This would need to be implemented based on your Instance factory
        // For now, return a basic mock with the data
        var instance = Mock.Of<Instance>();
        // Set up the mock to return the provided data
        return instance;
    }

    private static Transition CreateMockTransition()
    {
        var transition = Mock.Of<Transition>();
        Mock.Get(transition).Setup(t => t.Key).Returns("test-transition");
        return transition;
    }
}

/// <summary>
/// Internal result for validation operations
/// </summary>
internal class ValidationResult
{
    public bool IsValid { get; set; }
    public string Message { get; set; } = "";
    public string? ErrorMessage { get; set; }
    public IMapping? MappingInstance { get; set; }
    public object? ExecutionResult { get; set; }

    public static ValidationResult Success(string message, IMapping? mappingInstance = null, object? executionResult = null)
        => new() { IsValid = true, Message = message, MappingInstance = mappingInstance, ExecutionResult = executionResult };

    public static ValidationResult Failed(string errorMessage)
        => new() { IsValid = false, ErrorMessage = errorMessage, Message = "Validation failed" };
} 