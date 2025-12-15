using BBT.Workflow.Remote.Configuration;
using Shouldly;
using Xunit;

namespace BBT.Workflow.Infrastructure.Tests.Remote;

/// <summary>
/// Unit tests for RemoteOptions
/// </summary>
public sealed class RemoteOptionsTests
{
    #region Constructor and Property Tests

    [Fact]
    public void RemoteOptions_Should_Have_Default_Values()
    {
        // Arrange & Act
        var options = new RemoteOptions();

        // Assert
        options.BaseUrl.ShouldBe(string.Empty);
        options.ApiVersion.ShouldBe("1.0");
        options.TimeoutSeconds.ShouldBe(30); // Default timeout
        options.MaxRetryAttempts.ShouldBe(3); // Default retry
        options.RetryDelayMilliseconds.ShouldBe(1000); // Default delay
        options.CircuitBreakerFailureThreshold.ShouldBe(20);
        options.CircuitBreakerTimeoutSeconds.ShouldBe(30);
        options.EnableCircuitBreakerBypass.ShouldBe(true);
        options.InternalOperationHeader.ShouldBe("X-Internal-Operation");
    }

    [Fact]
    public void RemoteOptions_Should_Allow_Setting_BaseUrl()
    {
        // Arrange
        var options = new RemoteOptions();
        const string baseUrl = "https://api.example.com";

        // Act
        options.BaseUrl = baseUrl;

        // Assert
        options.BaseUrl.ShouldBe(baseUrl);
    }

    [Fact]
    public void RemoteOptions_Should_Allow_Setting_ApiVersion()
    {
        // Arrange
        var options = new RemoteOptions();
        const string apiVersion = "v1";

        // Act
        options.ApiVersion = apiVersion;

        // Assert
        options.ApiVersion.ShouldBe(apiVersion);
    }

    [Fact]
    public void RemoteOptions_Should_Allow_Setting_TimeoutSeconds()
    {
        // Arrange
        var options = new RemoteOptions();
        const int timeout = 60;

        // Act
        options.TimeoutSeconds = timeout;

        // Assert
        options.TimeoutSeconds.ShouldBe(timeout);
    }

    [Fact]
    public void RemoteOptions_Should_Allow_Setting_MaxRetryAttempts()
    {
        // Arrange
        var options = new RemoteOptions();
        const int maxRetry = 5;

        // Act
        options.MaxRetryAttempts = maxRetry;

        // Assert
        options.MaxRetryAttempts.ShouldBe(maxRetry);
    }

    [Fact]
    public void RemoteOptions_Should_Allow_Setting_RetryDelayMilliseconds()
    {
        // Arrange
        var options = new RemoteOptions();
        const int retryDelay = 2000;

        // Act
        options.RetryDelayMilliseconds = retryDelay;

        // Assert
        options.RetryDelayMilliseconds.ShouldBe(retryDelay);
    }

    [Fact]
    public void RemoteOptions_Should_Allow_Setting_CircuitBreaker_Properties()
    {
        // Arrange
        var options = new RemoteOptions
        {
            CircuitBreakerFailureThreshold = 10,
            CircuitBreakerTimeoutSeconds = 60,
            EnableCircuitBreakerBypass = false,
            InternalOperationHeader = "X-Custom-Header"
        };

        // Assert
        options.CircuitBreakerFailureThreshold.ShouldBe(10);
        options.CircuitBreakerTimeoutSeconds.ShouldBe(60);
        options.EnableCircuitBreakerBypass.ShouldBe(false);
        options.InternalOperationHeader.ShouldBe("X-Custom-Header");
    }

    #endregion

    #region Validation Tests

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void RemoteOptions_Should_Handle_Invalid_TimeoutSeconds(int timeout)
    {
        // Arrange
        var options = new RemoteOptions();

        // Act
        options.TimeoutSeconds = timeout;

        // Assert - Should accept any value, validation happens elsewhere
        options.TimeoutSeconds.ShouldBe(timeout);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void RemoteOptions_Should_Handle_Invalid_MaxRetryAttempts(int maxRetry)
    {
        // Arrange
        var options = new RemoteOptions();

        // Act
        options.MaxRetryAttempts = maxRetry;

        // Assert - Should accept any value, validation happens elsewhere
        options.MaxRetryAttempts.ShouldBe(maxRetry);
    }

    #endregion

    #region Complete Configuration Tests

    [Fact]
    public void RemoteOptions_Should_Support_Complete_Configuration()
    {
        // Arrange & Act
        var options = new RemoteOptions
        {
            BaseUrl = "https://api.workflow.com",
            ApiVersion = "2.0",
            TimeoutSeconds = 45,
            MaxRetryAttempts = 4,
            RetryDelayMilliseconds = 1500,
            CircuitBreakerFailureThreshold = 15,
            CircuitBreakerTimeoutSeconds = 45,
            EnableCircuitBreakerBypass = false,
            InternalOperationHeader = "X-My-Header"
        };

        // Assert
        options.BaseUrl.ShouldBe("https://api.workflow.com");
        options.ApiVersion.ShouldBe("2.0");
        options.TimeoutSeconds.ShouldBe(45);
        options.MaxRetryAttempts.ShouldBe(4);
        options.RetryDelayMilliseconds.ShouldBe(1500);
        options.CircuitBreakerFailureThreshold.ShouldBe(15);
        options.CircuitBreakerTimeoutSeconds.ShouldBe(45);
        options.EnableCircuitBreakerBypass.ShouldBe(false);
        options.InternalOperationHeader.ShouldBe("X-My-Header");
    }

    [Fact]
    public void RemoteOptions_Should_Have_Correct_SectionName()
    {
        // Assert
        RemoteOptions.SectionName.ShouldBe("vNextApi");
    }

    #endregion
}

