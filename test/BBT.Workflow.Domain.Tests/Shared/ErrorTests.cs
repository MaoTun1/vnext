using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using BBT.Workflow.Domain;
using Xunit;

namespace BBT.Workflow.Domain;

public class ErrorTests
{
    [Fact]
    public void Constructor_ShouldInitializeProperties()
    {
        // Arrange
        var code = "test.error";
        var message = "Test error message";
        var detail = "Additional details";
        var target = "testField";

        // Act
        var error = new Error(code, message, detail, target);

        // Assert
        Assert.Equal(code, error.Code);
        Assert.Equal(message, error.Message);
        Assert.Equal(detail, error.Detail);
        Assert.Equal(target, error.Target);
        Assert.Null(error.ValidationErrors);
    }

    [Fact]
    public void None_ShouldReturnStaticErrorWithNoneCode()
    {
        // Act
        var error = Error.None;

        // Assert
        Assert.Equal("none", error.Code);
        Assert.Null(error.Message);
        Assert.Null(error.Detail);
        Assert.Null(error.Target);
    }

    [Fact]
    public void Validation_ShouldCreateErrorWithValidationPrefix()
    {
        // Arrange
        var code = "required";
        var message = "Field is required";
        var target = "username";

        // Act
        var error = Error.Validation(code, message, target);

        // Assert
        Assert.Equal("validation.required", error.Code);
        Assert.Equal(message, error.Message);
        Assert.Equal(target, error.Target);
    }

    [Fact]
    public void Validation_WithValidationErrors_ShouldIncludeValidationResults()
    {
        // Arrange
        var code = "schema";
        var message = "Schema validation failed";
        var validationErrors = new List<ValidationResult>
        {
            new ValidationResult("Field1 is required", new[] { "Field1" }),
            new ValidationResult("Field2 is invalid", new[] { "Field2" })
        };

        // Act
        var error = Error.Validation(code, message, validationErrors);

        // Assert
        Assert.Equal("validation.schema", error.Code);
        Assert.Equal(message, error.Message);
        Assert.NotNull(error.ValidationErrors);
        Assert.Equal(2, error.ValidationErrors.Count);
    }

    [Fact]
    public void Conflict_ShouldCreateErrorWithConflictPrefix()
    {
        // Arrange
        var code = "duplicate";
        var message = "Duplicate entry";
        var target = "email";

        // Act
        var error = Error.Conflict(code, message, target);

        // Assert
        Assert.Equal("conflict.duplicate", error.Code);
        Assert.Equal(message, error.Message);
        Assert.Equal(target, error.Target);
    }

    [Fact]
    public void NotFound_ShouldCreateErrorWithNotFoundPrefix()
    {
        // Arrange
        var code = "resource";
        var message = "Resource not found";
        var target = "userId";

        // Act
        var error = Error.NotFound(code, message, target);

        // Assert
        Assert.Equal("notfound.resource", error.Code);
        Assert.Equal(message, error.Message);
        Assert.Equal(target, error.Target);
    }

    [Fact]
    public void Unauthorized_ShouldCreateErrorWithAuthPrefix()
    {
        // Arrange
        var code = "invalid_token";
        var message = "Invalid authentication token";

        // Act
        var error = Error.Unauthorized(code, message);

        // Assert
        Assert.Equal("auth.invalid_token", error.Code);
        Assert.Equal(message, error.Message);
    }

    [Fact]
    public void Unauthorized_WithDefaultCode_ShouldUseUnauthorizedCode()
    {
        // Act
        var error = Error.Unauthorized();

        // Assert
        Assert.Equal("auth.unauthorized", error.Code);
        Assert.Null(error.Message);
    }

    [Fact]
    public void Forbidden_ShouldCreateErrorWithAuthPrefix()
    {
        // Arrange
        var code = "insufficient_permissions";
        var message = "User lacks required permissions";

        // Act
        var error = Error.Forbidden(code, message);

        // Assert
        Assert.Equal("auth.insufficient_permissions", error.Code);
        Assert.Equal(message, error.Message);
    }

    [Fact]
    public void Forbidden_WithDefaultCode_ShouldUseForbiddenCode()
    {
        // Act
        var error = Error.Forbidden();

        // Assert
        Assert.Equal("auth.forbidden", error.Code);
        Assert.Null(error.Message);
    }

    [Fact]
    public void Dependency_ShouldCreateErrorWithDepPrefix()
    {
        // Arrange
        var code = "database";
        var message = "Database connection failed";
        var target = "sql-server";

        // Act
        var error = Error.Dependency(code, message, target);

        // Assert
        Assert.Equal("dep.database", error.Code);
        Assert.Equal(message, error.Message);
        Assert.Equal(target, error.Target);
    }

    [Fact]
    public void Transient_ShouldCreateErrorWithTransientPrefix()
    {
        // Arrange
        var code = "timeout";
        var message = "Operation timed out";
        var target = "external-api";

        // Act
        var error = Error.Transient(code, message, target);

        // Assert
        Assert.Equal("transient.timeout", error.Code);
        Assert.Equal(message, error.Message);
        Assert.Equal(target, error.Target);
    }

    [Fact]
    public void Failure_ShouldCreateErrorWithFailurePrefix()
    {
        // Arrange
        var code = "unexpected";
        var message = "Unexpected error occurred";
        var detail = "Stack trace here";

        // Act
        var error = Error.Failure(code, message, detail);

        // Assert
        Assert.Equal("failure.unexpected", error.Code);
        Assert.Equal(message, error.Message);
        Assert.Equal(detail, error.Detail);
    }

    [Fact]
    public void Equals_ShouldReturnTrue_WhenAllPropertiesAreSame()
    {
        // Arrange
        var error1 = new Error("test.error", "Message", "Detail", "target");
        var error2 = new Error("test.error", "Message", "Detail", "target");

        // Act & Assert
        Assert.Equal(error1, error2);
        Assert.True(error1.Equals(error2));
    }

    [Fact]
    public void Equals_ShouldReturnFalse_WhenCodesDiffer()
    {
        // Arrange
        var error1 = new Error("test.error1", "Message");
        var error2 = new Error("test.error2", "Message");

        // Act & Assert
        Assert.NotEqual(error1, error2);
        Assert.False(error1.Equals(error2));
    }

    [Fact]
    public void GetHashCode_ShouldBeSame_ForEqualErrors()
    {
        // Arrange
        var error1 = new Error("test.error", "Message", "Detail", "target");
        var error2 = new Error("test.error", "Message", "Detail", "target");

        // Act & Assert
        Assert.Equal(error1.GetHashCode(), error2.GetHashCode());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Validation_ShouldAcceptNullOrEmptyMessage(string? message)
    {
        // Act
        var error = Error.Validation("code", message);

        // Assert
        Assert.Equal("validation.code", error.Code);
        Assert.Equal(message, error.Message);
    }

    [Fact]
    public void Constructor_WithMinimalParameters_ShouldSetOnlyCode()
    {
        // Arrange & Act
        var error = new Error("minimal.error");

        // Assert
        Assert.Equal("minimal.error", error.Code);
        Assert.Null(error.Message);
        Assert.Null(error.Detail);
        Assert.Null(error.Target);
        Assert.Null(error.ValidationErrors);
    }

    [Fact]
    public void ValidationErrors_ShouldPreserveAllValidationResults()
    {
        // Arrange
        var validationResults = new List<ValidationResult>
        {
            new ValidationResult("Error 1", new[] { "Field1" }),
            new ValidationResult("Error 2", new[] { "Field2" }),
            new ValidationResult("Error 3", new[] { "Field3", "Field4" })
        };

        // Act
        var error = Error.Validation("multi", "Multiple validation errors", validationResults);

        // Assert
        Assert.NotNull(error.ValidationErrors);
        Assert.Equal(3, error.ValidationErrors.Count);
        Assert.Contains(error.ValidationErrors, v => v.ErrorMessage == "Error 1");
        Assert.Contains(error.ValidationErrors, v => v.ErrorMessage == "Error 2");
        Assert.Contains(error.ValidationErrors, v => v.ErrorMessage == "Error 3");
    }
}

