using System;
using BBT.Workflow.Definitions;
using Xunit;

namespace BBT.Workflow.Instances;

/// <summary>
/// Unit tests for InstanceMetadataExtensions
/// </summary>
public class InstanceMetadataExtensionsTests : DomainTestBase<DomainEntryPoint>
{
    [Fact]
    public void ToSubFlowContractInfo_FromInstance_ShouldMapAllProperties()
    {
        // Arrange
        var instance = InstanceFactory.CreateDefault();
        var expectedId = Guid.NewGuid();
        var expectedKey = "test-key";
        var expectedDomain = "test-domain";
        var expectedFlow = "test-flow";
        var expectedVersion = "1.0.0";
        var expectedState = "test-state";
        var expectedTransition = "test-transition";
        var expectedFlowType = "M";

        instance.MetaData[DomainConsts.MetaDataKeys.Id] = expectedId;
        instance.MetaData[DomainConsts.MetaDataKeys.Key] = expectedKey;
        instance.MetaData[DomainConsts.MetaDataKeys.Domain] = expectedDomain;
        instance.MetaData[DomainConsts.MetaDataKeys.Flow] = expectedFlow;
        instance.MetaData[DomainConsts.MetaDataKeys.Version] = expectedVersion;
        instance.MetaData[DomainConsts.MetaDataKeys.State] = expectedState;
        instance.MetaData[DomainConsts.MetaDataKeys.Transition] = expectedTransition;
        instance.MetaData[DomainConsts.MetaDataKeys.FlowType] = expectedFlowType;

        // Act
        var result = instance.ToSubFlowContractInfo();

        // Assert
        Assert.Equal(expectedId, result.Id);
        Assert.Equal(expectedKey, result.Key);
        Assert.Equal(expectedDomain, result.Domain);
        Assert.Equal(expectedFlow, result.Flow);
        Assert.Equal(expectedVersion, result.Version);
        Assert.Equal(expectedState, result.State);
        Assert.Equal(expectedTransition, result.Transition);
        Assert.Equal(expectedFlowType, result.SubType);
    }

    [Fact]
    public void ToSubFlowContractInfo_FromInstance_ShouldHandleMissingMetadata()
    {
        // Arrange
        var instance = InstanceFactory.CreateDefault();

        // Act
        var result = instance.ToSubFlowContractInfo();

        // Assert
        Assert.Equal(Guid.Empty, result.Id);
        Assert.Null(result.Key);
        Assert.Equal(string.Empty, result.Domain);
        Assert.Equal(string.Empty, result.Flow);
        Assert.Null(result.Version);
        Assert.Null(result.State);
        Assert.Null(result.Transition);
        Assert.Equal(string.Empty, result.SubType);
    }

    [Fact]
    public void ToSubFlowContractInfo_FromInstance_ShouldThrow_WhenInstanceIsNull()
    {
        // Arrange
        Instance? instance = null;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => instance!.ToSubFlowContractInfo());
    }

    [Fact]
    public void ToSubFlowContractInfo_FromObjectDictionary_ShouldMapAllProperties()
    {
        // Arrange
        var expectedId = Guid.NewGuid();
        var expectedKey = "test-key";
        var expectedDomain = "test-domain";
        var expectedFlow = "test-flow";
        var expectedVersion = "1.0.0";
        var expectedState = "test-state";
        var expectedTransition = "test-transition";
        var expectedFlowType = "S";

        var metaData = new ObjectDictionary
        {
            [DomainConsts.MetaDataKeys.Id] = expectedId,
            [DomainConsts.MetaDataKeys.Key] = expectedKey,
            [DomainConsts.MetaDataKeys.Domain] = expectedDomain,
            [DomainConsts.MetaDataKeys.Flow] = expectedFlow,
            [DomainConsts.MetaDataKeys.Version] = expectedVersion,
            [DomainConsts.MetaDataKeys.State] = expectedState,
            [DomainConsts.MetaDataKeys.Transition] = expectedTransition,
            [DomainConsts.MetaDataKeys.FlowType] = expectedFlowType
        };

        // Act
        var result = metaData.ToSubFlowContractInfo();

        // Assert
        Assert.Equal(expectedId, result.Id);
        Assert.Equal(expectedKey, result.Key);
        Assert.Equal(expectedDomain, result.Domain);
        Assert.Equal(expectedFlow, result.Flow);
        Assert.Equal(expectedVersion, result.Version);
        Assert.Equal(expectedState, result.State);
        Assert.Equal(expectedTransition, result.Transition);
        Assert.Equal(expectedFlowType, result.SubType);
    }

    [Fact]
    public void ToSubFlowContractInfo_FromObjectDictionary_ShouldHandleEmptyDictionary()
    {
        // Arrange
        var metaData = new ObjectDictionary();

        // Act
        var result = metaData.ToSubFlowContractInfo();

        // Assert
        Assert.Equal(Guid.Empty, result.Id);
        Assert.Null(result.Key);
        Assert.Equal(string.Empty, result.Domain);
        Assert.Equal(string.Empty, result.Flow);
        Assert.Null(result.Version);
        Assert.Null(result.State);
        Assert.Null(result.Transition);
        Assert.Equal(string.Empty, result.SubType);
    }

    [Fact]
    public void ToFlowType_ShouldReturnCorrectWorkflowType_WhenMetadataIsSet()
    {
        // Arrange
        var instance = InstanceFactory.CreateDefault();
        instance.MetaData[DomainConsts.MetaDataKeys.FlowType] = "S";

        // Act
        var result = instance.ToFlowType();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(WorkflowType.SubFlow, result);
    }

    [Fact]
    public void ToFlowType_ShouldReturnSubFlow_WhenMetadataIsSubFlow()
    {
        // Arrange
        var instance = InstanceFactory.CreateDefault();
        instance.MetaData[DomainConsts.MetaDataKeys.FlowType] = "S";

        // Act
        var result = instance.ToFlowType();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(WorkflowType.SubFlow, result);
    }

    [Fact]
    public void ToFlowType_ShouldReturnNull_WhenMetadataIsMissing()
    {
        // Arrange
        var instance = InstanceFactory.CreateDefault();

        // Act
        var result = instance.ToFlowType();

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void ToFlowType_ShouldReturnNull_WhenMetadataIsEmpty()
    {
        // Arrange
        var instance = InstanceFactory.CreateDefault();
        instance.MetaData[DomainConsts.MetaDataKeys.FlowType] = string.Empty;

        // Act
        var result = instance.ToFlowType();

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void GetValue_ShouldReturnTypedValue_WhenKeyExists()
    {
        // Arrange
        var instance = InstanceFactory.CreateDefault();
        instance.MetaData["test.key"] = "test-value";

        // Act
        var result = instance.GetValue<string>("test.key");

        // Assert
        Assert.Equal("test-value", result);
    }

    [Fact]
    public void GetValue_ShouldReturnDefault_WhenKeyDoesNotExist()
    {
        // Arrange
        var instance = InstanceFactory.CreateDefault();

        // Act
        var result = instance.GetValue<string>("non-existent-key");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void GetValue_ShouldReturnDefault_WhenTypeDoesNotMatch()
    {
        // Arrange
        var instance = InstanceFactory.CreateDefault();
        instance.MetaData["test.key"] = "test-value";

        // Act
        var result = instance.GetValue<int>("test.key");

        // Assert
        Assert.Equal(0, result);
    }

    [Fact]
    public void GetValue_ShouldReturnDefault_WhenMetaDataIsNull()
    {
        // Arrange
        var instance = InstanceFactory.CreateDefault();
        instance.SetMetaData(null!);

        // Act
        var result = instance.GetValue<string>("test.key");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void ToSubFlowContractInfo_ShouldParseGuidFromString()
    {
        // Arrange
        var expectedId = Guid.NewGuid();
        var metaData = new ObjectDictionary
        {
            [DomainConsts.MetaDataKeys.Id] = expectedId.ToString()
        };

        // Act
        var result = metaData.ToSubFlowContractInfo();

        // Assert
        Assert.Equal(expectedId, result.Id);
    }

    [Fact]
    public void ToSubFlowContractInfo_ShouldHandleInvalidGuidString()
    {
        // Arrange
        var metaData = new ObjectDictionary
        {
            [DomainConsts.MetaDataKeys.Id] = "invalid-guid"
        };

        // Act
        var result = metaData.ToSubFlowContractInfo();

        // Assert
        Assert.Equal(Guid.Empty, result.Id);
    }

    [Fact]
    public void ToSubFlowContractInfo_ShouldHandleNumericValues()
    {
        // Arrange
        var metaData = new ObjectDictionary
        {
            [DomainConsts.MetaDataKeys.Key] = 12345
        };

        // Act
        var result = metaData.ToSubFlowContractInfo();

        // Assert
        Assert.Equal("12345", result.Key);
    }
}

