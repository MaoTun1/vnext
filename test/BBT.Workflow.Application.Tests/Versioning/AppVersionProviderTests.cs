using System;
using BBT.Workflow.Versioning;
using Shouldly;
using Xunit;

namespace BBT.Workflow.Application.Tests.Versioning;

/// <summary>
/// Unit tests for <see cref="AppVersionProvider"/>.
/// Verifies environment variable reading and fallback behavior.
/// </summary>
public class AppVersionProviderTests : IDisposable
{
    private readonly string? originalValue;

    public AppVersionProviderTests()
    {
        originalValue = Environment.GetEnvironmentVariable("APP_VERSION");
    }

    public void Dispose()
    {
        if (originalValue is null)
            Environment.SetEnvironmentVariable("APP_VERSION", null);
        else
            Environment.SetEnvironmentVariable("APP_VERSION", originalValue);
    }

    [Fact]
    public void GetVersion_WhenEnvVarIsSet_ShouldReturnEnvValue()
    {
        // Arrange
        Environment.SetEnvironmentVariable("APP_VERSION", "2.5.1");
        var provider = new AppVersionProvider();

        // Act
        var version = provider.GetVersion();

        // Assert
        version.ShouldBe("2.5.1");
    }

    [Fact]
    public void GetVersion_WhenEnvVarIsNotSet_ShouldReturnUnknown()
    {
        // Arrange
        Environment.SetEnvironmentVariable("APP_VERSION", null);
        var provider = new AppVersionProvider();

        // Act
        var version = provider.GetVersion();

        // Assert
        version.ShouldBe("unknown");
    }

    [Fact]
    public void GetVersion_ShouldReturnConsistentValue()
    {
        // Arrange
        Environment.SetEnvironmentVariable("APP_VERSION", "1.0.0");
        var provider = new AppVersionProvider();

        // Act & Assert
        provider.GetVersion().ShouldBe(provider.GetVersion());
    }
}
