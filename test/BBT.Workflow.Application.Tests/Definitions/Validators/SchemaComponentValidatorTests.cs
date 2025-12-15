using System.Linq;
using System.Text.Json;
using BBT.Workflow.Definitions.Validators;
using BBT.Workflow.Runtime;
using Shouldly;
using Xunit;

namespace BBT.Workflow.Definitions.Validators;

/// <summary>
/// Unit tests for SchemaComponentValidator
/// </summary>
public class SchemaComponentValidatorTests
{
    private readonly SchemaComponentValidator _validator;

    public SchemaComponentValidatorTests()
    {
        _validator = new SchemaComponentValidator();
    }

    [Fact]
    public void CanHandle_ShouldReturnTrue_ForSysSchemas()
    {
        // Act
        var result = _validator.CanHandle(RuntimeSysSchemaInfo.Schemas);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void CanHandle_ShouldReturnFalse_ForOtherTypes()
    {
        // Assert
        _validator.CanHandle(RuntimeSysSchemaInfo.Flows).ShouldBeFalse();
        _validator.CanHandle(RuntimeSysSchemaInfo.Tasks).ShouldBeFalse();
        _validator.CanHandle(RuntimeSysSchemaInfo.Views).ShouldBeFalse();
        _validator.CanHandle(RuntimeSysSchemaInfo.Functions).ShouldBeFalse();
        _validator.CanHandle(RuntimeSysSchemaInfo.Extensions).ShouldBeFalse();
        _validator.CanHandle("unknown").ShouldBeFalse();
    }

    [Fact]
    public void Validate_ShouldReturnSuccess_ForValidSchema()
    {
        // Arrange
        var schemaJson = """
        {
            "type": "json-schema",
            "schema": {
                "type": "object",
                "properties": {
                    "name": { "type": "string" }
                }
            }
        }
        """;
        var attributes = JsonDocument.Parse(schemaJson).RootElement;

        // Act
        var result = _validator.Validate(attributes);

        // Assert
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_ShouldReturnError_ForMissingType()
    {
        // Arrange
        var schemaJson = """
        {
            "schema": {
                "type": "object"
            }
        }
        """;
        var attributes = JsonDocument.Parse(schemaJson).RootElement;

        // Act
        var result = _validator.Validate(attributes);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.ValidationErrors.ShouldContain(e => e.MemberNames.Contains("SchemaDefinition.Type"));
    }

    [Fact]
    public void Validate_ShouldReturnError_ForMissingSchema()
    {
        // Arrange
        var schemaJson = """
        {
            "type": "json-schema"
        }
        """;
        var attributes = JsonDocument.Parse(schemaJson).RootElement;

        // Act
        var result = _validator.Validate(attributes);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.ValidationErrors.ShouldContain(e => e.MemberNames.Contains("SchemaDefinition.Schema"));
    }

    [Fact]
    public void Validate_ShouldReturnError_ForInvalidJson()
    {
        // Arrange
        var invalidJson = JsonDocument.Parse("\"not an object\"").RootElement;

        // Act
        var result = _validator.Validate(invalidJson);

        // Assert
        result.IsValid.ShouldBeFalse();
    }
}
