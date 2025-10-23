using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BBT.Aether.Domain.Entities;
using BBT.Workflow.Caching;
using BBT.Workflow.Definitions;
using BBT.Workflow.Monitoring;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;
using Xunit;

namespace BBT.Workflow.Caching;

/// <summary>
/// Unit tests for MetricsAwareComponentCacheStore
/// Tests metrics recording decorator for component cache operations
/// </summary>
public class MetricsAwareComponentCacheStoreTests
{
    private readonly Mock<IComponentCacheStore> _mockInnerStore;
    private readonly Mock<IWorkflowMetrics> _mockWorkflowMetrics;
    private readonly Mock<ILogger<MetricsAwareComponentCacheStore>> _mockLogger;
    private readonly MetricsAwareComponentCacheStore _store;

    public MetricsAwareComponentCacheStoreTests()
    {
        _mockInnerStore = new Mock<IComponentCacheStore>();
        _mockWorkflowMetrics = new Mock<IWorkflowMetrics>();
        _mockLogger = new Mock<ILogger<MetricsAwareComponentCacheStore>>();

        _store = new MetricsAwareComponentCacheStore(
            _mockInnerStore.Object,
            _mockWorkflowMetrics.Object,
            _mockLogger.Object
        );
    }

    #region GetFlowAsync Tests

    [Fact]
    public async Task GetFlowAsync_ShouldRecordCacheMiss_WhenEntityNotFound()
    {
        // Arrange
        var domain = "test-domain";
        var key = "test-flow";

        _mockInnerStore
            .Setup(x => x.GetFlowAsync(domain, key, null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new EntityNotFoundException(typeof(Definitions.Workflow), new { domain, key }));

        // Act & Assert
        await Should.ThrowAsync<EntityNotFoundException>(
            async () => await _store.GetFlowAsync(domain, key, null, CancellationToken.None)
        );

        _mockWorkflowMetrics.Verify(x => x.RecordCacheMiss("Workflow"), Times.Once);
        _mockWorkflowMetrics.Verify(x => x.RecordCacheHit(It.IsAny<string>()), Times.Never);
    }

    #endregion

    #region GetTaskAsync Tests

    [Fact]
    public async Task GetTaskAsync_ShouldRecordCacheHit_WhenSuccessful()
    {
        // Arrange
        var domain = "test-domain";
        var key = "test-task";
        var task = Mock.Of<WorkflowTask>();

        _mockInnerStore
            .Setup(x => x.GetTaskAsync(domain, key, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(task);

        // Act
        await _store.GetTaskAsync(domain, key, null, CancellationToken.None);

        // Assert
        _mockWorkflowMetrics.Verify(x => x.RecordCacheHit("WorkflowTask"), Times.Once);
    }

    [Fact]
    public async Task GetTaskAsync_ShouldRecordCacheMiss_WhenFails()
    {
        // Arrange
        var domain = "test-domain";
        var key = "test-task";

        _mockInnerStore
            .Setup(x => x.GetTaskAsync(domain, key, null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new EntityNotFoundException(typeof(WorkflowTask), new { domain, key }));

        // Act & Assert
        await Should.ThrowAsync<EntityNotFoundException>(
            async () => await _store.GetTaskAsync(domain, key, null, CancellationToken.None)
        );

        _mockWorkflowMetrics.Verify(x => x.RecordCacheMiss("WorkflowTask"), Times.Once);
    }

    #endregion

    #region GetSchemaAsync Tests

    [Fact]
    public async Task GetSchemaAsync_ShouldRecordCacheHit_WhenSuccessful()
    {
        // Arrange
        var domain = "test-domain";
        var key = "test-schema";
        var schema = System.Text.Json.JsonSerializer.Deserialize<SchemaDefinition>(
            """{"type":"json-schema","schema":{"type":"object"}}""",
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true }
        );

        _mockInnerStore
            .Setup(x => x.GetSchemaAsync(domain, key, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(schema!);

        // Act
        await _store.GetSchemaAsync(domain, key, null, CancellationToken.None);

        // Assert
        _mockWorkflowMetrics.Verify(x => x.RecordCacheHit("SchemaDefinition"), Times.Once);
    }

    #endregion

    #region GetFunctionAsync Tests

    [Fact]
    public async Task GetFunctionAsync_ShouldRecordCacheHit_WhenSuccessful()
    {
        // Arrange
        var domain = "test-domain";
        var key = "test-function";
        var function = System.Text.Json.JsonSerializer.Deserialize<Function>(
            """{"name":"Test","description":"Test","script":"return true;"}""",
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true }
        );

        _mockInnerStore
            .Setup(x => x.GetFunctionAsync(domain, key, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(function!);

        // Act
        await _store.GetFunctionAsync(domain, key, null, CancellationToken.None);

        // Assert
        _mockWorkflowMetrics.Verify(x => x.RecordCacheHit("Function"), Times.Once);
    }

    #endregion

    #region GetViewAsync Tests

    [Fact]
    public async Task GetViewAsync_ShouldRecordCacheHit_WhenSuccessful()
    {
        // Arrange
        var domain = "test-domain";
        var key = "test-view";
        var view = System.Text.Json.JsonSerializer.Deserialize<View>(
            """{"type":1,"target":1,"content":"{}"}""",
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true }
        );

        _mockInnerStore
            .Setup(x => x.GetViewAsync(domain, key, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(view!);

        // Act
        await _store.GetViewAsync(domain, key, null, CancellationToken.None);

        // Assert
        _mockWorkflowMetrics.Verify(x => x.RecordCacheHit("View"), Times.Once);
    }

    #endregion

    #region GetExtensionAsync Tests

    [Fact]
    public async Task GetExtensionAsync_ShouldRecordCacheHit_WhenSuccessful()
    {
        // Arrange
        var domain = "test-domain";
        var key = "test-extension";
        var extension = System.Text.Json.JsonSerializer.Deserialize<Extension>(
            """{"type":1,"scope":1,"task":{"type":"6","name":"Test","url":"https://example.com","method":"POST"}}""",
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true }
        );

        _mockInnerStore
            .Setup(x => x.GetExtensionAsync(domain, key, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(extension!);

        // Act
        await _store.GetExtensionAsync(domain, key, null, CancellationToken.None);

        // Assert
        _mockWorkflowMetrics.Verify(x => x.RecordCacheHit("Extension"), Times.Once);
    }

    #endregion
    
    #region SetAsync Tests

    [Fact]
    public async Task SetAsync_ShouldDelegateToInnerStore()
    {
        // Arrange
        var workflow = System.Text.Json.JsonSerializer.Deserialize<Definitions.Workflow>(
            """{"type":"F","timeout":null,"labels":[],"functions":[],"features":[],"states":[],"sharedTransitions":[],"extensions":[],"startTransition":{"key":"start", "target": "init"}}""",
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true }
        );

        _mockInnerStore
            .Setup(x => x.SetAsync(workflow!, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _store.SetAsync(workflow!, CancellationToken.None);

        // Assert
        _mockInnerStore.Verify(x => x.SetAsync(workflow!, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SetAsync_ShouldUpdateCacheSizeMetrics()
    {
        // Arrange
        var workflow = System.Text.Json.JsonSerializer.Deserialize<Definitions.Workflow>(
            """{"type":"F","timeout":null,"labels":[],"functions":[],"features":[],"states":[],"sharedTransitions":[],"extensions":[],"startTransition":{"key":"start", "target": "init"}}""",
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true }
        );

        _mockInnerStore
            .Setup(x => x.SetAsync(workflow!, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _store.SetAsync(workflow!, CancellationToken.None);

        // Assert
        _mockWorkflowMetrics.Verify(x => x.SetCacheEntries("Workflow", It.IsAny<int>()), Times.Once);
        _mockWorkflowMetrics.Verify(x => x.SetCacheSize("Workflow", It.IsAny<long>()), Times.Once);
    }

    #endregion
    
}

