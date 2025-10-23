using System.Linq;
using System.Text.Json;
using Xunit;

namespace BBT.Workflow.Validation;

public class JsonSchemaValidatorTests: DomainTestBase<DomainEntryPoint>
{
    private readonly IJsonSchemaValidator _validator;

    public JsonSchemaValidatorTests()
    {
        _validator = GetRequiredService<IJsonSchemaValidator>();
    }

    [Fact]
    public void Validate_ValidData_ReturnsSuccess()
    {
        // Arrange
        var schemaJson = JsonSerializer.Serialize(new
        {
            type = "object",
            properties = new
            {
                name = new { type = "string" },
                age = new { type = "integer" }
            },
            required = new[] { "name", "age" }
        });

        var dataJson = JsonSerializer.Serialize(new
        {
            name = "John",
            age = 30
        });

        var schema = JsonDocument.Parse(schemaJson).RootElement;
        var data = JsonDocument.Parse(dataJson).RootElement;

        // Act
        var result = _validator.Validate(schema, data);

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Validate_InvalidData_ReturnsFailureWithValidationErrors()
    {
        // Arrange
        var schemaJson = JsonSerializer.Serialize(new
        {
            type = "object",
            properties = new
            {
                name = new { type = "string" },
                age = new { type = "integer" }
            },
            required = new[] { "name", "age" }
        });

        var dataJson = JsonSerializer.Serialize(new
        {
            name = "John"
            // age missing
        });

        var schema = JsonDocument.Parse(schemaJson).RootElement;
        var data = JsonDocument.Parse(dataJson).RootElement;

        // Act
        var result = _validator.Validate(schema, data);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(WorkflowErrorCodes.ValidationErrors, result.Error.Code);
        Assert.NotNull(result.Error.ValidationErrors);
        Assert.NotEmpty(result.Error.ValidationErrors);
        Assert.Contains(result.Error.ValidationErrors, vr => vr.MemberNames.Contains("required") || vr.MemberNames.Contains("age"));
    }

    [Fact]
    public void Validate_NullDataWithRequiredSchema_ReturnsFailureWithValidationErrors()
    {
        // Arrange
        var schemaJson = JsonSerializer.Serialize(new
        {
            type = "object",
            properties = new
            {
                id = new { type = "string" }
            },
            required = new[] { "id" }
        });

        var schema = JsonDocument.Parse(schemaJson).RootElement;

        // Act
        var result = _validator.Validate(schema, null);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(WorkflowErrorCodes.ValidationErrors, result.Error.Code);
        Assert.NotNull(result.Error.ValidationErrors);
        Assert.NotEmpty(result.Error.ValidationErrors);
    }
}