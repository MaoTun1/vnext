namespace BBT.Workflow.Definitions;

/// <summary>
/// URL template constants for instance-related API endpoints.
/// These templates follow the RESTful API design pattern and are used for building
/// consistent URL structures across the workflow instance management system.
/// </summary>
public static class InstanceUrlTemplates
{
    /// <summary>
    /// URL template for instance endpoints.
    /// Format: /{domain}/workflows/{workflow}/instances/{instance}
    /// </summary>
    public const string Instance = "/{0}/workflows/{1}/instances/{2}";

    /// <summary>
    /// URL template for instance list endpoints.
    /// Format: /{domain}/workflows/{workflow}/instances/{instance}
    /// </summary>
    public const string InstanceList = "/{0}/workflows/{1}/instances";

    /// <summary>
    /// URL template for instance history endpoints.
    /// Format: /{domain}/workflows/{workflow}/instances/{instance}
    /// </summary>
    public const string InstanceHistory = "/{0}/workflows/{1}/instances/{2}/transitions";

    /// <summary>
    /// URL template for instance transition endpoints.
    /// Format: /{domain}/workflows/{workflow}/instances/{instanceId}/transitions/{transitionKey}
    /// </summary>
    public const string Transition = "/{0}/workflows/{1}/instances/{2}/transitions/{3}";

    /// <summary>
    /// URL template for instance state endpoints.
    /// Format: /{domain}/workflows/{workflow}/instances/{instanceId}/functions/state
    /// </summary>
    public const string State = "/{0}/workflows/{1}/instances/{2}/functions/state";

    /// <summary>
    /// URL template for instance data endpoints.
    /// Format: /{domain}/workflows/{workflow}/instances/{instanceId}/functions/data
    /// </summary>
    public const string Data = "/{0}/workflows/{1}/instances/{2}/functions/data";

    /// <summary>
    /// URL template for instance data endpoints with extensions.
    /// Format: /{domain}/workflows/{workflow}/instances/{instanceId}/functions/data?extensions={extensions}
    /// </summary>
    public const string DataWithExtensions = "/{0}/workflows/{1}/instances/{2}/functions/data?extensions={3}";

    /// <summary>
    /// URL template for instance view endpoints.
    /// Format: /{domain}/workflows/{workflow}/instances/{instanceId}/functions/view
    /// </summary>
    public const string View = "/{0}/workflows/{1}/instances/{2}/functions/view";

    /// <summary>
    /// URL template for start instance endpoints.
    /// Format: /{domain}/workflows/{workflow}/instances/{instanceId}/start
    /// </summary>
    public const string Start = "/{0}/workflows/{1}/instances/start";

    /// <summary>
    /// URL template for start sub instance endpoints.
    /// Format: /{domain}/workflows/{workflow}/sub/instances/start
    /// </summary>
    public const string StartSub = "/{0}/workflows/{1}/sub/instances/start";

    /// <summary>
    /// URL template for complete instance endpoints.
    /// Format: /{domain}/workflows/{workflow}/instances/{instance}/complete
    /// </summary>
    public const string Complete = "/{0}/workflows/{1}/instances/{2}/complete";
    
    /// <summary>
    /// URL template for function list endpoints.
    /// Format: /{domain}/workflows/{workflow}/functions/{function}
    /// </summary>
    public const string FunctionList = "/{0}/workflows/{1}/functions/{2}";
}
