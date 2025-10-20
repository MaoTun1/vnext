using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using BBT.Workflow.Caching;
using BBT.Workflow.Definitions;
using BBT.Workflow.Definitions.CastHandlers;
using Moq;
using Shouldly;
using Xunit;

namespace BBT.Workflow.Definitions.CastHandlers;

/// <summary>
/// Unit tests for ExtensionWorkflowCastHandler
/// Tests extension workflow casting operations
/// </summary>
public class ExtensionWorkflowCastHandlerTests
{
    private readonly Mock<IDomainCacheContext> _mockCacheContext;
    private readonly Mock<ICacheSet<Extension>> _mockExtensionsCacheSet;
    private readonly ExtensionWorkflowCastHandler _handler;

    public ExtensionWorkflowCastHandlerTests()
    {
        _mockCacheContext = new Mock<IDomainCacheContext>();
        _mockExtensionsCacheSet = new Mock<ICacheSet<Extension>>();
        
        _mockCacheContext.Setup(x => x.Extensions).Returns(_mockExtensionsCacheSet.Object);
        
        _handler = new ExtensionWorkflowCastHandler(_mockCacheContext.Object);
    }

    [Fact]
    public void CanHandle_ShouldReturnTrue_ForSysExtensions()
    {
        // Act
        var result = _handler.CanHandle("sys-extensions");

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void CanHandle_ShouldReturnFalse_ForOtherWorkflowTypes()
    {
        // Arrange & Act & Assert
        _handler.CanHandle("sys-tasks").ShouldBeFalse();
        _handler.CanHandle("sys-flows").ShouldBeFalse();
        _handler.CanHandle("sys-views").ShouldBeFalse();
        _handler.CanHandle("sys-schemas").ShouldBeFalse();
        _handler.CanHandle("sys-functions").ShouldBeFalse();
        _handler.CanHandle("unknown").ShouldBeFalse();
    }

    [Fact]
    public async Task HandleAsync_ShouldDeserializeAndCacheExtension()
    {
        // Arrange
        var reference = new Reference("test-extension", "test-domain", "sys-extensions", "1.0.0");
        
        var extensionJson = """
        {
            "type": 1,
            "scope": 1,
            "task": {
                "order": 1,
                "task": {
                    "key": "test-task",
                    "domain": "test-domain",
                    "flow": "sys-tasks",
                    "version": "1.0.0"
                },
                "mapping": {
                    "script": "return input;"
                }
            }
        }
        """;
        
        var attributes = JsonDocument.Parse(extensionJson).RootElement;

        _mockExtensionsCacheSet
            .Setup(x => x.SetAsync(It.IsAny<Extension>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _handler.HandleAsync(reference, attributes, CancellationToken.None);

        // Assert
        _mockExtensionsCacheSet.Verify(
            x => x.SetAsync(It.Is<Extension>(e => 
                e.Key == "test-extension" && 
                e.Domain == "test-domain" && 
                e.Version == "1.0.0"), 
                It.IsAny<CancellationToken>()), 
            Times.Once);
    }
}

