namespace BBT.Workflow.Definitions;

/// <summary>
/// Extension types
/// </summary>
public enum ExtensionType
{
    /// <summary>
    /// Extension that will work while recording samples are rotating in all streams.
    /// </summary>
    Global = 1,

    /// <summary>
    /// Extension that will work on all streams and when requesting recording samples.
    /// </summary>
    GlobalAndRequested = 2,

    /// <summary>
    /// Extension that will only work on the streams for which it is defined.
    /// </summary>
    DefinedFlows = 3,
    
    /// <summary>
    /// An extension that will only work on the streams it is defined for and when requested.
    /// </summary>
    DefinedFlowAndRequested = 4
}

/// <summary>
/// Extension scopes
/// </summary>
public enum ExtensionScope
{
    /// <summary>
    /// The entension works on {domain}/workflows/{workflow}/instances/{instance} endpoint
    /// </summary>
    GetInstance = 1,

    /// <summary>
    /// The entension works on  {domain}/workflows/{workflow}/instances endpoint
    /// </summary>
    GetAllInstances = 2,
    
    /// <summary>
    /// The entension works on  {domain}/workflows/{workflow}/instances/{instance}/transitions endpoint
    /// </summary>
    GetHistoryTransition = 2,

    /// <summary>
    /// The entension works on  all get endpoints
    /// </summary>
    Everywhere = 3
}
