using System;
using System.Linq;
using BBT.Workflow.Definitions;
using Xunit;

namespace BBT.Workflow.Instances;

/// <summary>
/// Unit tests for InstanceData
/// </summary>
public class InstanceDataTests : DomainTestBase<DomainEntryPoint>
{
    [Fact]
    public void AddData_ShouldInitializeAllProperties()
    {
        // Arrange
        var instance = InstanceFactory.CreateDefault();
        var id = Guid.NewGuid();
        var data = JsonData.CreateFrom("{\"key\":\"value\"}");

        // Act
        var instanceData = instance.AddData(id, data);

        // Assert
        Assert.Equal(id, instanceData.Id);
        Assert.Equal(instance.Id, instanceData.InstanceId);
        Assert.Equal("1.0.0", instanceData.Version);
        Assert.True(instanceData.IsLatest);
        Assert.NotNull(instanceData.ETag);
        Assert.NotNull(instanceData.DataHash);
        Assert.NotNull(instanceData.Data);
        Assert.NotEqual(default, instanceData.EnteredAt);
    }

    [Fact]
    public void AddData_ShouldCreateNewVersion_WithIncrementedVersion()
    {
        // Arrange
        var instance = InstanceFactory.CreateDefault();
        var originalData = JsonData.CreateFrom("{\"key\":\"value1\"}");
        instance.AddData(Guid.NewGuid(), originalData);

        var newId = Guid.NewGuid();
        var newData = JsonData.CreateFrom("{\"key\":\"value2\"}");

        // Act
        var newVersion = instance.AddData(newId, newData, VersionStrategy.IncreasePatch);

        // Assert
        Assert.Equal(newId, newVersion.Id);
        Assert.Equal("1.0.1", newVersion.Version);
        Assert.True(newVersion.IsLatest);
        Assert.False(instance.DataList.First().IsLatest); // Old version should be marked as not latest
    }

    [Fact]
    public void AddData_ShouldMergeJsonData()
    {
        // Arrange
        var instance = InstanceFactory.CreateDefault();
        var originalData = JsonData.CreateFrom("{\"key1\":\"value1\"}");
        instance.AddData(Guid.NewGuid(), originalData);

        var additionalData = JsonData.CreateFrom("{\"key2\":\"value2\"}");

        // Act
        var newVersion = instance.AddData(Guid.NewGuid(), additionalData, VersionStrategy.IncreasePatch);

        // Assert
        Assert.Contains("key1", newVersion.Data.Json);
        Assert.Contains("key2", newVersion.Data.Json);
    }

    [Theory]
    [InlineData("Major", "2.0.0")]
    [InlineData("Minor", "1.1.0")]
    [InlineData("Patch", "1.0.1")]
    public void AddData_ShouldIncrementVersionCorrectly(string strategyCode, string expectedVersion)
    {
        // Arrange
        var instance = InstanceFactory.CreateDefault();
        var originalData = JsonData.CreateFrom("{}");
        instance.AddData(Guid.NewGuid(), originalData); // Creates 1.0.0

        var newData = JsonData.CreateFrom("{\"new\":\"data\"}");
        var strategy = VersionStrategy.FromCode(strategyCode);

        // Act
        var newVersion = instance.AddData(Guid.NewGuid(), newData, strategy);

        // Assert
        Assert.Equal(expectedVersion, newVersion.Version);
    }

    [Fact]
    public void HasSameData_ShouldReturnTrue_WhenDataIsIdentical()
    {
        // Arrange
        var instance = InstanceFactory.CreateDefault();
        var data1 = JsonData.CreateFrom("{\"key\":\"value\"}");
        var instanceData = instance.AddData(Guid.NewGuid(), data1);

        var data2 = JsonData.CreateFrom("{\"key\":\"value\"}");

        // Act
        var result = instanceData.HasSameData(data2);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void HasSameData_ShouldReturnFalse_WhenDataIsDifferent()
    {
        // Arrange
        var instance = InstanceFactory.CreateDefault();
        var data1 = JsonData.CreateFrom("{\"key\":\"value1\"}");
        var instanceData = instance.AddData(Guid.NewGuid(), data1);

        var data2 = JsonData.CreateFrom("{\"key\":\"value2\"}");

        // Act
        var result = instanceData.HasSameData(data2);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void HasSameData_ShouldReturnTrue_WithDifferentJsonFormatting()
    {
        // Arrange - Same semantic content but different formatting
        var instance = InstanceFactory.CreateDefault();
        var data1 = JsonData.CreateFrom("{\"key\":\"value\",\"number\":123}");
        var instanceData = instance.AddData(Guid.NewGuid(), data1);

        var data2 = JsonData.CreateFrom("{ \"number\": 123, \"key\": \"value\" }"); // Different order & spacing

        // Act
        var result = instanceData.HasSameData(data2);

        // Assert
        Assert.True(result); // Should be true because semantic content is same
    }

    [Fact]
    public void AddData_ShouldMarkPreviousAsNotLatest()
    {
        // Arrange
        var instance = InstanceFactory.CreateDefault();
        var data1 = JsonData.CreateFrom("{\"v\":1}");
        var instanceData1 = instance.AddData(Guid.NewGuid(), data1);

        Assert.True(instanceData1.IsLatest);

        // Act
        var data2 = JsonData.CreateFrom("{\"v\":2}");
        instance.AddData(Guid.NewGuid(), data2);

        // Assert
        Assert.False(instanceData1.IsLatest);
    }

    [Fact]
    public void InstanceData_ShouldHaveUniqueETag()
    {
        // Arrange
        var instance = InstanceFactory.CreateDefault();
        var data1 = JsonData.CreateFrom("{\"key\":\"value\"}");
        var data2 = JsonData.CreateFrom("{\"key\":\"value1\"}");

        // Act
        var instanceData1 = instance.AddData(Guid.NewGuid(), data1);
        var instanceData2 = instance.AddData(Guid.NewGuid(), data2);

        // Assert - Even with same data, ETags should be different
        Assert.NotEqual(instanceData1.ETag, instanceData2.ETag);
    }

    [Fact]
    public void DataHash_ShouldBeConsistent_ForSameData()
    {
        // Arrange
        var instance1 = InstanceFactory.CreateDefault();
        var instance2 = InstanceFactory.CreateDefault();
        var data1 = JsonData.CreateFrom("{\"key\":\"value\"}");
        var data2 = JsonData.CreateFrom("{\"key\":\"value\"}");

        // Act
        var instanceData1 = instance1.AddData(Guid.NewGuid(), data1);
        var instanceData2 = instance2.AddData(Guid.NewGuid(), data2);

        // Assert
        Assert.Equal(instanceData1.DataHash, instanceData2.DataHash);
    }

    [Fact]
    public void DataHash_ShouldBeDifferent_ForDifferentData()
    {
        // Arrange
        var instance = InstanceFactory.CreateDefault();
        var data1 = JsonData.CreateFrom("{\"key\":\"value1\"}");
        var data2 = JsonData.CreateFrom("{\"key\":\"value2\"}");

        // Act
        var instanceData1 = instance.AddData(Guid.NewGuid(), data1);
        var instanceData2 = instance.AddData(Guid.NewGuid(), data2);

        // Assert
        Assert.NotEqual(instanceData1.DataHash, instanceData2.DataHash);
    }

    [Fact]
    public void Attributes_ShouldReturnDynamicObject()
    {
        // Arrange
        var instance = InstanceFactory.CreateDefault();
        var data = JsonData.CreateFrom("{\"key\":\"value\",\"number\":42}");
        var instanceData = instance.AddData(Guid.NewGuid(), data);

        // Act
        var attributes = instanceData.Attributes;

        // Assert
        Assert.NotNull(attributes);
    }

    [Fact]
    public void ETag_ShouldBeUnique_ForDifferentInstances()
    {
        // Arrange
        var instance = InstanceFactory.CreateDefault();
        
        // Farklı datalar kullanmalıyız çünkü aynı data için 
        // yeni InstanceData oluşturulmaz (data hash kontrolü var)
        var data1 = JsonData.CreateFrom("{\"v\":1}");
        var data2 = JsonData.CreateFrom("{\"v\":2}");

        // Act
        var instanceData1 = instance.AddData(Guid.NewGuid(), data1);
        var instanceData2 = instance.AddData(Guid.NewGuid(), data2);

        // Assert
        Assert.NotEqual(instanceData1.ETag, instanceData2.ETag);
    }

    [Fact]
    public void AddData_ShouldSetEnteredAtToCurrentTime()
    {
        // Arrange
        var instance = InstanceFactory.CreateDefault();
        var before = DateTime.UtcNow.AddSeconds(-1);

        // Act
        var instanceData = instance.AddData(Guid.NewGuid(), JsonData.CreateFrom("{}"));
        var after = DateTime.UtcNow.AddSeconds(1);

        // Assert
        Assert.True(instanceData.EnteredAt >= before && instanceData.EnteredAt <= after);
    }

    [Fact]
    public void AddDataWithVersion_ShouldUseProvidedVersion()
    {
        // Arrange
        var instance = InstanceFactory.CreateDefault();
        var data = JsonData.CreateFrom("{}");

        // Act
        var instanceData = instance.AddDataWithVersion(Guid.NewGuid(), data, "2.5.3");

        // Assert
        Assert.Equal("2.5.3", instanceData.Version);
    }

    [Fact]
    public void AddData_WithDifferentStrategies_ShouldIncrementCorrectly()
    {
        // Arrange
        var instance = InstanceFactory.CreateDefault();
        instance.AddData(Guid.NewGuid(), JsonData.CreateFrom("{}"));

        // Act - Major
        var major = instance.AddData(Guid.NewGuid(), JsonData.CreateFrom("{\"v\":\"major\"}"), VersionStrategy.IncreaseMajor);
        
        // Assert
        Assert.Equal("2.0.0", major.Version);
    }

    [Fact]
    public void DataHash_ShouldBeConsistent_WithNormalizedJson()
    {
        // Arrange - Same semantic content with different formatting
        var instance1 = InstanceFactory.CreateDefault();
        var instance2 = InstanceFactory.CreateDefault();
        var data1 = JsonData.CreateFrom("{\"a\":1,\"b\":2}");
        var data2 = JsonData.CreateFrom("{ \"b\": 2, \"a\": 1 }"); // Different order

        // Act
        var instanceData1 = instance1.AddData(Guid.NewGuid(), data1);
        var instanceData2 = instance2.AddData(Guid.NewGuid(), data2);

        // Assert
        Assert.Equal(instanceData1.DataHash, instanceData2.DataHash);
    }

    #region Extended Version Format Tests (MAJOR.MINOR.PATCH-pkg.PKG_VERSION+PKG_NAME)

    [Fact]
    public void AddDataWithVersion_ShouldAcceptExtendedVersionFormat()
    {
        // Arrange
        var instance = InstanceFactory.CreateDefault();
        var data = JsonData.CreateFrom("{}");
        var extendedVersion = "1.0.0-pkg.1.17.0+account";

        // Act
        var instanceData = instance.AddDataWithVersion(Guid.NewGuid(), data, extendedVersion);

        // Assert
        Assert.Equal(extendedVersion, instanceData.Version);
    }

    [Theory]
    [InlineData("1.0.0-pkg.1.17.0+account", "Major", "2.0.0-pkg.1.17.0+account")]
    [InlineData("1.0.0-pkg.1.17.0+account", "Minor", "1.1.0-pkg.1.17.0+account")]
    [InlineData("1.0.0-pkg.1.17.0+account", "Patch", "1.0.1-pkg.1.17.0+account")]
    [InlineData("2.5.3-pkg.10.20.30+myapp", "Major", "3.0.0-pkg.10.20.30+myapp")]
    [InlineData("2.5.3-pkg.10.20.30+myapp", "Minor", "2.6.0-pkg.10.20.30+myapp")]
    [InlineData("2.5.3-pkg.10.20.30+myapp", "Patch", "2.5.4-pkg.10.20.30+myapp")]
    public void AddData_ShouldPreservePackageVersionAndMetadata_WhenIncrementing(
        string originalVersion,
        string strategyCode,
        string expectedVersion)
    {
        // Arrange
        var instance = InstanceFactory.CreateDefault();
        var originalData = JsonData.CreateFrom("{\"original\":true}");
        instance.AddDataWithVersion(Guid.NewGuid(), originalData, originalVersion);

        var newData = JsonData.CreateFrom("{\"new\":\"data\"}");
        var strategy = VersionStrategy.FromCode(strategyCode);

        // Act
        var newVersion = instance.AddData(Guid.NewGuid(), newData, strategy);

        // Assert
        Assert.Equal(expectedVersion, newVersion.Version);
    }

    [Fact]
    public void AddData_ShouldPreserveSuffix_WhenNoStrategyApplied()
    {
        // Arrange
        var instance = InstanceFactory.CreateDefault();
        var version = "1.0.0-pkg.1.17.0+account";
        var originalData = JsonData.CreateFrom("{\"original\":true}");
        instance.AddDataWithVersion(Guid.NewGuid(), originalData, version);

        var newData = JsonData.CreateFrom("{\"new\":\"data\"}");

        // Act - Using None strategy should keep the same version
        var newVersion = instance.AddData(Guid.NewGuid(), newData, VersionStrategy.None);

        // Assert
        Assert.Equal(version, newVersion.Version);
    }

    [Fact]
    public void AddDataWithVersion_ShouldHandleMultipleExtendedVersions()
    {
        // Arrange
        var instance = InstanceFactory.CreateDefault();

        // Act - Add multiple versions with extended format
        var v1 = instance.AddDataWithVersion(Guid.NewGuid(), JsonData.CreateFrom("{\"v\":1}"), "1.0.0-pkg.1.2.0+account");
        var v2 = instance.AddDataWithVersion(Guid.NewGuid(), JsonData.CreateFrom("{\"v\":2}"), "1.0.0-pkg.1.2.1+account");
        var v3 = instance.AddDataWithVersion(Guid.NewGuid(), JsonData.CreateFrom("{\"v\":3}"), "2.0.0-pkg.1.3.0+account");

        // Assert
        Assert.Equal(3, instance.DataList.Count);
        Assert.Equal("1.0.0-pkg.1.2.0+account", v1.Version);
        Assert.Equal("1.0.0-pkg.1.2.1+account", v2.Version);
        Assert.Equal("2.0.0-pkg.1.3.0+account", v3.Version);
    }

    [Fact]
    public void LatestData_ShouldReturnHighestExtendedVersion()
    {
        // Arrange
        var instance = InstanceFactory.CreateDefault();

        // Add versions in non-sequential order
        instance.AddDataWithVersion(Guid.NewGuid(), JsonData.CreateFrom("{\"v\":2}"), "1.0.0-pkg.1.2.1+account");
        instance.AddDataWithVersion(Guid.NewGuid(), JsonData.CreateFrom("{\"v\":1}"), "1.0.0-pkg.1.2.0+account");
        instance.AddDataWithVersion(Guid.NewGuid(), JsonData.CreateFrom("{\"v\":3}"), "2.0.0-pkg.1.3.0+account");

        // Act
        var latest = instance.LatestData;

        // Assert - Should be the highest version (2.0.0-pkg.1.3.0+account)
        Assert.NotNull(latest);
        Assert.Equal("2.0.0-pkg.1.3.0+account", latest.Version);
    }

    #endregion

    #region Pre-Release Version Tests

    [Fact]
    public void AddDataWithVersion_ShouldAcceptPreReleaseVersion()
    {
        // Arrange
        var instance = InstanceFactory.CreateDefault();
        var preReleaseVersion = "1.0.0-alpha.1-pkg.1.17.0+account";

        // Act
        var instanceData = instance.AddDataWithVersion(Guid.NewGuid(), JsonData.CreateFrom("{}"), preReleaseVersion);

        // Assert
        Assert.Equal(preReleaseVersion, instanceData.Version);
    }

    [Theory]
    [InlineData("1.0.0-alpha.1-pkg.1.17.0+account", "Major", "2.0.0-pkg.1.17.0+account")]
    [InlineData("1.0.0-alpha.1-pkg.1.17.0+account", "Minor", "1.1.0-pkg.1.17.0+account")]
    [InlineData("1.0.0-alpha.1-pkg.1.17.0+account", "Patch", "1.0.1-pkg.1.17.0+account")]
    [InlineData("1.0.0-beta-pkg.1.0.0+test", "Patch", "1.0.1-pkg.1.0.0+test")]
    [InlineData("2.5.3-rc.1-pkg.10.20.30+myapp", "Minor", "2.6.0-pkg.10.20.30+myapp")]
    public void AddData_ShouldDropPreRelease_WhenIncrementing(
        string originalVersion,
        string strategyCode,
        string expectedVersion)
    {
        // Arrange
        var instance = InstanceFactory.CreateDefault();
        var originalData = JsonData.CreateFrom("{\"original\":true}");
        instance.AddDataWithVersion(Guid.NewGuid(), originalData, originalVersion);

        var newData = JsonData.CreateFrom("{\"new\":\"data\"}");
        var strategy = VersionStrategy.FromCode(strategyCode);

        // Act
        var newVersion = instance.AddData(Guid.NewGuid(), newData, strategy);

        // Assert - Pre-release should be dropped, pkg suffix preserved
        Assert.Equal(expectedVersion, newVersion.Version);
    }

    [Fact]
    public void AddData_ShouldHandleMultipleBuildMetadata()
    {
        // Arrange
        var instance = InstanceFactory.CreateDefault();
        var version = "1.0.0-pkg.1.17.0+account+build.123";
        var originalData = JsonData.CreateFrom("{\"original\":true}");
        instance.AddDataWithVersion(Guid.NewGuid(), originalData, version);

        var newData = JsonData.CreateFrom("{\"new\":\"data\"}");

        // Act
        var newVersion = instance.AddData(Guid.NewGuid(), newData, VersionStrategy.IncreasePatch);

        // Assert - Multiple build metadata should be preserved
        Assert.Equal("1.0.1-pkg.1.17.0+account+build.123", newVersion.Version);
    }

    #endregion
}

