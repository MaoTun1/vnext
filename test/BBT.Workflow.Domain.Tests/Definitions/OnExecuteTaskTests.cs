using System;
using System.Linq;
using BBT.Workflow.Definitions;
using Xunit;

namespace BBT.Workflow.Definitions;

public class OnExecuteTaskTests
{
    [Fact]
    public void Create_ShouldInitializeProperties()
    {
        // Arrange
        var order = 1;
        var taskReference = new Reference("task-1", "domain", "sys-tasks", "1.0.0");
        var mapping = new ScriptCode("mapping-location", 
            Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("return data;")));

        // Act
        var onExecuteTask = OnExecuteTask.Create(order, taskReference, mapping);

        // Assert
        Assert.NotNull(onExecuteTask);
        Assert.Equal(order, onExecuteTask.Order);
        Assert.NotNull(onExecuteTask.Task);
        Assert.Equal("task-1", onExecuteTask.Task.Key);
        Assert.NotNull(onExecuteTask.Mapping);
        Assert.Equal("mapping-location", onExecuteTask.Mapping.Location);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(100)]
    public void Create_ShouldAcceptVariousOrderValues(int order)
    {
        // Arrange
        var taskReference = new Reference("task", "domain", "sys-tasks", "1.0.0");
        var mapping = new ScriptCode("location", Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("code")));

        // Act
        var onExecuteTask = OnExecuteTask.Create(order, taskReference, mapping);

        // Assert
        Assert.Equal(order, onExecuteTask.Order);
    }

    [Fact]
    public void Task_ShouldBeReferenceType()
    {
        // Arrange
        var taskReference = new Reference("task-1", "domain", "sys-tasks", "1.0.0");
        var mapping = new ScriptCode("location", Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("code")));

        // Act
        var onExecuteTask = OnExecuteTask.Create(1, taskReference, mapping);

        // Assert
        Assert.NotNull(onExecuteTask.Task);
        Assert.IsType<Reference>(onExecuteTask.Task);
    }

    [Fact]
    public void Mapping_ShouldBeScriptCodeType()
    {
        // Arrange
        var taskReference = new Reference("task-1", "domain", "sys-tasks", "1.0.0");
        var mapping = new ScriptCode("location", Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("code")));

        // Act
        var onExecuteTask = OnExecuteTask.Create(1, taskReference, mapping);

        // Assert
        Assert.NotNull(onExecuteTask.Mapping);
        Assert.IsType<ScriptCode>(onExecuteTask.Mapping);
    }

    [Fact]
    public void Create_ShouldPreserveTaskReferenceProperties()
    {
        // Arrange
        var key = "task-key";
        var domain = "test-domain";
        var flow = "sys-tasks";
        var version = "2.3.1";
        var taskReference = new Reference(key, domain, flow, version);
        var mapping = new ScriptCode("location", Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("code")));

        // Act
        var onExecuteTask = OnExecuteTask.Create(1, taskReference, mapping);

        // Assert
        Assert.Equal(key, onExecuteTask.Task.Key);
        Assert.Equal(domain, onExecuteTask.Task.Domain);
        Assert.Equal(flow, onExecuteTask.Task.Flow);
        Assert.Equal(version, onExecuteTask.Task.Version);
    }

    [Fact]
    public void Create_ShouldPreserveMappingProperties()
    {
        // Arrange
        var location = "test-mapping-location";
        var code = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("function() { return result; }"));
        var taskReference = new Reference("task", "domain", "sys-tasks", "1.0.0");
        var mapping = new ScriptCode(location, code);

        // Act
        var onExecuteTask = OnExecuteTask.Create(1, taskReference, mapping);

        // Assert
        Assert.Equal(location, onExecuteTask.Mapping.Location);
        Assert.Equal(code, onExecuteTask.Mapping.Code);
    }

    [Fact]
    public void Create_ShouldHandleComplexMapping()
    {
        // Arrange
        var complexScript = @"
            function transform(input) {
                const result = {
                    ...input,
                    timestamp: new Date().toISOString(),
                    processed: true
                };
                return result;
            }
            return transform(data);
        ";
        var taskReference = new Reference("complex-task", "domain", "sys-tasks", "1.0.0");
        var mapping = new ScriptCode("complex-mapping", 
            Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(complexScript)));

        // Act
        var onExecuteTask = OnExecuteTask.Create(5, taskReference, mapping);

        // Assert
        Assert.Equal(5, onExecuteTask.Order);
        Assert.Equal(complexScript, onExecuteTask.Mapping.DecodedCode);
    }

    [Fact]
    public void Create_ShouldAllowMultipleTasksWithSameReference()
    {
        // Arrange
        var taskReference = new Reference("task-1", "domain", "sys-tasks", "1.0.0");
        var mapping1 = new ScriptCode("location1", Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("code1")));
        var mapping2 = new ScriptCode("location2", Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("code2")));

        // Act
        var task1 = OnExecuteTask.Create(1, taskReference, mapping1);
        var task2 = OnExecuteTask.Create(2, taskReference, mapping2);

        // Assert
        Assert.Equal(1, task1.Order);
        Assert.Equal(2, task2.Order);
        Assert.Equal("task-1", task1.Task.Key);
        Assert.Equal("task-1", task2.Task.Key);
        Assert.NotEqual(task1.Mapping.Location, task2.Mapping.Location);
    }

    [Fact]
    public void Order_ShouldAllowNegativeValues()
    {
        // Arrange
        var taskReference = new Reference("task", "domain", "sys-tasks", "1.0.0");
        var mapping = new ScriptCode("location", Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("code")));

        // Act
        var onExecuteTask = OnExecuteTask.Create(-1, taskReference, mapping);

        // Assert
        Assert.Equal(-1, onExecuteTask.Order);
    }

    [Fact]
    public void Create_ShouldConvertIReferenceToReference()
    {
        // Arrange
        IReference taskReference = new Reference("task", "domain", "sys-tasks", "1.0.0");
        var mapping = new ScriptCode("location", Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("code")));

        // Act
        var onExecuteTask = OnExecuteTask.Create(1, taskReference, mapping);

        // Assert
        Assert.NotNull(onExecuteTask.Task);
        Assert.IsType<Reference>(onExecuteTask.Task);
        Assert.Equal(taskReference.Key, onExecuteTask.Task.Key);
    }
}

