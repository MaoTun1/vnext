using System;
using System.Collections.Generic;
using BBT.Workflow.BackgroundJobs;
using Xunit;

namespace BBT.Workflow.Domain.Tests.BackgroundJobs;

/// <summary>
/// Unit tests for BackgroundJobInfo
/// </summary>
public class BackgroundJobInfoTests : DomainTestBase<DomainEntryPoint>
{
    private class TestPayload
    {
        public string Data { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    [Fact]
    public void BackgroundJobInfo_ShouldInitializeWithDefaultValues()
    {
        // Arrange & Act
        var jobInfo = new BackgroundJobInfo<TestPayload>();

        // Assert
        Assert.NotNull(jobInfo);
        Assert.Null(jobInfo.Metadata);
        Assert.False(jobInfo.IsTriggered);
    }

    [Fact]
    public void BackgroundJobInfo_ShouldSetAndGetProperties()
    {
        // Arrange
        var payload = new TestPayload { Data = "test-data", Count = 42 };
        var metadata = new Dictionary<string, string>
        {
            ["domain"] = "test-domain",
            ["flowName"] = "test-flow",
            ["instanceId"] = Guid.NewGuid().ToString()
        };

        // Act
        var jobInfo = new BackgroundJobInfo<TestPayload>
        {
            JobName = "test-job",
            JobId = "job-123",
            ExpressionValue = "0 0 * * *",
            Payload = payload,
            Metadata = metadata,
            IsTriggered = true
        };

        // Assert
        Assert.Equal("test-job", jobInfo.JobName);
        Assert.Equal("job-123", jobInfo.JobId);
        Assert.Equal("0 0 * * *", jobInfo.ExpressionValue);
        Assert.Equal(payload, jobInfo.Payload);
        Assert.Equal(metadata, jobInfo.Metadata);
        Assert.True(jobInfo.IsTriggered);
    }

    [Fact]
    public void GetDomain_ShouldReturnEmptyString_WhenMetadataIsNull()
    {
        // Arrange
        var jobInfo = new BackgroundJobInfo<TestPayload>
        {
            Metadata = null
        };

        // Act
        var domain = jobInfo.GetDomain();

        // Assert
        Assert.Equal(string.Empty, domain);
    }

    [Fact]
    public void GetDomain_ShouldReturnDomainValue_WhenLowercaseKeyExists()
    {
        // Arrange
        var jobInfo = new BackgroundJobInfo<TestPayload>
        {
            Metadata = new Dictionary<string, string>
            {
                ["domain"] = "test-domain"
            }
        };

        // Act
        var domain = jobInfo.GetDomain();

        // Assert
        Assert.Equal("test-domain", domain);
    }

    [Fact]
    public void GetDomain_ShouldReturnDomainValue_WhenPascalCaseKeyExists()
    {
        // Arrange
        var jobInfo = new BackgroundJobInfo<TestPayload>
        {
            Metadata = new Dictionary<string, string>
            {
                ["Domain"] = "test-domain-pascal"
            }
        };

        // Act
        var domain = jobInfo.GetDomain();

        // Assert
        Assert.Equal("test-domain-pascal", domain);
    }

    [Fact]
    public void GetDomain_ShouldPreferLowercaseKey_WhenBothExist()
    {
        // Arrange
        var jobInfo = new BackgroundJobInfo<TestPayload>
        {
            Metadata = new Dictionary<string, string>
            {
                ["domain"] = "lowercase-domain",
                ["Domain"] = "pascal-domain"
            }
        };

        // Act
        var domain = jobInfo.GetDomain();

        // Assert
        Assert.Equal("lowercase-domain", domain);
    }

    [Fact]
    public void GetDomain_ShouldReturnEmptyString_WhenDomainValueIsNull()
    {
        // Arrange
        var jobInfo = new BackgroundJobInfo<TestPayload>
        {
            Metadata = new Dictionary<string, string>
            {
                ["domain"] = null!
            }
        };

        // Act
        var domain = jobInfo.GetDomain();

        // Assert
        Assert.Equal(string.Empty, domain);
    }

    [Fact]
    public void GetDomain_ShouldReturnEmptyString_WhenDomainValueIsWhitespace()
    {
        // Arrange
        var jobInfo = new BackgroundJobInfo<TestPayload>
        {
            Metadata = new Dictionary<string, string>
            {
                ["domain"] = "   "
            }
        };

        // Act
        var domain = jobInfo.GetDomain();

        // Assert
        Assert.Equal(string.Empty, domain);
    }

    [Fact]
    public void GetDomain_ShouldReturnEmptyString_WhenKeyDoesNotExist()
    {
        // Arrange
        var jobInfo = new BackgroundJobInfo<TestPayload>
        {
            Metadata = new Dictionary<string, string>
            {
                ["otherKey"] = "value"
            }
        };

        // Act
        var domain = jobInfo.GetDomain();

        // Assert
        Assert.Equal(string.Empty, domain);
    }

    [Fact]
    public void GetFlowName_ShouldReturnEmptyString_WhenMetadataIsNull()
    {
        // Arrange
        var jobInfo = new BackgroundJobInfo<TestPayload>
        {
            Metadata = null
        };

        // Act
        var flowName = jobInfo.GetFlowName();

        // Assert
        Assert.Equal(string.Empty, flowName);
    }

    [Fact]
    public void GetFlowName_ShouldReturnFlowNameValue_WhenCamelCaseKeyExists()
    {
        // Arrange
        var jobInfo = new BackgroundJobInfo<TestPayload>
        {
            Metadata = new Dictionary<string, string>
            {
                ["flowName"] = "test-flow"
            }
        };

        // Act
        var flowName = jobInfo.GetFlowName();

        // Assert
        Assert.Equal("test-flow", flowName);
    }

    [Fact]
    public void GetFlowName_ShouldReturnFlowNameValue_WhenPascalCaseKeyExists()
    {
        // Arrange
        var jobInfo = new BackgroundJobInfo<TestPayload>
        {
            Metadata = new Dictionary<string, string>
            {
                ["FlowName"] = "test-flow-pascal"
            }
        };

        // Act
        var flowName = jobInfo.GetFlowName();

        // Assert
        Assert.Equal("test-flow-pascal", flowName);
    }

    [Fact]
    public void GetFlowName_ShouldPreferCamelCaseKey_WhenBothExist()
    {
        // Arrange
        var jobInfo = new BackgroundJobInfo<TestPayload>
        {
            Metadata = new Dictionary<string, string>
            {
                ["flowName"] = "camel-flow",
                ["FlowName"] = "pascal-flow"
            }
        };

        // Act
        var flowName = jobInfo.GetFlowName();

        // Assert
        Assert.Equal("camel-flow", flowName);
    }

    [Fact]
    public void GetFlowName_ShouldReturnEmptyString_WhenFlowNameValueIsNull()
    {
        // Arrange
        var jobInfo = new BackgroundJobInfo<TestPayload>
        {
            Metadata = new Dictionary<string, string>
            {
                ["flowName"] = null!
            }
        };

        // Act
        var flowName = jobInfo.GetFlowName();

        // Assert
        Assert.Equal(string.Empty, flowName);
    }

    [Fact]
    public void GetFlowName_ShouldReturnEmptyString_WhenFlowNameValueIsWhitespace()
    {
        // Arrange
        var jobInfo = new BackgroundJobInfo<TestPayload>
        {
            Metadata = new Dictionary<string, string>
            {
                ["flowName"] = "   "
            }
        };

        // Act
        var flowName = jobInfo.GetFlowName();

        // Assert
        Assert.Equal(string.Empty, flowName);
    }

    [Fact]
    public void GetFlowName_ShouldReturnEmptyString_WhenKeyDoesNotExist()
    {
        // Arrange
        var jobInfo = new BackgroundJobInfo<TestPayload>
        {
            Metadata = new Dictionary<string, string>
            {
                ["otherKey"] = "value"
            }
        };

        // Act
        var flowName = jobInfo.GetFlowName();

        // Assert
        Assert.Equal(string.Empty, flowName);
    }

    [Fact]
    public void GetInstanceId_ShouldReturnEmptyGuid_WhenMetadataIsNull()
    {
        // Arrange
        var jobInfo = new BackgroundJobInfo<TestPayload>
        {
            Metadata = null
        };

        // Act
        var instanceId = jobInfo.GetInstanceId();

        // Assert
        Assert.Equal(Guid.Empty, instanceId);
    }

    [Fact]
    public void GetInstanceId_ShouldReturnGuidValue_WhenCamelCaseKeyExists()
    {
        // Arrange
        var expectedGuid = Guid.NewGuid();
        var jobInfo = new BackgroundJobInfo<TestPayload>
        {
            Metadata = new Dictionary<string, string>
            {
                ["instanceId"] = expectedGuid.ToString()
            }
        };

        // Act
        var instanceId = jobInfo.GetInstanceId();

        // Assert
        Assert.Equal(expectedGuid, instanceId);
    }

    [Fact]
    public void GetInstanceId_ShouldReturnGuidValue_WhenPascalCaseKeyExists()
    {
        // Arrange
        var expectedGuid = Guid.NewGuid();
        var jobInfo = new BackgroundJobInfo<TestPayload>
        {
            Metadata = new Dictionary<string, string>
            {
                ["InstanceId"] = expectedGuid.ToString()
            }
        };

        // Act
        var instanceId = jobInfo.GetInstanceId();

        // Assert
        Assert.Equal(expectedGuid, instanceId);
    }

    [Fact]
    public void GetInstanceId_ShouldPreferCamelCaseKey_WhenBothExist()
    {
        // Arrange
        var camelGuid = Guid.NewGuid();
        var pascalGuid = Guid.NewGuid();
        var jobInfo = new BackgroundJobInfo<TestPayload>
        {
            Metadata = new Dictionary<string, string>
            {
                ["instanceId"] = camelGuid.ToString(),
                ["InstanceId"] = pascalGuid.ToString()
            }
        };

        // Act
        var instanceId = jobInfo.GetInstanceId();

        // Assert
        Assert.Equal(camelGuid, instanceId);
    }

    [Fact]
    public void GetInstanceId_ShouldReturnEmptyGuid_WhenInstanceIdValueIsNull()
    {
        // Arrange
        var jobInfo = new BackgroundJobInfo<TestPayload>
        {
            Metadata = new Dictionary<string, string>
            {
                ["instanceId"] = null!
            }
        };

        // Act
        var instanceId = jobInfo.GetInstanceId();

        // Assert
        Assert.Equal(Guid.Empty, instanceId);
    }

    [Fact]
    public void GetInstanceId_ShouldReturnEmptyGuid_WhenInstanceIdValueIsWhitespace()
    {
        // Arrange
        var jobInfo = new BackgroundJobInfo<TestPayload>
        {
            Metadata = new Dictionary<string, string>
            {
                ["instanceId"] = "   "
            }
        };

        // Act
        var instanceId = jobInfo.GetInstanceId();

        // Assert
        Assert.Equal(Guid.Empty, instanceId);
    }

    [Fact]
    public void GetInstanceId_ShouldReturnEmptyGuid_WhenKeyDoesNotExist()
    {
        // Arrange
        var jobInfo = new BackgroundJobInfo<TestPayload>
        {
            Metadata = new Dictionary<string, string>
            {
                ["otherKey"] = "value"
            }
        };

        // Act
        var instanceId = jobInfo.GetInstanceId();

        // Assert
        Assert.Equal(Guid.Empty, instanceId);
    }

    [Fact]
    public void GetInstanceId_ShouldThrowFormatException_WhenInstanceIdIsInvalidGuid()
    {
        // Arrange
        var jobInfo = new BackgroundJobInfo<TestPayload>
        {
            Metadata = new Dictionary<string, string>
            {
                ["instanceId"] = "invalid-guid"
            }
        };

        // Act & Assert
        Assert.Throws<FormatException>(() => jobInfo.GetInstanceId());
    }

    [Fact]
    public void BackgroundJobInfo_ShouldSupportMultiplePayloadTypes()
    {
        // Arrange
        var stringPayloadJob = new BackgroundJobInfo<string>
        {
            JobName = "string-job",
            Payload = "test string"
        };

        var dictPayloadJob = new BackgroundJobInfo<Dictionary<string, object>>
        {
            JobName = "dict-job",
            Payload = new Dictionary<string, object> { ["key"] = "value" }
        };

        // Assert
        Assert.Equal("test string", stringPayloadJob.Payload);
        Assert.Equal("value", dictPayloadJob.Payload["key"]);
    }

    [Fact]
    public void BackgroundJobInfo_ShouldHandleCompleteMetadata()
    {
        // Arrange
        var instanceId = Guid.NewGuid();
        var jobInfo = new BackgroundJobInfo<TestPayload>
        {
            Metadata = new Dictionary<string, string>
            {
                ["domain"] = "test-domain",
                ["flowName"] = "test-flow",
                ["instanceId"] = instanceId.ToString()
            }
        };

        // Act
        var domain = jobInfo.GetDomain();
        var flowName = jobInfo.GetFlowName();
        var retrievedInstanceId = jobInfo.GetInstanceId();

        // Assert
        Assert.Equal("test-domain", domain);
        Assert.Equal("test-flow", flowName);
        Assert.Equal(instanceId, retrievedInstanceId);
    }
}

