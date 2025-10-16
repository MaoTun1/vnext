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
    public void InstanceNotFound_ShouldCreateNotFoundError()
    {
        // Arrange
        var instanceId = Guid.NewGuid();
        var reason = "was not found in the database";

        // Act
        var error = WorkflowErrors.InstanceNotFound(instanceId, reason);

        // Assert
        Assert.Equal($"notfound.{WorkflowErrorCodes.NotFoundInitialState}", error.Code);
        Assert.Contains(instanceId.ToString(), error.Message);
        Assert.Contains(reason, error.Message);
        Assert.Equal(instanceId.ToString(), error.Target);
    }

    [Fact]
    public void InstanceAlreadyExists_ShouldCreateConflictError()
    {
        // Arrange
        var instanceKey = "order-12345";

        // Act
        var error = WorkflowErrors.InstanceAlreadyExists(instanceKey);

        // Assert
        Assert.Equal("conflict.instanceExists", error.Code);
        Assert.Contains(instanceKey, error.Message);
        Assert.Contains("already exists", error.Message);
        Assert.Equal(instanceKey, error.Target);
    }

    [Fact]
    public void InstanceCompleted_ShouldCreateValidationError()
    {
        // Arrange
        var instanceId = Guid.NewGuid();

        // Act
        var error = WorkflowErrors.InstanceCompleted(instanceId);

        // Assert
        Assert.Equal("validation.instanceCompleted", error.Code);
        Assert.Contains(instanceId.ToString(), error.Message);
        Assert.Contains("already completed", error.Message);
        Assert.Equal(instanceId.ToString(), error.Target);
    }

    [Fact]
    public void SubFlowBlocked_ShouldCreateConflictError_WithSingularSubFlow()
    {
        // Arrange
        var instanceId = Guid.NewGuid();
        var transitionKey = "approve";
        var activeSubFlowCount = 1;

        // Act
        var error = WorkflowErrors.SubFlowBlocked(instanceId, transitionKey, activeSubFlowCount);

        // Assert
        Assert.Equal($"conflict.{WorkflowErrorCodes.SubFlowBlocked}", error.Code);
        Assert.Contains(transitionKey, error.Message);
        Assert.Contains(instanceId.ToString(), error.Message);
        Assert.Contains("1 active blocking SubFlow instance", error.Message);
        Assert.Equal(transitionKey, error.Target);
    }

    [Fact]
    public void SubFlowBlocked_ShouldCreateConflictError_WithPluralSubFlows()
    {
        // Arrange
        var instanceId = Guid.NewGuid();
        var transitionKey = "approve";
        var activeSubFlowCount = 3;

        // Act
        var error = WorkflowErrors.SubFlowBlocked(instanceId, transitionKey, activeSubFlowCount);

        // Assert
        Assert.Equal($"conflict.{WorkflowErrorCodes.SubFlowBlocked}", error.Code);
        Assert.Contains("3 active blocking SubFlow instances", error.Message);
    }

    [Fact]
    public void TransitionLocked_ShouldCreateConflictError()
    {
        // Arrange
        var instanceId = Guid.NewGuid();
        var transitionKey = "submit";

        // Act
        var error = WorkflowErrors.TransitionLocked(instanceId, transitionKey);

        // Assert
        Assert.Equal($"conflict.{WorkflowErrorCodes.TransitionLocked}", error.Code);
        Assert.Contains(instanceId.ToString(), error.Message);
        Assert.Contains(transitionKey, error.Message);
        Assert.Contains("already in progress", error.Message);
        Assert.Equal(instanceId.ToString(), error.Target);
    }

    [Fact]
    public void WorkflowNotFound_ShouldCreateNotFoundError_WithoutVersion()
    {
        // Arrange
        var workflowKey = "order-approval";

        // Act
        var error = WorkflowErrors.WorkflowNotFound(workflowKey);

        // Assert
        Assert.Equal("notfound.workflowNotFound", error.Code);
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
        Assert.Equal("notfound.workflowNotFound", error.Code);
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
        Assert.Equal($"notfound.{WorkflowErrorCodes.NotFoundInitialState}", error.Code);
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
        Assert.Equal($"validation.{WorkflowErrorCodes.InvalidState}", error.Code);
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
        Assert.Equal($"notfound.{WorkflowErrorCodes.NotFoundTransition}", error.Code);
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
        Assert.Equal($"validation.{WorkflowErrorCodes.TransitionRuleFailed}", error.Code);
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
        Assert.Equal("validation.schemaValidation", error.Code);
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
        Assert.Equal($"auth.{WorkflowErrorCodes.UnauthorizedTransition}", error.Code);
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
        Assert.Equal($"validation.{WorkflowErrorCodes.AutoTransitionFailed}", error.Code);
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
        Assert.Equal($"validation.{WorkflowErrorCodes.AutoTransitionConditionNotMet}", error.Code);
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

    [Fact]
    public void ConfigInvalid_ShouldCreateValidationError()
    {
        // Arrange
        var instanceId = Guid.NewGuid();

        // Act
        var error = WorkflowErrors.ConfigInvalid(instanceId);

        // Assert
        Assert.Equal($"validation.{WorkflowErrorCodes.ConfigInvalid}", error.Code);
        Assert.Contains(instanceId.ToString(), error.Message);
        Assert.Contains("SubFlow configuration not found", error.Message);
        Assert.Equal(instanceId.ToString(), error.Target);
    }

    [Fact]
    public void RuntimeSchemaInvalid_ShouldCreateValidationError()
    {
        // Act
        var error = WorkflowErrors.RuntimeSchemaInvalid();

        // Assert
        Assert.Equal($"validation.{WorkflowErrorCodes.RuntimeSchemaInvalidState}", error.Code);
        Assert.Contains("system flows", error.Message);
    }

    [Fact]
    public void DomainNotFound_ShouldCreateNotFoundError()
    {
        // Arrange
        var requestedDomain = "finance";
        var expectedDomain = "operations";

        // Act
        var error = WorkflowErrors.DomainNotFound(requestedDomain, expectedDomain);

        // Assert
        Assert.Equal($"notfound.{WorkflowErrorCodes.NotFoundDomain}", error.Code);
        Assert.Contains(requestedDomain, error.Message);
        Assert.Contains(expectedDomain, error.Message);
        Assert.Equal(requestedDomain, error.Target);
    }

    [Fact]
    public void Conflict_ShouldCreateConflictError_WithDefaultMessage()
    {
        // Act
        var error = WorkflowErrors.Conflict();

        // Assert
        Assert.Equal($"conflict.{WorkflowErrorCodes.ConflictWorkflow}", error.Code);
        Assert.Contains("same version already exists", error.Message);
    }

    [Fact]
    public void Conflict_ShouldCreateConflictError_WithCustomMessage()
    {
        // Arrange
        var customMessage = "Custom conflict message";

        // Act
        var error = WorkflowErrors.Conflict(customMessage);

        // Assert
        Assert.Equal($"conflict.{WorkflowErrorCodes.ConflictWorkflow}", error.Code);
        Assert.Equal(customMessage, error.Message);
    }
}

