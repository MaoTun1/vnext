using System.ComponentModel.DataAnnotations;
using BBT.Workflow.Definitions.Validators;
using Shouldly;
using Xunit;

namespace BBT.Workflow.Definitions.Validators;

/// <summary>
/// Unit tests for ComponentValidationResult
/// </summary>
public class ComponentValidationResultTests
{
    [Fact]
    public void IsValid_ShouldReturnTrue_WhenNoErrors()
    {
        // Arrange
        var result = new ComponentValidationResult();

        // Act & Assert
        result.IsValid.ShouldBeTrue();
        result.ValidationErrors.ShouldBeEmpty();
    }

    [Fact]
    public void IsValid_ShouldReturnFalse_WhenHasErrors()
    {
        // Arrange
        var result = new ComponentValidationResult();
        result.AddError(new ValidationResult("Test error", new[] { "TestField" }));

        // Act & Assert
        result.IsValid.ShouldBeFalse();
        result.ValidationErrors.Count.ShouldBe(1);
    }

    [Fact]
    public void AddError_WithMessage_ShouldAddValidationResult()
    {
        // Arrange
        var result = new ComponentValidationResult();

        // Act
        result.AddError("Error message", "Field1", "Field2");

        // Assert
        result.IsValid.ShouldBeFalse();
        result.ValidationErrors.Count.ShouldBe(1);
        result.ValidationErrors[0].ErrorMessage.ShouldBe("Error message");
        result.ValidationErrors[0].MemberNames.ShouldContain("Field1");
        result.ValidationErrors[0].MemberNames.ShouldContain("Field2");
    }

    [Fact]
    public void Success_ShouldReturnValidResult()
    {
        // Act
        var result = ComponentValidationResult.Success();

        // Assert
        result.IsValid.ShouldBeTrue();
        result.ValidationErrors.ShouldBeEmpty();
    }

    [Fact]
    public void FromWorkflowValidationResult_ShouldCopyErrors()
    {
        // Arrange
        var workflowResult = new WorkflowValidationResult();
        workflowResult.AddError(new ValidationResult("Workflow error 1", new[] { "Field1" }));
        workflowResult.AddError(new ValidationResult("Workflow error 2", new[] { "Field2" }));

        // Act
        var componentResult = ComponentValidationResult.FromWorkflowValidationResult(workflowResult);

        // Assert
        componentResult.IsValid.ShouldBeFalse();
        componentResult.ValidationErrors.Count.ShouldBe(2);
        componentResult.ValidationErrors.ShouldContain(e => e.ErrorMessage == "Workflow error 1");
        componentResult.ValidationErrors.ShouldContain(e => e.ErrorMessage == "Workflow error 2");
    }

    [Fact]
    public void FromWorkflowValidationResult_ShouldReturnValid_WhenNoErrors()
    {
        // Arrange
        var workflowResult = new WorkflowValidationResult();

        // Act
        var componentResult = ComponentValidationResult.FromWorkflowValidationResult(workflowResult);

        // Assert
        componentResult.IsValid.ShouldBeTrue();
        componentResult.ValidationErrors.ShouldBeEmpty();
    }
}
