using System.Linq;
using System.Text.Json;
using BBT.Workflow.Definitions.Validators;
using BBT.Workflow.Runtime;
using Shouldly;
using Xunit;

namespace BBT.Workflow.Definitions.Validators;

/// <summary>
/// Unit tests for TaskComponentValidator
/// </summary>
public class TaskComponentValidatorTests
{
    private readonly TaskComponentValidator _validator;

    public TaskComponentValidatorTests()
    {
        _validator = new TaskComponentValidator();
    }

    [Fact]
    public void CanHandle_ShouldReturnTrue_ForSysTasks()
    {
        // Act
        var result = _validator.CanHandle(RuntimeSysSchemaInfo.Tasks);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void CanHandle_ShouldReturnFalse_ForOtherTypes()
    {
        // Assert
        _validator.CanHandle(RuntimeSysSchemaInfo.Flows).ShouldBeFalse();
        _validator.CanHandle(RuntimeSysSchemaInfo.Views).ShouldBeFalse();
        _validator.CanHandle(RuntimeSysSchemaInfo.Functions).ShouldBeFalse();
        _validator.CanHandle(RuntimeSysSchemaInfo.Schemas).ShouldBeFalse();
        _validator.CanHandle(RuntimeSysSchemaInfo.Extensions).ShouldBeFalse();
        _validator.CanHandle("unknown").ShouldBeFalse();
    }

    [Fact]
    public void Validate_ShouldReturnSuccess_ForValidTask()
    {
        // Arrange
        var taskJson = """
        {
            "type": "6",
            "config": {
                "url": "https://example.com",
                "method": "GET"
            }
        }
        """;
        var attributes = JsonDocument.Parse(taskJson).RootElement;

        // Act
        var result = _validator.Validate(attributes);

        // Assert
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_ShouldReturnError_ForInvalidTaskType()
    {
        // Arrange
        var taskJson = """
        {
            "type": "InvalidType",
            "config": {}
        }
        """;
        var attributes = JsonDocument.Parse(taskJson).RootElement;

        // Act
        var result = _validator.Validate(attributes);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.ValidationErrors.ShouldContain(e => e.MemberNames.Contains("WorkflowTask.Type"));
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
