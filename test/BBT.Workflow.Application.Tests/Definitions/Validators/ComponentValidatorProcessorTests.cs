using System;
using System.Text.Json;
using BBT.Workflow.Definitions.Validators;
using Moq;
using Shouldly;
using Xunit;

namespace BBT.Workflow.Definitions.Validators;

/// <summary>
/// Unit tests for ComponentValidatorProcessor
/// </summary>
public class ComponentValidatorProcessorTests
{
    [Fact]
    public void Validate_ShouldUseCorrectValidator_ForComponentType()
    {
        // Arrange
        var mockValidator1 = new Mock<IComponentValidator>();
        var mockValidator2 = new Mock<IComponentValidator>();
        
        mockValidator1.Setup(v => v.CanHandle("sys-flows")).Returns(true);
        mockValidator1.Setup(v => v.Validate(It.IsAny<JsonElement>()))
            .Returns(ComponentValidationResult.Success());
        
        mockValidator2.Setup(v => v.CanHandle("sys-tasks")).Returns(true);
        
        var processor = new ComponentValidatorProcessor(new[] { mockValidator1.Object, mockValidator2.Object });
        var attributes = JsonDocument.Parse("{}").RootElement;
        
        // Act
        var result = processor.Validate("sys-flows", attributes);
        
        // Assert
        result.IsValid.ShouldBeTrue();
        mockValidator1.Verify(v => v.Validate(It.IsAny<JsonElement>()), Times.Once);
        mockValidator2.Verify(v => v.Validate(It.IsAny<JsonElement>()), Times.Never);
    }

    [Fact]
    public void Validate_ShouldThrowNotSupportedException_WhenNoValidatorFound()
    {
        // Arrange
        var mockValidator = new Mock<IComponentValidator>();
        mockValidator.Setup(v => v.CanHandle(It.IsAny<string>())).Returns(false);
        
        var processor = new ComponentValidatorProcessor(new[] { mockValidator.Object });
        var attributes = JsonDocument.Parse("{}").RootElement;
        
        // Act & Assert
        var exception = Should.Throw<NotSupportedException>(() => processor.Validate("unknown-type", attributes));
        exception.Message.ShouldContain("unknown-type");
    }

    [Fact]
    public void TryValidate_ShouldReturnFalse_WhenNoValidatorFound()
    {
        // Arrange
        var mockValidator = new Mock<IComponentValidator>();
        mockValidator.Setup(v => v.CanHandle(It.IsAny<string>())).Returns(false);
        
        var processor = new ComponentValidatorProcessor(new[] { mockValidator.Object });
        var attributes = JsonDocument.Parse("{}").RootElement;
        
        // Act
        var found = processor.TryValidate("unknown-type", attributes, out var result);
        
        // Assert
        found.ShouldBeFalse();
        result.IsValid.ShouldBeTrue(); // Returns success by default when no validator found
    }

    [Fact]
    public void TryValidate_ShouldReturnTrue_WhenValidatorFound()
    {
        // Arrange
        var mockValidator = new Mock<IComponentValidator>();
        mockValidator.Setup(v => v.CanHandle("sys-flows")).Returns(true);
        mockValidator.Setup(v => v.Validate(It.IsAny<JsonElement>()))
            .Returns(ComponentValidationResult.Success());
        
        var processor = new ComponentValidatorProcessor(new[] { mockValidator.Object });
        var attributes = JsonDocument.Parse("{}").RootElement;
        
        // Act
        var found = processor.TryValidate("sys-flows", attributes, out var result);
        
        // Assert
        found.ShouldBeTrue();
        result.IsValid.ShouldBeTrue();
    }
}
