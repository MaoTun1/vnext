using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using BBT.Workflow.Definitions;
using BBT.Workflow.Domain;
using BBT.Workflow.Shared;
using Xunit;

namespace BBT.Workflow.Domain;

public class WorkflowErrorsTests
{
    [Fact]
    public void InstanceAlreadyExists_ShouldCreateConflictError()
    {
        // Arrange
        var instanceKey = "order-12345";

        // Act
        var error = WorkflowErrors.InstanceAlreadyExists(instanceKey);

        // Assert
        Assert.Equal(WorkflowErrorCodes.ConflictWorkflow, error.Code);
        Assert.Contains(instanceKey, error.Message);
        Assert.Contains("already exists", error.Message);
        Assert.Equal(instanceKey, error.Target);
    }

    [Fact]
    public void WorkflowNotFound_ShouldCreateNotFoundError_WithoutVersion()
    {
        // Arrange
        var workflowKey = "order-approval";

        // Act
        var error = WorkflowErrors.WorkflowNotFound(workflowKey);

        // Assert
        Assert.Equal(WorkflowErrorCodes.NotFoundWorkflow, error.Code);
        Assert.Contains(workflowKey, error.Message);
        Assert.Contains("not found", error.Message);
        Assert.Equal(workflowKey, error.Target);
    }

    [Fact]
    public void WorkflowNotFound_ShouldCreateNotFoundError_WithVersion()
    {
        // Arrange
        var workflowKey = "order-approval";
        var version = "2.0.0";

        // Act
        var error = WorkflowErrors.WorkflowNotFound(workflowKey, version);

        // Assert
        Assert.Equal(WorkflowErrorCodes.NotFoundWorkflow, error.Code);
        Assert.Contains(workflowKey, error.Message);
        Assert.Contains(version, error.Message);
        Assert.Contains("not found", error.Message);
    }

    [Fact]
    public void StateNotFound_ShouldCreateNotFoundError()
    {
        // Arrange
        var flow = "order-flow";
        var state = "initial";

        // Act
        var error = WorkflowErrors.StateNotFound(flow, state);

        // Assert
        Assert.Equal(WorkflowErrorCodes.NotFoundInitialState, error.Code);
        Assert.Contains(flow, error.Message);
        Assert.Contains(state, error.Message);
        Assert.Equal(state, error.Target);
    }

    [Fact]
    public void InvalidState_ShouldCreateValidationError()
    {
        // Arrange
        var transition = "approve";
        var fromState = "pending";
        var currentState = "cancelled";

        // Act
        var error = WorkflowErrors.InvalidState(transition, fromState, currentState);

        // Assert
        Assert.Equal(WorkflowErrorCodes.InvalidState, error.Code);
        Assert.Contains(transition, error.Message);
        Assert.Contains(fromState, error.Message);
        Assert.Contains(currentState, error.Message);
        Assert.Equal(transition, error.Target);
    }

    [Fact]
    public void InvalidState_ShouldHandleNullStates()
    {
        // Arrange
        var transition = "approve";

        // Act
        var error = WorkflowErrors.InvalidState(transition);

        // Assert
        Assert.Contains("N/A", error.Message);
    }

    [Fact]
    public void TransitionNotFound_ShouldCreateNotFoundError()
    {
        // Arrange
        var transitionKey = "submit";

        // Act
        var error = WorkflowErrors.TransitionNotFound(transitionKey);

        // Assert
        Assert.Equal(WorkflowErrorCodes.NotFoundTransition, error.Code);
        Assert.Contains(transitionKey, error.Message);
        Assert.Equal(transitionKey, error.Target);
    }

    [Fact]
    public void TransitionRuleFailed_ShouldCreateValidationError()
    {
        // Arrange
        var transitionKey = "approve";
        var reason = "Amount exceeds approval limit";

        // Act
        var error = WorkflowErrors.TransitionRuleFailed(transitionKey, reason);

        // Assert
        Assert.Equal(WorkflowErrorCodes.TransitionRuleFailed, error.Code);
        Assert.Contains(transitionKey, error.Message);
        Assert.Contains(reason, error.Message);
        Assert.Equal(transitionKey, error.Target);
    }

    [Fact]
    public void SchemaValidationFailed_ShouldCreateValidationErrorWithResults()
    {
        // Arrange
        var transitionKey = "submit";
        var validationErrors = new List<ValidationResult>
        {
            new ValidationResult("Field1 is required"),
            new ValidationResult("Field2 is invalid")
        };

        // Act
        var error = WorkflowErrors.SchemaValidationFailed(transitionKey, validationErrors);

        // Assert
        Assert.Equal(WorkflowErrorCodes.ValidationErrors, error.Code);
        Assert.Contains(transitionKey, error.Message);
        Assert.NotNull(error.ValidationErrors);
        Assert.Equal(2, error.ValidationErrors.Count);
        Assert.Equal(transitionKey, error.Target);
    }

    [Fact]
    public void TransitionUnauthorized_ShouldCreateForbiddenError()
    {
        // Arrange
        var transitionKey = "system-action";
        var triggerType = TriggerType.Automatic;
        var executionActor = ExecutionActor.User;

        // Act
        var error = WorkflowErrors.TransitionUnauthorized(transitionKey, triggerType, executionActor);

        // Assert
        Assert.Equal(WorkflowErrorCodes.UnauthorizedTransition, error.Code);
        Assert.Contains(transitionKey, error.Message);
        Assert.Contains(triggerType.ToString(), error.Message);
        Assert.Contains(executionActor.ToString(), error.Message);
    }

    [Fact]
    public void AutoTransitionFailed_ShouldCreateValidationError()
    {
        // Arrange
        var instanceId = Guid.NewGuid();
        var workflow = "order-workflow";

        // Act
        var error = WorkflowErrors.AutoTransitionFailed(instanceId, workflow);

        // Assert
        Assert.Equal(WorkflowErrorCodes.AutoTransitionFailed, error.Code);
        Assert.Contains(instanceId.ToString(), error.Message);
        Assert.Contains(workflow, error.Message);
        Assert.Equal(instanceId.ToString(), error.Target);
    }

    [Fact]
    public void AutoTransitionConditionNotMet_ShouldCreateValidationError_WithoutReason()
    {
        // Arrange
        var transitionKey = "auto-approve";

        // Act
        var error = WorkflowErrors.AutoTransitionConditionNotMet(transitionKey);

        // Assert
        Assert.Equal(WorkflowErrorCodes.AutoTransitionConditionNotMet, error.Code);
        Assert.Contains(transitionKey, error.Message);
        Assert.Contains("condition not met", error.Message);
        Assert.Equal(transitionKey, error.Target);
    }

    [Fact]
    public void AutoTransitionConditionNotMet_ShouldCreateValidationError_WithReason()
    {
        // Arrange
        var transitionKey = "auto-approve";
        var reason = "amount is below threshold";

        // Act
        var error = WorkflowErrors.AutoTransitionConditionNotMet(transitionKey, reason);

        // Assert
        Assert.Contains(reason, error.Message);
    }
}

