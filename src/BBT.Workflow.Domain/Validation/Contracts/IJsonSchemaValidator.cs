using System.Text.Json;
using BBT.Aether.Validation;

namespace BBT.Workflow.Validation;

public interface IJsonSchemaValidator
{
    /// <summary>
    /// Validates the given JSON data against the specified JSON schema.
    /// </summary>
    /// <param name="jsonSchema">JSON schema to be used for validation.</param>
    /// <param name="data">JSON data to be validated. In case of null, an empty JSON object is used..</param>
    /// <exception cref="AetherValidationException">
    /// If the JSON data does not conform to the schema, an exception is thrown containing validation errors.
    /// </exception>
    void Validate(JsonElement jsonSchema, JsonElement? data);
}