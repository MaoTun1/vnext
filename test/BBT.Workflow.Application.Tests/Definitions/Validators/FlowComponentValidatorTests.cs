using System.Text.Json;
using BBT.Workflow.Definitions.Validators;
using BBT.Workflow.Runtime;
using Moq;
using Shouldly;
using Xunit;

namespace BBT.Workflow.Definitions.Validators;

/// <summary>
/// Unit tests for FlowComponentValidator
/// </summary>
public class FlowComponentValidatorTests
{
    private readonly Mock<WorkflowValidator> _mockWorkflowValidator;
    private readonly FlowComponentValidator _validator;

    public FlowComponentValidatorTests()
    {
        _mockWorkflowValidator = new Mock<WorkflowValidator>();
        _validator = new FlowComponentValidator(_mockWorkflowValidator.Object);
    }

    [Fact]
    public void CanHandle_ShouldReturnTrue_ForSysFlows()
    {
        // Act
        var result = _validator.CanHandle(RuntimeSysSchemaInfo.Flows);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void CanHandle_ShouldReturnFalse_ForOtherTypes()
    {
        // Assert
        _validator.CanHandle(RuntimeSysSchemaInfo.Tasks).ShouldBeFalse();
        _validator.CanHandle(RuntimeSysSchemaInfo.Views).ShouldBeFalse();
        _validator.CanHandle(RuntimeSysSchemaInfo.Functions).ShouldBeFalse();
        _validator.CanHandle(RuntimeSysSchemaInfo.Schemas).ShouldBeFalse();
        _validator.CanHandle(RuntimeSysSchemaInfo.Extensions).ShouldBeFalse();
        _validator.CanHandle("unknown").ShouldBeFalse();
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

    [Fact]
    public void Validate_ShouldDelegateToWorkflowValidator_ForValidWorkflow()
    {
        // Arrange
        var workflowJson = """
        {
            "key": "test-flow",
            "domain": "test-domain",
            "version": "1.0.0",
            "flow": "sys-flows",
            "type": "F",
            "states": [
                { "key": "initial", "stateType": "I" },
                { "key": "completed", "stateType": "C" }
            ],
            "startTransition": {
                "key": "start",
                "target": "initial"
            }
        }
        """;
        var attributes = JsonDocument.Parse(workflowJson).RootElement;

        var workflowValidationResult = new WorkflowValidationResult();
        _mockWorkflowValidator.Setup(v => v.Validate(It.IsAny<Workflow>()))
            .Returns(workflowValidationResult);

        // Act
        var result = _validator.Validate(attributes);

        // Assert
        result.IsValid.ShouldBeTrue();
        _mockWorkflowValidator.Verify(v => v.Validate(It.IsAny<Workflow>()), Times.Once);
    }
}
