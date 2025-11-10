namespace BBT.Workflow.Scripting;

public interface ITransitionMapping 
{
    /// <summary>
    /// Handles input data binding and task configuration before task execution.
    /// This method is called prior to executing the workflow task and allows for:
    /// - Modifying task parameters and configuration
    /// - Validating input data
    /// - Preparing audit information
    /// - Transforming data for task consumption
    /// </summary>
    /// <param name="task">
    /// The WorkflowTask object that will be executed. This object can be modified directly
    /// to change task behavior, endpoint URLs, headers, or other configuration parameters.
    /// </param>
    /// <param name="context">
    /// The ScriptContext containing workflow state, instance data, headers, route values,
    /// and other contextual information needed for input processing.
    /// </param>
    /// <returns>
    /// A ScriptResponse containing audit data and metadata about the input processing.
    /// The response data is logged for task audit purposes and can include:
    /// - Input validation results
    /// - Data transformation logs
    /// - Processing timestamps
    /// - Custom audit information
    /// </returns>
    /// <remarks>
    /// <para>
    /// The InputHandler is invoked during the task preparation phase and should:
    /// </para>
    /// <list type="bullet">
    /// <item><description>Validate and transform input data as needed</description></item>
    /// <item><description>Configure the WorkflowTask object for execution</description></item>
    /// <item><description>Generate comprehensive audit information</description></item>
    /// <item><description>Handle any input-related errors gracefully</description></item>
    /// </list>
    /// <para>
    /// Common use cases include dynamic endpoint generation, input validation,
    /// authentication token preparation, and custom header configuration.
    /// </para>
    /// </remarks>
    Task<dynamic> Handler(
        ScriptContext context);
}