using BBT.Workflow.Caching;
using BBT.Workflow.Definitions;
using BBT.Workflow.Runtime;
using BBT.Workflow.Scripting;
using System.Extensions;
using System.Text;
using System.Text.Json;
using BBT.Workflow.Tasks;
using BBT.Workflow.Schemas;

namespace BBT.Workflow.Extentions;

/// <summary>
/// Implementation of extension processing service
/// </summary>
public sealed class InstanceExtensionService(
    IComponentCacheStore componentCacheStore,
    ITaskOrchestrationService taskExecutionService,
    IRuntimeInfoProvider runtimeInfoProvider,
    ICurrentSchema currentSchema) : IInstanceExtensionService
{
    public async Task<Dictionary<string, object>> ProcessExtensionsAsync(
        string[]? extensionRequested,
        ScriptContext scriptContext,
        Definitions.Workflow workflow,
        ExtensionScope currentScope,
        CancellationToken cancellationToken = default)
    {
        var responseExtension = new Dictionary<string, object>();
        var executedExtensionKeys = new HashSet<string>();

        // Process core system extensions first (runtime-wide, always included)
        await ProcessCoreExtensionsAsync(
            scriptContext,
            currentScope,
            responseExtension,
            executedExtensionKeys,
            cancellationToken);

        // Process workflow-specific extensions (excluding already executed core extensions)
        await ProcessWorkflowExtensionsAsync(
            extensionRequested,
            scriptContext,
            workflow,
            currentScope,
            responseExtension,
            executedExtensionKeys,
            cancellationToken);

        return responseExtension;
    }

    private async Task ProcessWorkflowExtensionsAsync(
        string[]? extensionRequested,
        ScriptContext scriptContext,
        Definitions.Workflow workflow,
        ExtensionScope currentScope,
        Dictionary<string, object> responseExtension,
        HashSet<string> executedExtensionKeys,
        CancellationToken cancellationToken)
    {
        // Get extension references from workflow and fetch actual Extension objects
        var extensionReferences = workflow.Extensions.ToList();
        var extensions = await FetchExtensionsFromReferencesAsync(extensionReferences, cancellationToken);

        // Filter out extensions that were already executed as core extensions
        var filteredExtensions = extensions
            .Where(ext => !executedExtensionKeys.Contains(ext.Key))
            .ToList();

        await ExecuteExtensions(
            extensionRequested,
            scriptContext,
            filteredExtensions,
            currentScope,
            responseExtension,
            executedExtensionKeys,
            cancellationToken);
    }

    /// <summary>
    /// Processes core extensions that are runtime-wide and always included in instance responses.
    /// Core extensions provide essential data like state, createBy, etc. that should be available
    /// in every instance response regardless of specific extension requests.
    /// </summary>
    private async Task ProcessCoreExtensionsAsync(
        ScriptContext scriptContext,
        ExtensionScope currentScope,
        Dictionary<string, object> responseExtension,
        HashSet<string> executedExtensionKeys,
        CancellationToken cancellationToken)
    {
        try
        {
            // Get all runtime-wide core extensions from cache
            var coreExtensions = await GetCoreExtensionsAsync(cancellationToken);

            if (coreExtensions.Count > 0)
            {
                await ExecuteCoreExtensions(
                    scriptContext,
                    coreExtensions,
                    currentScope,
                    responseExtension,
                    executedExtensionKeys,
                    cancellationToken);
            }
        }
        catch
        {
            // Core extensions not available - this is acceptable
            // The system should continue to work even if no core extensions are defined
        }
    }

    /// <summary>
    /// Retrieves all core extensions from the cache.
    /// Core extensions are identified by Global or GlobalAndRequested ExtensionType.
    /// These extensions are runtime-wide and should be executed for all instances.
    /// </summary>
    private async Task<List<Extension>> GetCoreExtensionsAsync(CancellationToken cancellationToken)
    {
        using (currentSchema.Change(RuntimeSysSchemaInfo.Extensions))
        {
            var coreExtensions = new List<Extension>();

            try
            {
                // Query all extensions from cache and filter for core extensions
                // Core extensions are those with Global or GlobalAndRequested type
                var allExtensions = await componentCacheStore.GetAllExtensionsAsync(
                    runtimeInfoProvider.Domain,
                    cancellationToken);

                coreExtensions.AddRange(allExtensions.Where(ext =>
                    ext.Type == ExtensionType.Global ||
                    ext.Type == ExtensionType.GlobalAndRequested));
            }
            catch
            {
                // No extensions found or cache error - continue with empty list
            }

            return coreExtensions;
        }
    }

    /// <summary>
    /// Executes core extensions which are always processed regardless of extension requests.
    /// Core extensions provide essential runtime data like state, createBy, etc.
    /// </summary>
    private async Task ExecuteCoreExtensions(
        ScriptContext scriptContext,
        List<Extension> coreExtensions,
        ExtensionScope currentScope,
        Dictionary<string, object> responseExtension,
        HashSet<string> executedExtensionKeys,
        CancellationToken cancellationToken)
    {
        foreach (Extension extension in coreExtensions)
        {
            if (extension.Task != null && extension.ShouldExecute(null, currentScope))
            {
                await taskExecutionService.ExecuteAsync(
                    new List<OnExecuteTask>() { extension.Task },
                    null,
                    TaskTrigger.Extension,
                    scriptContext,
                    cancellationToken);
                var variableKeyExtension = extension.Key.ToVariableName();
                var variableKeyTask = extension.Task.Task.Key.ToVariableName();
                if (scriptContext.TaskResponse.TryGetValue(variableKeyTask, out var value))
                {
                    // Try to extract Data from ScriptResponse if available
                    try
                    {
                        // Try to extract data property from JsonElement
                        responseExtension[variableKeyExtension] = value is JsonElement jsonElement &&
                                                                  jsonElement.TryGetProperty("data",
                                                                      out var dataProperty)
                            ? dataProperty
                            : value!;
                    }
                    catch
                    {
                        // If extraction fails, use the original value
                        responseExtension[variableKeyExtension] = value!;
                    }

                    executedExtensionKeys.Add(extension.Key); // Track executed extension
                }
            }
        }
    }

    private async Task<List<Extension>> FetchExtensionsFromReferencesAsync(
        List<IReference> extensionReferences,
        CancellationToken cancellationToken)
    {
        if (!extensionReferences.Any())
            return new List<Extension>();

        // Create tasks for parallel execution
        var extensionTasks = extensionReferences.Select(async reference =>
        {
            try
            {
                return await componentCacheStore.GetExtensionAsync(reference, cancellationToken);
            }
            catch
            {
                // Extension not found in cache - return null to indicate failure
                // This can happen if an extension reference is defined but the actual extension doesn't exist
                return null;
            }
        });

        // Execute all extension fetching tasks in parallel
        var extensionResults = await Task.WhenAll(extensionTasks);

        // Filter out null results (failed extension fetches) and return valid extensions
        return extensionResults.Where(ext => ext != null).ToList()!;
    }

    private async Task ExecuteExtensions(
        string[]? extensionRequested,
        ScriptContext scriptContext,
        List<Extension> extensions,
        ExtensionScope currentScope,
        Dictionary<string, object> responseExtension,
        HashSet<string> executedExtensionKeys,
        CancellationToken cancellationToken)
    {
        var tasks = extensions
            .Where(ext => ext.Task != null && ext.ShouldExecute(extensionRequested, currentScope))
            .Select(ext => ext.Task);

        if (!tasks.Any()) return;

        await taskExecutionService.ExecuteAsync(
            tasks,
            null,
            TaskTrigger.Extension,
            scriptContext,
            cancellationToken);

        // Merge task responses into extension dictionary using variable naming format
        foreach (var extension in extensions)
        {
            var variableKeyExtension = extension.Key.ToVariableName();
            var variableKeyTask = extension.Task.Task.Key.ToVariableName();
            if (scriptContext.TaskResponse.TryGetValue(variableKeyTask, out var value))
            {
                // Try to extract Data from ScriptResponse if available
                try
                {
                    // Try to extract data property from JsonElement
                    responseExtension[variableKeyExtension] = value is JsonElement jsonElement &&
                                                              jsonElement.TryGetProperty("data", out var dataProperty)
                        ? dataProperty
                        : value!;
                }
                catch
                {
                    // If extraction fails, use the original value
                    responseExtension[variableKeyExtension] = value!;
                }

                executedExtensionKeys.Add(extension.Key); // Track executed extension
            }
        }
    }
}