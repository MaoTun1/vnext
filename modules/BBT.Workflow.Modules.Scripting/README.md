# BBT Workflow Scripting Module

This module provides the necessary infrastructure to run dynamic C# scripts within the BBT Workflow system.

## Features

- **Dynamic C# Script Compilation**: Compile and run C# code at runtime
- **Custom Functions**: Special functions available for use within scripts
- **Dapr Integration**: Secure data access via Dapr secret store
- **Caching**: Caching to improve script compilation performance

## Custom Functions

### GetSecret Functions

Functions used to securely retrieve secrets from the Dapr secret store:

#### Three Ways to Use Custom Functions

**1. Static ScriptHelper (Recommended for most cases)**
```csharp
var apiKey = ScriptHelper.GetSecret("dapr_store", "secret_store", "Asgard_ApiKey");
```

**2. ScriptBase Inheritance (Clean syntax for compiled scripts)**
```csharp
public class MyMapping : ScriptBase, IMapping
{
    public Task<ScriptResponse> InputHandler(WorkflowTask task, ScriptContext context)
    {
        var apiKey = GetSecret("dapr_store", "secret_store", "Asgard_ApiKey"); // Clean syntax!
        // ...
    }
}
```

**3. GlobalScriptFunctions (For simple script evaluations)**
```csharp
// Available in EvaluateAsync calls as global functions
string code = @"GetSecret(""dapr_store"", ""secret_store"", ""Asgard_ApiKey"")";
var result = await scriptEngine.EvaluateAsync<string>(code);
```

#### Function Signatures

#### `GetSecret(storeName, secretKey)`

Retrieves a single secret value (synchronous).

```csharp
var apiKey = ScriptHelper.GetSecret("dapr_store", "secret_store", "Asgard_ApiKey");
// OR (when inheriting from ScriptBase)
var apiKey = GetSecret("dapr_store", "secret_store", "Asgard_ApiKey");
```

#### `GetSecretAsync(storeName, secretKey)`

Retrieves a single secret value (asynchronous).

```csharp
var apiKey = await ScriptHelper.GetSecretAsync("dapr_store", "secret_store", "Asgard_ApiKey");
// OR (when inheriting from ScriptBase)
var apiKey = await GetSecretAsync("dapr_store", "secret_store", "Asgard_ApiKey");
```

#### `GetSecrets(storeName, secretStore)`

Retrieves bulk secret values (synchronous).

```csharp
var secrets = ScriptHelper.GetSecrets("dapr_store", "secret_store");
// OR (when inheriting from ScriptBase)
var secrets = GetSecrets("dapr_store", "secret_store");
```

#### `GetSecretsAsync(storeName, ...secretKeys)`

Retrieves bulk secret values (asynchronous).

```csharp
var secrets = await ScriptHelper.GetSecretsAsync("dapr_store", "secret_store");
// OR (when inheriting from ScriptBase)
var secrets = await GetSecretsAsync("dapr_store", "secret_store");
```

## Usage Example

```csharp
using System.Collections.Generic;
using System.Threading.Tasks;
using BBT.Workflow.Scripting;
using BBT.Workflow.Definitions;
using BBT.Workflow.Scripting.Functions;

public class MockMapping : IMapping
{
    public Task<ScriptResponse> InputHandler(WorkflowTask task, ScriptContext context)
    {
        var httpTask = (task as HttpTask)!;
        httpTask.Url = "https://httpbin.org/post/" + context.Transition.Key;
        httpTask.Method = "POST";
        
        return Task.FromResult(new ScriptResponse
        {
            Data = "Hello Input",
            Headers = new Dictionary<string, string>
            {
                {"ApiKey", ScriptHelper.GetSecret("dapr_store", "secret_store", "Asgard_ApiKey")}
            }
        });
    }

    public Task<ScriptResponse> OutputHandler(ScriptContext context)
    {
        return Task.FromResult(new ScriptResponse
        {
            Data = "Hello Output",
            Headers = null
        });
    }
}
```

## Configuration

### Secret Store Configuration

The Dapr secret store component is defined in the `etc/dapr/components/secretstore.yaml` file:

```yaml
apiVersion: dapr.io/v1alpha1
kind: Component
metadata:
  name: workflow-secretstore
spec:
  type: secretstores.hashicorp.vault
  version: v1
  metadata:
    - name: vaultAddr
      value: http://vnext-vault:8200
    - name: vaultToken
      value: "admin"
    # ... other configurations
```

### Application Startup

The `ScriptingInitializationService` automatically initializes the `ScriptHelper` with a `DaprClient` during application startup.

## Security

- All secret accesses are performed via Dapr
- Secret values are not stored in the script cache
- Secret values are not logged in case of errors

## Performance

- Scripts are cached after the initial compilation
- Async functions are recommended for I/O-bound operations
- Secret accesses involve network calls and should be used carefully

## Error Handling

- If the `DaprClient` is not initialized, an `InvalidOperationException is thrown
- `ArgumentException` is thrown for invalid parameter values
- Detailed error messages are provided for secret access errors