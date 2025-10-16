using System.Text.Json;
using Json.Schema;
using BBT.Workflow.Domain;

namespace BBT.Workflow.Validation;

/// <summary>
/// Provides JSON schema validation functionality using the Json.Schema library and Result Pattern.
/// This sealed class implements IJsonSchemaValidator and validates JSON data against JSON Schema specifications.
/// Validation errors are returned as Result with detailed field-level error information.
/// </summary>
public sealed class JsonSchemaValidator : IJsonSchemaValidator
{
    /// <summary>
    /// Validates the given JSON data against the specified JSON schema using Result Pattern.
    /// Uses hierarchical output format and requires format validation for comprehensive validation.
    /// Returns Result.Ok() on success, or Result.Fail() with detailed validation errors on failure.
    /// </summary>
    /// <param name="jsonSchema">JSON schema to be used for validation</param>
    /// <param name="data">JSON data to be validated. If null, an empty JSON object "{}" is used for validation</param>
    /// <returns>Result containing validation outcome. On failure, Error.ValidationErrors contains detailed field-level errors.</returns>
    public Result Validate(JsonElement jsonSchema, JsonElement? data)
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
            return Result.Ok();
        }

        var validationErrors = validationResult.ToValidationResults();

        return Result.Fail(
            Error.Validation(
                "schemaValidation",
                "JSON schema validation failed",
                validationErrors.AsReadOnly()));
    }
}