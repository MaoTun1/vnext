using System;
using System.Collections.Generic;
using BBT.Workflow.Scripting;
using Xunit;

namespace BBT.Workflow.Scripting;

public class StandardTaskResponseTests
{
    [Fact]
    public void Constructor_ShouldInitializeWithDefaultValues()
    {
        // Arrange & Act
        var response = new StandardTaskResponse();

        // Assert
        Assert.Null(response.Data);
        Assert.Null(response.StatusCode);
        Assert.True(response.IsSuccess); // Default is true
        Assert.Null(response.ErrorMessage);
        Assert.Null(response.Headers);
        Assert.Null(response.Metadata);
        Assert.Null(response.ExecutionDurationMs);
        Assert.Null(response.TaskType);
    }

    [Fact]
    public void Data_ShouldBeSettable()
    {
        // Arrange
        var response = new StandardTaskResponse();
        var data = new { Name = "Test", Value = 123 };

        // Act
        response.Data = data;

        // Assert
        Assert.NotNull(response.Data);
        Assert.Equal("Test", response.Data.Name);
        Assert.Equal(123, response.Data.Value);
    }

    [Fact]
    public void StatusCode_ShouldBeSettable()
    {
        // Arrange
        var response = new StandardTaskResponse();

        // Act
        response.StatusCode = 200;

        // Assert
        Assert.Equal(200, response.StatusCode);
    }

    [Fact]
    public void IsSuccess_ShouldBeSettable()
    {
        // Arrange
        var response = new StandardTaskResponse();

        // Act
        response.IsSuccess = false;

        // Assert
        Assert.False(response.IsSuccess);
    }

    [Fact]
    public void ErrorMessage_ShouldBeSettable()
    {
        // Arrange
        var response = new StandardTaskResponse();
        var errorMessage = "Task execution failed";

        // Act
        response.ErrorMessage = errorMessage;

        // Assert
        Assert.Equal(errorMessage, response.ErrorMessage);
    }

    [Fact]
    public void Headers_ShouldBeSettable()
    {
        // Arrange
        var response = new StandardTaskResponse();
        var headers = new Dictionary<string, string>
        {
            { "Content-Type", "application/json" },
            { "Authorization", "Bearer token" }
        };

        // Act
        response.Headers = headers;

        // Assert
        Assert.NotNull(response.Headers);
        Assert.Equal(2, response.Headers.Count);
        Assert.Equal("application/json", response.Headers["Content-Type"]);
        Assert.Equal("Bearer token", response.Headers["Authorization"]);
    }

    [Fact]
    public void Metadata_ShouldBeSettable()
    {
        // Arrange
        var response = new StandardTaskResponse();
        var metadata = new Dictionary<string, object>
        {
            { "Retry", 3 },
            { "Source", "API" },
            { "Timestamp", DateTime.UtcNow }
        };

        // Act
        response.Metadata = metadata;

        // Assert
        Assert.NotNull(response.Metadata);
        Assert.Equal(3, response.Metadata.Count);
        Assert.Equal(3, response.Metadata["Retry"]);
        Assert.Equal("API", response.Metadata["Source"]);
    }

    [Fact]
    public void ExecutionDurationMs_ShouldBeSettable()
    {
        // Arrange
        var response = new StandardTaskResponse();

        // Act
        response.ExecutionDurationMs = 1250;

        // Assert
        Assert.Equal(1250, response.ExecutionDurationMs);
    }

    [Fact]
    public void TaskType_ShouldBeSettable()
    {
        // Arrange
        var response = new StandardTaskResponse();
        var taskType = "HttpTask";

        // Act
        response.TaskType = taskType;

        // Assert
        Assert.Equal(taskType, response.TaskType);
    }

    [Fact]
    public void Response_ShouldRepresentSuccessfulExecution()
    {
        // Arrange & Act
        var response = new StandardTaskResponse
        {
            Data = new { Result = "Success" },
            StatusCode = 200,
            IsSuccess = true,
            ExecutionDurationMs = 500,
            TaskType = "HttpTask"
        };

        // Assert
        Assert.True(response.IsSuccess);
        Assert.Equal(200, response.StatusCode);
        Assert.Null(response.ErrorMessage);
        Assert.NotNull(response.Data);
    }

    [Fact]
    public void Response_ShouldRepresentFailedExecution()
    {
        // Arrange & Act
        var response = new StandardTaskResponse
        {
            IsSuccess = false,
            ErrorMessage = "Connection timeout",
            StatusCode = 504,
            ExecutionDurationMs = 30000,
            TaskType = "HttpTask"
        };

        // Assert
        Assert.False(response.IsSuccess);
        Assert.Equal("Connection timeout", response.ErrorMessage);
        Assert.Equal(504, response.StatusCode);
    }

    [Fact]
    public void Response_ShouldSupportCompleteInformation()
    {
        // Arrange
        var response = new StandardTaskResponse();

        // Act
        response.Data = new { Id = 1, Name = "Task" };
        response.StatusCode = 201;
        response.IsSuccess = true;
        response.ErrorMessage = null;
        response.Headers = new Dictionary<string, string> { { "Location", "/tasks/1" } };
        response.Metadata = new Dictionary<string, object> { { "CreatedAt", DateTime.UtcNow } };
        response.ExecutionDurationMs = 750;
        response.TaskType = "CreateTask";

        // Assert
        Assert.NotNull(response.Data);
        Assert.Equal(201, response.StatusCode);
        Assert.True(response.IsSuccess);
        Assert.Null(response.ErrorMessage);
        Assert.Single(response.Headers!);
        Assert.Single(response.Metadata!);
        Assert.Equal(750, response.ExecutionDurationMs);
        Assert.Equal("CreateTask", response.TaskType);
    }

    [Fact]
    public void Headers_ShouldHandleEmptyDictionary()
    {
        // Arrange
        var response = new StandardTaskResponse();

        // Act
        response.Headers = new Dictionary<string, string>();

        // Assert
        Assert.NotNull(response.Headers);
        Assert.Empty(response.Headers);
    }

    [Fact]
    public void Metadata_ShouldHandleEmptyDictionary()
    {
        // Arrange
        var response = new StandardTaskResponse();

        // Act
        response.Metadata = new Dictionary<string, object>();

        // Assert
        Assert.NotNull(response.Metadata);
        Assert.Empty(response.Metadata);
    }

    [Fact]
    public void StatusCode_ShouldSupportCommonHttpCodes()
    {
        // Arrange
        var response = new StandardTaskResponse();

        // Act & Assert - Success codes
        response.StatusCode = 200;
        Assert.Equal(200, response.StatusCode);

        response.StatusCode = 201;
        Assert.Equal(201, response.StatusCode);

        response.StatusCode = 204;
        Assert.Equal(204, response.StatusCode);

        // Act & Assert - Error codes
        response.StatusCode = 400;
        Assert.Equal(400, response.StatusCode);

        response.StatusCode = 404;
        Assert.Equal(404, response.StatusCode);

        response.StatusCode = 500;
        Assert.Equal(500, response.StatusCode);
    }

    [Fact]
    public void ExecutionDurationMs_ShouldSupportVariousRanges()
    {
        // Arrange
        var response = new StandardTaskResponse();

        // Act & Assert
        response.ExecutionDurationMs = 0;
        Assert.Equal(0, response.ExecutionDurationMs);

        response.ExecutionDurationMs = 10;
        Assert.Equal(10, response.ExecutionDurationMs);

        response.ExecutionDurationMs = 60000; // 1 minute
        Assert.Equal(60000, response.ExecutionDurationMs);
    }

    [Fact]
    public void Metadata_ShouldSupportDifferentValueTypes()
    {
        // Arrange
        var response = new StandardTaskResponse();
        var metadata = new Dictionary<string, object>
        {
            { "StringValue", "test" },
            { "IntValue", 42 },
            { "BoolValue", true },
            { "DateValue", DateTime.UtcNow },
            { "ObjectValue", new { Nested = "data" } }
        };

        // Act
        response.Metadata = metadata;

        // Assert
        Assert.Equal(5, response.Metadata!.Count);
        Assert.IsType<string>(response.Metadata["StringValue"]);
        Assert.IsType<int>(response.Metadata["IntValue"]);
        Assert.IsType<bool>(response.Metadata["BoolValue"]);
        Assert.IsType<DateTime>(response.Metadata["DateValue"]);
    }

    [Fact]
    public void Data_ShouldAcceptNullValue()
    {
        // Arrange
        var response = new StandardTaskResponse();

        // Act
        response.Data = null;

        // Assert
        Assert.Null(response.Data);
    }
}

