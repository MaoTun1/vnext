using System;
using Xunit;

namespace BBT.Workflow.Instances;

/// <summary>
/// Unit tests for InstanceAction
/// </summary>
public class InstanceActionTests : DomainTestBase<DomainEntryPoint>
{
    [Fact]
    public void Constructor_ShouldInitializeAllProperties()
    {
        // Arrange
        var id = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var status = "Pending";
        var detail = JsonData.CreateFrom("{\"key\":\"value\"}");

        // Act
        var instanceAction = new InstanceAction(id, taskId, status, detail);

        // Assert
        Assert.Equal(id, instanceAction.Id);
        Assert.Equal(taskId, instanceAction.TaskId);
        Assert.Equal(status, instanceAction.Status);
        Assert.Equal(detail, instanceAction.Detail);
        Assert.Null(instanceAction.FinishedAt);
        Assert.Null(instanceAction.Duration);
    }

    [Fact]
    public void Constructor_ShouldSetStartedAtToCurrentTime()
    {
        // Arrange
        var before = DateTime.UtcNow.AddSeconds(-1);

        // Act
        var instanceAction = new InstanceAction(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Pending",
            JsonData.CreateFrom("{}")
        );
        var after = DateTime.UtcNow.AddSeconds(1);

        // Assert
        Assert.True(instanceAction.StartedAt >= before && instanceAction.StartedAt <= after);
    }

    [Fact]
    public void Constructor_ShouldHandleNullDetail()
    {
        // Arrange & Act
        var instanceAction = new InstanceAction(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Pending",
            null
        );

        // Assert
        Assert.NotNull(instanceAction.Detail);
        Assert.Equal(JsonData.Empty, instanceAction.Detail);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Constructor_ShouldThrow_WhenStatusIsInvalid(string? status)
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentException>(() =>
            new InstanceAction(
                Guid.NewGuid(),
                Guid.NewGuid(),
                status!,
                JsonData.CreateFrom("{}")
            ));
    }

    [Fact]
    public void Constructor_WithDetailData_ShouldStoreCorrectly()
    {
        // Arrange
        var detail = JsonData.CreateFrom(@"
        {
            ""action"": ""process"",
            ""parameters"": {
                ""timeout"": 30,
                ""retries"": 3
            }
        }");

        // Act
        var instanceAction = new InstanceAction(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Processing",
            detail
        );

        // Assert
        Assert.NotNull(instanceAction.Detail);
        Assert.Contains("action", instanceAction.Detail.Json);
        Assert.Contains("parameters", instanceAction.Detail.Json);
    }

    [Fact]
    public void Duration_ShouldBeNull_Initially()
    {
        // Arrange & Act
        var instanceAction = new InstanceAction(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Pending",
            JsonData.CreateFrom("{}")
        );

        // Assert
        Assert.Null(instanceAction.Duration);
    }

    [Fact]
    public void FinishedAt_ShouldBeNull_Initially()
    {
        // Arrange & Act
        var instanceAction = new InstanceAction(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Pending",
            JsonData.CreateFrom("{}")
        );

        // Assert
        Assert.Null(instanceAction.FinishedAt);
    }

    [Theory]
    [InlineData("Pending")]
    [InlineData("Processing")]
    [InlineData("Completed")]
    [InlineData("Failed")]
    [InlineData("Cancelled")]
    public void Constructor_ShouldAcceptVariousStatusValues(string status)
    {
        // Arrange & Act
        var instanceAction = new InstanceAction(
            Guid.NewGuid(),
            Guid.NewGuid(),
            status,
            JsonData.CreateFrom("{}")
        );

        // Assert
        Assert.Equal(status, instanceAction.Status);
    }

    [Fact]
    public void Constructor_WithEmptyJsonDetail_ShouldWork()
    {
        // Arrange
        var detail = JsonData.CreateFrom("{}");

        // Act
        var instanceAction = new InstanceAction(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Pending",
            detail
        );

        // Assert
        Assert.NotNull(instanceAction.Detail);
        Assert.Equal("{}", instanceAction.Detail.Json);
    }

    [Fact]
    public void Constructor_WithComplexJsonDetail_ShouldPreserveStructure()
    {
        // Arrange
        var detail = JsonData.CreateFrom(@"
        {
            ""nested"": {
                ""array"": [1, 2, 3],
                ""object"": {
                    ""key"": ""value""
                }
            }
        }");

        // Act
        var instanceAction = new InstanceAction(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Processing",
            detail
        );

        // Assert
        Assert.Contains("nested", instanceAction.Detail.Json);
        Assert.Contains("array", instanceAction.Detail.Json);
        Assert.Contains("object", instanceAction.Detail.Json);
    }

    [Fact]
    public void TaskId_ShouldBeAccessible()
    {
        // Arrange
        var taskId = Guid.NewGuid();

        // Act
        var instanceAction = new InstanceAction(
            Guid.NewGuid(),
            taskId,
            "Pending",
            JsonData.CreateFrom("{}")
        );

        // Assert
        Assert.Equal(taskId, instanceAction.TaskId);
    }

    [Fact]
    public void Status_ShouldBeAccessible()
    {
        // Arrange
        var status = "CustomStatus";

        // Act
        var instanceAction = new InstanceAction(
            Guid.NewGuid(),
            Guid.NewGuid(),
            status,
            JsonData.CreateFrom("{}")
        );

        // Assert
        Assert.Equal(status, instanceAction.Status);
    }

    [Fact]
    public void Detail_ShouldBeAccessible()
    {
        // Arrange
        var detail = JsonData.CreateFrom("{\"key\":\"value\"}");

        // Act
        var instanceAction = new InstanceAction(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Pending",
            detail
        );

        // Assert
        Assert.Equal(detail.Json, instanceAction.Detail.Json);
    }

    [Fact]
    public void Constructor_WithMaxLengthStatus_ShouldWork()
    {
        // Arrange
        // InstanceActionConstants.MaxStatusLength is 70
        var status = new string('A', InstanceActionConstants.MaxStatusLength);

        // Act
        var instanceAction = new InstanceAction(
            Guid.NewGuid(),
            Guid.NewGuid(),
            status,
            JsonData.CreateFrom("{}")
        );

        // Assert
        Assert.Equal(status, instanceAction.Status);
    }

    [Fact]
    public void Constructor_WithMultipleInstances_ShouldHaveUniqueStartedAt()
    {
        // Arrange & Act
        var action1 = new InstanceAction(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Pending",
            JsonData.CreateFrom("{}")
        );

        System.Threading.Thread.Sleep(10);

        var action2 = new InstanceAction(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Pending",
            JsonData.CreateFrom("{}")
        );

        // Assert
        Assert.NotEqual(action1.StartedAt, action2.StartedAt);
    }

    [Fact]
    public void Constructor_WithSameTaskId_ShouldAllowMultipleActions()
    {
        // Arrange
        var taskId = Guid.NewGuid();

        // Act
        var action1 = new InstanceAction(
            Guid.NewGuid(),
            taskId,
            "Pending",
            JsonData.CreateFrom("{\"step\":1}")
        );

        var action2 = new InstanceAction(
            Guid.NewGuid(),
            taskId,
            "Processing",
            JsonData.CreateFrom("{\"step\":2}")
        );

        // Assert
        Assert.Equal(taskId, action1.TaskId);
        Assert.Equal(taskId, action2.TaskId);
        Assert.NotEqual(action1.Id, action2.Id);
        Assert.NotEqual(action1.Status, action2.Status);
    }

    [Fact]
    public void Constructor_WithDifferentDetailTypes_ShouldStoreCorrectly()
    {
        // Arrange
        var stringDetail = JsonData.CreateFrom("{\"type\":\"string\",\"value\":\"test\"}");
        var numberDetail = JsonData.CreateFrom("{\"type\":\"number\",\"value\":42}");
        var arrayDetail = JsonData.CreateFrom("{\"type\":\"array\",\"value\":[1,2,3]}");
        var boolDetail = JsonData.CreateFrom("{\"type\":\"boolean\",\"value\":true}");

        // Act
        var action1 = new InstanceAction(Guid.NewGuid(), Guid.NewGuid(), "Test1", stringDetail);
        var action2 = new InstanceAction(Guid.NewGuid(), Guid.NewGuid(), "Test2", numberDetail);
        var action3 = new InstanceAction(Guid.NewGuid(), Guid.NewGuid(), "Test3", arrayDetail);
        var action4 = new InstanceAction(Guid.NewGuid(), Guid.NewGuid(), "Test4", boolDetail);

        // Assert
        Assert.Contains("string", action1.Detail.Json);
        Assert.Contains("number", action2.Detail.Json);
        Assert.Contains("array", action3.Detail.Json);
        Assert.Contains("boolean", action4.Detail.Json);
    }
}

