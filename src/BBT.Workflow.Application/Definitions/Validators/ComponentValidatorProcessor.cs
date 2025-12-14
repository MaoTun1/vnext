using System.Text.Json;

namespace BBT.Workflow.Definitions.Validators;

/// <summary>
/// Processes component validation operations by delegating to appropriate validators.
/// This class acts as a coordinator that selects the correct validator based on the component type
/// and orchestrates the validation operation.
/// </summary>
/// <param name="validators">A collection of component validators available for validation.</param>
public sealed class ComponentValidatorProcessor(IEnumerable<IComponentValidator> validators)
{
    /// <summary>
    /// Validates a component by finding the appropriate validator and executing the validation.
    /// </summary>
    /// <param name="componentType">The component type identifier that determines which validator to use.</param>
    /// <param name="attributes">The JSON element containing the component attributes to be validated.</param>
    /// <returns>A <see cref="ComponentValidationResult"/> containing any validation errors.</returns>
    /// <exception cref="NotSupportedException">Thrown when no validator is found for the specified component type.</exception>
    public ComponentValidationResult Validate(string componentType, JsonElement attributes)
    {
        var validator = validators.FirstOrDefault(v => v.CanHandle(componentType));
        if (validator == null)
            throw new NotSupportedException($"No validator found for component type '{componentType}'.");

        return validator.Validate(attributes);
    }

    /// <summary>
    /// Attempts to validate a component, returning false if no validator is found.
    /// </summary>
    /// <param name="componentType">The component type identifier that determines which validator to use.</param>
    /// <param name="attributes">The JSON element containing the component attributes to be validated.</param>
    /// <param name="result">When this method returns, contains the validation result if a validator was found.</param>
    /// <returns>True if a validator was found and validation was performed; otherwise, false.</returns>
    public bool TryValidate(string componentType, JsonElement attributes, out ComponentValidationResult result)
    {
        var validator = validators.FirstOrDefault(v => v.CanHandle(componentType));
        if (validator == null)
        {
            result = ComponentValidationResult.Success();
            return false;
        }

        result = validator.Validate(attributes);
        return true;
    }
}
