using System.Text.Json;

namespace BBT.Workflow.Definitions.Validators;

/// <summary>
/// Defines a contract for validating workflow components.
/// Implementations of this interface are responsible for validating specific component types
/// such as flows, tasks, views, functions, schemas, and extensions.
/// </summary>
public interface IComponentValidator
{
    /// <summary>
    /// Determines whether this validator can handle the specified component type.
    /// </summary>
    /// <param name="componentType">The component type identifier to check (e.g., "sys-flows", "sys-tasks").</param>
    /// <returns>True if this validator can handle the component type; otherwise, false.</returns>
    bool CanHandle(string componentType);

    /// <summary>
    /// Validates the component attributes.
    /// </summary>
    /// <param name="attributes">The JSON element containing the component attributes to validate.</param>
    /// <returns>A <see cref="ComponentValidationResult"/> containing any validation errors.</returns>
    ComponentValidationResult Validate(JsonElement attributes);
}
