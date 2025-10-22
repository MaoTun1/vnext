using System.Linq;
using BBT.Workflow.BackgroundJobs;
using Xunit;

namespace BBT.Workflow.Domain.Tests.BackgroundJobs;

/// <summary>
/// Unit tests for BackgroundJobConsts
/// </summary>
public class BackgroundJobConstsTests : DomainTestBase<DomainEntryPoint>
{
    [Fact]
    public void FlowTimeoutJobName_ShouldHaveCorrectValue()
    {
        // Assert
        Assert.Equal("workflow-timeout-job", BackgroundJobConsts.FlowTimeoutJobName);
    }

    [Fact]
    public void TransitionTimerJobName_ShouldHaveCorrectValue()
    {
        // Assert
        Assert.Equal("workflow-transition-timer-job", BackgroundJobConsts.TransitionTimerJobName);
    }

    [Fact]
    public void TransitionJobName_ShouldHaveCorrectValue()
    {
        // Assert
        Assert.Equal("workflow-transition-job", BackgroundJobConsts.TransitionJobName);
    }

    [Fact]
    public void AllJobNames_ShouldBeDistinct()
    {
        // Arrange
        var jobNames = new[]
        {
            BackgroundJobConsts.FlowTimeoutJobName,
            BackgroundJobConsts.TransitionTimerJobName,
            BackgroundJobConsts.TransitionJobName
        };

        // Act
        var distinctCount = jobNames.Distinct().Count();

        // Assert
        Assert.Equal(jobNames.Length, distinctCount);
    }

    [Fact]
    public void AllJobNames_ShouldFollowNamingConvention()
    {
        // Arrange
        var jobNames = new[]
        {
            BackgroundJobConsts.FlowTimeoutJobName,
            BackgroundJobConsts.TransitionTimerJobName,
            BackgroundJobConsts.TransitionJobName
        };

        // Assert
        foreach (var jobName in jobNames)
        {
            Assert.StartsWith("workflow-", jobName);
            Assert.EndsWith("-job", jobName);
            Assert.DoesNotContain(" ", jobName);
            Assert.Equal(jobName.ToLowerInvariant(), jobName);
        }
    }

    [Fact]
    public void AllJobNames_ShouldNotBeNullOrEmpty()
    {
        // Assert
        Assert.False(string.IsNullOrWhiteSpace(BackgroundJobConsts.FlowTimeoutJobName));
        Assert.False(string.IsNullOrWhiteSpace(BackgroundJobConsts.TransitionTimerJobName));
        Assert.False(string.IsNullOrWhiteSpace(BackgroundJobConsts.TransitionJobName));
    }
}

