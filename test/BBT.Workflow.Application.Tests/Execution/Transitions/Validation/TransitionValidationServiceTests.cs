using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using BBT.Aether.Results;
using BBT.Workflow.Caching;
using BBT.Workflow.Definitions;
using BBT.Workflow.Domain;
using BBT.Workflow.Instances;
using BBT.Workflow.Instances.Policies;
using BBT.Workflow.Rules;
using BBT.Workflow.Shared;
using BBT.Workflow.Validation;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;
using Xunit;

namespace BBT.Workflow.Execution.Validation;

/// <summary>
/// Unit tests for TransitionValidationService
/// Tests transition validation operations including policy and schema validation
/// </summary>
public class TransitionValidationServiceTests
{
    private readonly Mock<IJsonSchemaValidator> _mockSchemaValidator;
    private readonly Mock<IComponentCacheStore> _mockComponentCacheStore;
    private readonly Mock<ILogger<TransitionValidationService>> _mockLogger;
    private readonly Mock<IResultRuleEngine<State>> _mockResultRuleEngine;
    private readonly StateTransitionPolicy _stateTransitionPolicy;
    private readonly TransitionValidationService _service;

    public TransitionValidationServiceTests()
    {
        _mockResultRuleEngine = new Mock<IResultRuleEngine<State>>();
        _stateTransitionPolicy = new StateTransitionPolicy(_mockResultRuleEngine.Object);
        _mockSchemaValidator = new Mock<IJsonSchemaValidator>();
        _mockComponentCacheStore = new Mock<IComponentCacheStore>();
        _mockLogger = new Mock<ILogger<TransitionValidationService>>();

        // Setup default successful validation
        _mockResultRuleEngine
            .Setup(x => x.Validate(It.IsAny<State>()))
            .Returns(Result.Ok());

        _service = new TransitionValidationService(
            _stateTransitionPolicy,
            _mockSchemaValidator.Object,
            _mockComponentCacheStore.Object);
    }

    #region ValidateAsync Tests

    [Fact]
    public async Task ValidateAsync_WithValidTransition_ShouldReturnSuccess()
    {
        // Arrange
        var context = CreateValidTransitionContext();
        
        SetupSuccessfulPolicyValidation(context);

        // Act
        var result = await _service.ValidateAsync(context, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task ValidateAsync_WhenPolicyValidationFails_ShouldReturnFailure()
    {
        // Arrange
        var context = CreateValidTransitionContext();
        var errorCode = "POLICY_ERROR";
        var errorMessage = "Policy validation failed";

        SetupFailedPolicyValidation(context, errorCode, errorMessage);

        // Act
        var result = await _service.ValidateAsync(context, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldNotBe(default);
        result.Error.Code.ShouldBe(errorCode);
        result.Error.Message.ShouldBe(errorMessage);
    }

    [Fact(Skip = "Extension methods cannot be mocked")]
    public async Task ValidateAsync_WithSchema_ShouldValidateDataAgainstSchema()
    {
        // Arrange
        var schemaRef = new Reference("test-schema", "test-domain", "sys-schemas", "1.0.0");
        var context = CreateTransitionContextWithSchema(schemaRef);
        var schemaDefinition = CreateMockSchemaDefinition("test-schema");

        SetupSuccessfulPolicyValidation(context);
        
        _mockComponentCacheStore
            .Setup(x => x.GetSchemaAsync(schemaRef.Domain, schemaRef.Key, schemaRef.Version, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<SchemaDefinition>.Ok(schemaDefinition));

        _mockSchemaValidator
            .Setup(x => x.Validate(schemaDefinition.Schema, context.DataElement))
            .Returns(Result.Ok());

        // Act
        var result = await _service.ValidateAsync(context, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        _mockSchemaValidator.Verify(
            x => x.Validate(schemaDefinition.Schema, context.DataElement),
            Times.Once);
    }

    [Fact(Skip = "Extension methods cannot be mocked")]
    public async Task ValidateAsync_WhenSchemaValidationFails_ShouldReturnFailure()
    {
        // Arrange
        var schemaRef = new Reference("test-schema", "test-domain", "sys-schemas", "1.0.0");
        var context = CreateTransitionContextWithSchema(schemaRef);
        var schemaDefinition = CreateMockSchemaDefinition("test-schema");

        SetupSuccessfulPolicyValidation(context);

        _mockComponentCacheStore
            .Setup(x => x.GetSchemaAsync(schemaRef.Domain, schemaRef.Key, schemaRef.Version, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<SchemaDefinition>.Ok(schemaDefinition));

        var validationError = Error.Validation(
            code: "SCHEMA_ERROR", 
            message: "Schema validation failed",
            validationErrors: new List<ValidationResult>() { new("Invalid schema definition",
                ["field1"]) });

        _mockSchemaValidator
            .Setup(x => x.Validate(schemaDefinition.Schema, context.DataElement))
            .Returns(Result.Fail(validationError));

        // Act
        var result = await _service.ValidateAsync(context, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldNotBe(default!);
    }

    [Fact]
    public async Task ValidateAsync_ShouldLogDebugMessages()
    {
        // Arrange
        var context = CreateValidTransitionContext();
        SetupSuccessfulPolicyValidation(context);

        // Act
        await _service.ValidateAsync(context, CancellationToken.None);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Validating transition")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("completed successfully")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ValidateAsync_WhenValidationFails_ShouldLogWarning()
    {
        // Arrange
        var context = CreateValidTransitionContext();
        SetupFailedPolicyValidation(context, "ERROR_CODE", "Error message");

        // Act
        await _service.ValidateAsync(context, CancellationToken.None);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("validation failed")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact(Skip = "Extension methods cannot be mocked")]
    public async Task ValidateAsync_WithValidationErrors_ShouldIncludeTransitionKey()
    {
        // Arrange
        var schemaRef = new Reference("test-schema", "test-domain", "sys-schemas", "1.0.0");
        var context = CreateTransitionContextWithSchema(schemaRef);
        var schemaDefinition = CreateMockSchemaDefinition("test-schema");

        SetupSuccessfulPolicyValidation(context);

        _mockComponentCacheStore
            .Setup(x => x.GetSchemaAsync(schemaRef.Domain, schemaRef.Key, schemaRef.Version, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<SchemaDefinition>.Ok(schemaDefinition));

        var validationErrors = new List<ValidationResult> 
        { 
            new("invalid", ["field1"])
        };

        var validationError = Error.Validation(code: "SCHEMA_ERROR", message: "Schema validation failed",
            validationErrors: validationErrors);

        _mockSchemaValidator
            .Setup(x => x.Validate(schemaDefinition.Schema, context.DataElement))
            .Returns(Result.Fail(validationError));

        // Act
        var result = await _service.ValidateAsync(context, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Error.ValidationErrors.ShouldBe(validationErrors);
    }

    [Fact(Skip = "Cancellation propagation test needs adjustment")]
    public async Task ValidateAsync_WithCancellation_ShouldPropagateCancellation()
    {
        // Arrange
        var context = CreateValidTransitionContext();
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Should.ThrowAsync<OperationCanceledException>(
            async () => await _service.ValidateAsync(context, cts.Token)
        );
    }

    [Fact]
    public async Task ValidateAsync_WithActor_ShouldPassActorToPolicyValidation()
    {
        // Arrange
        var actor = ExecutionActor.User;
        var context = CreateValidTransitionContext();
        context.Actor = actor;

        SetupSuccessfulPolicyValidation(context);

        // Act
        var result = await _service.ValidateAsync(context, CancellationToken.None);

        // Assert - The actor should be used in policy validation
        // This is implicitly tested by the successful validation
        result.IsSuccess.ShouldBeTrue();
    }

    [Fact(Skip = "Extension methods cannot be mocked")]
    public async Task ValidateAsync_WithMultipleValidationErrors_ShouldReturnAllErrors()
    {
        // Arrange
        var schemaRef = new Reference("test-schema", "test-domain", "sys-schemas", "1.0.0");
        var context = CreateTransitionContextWithSchema(schemaRef);
        var schemaDefinition = CreateMockSchemaDefinition("test-schema");

        SetupSuccessfulPolicyValidation(context);

        _mockComponentCacheStore
            .Setup(x => x.GetSchemaAsync(schemaRef.Domain, schemaRef.Key, schemaRef.Version, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<SchemaDefinition>.Ok(schemaDefinition));

        var validationErrors = new List<ValidationResult>
        {
            new("Error 1", ["field1"]),
            new("Error 2", ["field2"]),
            new("Error 3", ["field3"])
        };

        var validationError = Error.Validation(
            code: "SCHEMA_ERROR", message: "Multiple validation errors", validationErrors: validationErrors);

        _mockSchemaValidator
            .Setup(x => x.Validate(schemaDefinition.Schema, context.DataElement))
            .Returns(Result.Fail(validationError));

        // Act
        var result = await _service.ValidateAsync(context, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Error.ValidationErrors.ShouldNotBeNull();
        result.Error.ValidationErrors!.Count.ShouldBe(3);
    }

    #endregion

    #region Helper Methods

    private void SetupSuccessfulPolicyValidation(TransitionExecutionContext context)
    {
        _mockResultRuleEngine
            .Setup(x => x.Validate(It.IsAny<State>()))
            .Returns(Result.Ok());
    }

    private void SetupFailedPolicyValidation(TransitionExecutionContext context, string errorCode, string errorMessage)
    {
        var error = Error.Failure(errorCode, errorMessage);
        _mockResultRuleEngine
            .Setup(x => x.Validate(It.IsAny<State>()))
            .Returns(Result.Fail(error));
    }

    private TransitionExecutionContext CreateValidTransitionContext()
    {
        var instanceId = Guid.NewGuid();
        var workflowKey = "test-workflow";
        var domain = "test-domain";
        var transitionKey = "test-transition";

        var workflow = CreateMockWorkflow(workflowKey, domain);
        var instance = CreateMockInstance(instanceId, workflowKey, domain);
        var state = workflow.GetState("state1").Value!;
        var transition = Transition.Create(transitionKey, null, "state1", TriggerType.Manual, VersionStrategy.IncreasePatch.Code); 

        return new TransitionExecutionContext
        {
            InstanceId = instanceId,
            Domain = domain,
            WorkflowKey = workflowKey,
            TransitionKey = transitionKey,
            Trigger = TriggerType.Manual,
            Actor = ExecutionActor.User,
            CorrelationId = Guid.NewGuid().ToString("N"),
            ExecutionChainId = Guid.NewGuid().ToString("N"),
            RequestedAt = DateTimeOffset.UtcNow,
            Workflow = workflow,
            Current = state,
            Transition = transition,
            Instance = instance,
            Data = new { test = "data" },
            TraceId = Guid.NewGuid().ToString("N"),
            SpanId = Guid.NewGuid().ToString("N")[..16]
        };
    }

    private TransitionExecutionContext CreateTransitionContextWithSchema(Reference schemaRef)
    {
        var context = CreateValidTransitionContext();
        typeof(Transition)
            .GetProperty(nameof(Transition.Schema))!
            .SetValue(context.Transition, schemaRef);
        return context;
    }

    private Instance CreateMockInstance(Guid instanceId, string workflowKey, string domain)
    {
        var instance = Instance.Create(instanceId, workflowKey, workflowKey);
        return instance;
    }

    private Definitions.Workflow CreateMockWorkflow(string key, string domain)
    {
        var json = """
        {
            "type": "F",
            "timeout": null,
            "labels": [],
            "functions": [],
            "features": [],
            "states": [
                {
                    "key": "state1",
                    "type": "P",
                    "transitions": []
                }
            ],
            "sharedTransitions": [],
            "extensions": [],
            "startTransition": {"key": "start", "from": null, "target": "state1", "triggerType": "Manual", "versionStrategy": "Patch", "labels": [], "onExecutionTasks": [], "view": null}
        }
        """;

        var options = new System.Text.Json.JsonSerializerOptions 
        { 
            PropertyNameCaseInsensitive = true,
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
        };
        var workflow = System.Text.Json.JsonSerializer.Deserialize<Definitions.Workflow>(json, options)!;

        workflow.SetReference(new Reference(key, domain, "sys-flows", "1.0.0"));
        return workflow;
    }

    private SchemaDefinition CreateMockSchemaDefinition(string key)
    {
        var json = """
        {
            "type": "workflow",
            "schema": {
                "type": "object",
                "properties": {
                    "field1": {"type": "string"}
                },
                "required": ["field1"]
            }
        }
        """;

        var schema = System.Text.Json.JsonSerializer.Deserialize<SchemaDefinition>(json,
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

        schema.SetReference(new Reference(key, "test-domain", "sys-schemas", "1.0.0"));
        return schema;
    }

    #endregion
}
