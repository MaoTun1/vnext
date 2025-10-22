using System;
using Xunit;

namespace BBT.Workflow.Shared;

public class ExecutionActorTests
{
    [Fact]
    public void ExecutionActor_ShouldHaveUserValue()
    {
        // Act
        var actor = ExecutionActor.User;

        // Assert
        Assert.Equal(0, (int)actor);
        Assert.Equal("User", actor.ToString());
    }

    [Fact]
    public void ExecutionActor_ShouldHaveSystemValue()
    {
        // Act
        var actor = ExecutionActor.System;

        // Assert
        Assert.Equal(1, (int)actor);
        Assert.Equal("System", actor.ToString());
    }

    [Fact]
    public void ExecutionActor_ShouldBeComparable()
    {
        // Arrange
        var user = ExecutionActor.User;
        var system = ExecutionActor.System;

        // Act & Assert
        Assert.True(user == ExecutionActor.User);
        Assert.True(system == ExecutionActor.System);
        Assert.False(user == system);
    }

    [Theory]
    [InlineData(ExecutionActor.User, 0)]
    [InlineData(ExecutionActor.System, 1)]
    public void ExecutionActor_ShouldHaveCorrectIntegerValue(ExecutionActor actor, int expectedValue)
    {
        // Act
        var value = (int)actor;

        // Assert
        Assert.Equal(expectedValue, value);
    }

    [Fact]
    public void ExecutionActor_ShouldCastFromInteger()
    {
        // Act
        var user = (ExecutionActor)0;
        var system = (ExecutionActor)1;

        // Assert
        Assert.Equal(ExecutionActor.User, user);
        Assert.Equal(ExecutionActor.System, system);
    }

    [Fact]
    public void ExecutionActor_ShouldBeUsableInSwitch()
    {
        // Arrange
        var actor = ExecutionActor.User;
        var result = "";

        // Act
        switch (actor)
        {
            case ExecutionActor.User:
                result = "User executed";
                break;
            case ExecutionActor.System:
                result = "System executed";
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(actor), actor, null);
        }

        // Assert
        Assert.Equal("User executed", result);
    }

    [Theory]
    [InlineData(ExecutionActor.User, "User")]
    [InlineData(ExecutionActor.System, "System")]
    public void ToString_ShouldReturnCorrectName(ExecutionActor actor, string expected)
    {
        // Act
        var result = actor.ToString();

        // Assert
        Assert.Equal(expected, result);
    }
}

