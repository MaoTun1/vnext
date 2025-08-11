using System.Linq;
using System.Text.Json;
using BBT.Aether.Validation;
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
    public void Validate_ValidData_DoesNotThrowException()
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

        // Act & Assert
        var exception = Record.Exception(() => _validator.Validate(schema, data));
        Assert.Null(exception);
    }

    [Fact]
    public void Validate_InvalidData_ThrowsAetherValidationException()
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

        // Act & Assert
        var exception = Assert.Throws<AetherValidationException>(() => _validator.Validate(schema, data));
        Assert.NotEmpty(exception.ValidationErrors);
        Assert.Contains(exception.ValidationErrors, vr => vr.MemberNames.Contains("required") ||  vr.MemberNames.Contains("age"));
    }

    [Fact]
    public void Validate_NullDataWithRequiredSchema_ThrowsAetherValidationException()
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

        // Act & Assert
        var exception = Assert.Throws<AetherValidationException>(() => _validator.Validate(schema, null));
        Assert.NotEmpty(exception.ValidationErrors);
    }
}