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
/// Unit tests for SchemaWorkflowCastHandler
/// Tests schema workflow casting operations
/// </summary>
public class SchemaWorkflowCastHandlerTests
{
    private readonly Mock<IDomainCacheContext> _mockCacheContext;
    private readonly Mock<ICacheSet<SchemaDefinition>> _mockSchemasCacheSet;
    private readonly SchemaWorkflowCastHandler _handler;

    public SchemaWorkflowCastHandlerTests()
    {
        _mockCacheContext = new Mock<IDomainCacheContext>();
        _mockSchemasCacheSet = new Mock<ICacheSet<SchemaDefinition>>();
        
        _mockCacheContext.Setup(x => x.Schemas).Returns(_mockSchemasCacheSet.Object);
        
        _handler = new SchemaWorkflowCastHandler(_mockCacheContext.Object);
    }

    [Fact]
    public void CanHandle_ShouldReturnTrue_ForSysSchemas()
    {
        // Act
        var result = _handler.CanHandle("sys-schemas");

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
        _handler.CanHandle("sys-functions").ShouldBeFalse();
        _handler.CanHandle("sys-extensions").ShouldBeFalse();
        _handler.CanHandle("unknown").ShouldBeFalse();
    }

    [Fact]
    public async Task HandleAsync_ShouldDeserializeAndCacheSchema()
    {
        // Arrange
        var reference = new Reference("test-schema", "test-domain", "sys-schemas", "1.0.0");
        
        var schemaJson = """
        {
            "type": "json-schema",
            "schema": {
                "type": "object",
                "properties": {
                    "name": { "type": "string" }
                }
            }
        }
        """;
        
        var attributes = JsonDocument.Parse(schemaJson).RootElement;

        _mockSchemasCacheSet
            .Setup(x => x.SetAsync(It.IsAny<SchemaDefinition>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _handler.HandleAsync(reference, attributes, CancellationToken.None);

        // Assert
        _mockSchemasCacheSet.Verify(
            x => x.SetAsync(It.Is<SchemaDefinition>(s => 
                s.Key == "test-schema" && 
                s.Domain == "test-domain" && 
                s.Version == "1.0.0"), 
                It.IsAny<CancellationToken>()), 
            Times.Once);
    }
}

