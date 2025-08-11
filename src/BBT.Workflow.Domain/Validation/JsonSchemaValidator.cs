using System.Text.Json;
using BBT.Aether.Validation;
using Json.Schema;

namespace BBT.Workflow.Validation;

/// <summary>
/// Provides JSON schema validation functionality using the Json.Schema library.
/// This sealed class implements IJsonSchemaValidator and validates JSON data against JSON Schema specifications.
/// Validation errors are converted to AetherValidationException with detailed error information.
/// </summary>
public sealed class JsonSchemaValidator : IJsonSchemaValidator
{
    /// <summary>
    /// Validates the given JSON data against the specified JSON schema.
    /// Uses hierarchical output format and requires format validation for comprehensive validation.
    /// If validation fails, detailed error information is collected and thrown as an exception.
    /// </summary>
    /// <param name="jsonSchema">JSON schema to be used for validation</param>
    /// <param name="data">JSON data to be validated. If null, an empty JSON object "{}" is used for validation</param>
    /// <exception cref="AetherValidationException">
    /// Thrown when the JSON data does not conform to the schema, containing detailed validation errors
    /// </exception>
    public void Validate(JsonElement jsonSchema, JsonElement? data)
    {
        var schema = JsonSchema.FromText(jsonSchema.GetRawText());
        var json = JsonDocument.Parse(data?.GetRawText() ?? "{}");

        var validationResult = schema.Evaluate(json.RootElement, new EvaluationOptions()
        {
            OutputFormat = OutputFormat.Hierarchical,
            RequireFormatValidation = true
        });
        if (validationResult.IsValid)
        {
            return;
        }

        var validationErrors = validationResult.ToValidationResults();

        throw new AetherValidationException(validationErrors);
    }
}