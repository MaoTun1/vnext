using System;
using System.Linq;
using BBT.Workflow.Definitions;
using Xunit;

namespace BBT.Workflow.Definitions;

public class SubFlowTests
{
    [Fact]
    public void Create_ShouldInitializeProperties()
    {
        // Arrange
        var type = "S";
        var reference = new Reference("sub-flow-key", "test-domain", "sys-flows", "1.0.0");
        var mapping = new ScriptCode("mapping-location", 
            Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("return data;")));

        // Act
        var subFlow = SubFlow.Create(type, reference, mapping);

        // Assert
        Assert.NotNull(subFlow);
        Assert.Equal(SubFlowType.SubFlow, subFlow.Type);
        Assert.NotNull(subFlow.Process);
        Assert.Equal("sub-flow-key", subFlow.Process.Key);
        Assert.Equal("test-domain", subFlow.Process.Domain);
        Assert.NotNull(subFlow.Mapping);
        Assert.Equal("mapping-location", subFlow.Mapping.Location);
    }

    [Fact]
    public void Create_ShouldAcceptSubFlowType()
    {
        // Arrange
        var type = "S";
        var reference = new Reference("key", "domain", "flow", "1.0.0");
        var mapping = new ScriptCode("location", Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("code")));

        // Act
        var subFlow = SubFlow.Create(type, reference, mapping);

        // Assert
        Assert.Equal(SubFlowType.SubFlow, subFlow.Type);
        Assert.Equal("S", subFlow.Type.Code);
    }

    [Fact]
    public void Create_ShouldAcceptSubProcessType()
    {
        // Arrange
        var type = "P";
        var reference = new Reference("key", "domain", "flow", "1.0.0");
        var mapping = new ScriptCode("location", Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("code")));

        // Act
        var subFlow = SubFlow.Create(type, reference, mapping);

        // Assert
        Assert.Equal(SubFlowType.SubProcess, subFlow.Type);
        Assert.Equal("P", subFlow.Type.Code);
    }

    [Fact]
    public void Create_ShouldThrowException_WhenTypeIsInvalid()
    {
        // Arrange
        var reference = new Reference("key", "domain", "flow", "1.0.0");
        var mapping = new ScriptCode("location", Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("code")));

        // Act & Assert
        Assert.Throws<ArgumentException>(() => SubFlow.Create("X", reference, mapping));
    }

    [Theory]
    [InlineData("F")]
    [InlineData("C")]
    [InlineData("")]
    [InlineData("INVALID")]
    public void Create_ShouldThrowException_WhenTypeIsNotSubFlowType(string type)
    {
        // Arrange
        var reference = new Reference("key", "domain", "flow", "1.0.0");
        var mapping = new ScriptCode("location", Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("code")));

        // Act & Assert
        Assert.Throws<ArgumentException>(() => SubFlow.Create(type, reference, mapping));
    }

    [Fact]
    public void Process_ShouldBeReferenceType()
    {
        // Arrange
        var reference = new Reference("key", "domain", "flow", "1.0.0");
        var mapping = new ScriptCode("location", Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("code")));

        // Act
        var subFlow = SubFlow.Create("S", reference, mapping);

        // Assert
        Assert.NotNull(subFlow.Process);
        Assert.IsType<Reference>(subFlow.Process);
    }

    [Fact]
    public void Mapping_ShouldBeScriptCodeType()
    {
        // Arrange
        var reference = new Reference("key", "domain", "flow", "1.0.0");
        var mapping = new ScriptCode("location", Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("code")));

        // Act
        var subFlow = SubFlow.Create("S", reference, mapping);

        // Assert
        Assert.NotNull(subFlow.Mapping);
        Assert.IsType<ScriptCode>(subFlow.Mapping);
    }

    [Fact]
    public void Create_ShouldPreserveReferenceProperties()
    {
        // Arrange
        var key = "sub-flow-key";
        var domain = "test-domain";
        var flow = "sys-flows";
        var version = "2.5.3";
        var reference = new Reference(key, domain, flow, version);
        var mapping = new ScriptCode("location", Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("code")));

        // Act
        var subFlow = SubFlow.Create("S", reference, mapping);

        // Assert
        Assert.Equal(key, subFlow.Process.Key);
        Assert.Equal(domain, subFlow.Process.Domain);
        Assert.Equal(flow, subFlow.Process.Flow);
        Assert.Equal(version, subFlow.Process.Version);
    }

    [Fact]
    public void Create_ShouldPreserveMappingProperties()
    {
        // Arrange
        var location = "test-location";
        var code = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("function map() { return data; }"));
        var reference = new Reference("key", "domain", "flow", "1.0.0");
        var mapping = new ScriptCode(location, code);

        // Act
        var subFlow = SubFlow.Create("S", reference, mapping);

        // Assert
        Assert.Equal(location, subFlow.Mapping.Location);
        Assert.Equal(code, subFlow.Mapping.Code);
    }

    [Fact]
    public void Create_ShouldHandleComplexScriptCode()
    {
        // Arrange
        var complexScript = @"
            function mapData(input) {
                return {
                    ...input,
                    transformedAt: new Date().toISOString()
                };
            }
            return mapData(data);
        ";
        var reference = new Reference("key", "domain", "flow", "1.0.0");
        var mapping = new ScriptCode("complex-mapping", 
            Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(complexScript)));

        // Act
        var subFlow = SubFlow.Create("P", reference, mapping);

        // Assert
        Assert.NotNull(subFlow.Mapping);
        Assert.Equal(complexScript, subFlow.Mapping.DecodedCode);
    }
}

