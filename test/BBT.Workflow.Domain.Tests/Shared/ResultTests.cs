using System;
using System.Linq;
using BBT.Workflow.Domain;
using Xunit;

namespace BBT.Workflow.Domain;

public class ResultTests
{
    #region Non-Generic Result Tests

    [Fact]
    public void Ok_ShouldCreateSuccessfulResult()
    {
        // Act
        var result = Result.Ok();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(Error.None, result.Error);
    }

    [Fact]
    public void Fail_ShouldCreateFailedResult()
    {
        // Arrange
        var error = Error.Validation("test", "Test error");

        // Act
        var result = Result.Fail(error);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(error, result.Error);
    }

    [Fact]
    public void ImplicitBool_ShouldReturnTrue_ForSuccessfulResult()
    {
        // Arrange
        var result = Result.Ok();

        // Act
        bool isSuccess = result;

        // Assert
        Assert.True(isSuccess);
    }

    [Fact]
    public void ImplicitBool_ShouldReturnFalse_ForFailedResult()
    {
        // Arrange
        var result = Result.Fail(Error.Validation("test"));

        // Act
        bool isSuccess = result;

        // Assert
        Assert.False(isSuccess);
    }

    #endregion

    #region Generic Result<T> Tests

    [Fact]
    public void Ok_WithValue_ShouldCreateSuccessfulResult()
    {
        // Arrange
        var value = 42;

        // Act
        var result = Result<int>.Ok(value);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(value, result.Value);
        Assert.Equal(Error.None, result.Error);
    }

    [Fact]
    public void Fail_WithError_ShouldCreateFailedResult()
    {
        // Arrange
        var error = Error.NotFound("resource", "Resource not found");

        // Act
        var result = Result<string>.Fail(error);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Null(result.Value);
        Assert.Equal(error, result.Error);
    }

    [Fact]
    public void Deconstruct_ShouldExtractAllComponents()
    {
        // Arrange
        var value = "test-value";
        var result = Result<string>.Ok(value);

        // Act
        var (ok, actualValue, error) = result;

        // Assert
        Assert.True(ok);
        Assert.Equal(value, actualValue);
        Assert.Equal(Error.None, error);
    }

    [Fact]
    public void Deconstruct_ForFailedResult_ShouldReturnError()
    {
        // Arrange
        var error = Error.Conflict("test", "Conflict");
        var result = Result<int>.Fail(error);

        // Act
        var (ok, value, actualError) = result;

        // Assert
        Assert.False(ok);
        Assert.Equal(0, value);
        Assert.Equal(error, actualError);
    }

    [Fact]
    public void ImplicitBool_ShouldReturnTrue_ForSuccessfulGenericResult()
    {
        // Arrange
        var result = Result<string>.Ok("success");

        // Act
        bool isSuccess = result;

        // Assert
        Assert.True(isSuccess);
    }

    [Fact]
    public void ImplicitBool_ShouldReturnFalse_ForFailedGenericResult()
    {
        // Arrange
        var result = Result<string>.Fail(Error.Validation("test"));

        // Act
        bool isSuccess = result;

        // Assert
        Assert.False(isSuccess);
    }

    [Fact]
    public void ToResult_ShouldConvertToNonGenericResult_WhenSuccessful()
    {
        // Arrange
        var result = Result<int>.Ok(42);

        // Act
        var nonGeneric = result.ToResult();

        // Assert
        Assert.True(nonGeneric.IsSuccess);
        Assert.Equal(Error.None, nonGeneric.Error);
    }

    [Fact]
    public void ToResult_ShouldConvertToNonGenericResult_WhenFailed()
    {
        // Arrange
        var error = Error.Dependency("db", "Database error");
        var result = Result<int>.Fail(error);

        // Act
        var nonGeneric = result.ToResult();

        // Assert
        Assert.False(nonGeneric.IsSuccess);
        Assert.Equal(error, nonGeneric.Error);
    }

    [Fact]
    public void Ok_WithNullValue_ShouldStillBeSuccessful()
    {
        // Act
        var result = Result<string?>.Ok(null);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Null(result.Value);
    }

    [Fact]
    public void Value_ShouldBeDefault_ForFailedResult()
    {
        // Arrange
        var error = Error.Validation("test");

        // Act
        var result = Result<int>.Fail(error);

        // Assert
        Assert.Equal(0, result.Value);
    }

    [Fact]
    public void Value_ShouldBeNull_ForFailedReferenceTypeResult()
    {
        // Arrange
        var error = Error.Validation("test");

        // Act
        var result = Result<string>.Fail(error);

        // Assert
        Assert.Null(result.Value);
    }

    [Fact]
    public void Equals_ShouldReturnTrue_ForIdenticalSuccessResults()
    {
        // Arrange
        var result1 = Result<int>.Ok(42);
        var result2 = Result<int>.Ok(42);

        // Act & Assert
        Assert.Equal(result1, result2);
    }

    [Fact]
    public void Equals_ShouldReturnTrue_ForIdenticalFailureResults()
    {
        // Arrange
        var error = Error.Validation("test", "message");
        var result1 = Result<int>.Fail(error);
        var result2 = Result<int>.Fail(error);

        // Act & Assert
        Assert.Equal(result1, result2);
    }

    [Fact]
    public void Equals_ShouldReturnFalse_ForDifferentValues()
    {
        // Arrange
        var result1 = Result<int>.Ok(42);
        var result2 = Result<int>.Ok(43);

        // Act & Assert
        Assert.NotEqual(result1, result2);
    }

    [Fact]
    public void Equals_ShouldReturnFalse_ForDifferentErrors()
    {
        // Arrange
        var result1 = Result<int>.Fail(Error.Validation("code1"));
        var result2 = Result<int>.Fail(Error.Validation("code2"));

        // Act & Assert
        Assert.NotEqual(result1, result2);
    }

    [Fact]
    public void GetHashCode_ShouldBeSame_ForIdenticalResults()
    {
        // Arrange
        var result1 = Result<int>.Ok(42);
        var result2 = Result<int>.Ok(42);

        // Act & Assert
        Assert.Equal(result1.GetHashCode(), result2.GetHashCode());
    }

    [Fact]
    public void NonGenericResult_Equals_ShouldWork()
    {
        // Arrange
        var result1 = Result.Ok();
        var result2 = Result.Ok();

        // Act & Assert
        Assert.Equal(result1, result2);
    }

    [Fact]
    public void NonGenericResult_Equals_ShouldReturnFalse_ForDifferentStates()
    {
        // Arrange
        var result1 = Result.Ok();
        var result2 = Result.Fail(Error.Validation("test"));

        // Act & Assert
        Assert.NotEqual(result1, result2);
    }

    #endregion

    #region Result With Complex Types

    [Fact]
    public void Ok_WithComplexObject_ShouldPreserveValue()
    {
        // Arrange
        var obj = new { Id = 1, Name = "Test" };

        // Act
        var result = Result<object>.Ok(obj);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Same(obj, result.Value);
    }

    [Fact]
    public void Ok_WithList_ShouldPreserveList()
    {
        // Arrange
        var list = new[] { 1, 2, 3 }.ToList();

        // Act
        var result = Result<System.Collections.Generic.List<int>>.Ok(list);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Same(list, result.Value);
        Assert.Equal(3, result.Value!.Count);
    }

    #endregion
}

