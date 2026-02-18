using System.Threading.Tasks;
using BBT.Workflow.Middlewares;
using BBT.Workflow.Versioning;
using Microsoft.AspNetCore.Http;
using NSubstitute;
using Shouldly;
using Xunit;

namespace BBT.Workflow.Application.Tests.Versioning;

/// <summary>
/// Unit tests for <see cref="AppVersionMiddleware"/>.
/// Verifies that X-App-Version header is added to every response.
/// </summary>
public class AppVersionMiddlewareTests
{
    private readonly IAppVersionProvider _versionProvider;

    public AppVersionMiddlewareTests()
    {
        _versionProvider = Substitute.For<IAppVersionProvider>();
        _versionProvider.GetVersion().Returns("3.2.1");
    }

    [Fact]
    public async Task InvokeAsync_ShouldAddXAppVersionHeader()
    {
        // Arrange
        var context = new DefaultHttpContext();
        var middleware = new AppVersionMiddleware(_ => Task.CompletedTask, _versionProvider);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.Headers["X-App-Version"].ToString().ShouldBe("3.2.1");
    }

    [Fact]
    public async Task InvokeAsync_ShouldCallNextMiddleware()
    {
        // Arrange
        var context = new DefaultHttpContext();
        var nextCalled = false;
        var middleware = new AppVersionMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        }, _versionProvider);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        nextCalled.ShouldBeTrue();
    }

    [Fact]
    public async Task InvokeAsync_WhenVersionIsUnknown_ShouldStillAddHeader()
    {
        // Arrange
        _versionProvider.GetVersion().Returns("unknown");
        var context = new DefaultHttpContext();
        var middleware = new AppVersionMiddleware(_ => Task.CompletedTask, _versionProvider);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.Headers["X-App-Version"].ToString().ShouldBe("unknown");
    }
}
