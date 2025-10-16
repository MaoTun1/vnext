using System;
using System.Text.Json;
using Xunit;

namespace BBT.Workflow.Instances;

/// <summary>
/// Unit tests for InstanceJob
/// </summary>
public class InstanceJobTests : DomainTestBase<DomainEntryPoint>
{
    [Fact]
    public void Create_ShouldInitializeAllProperties()
    {
        // Arrange
        var id = Guid.NewGuid();
        var jobName = "test-job";
        var jobId = "job-id-123";
        var domain = "test-domain";
        var flowName = "test-flow";
        var instanceId = Guid.NewGuid();
        var expressionValue = "0 0 * * *";
        var payload = JsonDocument.Parse("{\"key\":\"value\"}").RootElement;

        // Act
        var instanceJob = InstanceJob.Create(
            id,
            jobName,
            jobId,
            domain,
            flowName,
            instanceId,
            expressionValue,
            payload
        );

        // Assert
        Assert.Equal(id, instanceJob.Id);
        Assert.Equal(jobName, instanceJob.JobName);
        Assert.Equal(jobId, instanceJob.JobId);
        Assert.Equal(domain, instanceJob.Domain);
        Assert.Equal(flowName, instanceJob.FlowName);
        Assert.Equal(instanceId, instanceJob.InstanceId);
        Assert.Equal(expressionValue, instanceJob.ExpressionValue);
        Assert.False(instanceJob.IsTriggered);
        Assert.NotNull(instanceJob.Payload);
        Assert.Null(instanceJob.ModifiedAt);
    }

    [Fact]
    public void Create_ShouldSetCreatedAtToCurrentTime()
    {
        // Arrange
        var before = DateTime.UtcNow.AddSeconds(-1);
        var payload = JsonDocument.Parse("{}").RootElement;

        // Act
        var instanceJob = InstanceJob.Create(
            Guid.NewGuid(),
            "job-name",
            "job-id",
            "domain",
            "flow",
            Guid.NewGuid(),
            "expression",
            payload
        );
        var after = DateTime.UtcNow.AddSeconds(1);

        // Assert
        Assert.True(instanceJob.CreatedAt >= before && instanceJob.CreatedAt <= after);
    }

    [Fact]
    public void Create_ShouldThrow_WhenJobNameIsNull()
    {
        // Arrange
        var payload = JsonDocument.Parse("{}").RootElement;

        // Act & Assert
        // Check.NotNullOrWhiteSpace throws ArgumentException, not ArgumentNullException
        Assert.Throws<ArgumentException>(() =>
            InstanceJob.Create(
                Guid.NewGuid(),
                null!,
                "job-id",
                "domain",
                "flow",
                Guid.NewGuid(),
                "expression",
                payload
            ));
    }

    [Fact]
    public void Create_ShouldThrow_WhenJobIdIsNull()
    {
        // Arrange
        var payload = JsonDocument.Parse("{}").RootElement;

        // Act & Assert
        // Check.NotNullOrWhiteSpace throws ArgumentException, not ArgumentNullException
        Assert.Throws<ArgumentException>(() =>
            InstanceJob.Create(
                Guid.NewGuid(),
                "job-name",
                null!,
                "domain",
                "flow",
                Guid.NewGuid(),
                "expression",
                payload
            ));
    }

    [Fact]
    public void Create_ShouldThrow_WhenDomainIsNull()
    {
        // Arrange
        var payload = JsonDocument.Parse("{}").RootElement;

        // Act & Assert
        // Check.NotNullOrWhiteSpace throws ArgumentException, not ArgumentNullException
        Assert.Throws<ArgumentException>(() =>
            InstanceJob.Create(
                Guid.NewGuid(),
                "job-name",
                "job-id",
                null!,
                "flow",
                Guid.NewGuid(),
                "expression",
                payload
            ));
    }

    [Fact]
    public void Create_ShouldThrow_WhenFlowNameIsNull()
    {
        // Arrange
        var payload = JsonDocument.Parse("{}").RootElement;

        // Act & Assert
        // Check.NotNullOrWhiteSpace throws ArgumentException, not ArgumentNullException
        Assert.Throws<ArgumentException>(() =>
            InstanceJob.Create(
                Guid.NewGuid(),
                "job-name",
                "job-id",
                "domain",
                null!,
                Guid.NewGuid(),
                "expression",
                payload
            ));
    }

    [Fact]
    public void Create_ShouldThrow_WhenExpressionValueIsNull()
    {
        // Arrange
        var payload = JsonDocument.Parse("{}").RootElement;

        // Act & Assert
        // Check.NotNullOrWhiteSpace throws ArgumentException, not ArgumentNullException
        Assert.Throws<ArgumentException>(() =>
            InstanceJob.Create(
                Guid.NewGuid(),
                "job-name",
                "job-id",
                "domain",
                "flow",
                Guid.NewGuid(),
                null!,
                payload
            ));
    }

    [Fact]
    public void Triggered_ShouldSetIsTriggeredToTrue()
    {
        // Arrange
        var payload = JsonDocument.Parse("{}").RootElement;
        var instanceJob = InstanceJob.Create(
            Guid.NewGuid(),
            "job-name",
            "job-id",
            "domain",
            "flow",
            Guid.NewGuid(),
            "expression",
            payload
        );

        Assert.False(instanceJob.IsTriggered);
        Assert.Null(instanceJob.ModifiedAt);

        // Act
        instanceJob.Triggered();

        // Assert
        Assert.True(instanceJob.IsTriggered);
        Assert.NotNull(instanceJob.ModifiedAt);
    }

    [Fact]
    public void Triggered_ShouldSetModifiedAtToCurrentTime()
    {
        // Arrange
        var payload = JsonDocument.Parse("{}").RootElement;
        var instanceJob = InstanceJob.Create(
            Guid.NewGuid(),
            "job-name",
            "job-id",
            "domain",
            "flow",
            Guid.NewGuid(),
            "expression",
            payload
        );
        var before = DateTime.UtcNow.AddSeconds(-1);

        // Act
        instanceJob.Triggered();
        var after = DateTime.UtcNow.AddSeconds(1);

        // Assert
        Assert.NotNull(instanceJob.ModifiedAt);
        Assert.True(instanceJob.ModifiedAt >= before && instanceJob.ModifiedAt <= after);
    }

    [Fact]
    public void UpdateTriggerAt_ShouldUpdateExpressionValue()
    {
        // Arrange
        var payload = JsonDocument.Parse("{}").RootElement;
        var instanceJob = InstanceJob.Create(
            Guid.NewGuid(),
            "job-name",
            "job-id",
            "domain",
            "flow",
            Guid.NewGuid(),
            "0 0 * * *",
            payload
        );
        var newExpression = "0 12 * * *";

        // Act
        instanceJob.UpdateTriggerAt(newExpression);

        // Assert
        Assert.Equal(newExpression, instanceJob.ExpressionValue);
    }

    [Fact]
    public void UpdateTriggerAt_ShouldSetModifiedAtToCurrentTime()
    {
        // Arrange
        var payload = JsonDocument.Parse("{}").RootElement;
        var instanceJob = InstanceJob.Create(
            Guid.NewGuid(),
            "job-name",
            "job-id",
            "domain",
            "flow",
            Guid.NewGuid(),
            "expression",
            payload
        );
        var before = DateTime.UtcNow.AddSeconds(-1);

        // Act
        instanceJob.UpdateTriggerAt("new-expression");
        var after = DateTime.UtcNow.AddSeconds(1);

        // Assert
        Assert.NotNull(instanceJob.ModifiedAt);
        Assert.True(instanceJob.ModifiedAt >= before && instanceJob.ModifiedAt <= after);
    }

    [Fact]
    public void Payload_ShouldBeAccessible()
    {
        // Arrange
        var payload = JsonDocument.Parse("{\"key\":\"value\",\"number\":42}").RootElement;
        var instanceJob = InstanceJob.Create(
            Guid.NewGuid(),
            "job-name",
            "job-id",
            "domain",
            "flow",
            Guid.NewGuid(),
            "expression",
            payload
        );

        // Act
        var jobPayload = instanceJob.Payload;

        // Assert
        Assert.NotNull(jobPayload);
        Assert.Contains("key", jobPayload.Json);
        Assert.Contains("number", jobPayload.Json);
    }

    [Fact]
    public void Triggered_ShouldBeIdempotent()
    {
        // Arrange
        var payload = JsonDocument.Parse("{}").RootElement;
        var instanceJob = InstanceJob.Create(
            Guid.NewGuid(),
            "job-name",
            "job-id",
            "domain",
            "flow",
            Guid.NewGuid(),
            "expression",
            payload
        );

        // Act
        instanceJob.Triggered();
        var firstModifiedAt = instanceJob.ModifiedAt;

        System.Threading.Thread.Sleep(10);

        instanceJob.Triggered();
        var secondModifiedAt = instanceJob.ModifiedAt;

        // Assert
        Assert.True(instanceJob.IsTriggered);
        Assert.NotEqual(firstModifiedAt, secondModifiedAt); // ModifiedAt gets updated
    }

    [Fact]
    public void UpdateTriggerAt_ShouldWorkMultipleTimes()
    {
        // Arrange
        var payload = JsonDocument.Parse("{}").RootElement;
        var instanceJob = InstanceJob.Create(
            Guid.NewGuid(),
            "job-name",
            "job-id",
            "domain",
            "flow",
            Guid.NewGuid(),
            "expression1",
            payload
        );

        // Act
        instanceJob.UpdateTriggerAt("expression2");
        var firstModifiedAt = instanceJob.ModifiedAt;

        System.Threading.Thread.Sleep(10);

        instanceJob.UpdateTriggerAt("expression3");
        var secondModifiedAt = instanceJob.ModifiedAt;

        // Assert
        Assert.Equal("expression3", instanceJob.ExpressionValue);
        Assert.NotEqual(firstModifiedAt, secondModifiedAt);
    }

    [Fact]
    public void Create_ShouldHandleComplexPayload()
    {
        // Arrange
        var complexPayload = JsonDocument.Parse(@"
        {
            ""user"": {
                ""id"": 123,
                ""name"": ""Test User""
            },
            ""items"": [1, 2, 3],
            ""metadata"": {
                ""tags"": [""tag1"", ""tag2""]
            }
        }").RootElement;

        // Act
        var instanceJob = InstanceJob.Create(
            Guid.NewGuid(),
            "job-name",
            "job-id",
            "domain",
            "flow",
            Guid.NewGuid(),
            "expression",
            complexPayload
        );

        // Assert
        Assert.NotNull(instanceJob.Payload);
        Assert.Contains("user", instanceJob.Payload.Json);
        Assert.Contains("items", instanceJob.Payload.Json);
        Assert.Contains("metadata", instanceJob.Payload.Json);
    }

    [Fact]
    public void IsTriggered_ShouldBeFalse_Initially()
    {
        // Arrange
        var payload = JsonDocument.Parse("{}").RootElement;

        // Act
        var instanceJob = InstanceJob.Create(
            Guid.NewGuid(),
            "job-name",
            "job-id",
            "domain",
            "flow",
            Guid.NewGuid(),
            "expression",
            payload
        );

        // Assert
        Assert.False(instanceJob.IsTriggered);
    }

    [Fact]
    public void ModifiedAt_ShouldBeNull_Initially()
    {
        // Arrange
        var payload = JsonDocument.Parse("{}").RootElement;

        // Act
        var instanceJob = InstanceJob.Create(
            Guid.NewGuid(),
            "job-name",
            "job-id",
            "domain",
            "flow",
            Guid.NewGuid(),
            "expression",
            payload
        );

        // Assert
        Assert.Null(instanceJob.ModifiedAt);
    }
}

