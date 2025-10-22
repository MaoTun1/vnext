using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BBT.Workflow.Domain;
using Xunit;

namespace BBT.Workflow.Domain;

public class ResultExtensionsTests
{
    #region Map Tests

    [Fact]
    public void Map_ShouldTransformValue_WhenSuccessful()
    {
        // Arrange
        var result = Result<int>.Ok(5);

        // Act
        var mapped = result.Map(x => x * 2);

        // Assert
        Assert.True(mapped.IsSuccess);
        Assert.Equal(10, mapped.Value);
    }

    [Fact]
    public void Map_ShouldPropagateError_WhenFailed()
    {
        // Arrange
        var error = Error.Validation("test");
        var result = Result<int>.Fail(error);

        // Act
        var mapped = result.Map(x => x * 2);

        // Assert
        Assert.False(mapped.IsSuccess);
        Assert.Equal(error, mapped.Error);
    }

    #endregion

    #region Bind Tests

    [Fact]
    public void Bind_ShouldExecuteBinder_WhenSuccessful()
    {
        // Arrange
        var result = Result<int>.Ok(5);

        // Act
        var bound = result.Bind(x => Result<string>.Ok(x.ToString()));

        // Assert
        Assert.True(bound.IsSuccess);
        Assert.Equal("5", bound.Value);
    }

    [Fact]
    public void Bind_ShouldPropagateError_WhenFailed()
    {
        // Arrange
        var error = Error.Validation("test");
        var result = Result<int>.Fail(error);

        // Act
        var bound = result.Bind(x => Result<string>.Ok(x.ToString()));

        // Assert
        Assert.False(bound.IsSuccess);
        Assert.Equal(error, bound.Error);
    }

    [Fact]
    public void Bind_ShouldPropagateBinderError()
    {
        // Arrange
        var result = Result<int>.Ok(5);
        var binderError = Error.Validation("binder");

        // Act
        var bound = result.Bind(x => Result<string>.Fail(binderError));

        // Assert
        Assert.False(bound.IsSuccess);
        Assert.Equal(binderError, bound.Error);
    }

    #endregion

    #region Tap Tests

    [Fact]
    public void Tap_ShouldExecuteSideEffect_WhenSuccessful()
    {
        // Arrange
        var result = Result<int>.Ok(5);
        var sideEffectExecuted = false;

        // Act
        var tapped = result.Tap(x => sideEffectExecuted = true);

        // Assert
        Assert.True(sideEffectExecuted);
        Assert.True(tapped.IsSuccess);
        Assert.Equal(5, tapped.Value);
    }

    [Fact]
    public void Tap_ShouldNotExecuteSideEffect_WhenFailed()
    {
        // Arrange
        var error = Error.Validation("test");
        var result = Result<int>.Fail(error);
        var sideEffectExecuted = false;

        // Act
        var tapped = result.Tap(x => sideEffectExecuted = true);

        // Assert
        Assert.False(sideEffectExecuted);
        Assert.False(tapped.IsSuccess);
    }

    #endregion

    #region Ensure Tests

    [Fact]
    public void Ensure_ShouldPassThrough_WhenPredicateIsTrue()
    {
        // Arrange
        var result = Result<int>.Ok(10);

        // Act
        var ensured = result.Ensure(x => x > 5, Error.Validation("too_small"));

        // Assert
        Assert.True(ensured.IsSuccess);
        Assert.Equal(10, ensured.Value);
    }

    [Fact]
    public void Ensure_ShouldFail_WhenPredicateIsFalse()
    {
        // Arrange
        var result = Result<int>.Ok(3);
        var error = Error.Validation("too_small");

        // Act
        var ensured = result.Ensure(x => x > 5, error);

        // Assert
        Assert.False(ensured.IsSuccess);
        Assert.Equal(error, ensured.Error);
    }

    [Fact]
    public void Ensure_ShouldPropagateError_WhenAlreadyFailed()
    {
        // Arrange
        var error = Error.Validation("original");
        var result = Result<int>.Fail(error);

        // Act
        var ensured = result.Ensure(x => x > 5, Error.Validation("other"));

        // Assert
        Assert.False(ensured.IsSuccess);
        Assert.Equal(error, ensured.Error);
    }

    #endregion

    #region Match Tests

    [Fact]
    public void Match_ShouldExecuteOnSuccess_WhenSuccessful()
    {
        // Arrange
        var result = Result<int>.Ok(5);

        // Act
        var output = result.Match(
            onSuccess: x => $"Success: {x}",
            onFailure: e => $"Error: {e.Code}"
        );

        // Assert
        Assert.Equal("Success: 5", output);
    }

    [Fact]
    public void Match_ShouldExecuteOnFailure_WhenFailed()
    {
        // Arrange
        var error = Error.Validation("test", "Test error");
        var result = Result<int>.Fail(error);

        // Act
        var output = result.Match(
            onSuccess: x => $"Success: {x}",
            onFailure: e => $"Error: {e.Code}"
        );

        // Assert
        Assert.Equal("Error: test", output);
    }

    #endregion

    #region Async Map Tests

    [Fact]
    public async Task MapAsync_ShouldTransformValue_WhenSuccessful()
    {
        // Arrange
        var result = Task.FromResult(Result<int>.Ok(5));

        // Act
        var mapped = await result.MapAsync(x => x * 2);

        // Assert
        Assert.True(mapped.IsSuccess);
        Assert.Equal(10, mapped.Value);
    }

    [Fact]
    public async Task MapAsync_WithAsyncMapper_ShouldWork()
    {
        // Arrange
        var result = Task.FromResult(Result<int>.Ok(5));

        // Act
        var mapped = await result.MapAsync(async x =>
        {
            await Task.Delay(1);
            return x * 2;
        });

        // Assert
        Assert.True(mapped.IsSuccess);
        Assert.Equal(10, mapped.Value);
    }

    #endregion

    #region Exception Handling Tests

    [Fact]
    public void Try_ShouldReturnOk_WhenActionSucceeds()
    {
        // Act
        var result = ResultExtensions.Try(() => 42);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void Try_ShouldReturnError_WhenActionThrows()
    {
        // Act
        var result = ResultExtensions.Try<int>(() => throw new InvalidOperationException("Test error"));

        // Assert
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void Try_ShouldReturnTransientError_ForOperationCanceledException()
    {
        // Act
        var result = ResultExtensions.Try<int>(() => throw new OperationCanceledException("Canceled"));

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("canceled", result.Error.Code);
    }

    [Fact]
    public void Try_WithCustomErrorMapper_ShouldUseMapper()
    {
        // Arrange
        var customError = Error.Validation("custom");

        // Act
        var result = ResultExtensions.Try<int>(
            () => throw new Exception("Test"),
            ex => customError
        );

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(customError, result.Error);
    }

    [Fact]
    public void Try_NonGeneric_ShouldReturnOk_WhenActionSucceeds()
    {
        // Arrange
        var executed = false;

        // Act
        var result = ResultExtensions.Try(() => executed = true);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.True(executed);
    }

    [Fact]
    public void Try_NonGeneric_ShouldReturnError_WhenActionThrows()
    {
        // Act
        var result = ResultExtensions.Try(() => throw new InvalidOperationException("Test"));

        // Assert
        Assert.False(result.IsSuccess);
    }

    #endregion

    #region Combining Results Tests

    [Fact]
    public void Combine_ShouldReturnOk_WhenAllResultsSucceed()
    {
        // Arrange
        var results = new[]
        {
            Result.Ok(),
            Result.Ok(),
            Result.Ok()
        };

        // Act
        var combined = ResultExtensions.Combine(results);

        // Assert
        Assert.True(combined.IsSuccess);
    }

    [Fact]
    public void Combine_ShouldReturnFirstError_WhenAnyResultFails()
    {
        // Arrange
        var error1 = Error.Validation("error1");
        var error2 = Error.Validation("error2");
        var results = new[]
        {
            Result.Ok(),
            Result.Fail(error1),
            Result.Fail(error2)
        };

        // Act
        var combined = ResultExtensions.Combine(results);

        // Assert
        Assert.False(combined.IsSuccess);
        Assert.Equal(error1, combined.Error);
    }

    [Fact]
    public void WhenAll_ShouldReturnArray_WhenAllSucceed()
    {
        // Arrange
        var results = new[]
        {
            Result<int>.Ok(1),
            Result<int>.Ok(2),
            Result<int>.Ok(3)
        };

        // Act
        var combined = ResultExtensions.WhenAll(results);

        // Assert
        Assert.True(combined.IsSuccess);
        Assert.Equal(new[] { 1, 2, 3 }, combined.Value);
    }

    [Fact]
    public void WhenAll_ShouldReturnFirstError_WhenAnyFails()
    {
        // Arrange
        var error = Error.Validation("test");
        var results = new[]
        {
            Result<int>.Ok(1),
            Result<int>.Fail(error),
            Result<int>.Ok(3)
        };

        // Act
        var combined = ResultExtensions.WhenAll(results);

        // Assert
        Assert.False(combined.IsSuccess);
        Assert.Equal(error, combined.Error);
    }

    #endregion

    #region Railway Oriented Programming Tests

    [Fact]
    public async Task ThenAsync_ShouldChainOperations_WhenSuccessful()
    {
        // Arrange
        var result = Task.FromResult(Result<int>.Ok(5));

        // Act
        var chained = await result.ThenAsync(x => Task.FromResult(Result<string>.Ok(x.ToString())));

        // Assert
        Assert.True(chained.IsSuccess);
        Assert.Equal("5", chained.Value);
    }

    [Fact]
    public async Task ThenAsync_ShouldPropagateError()
    {
        // Arrange
        var error = Error.Validation("test");
        var result = Task.FromResult(Result<int>.Fail(error));

        // Act
        var chained = await result.ThenAsync(x => Task.FromResult(Result<string>.Ok(x.ToString())));

        // Assert
        Assert.False(chained.IsSuccess);
        Assert.Equal(error, chained.Error);
    }

    [Fact]
    public async Task OnSuccessAsync_ShouldExecuteAction_WhenSuccessful()
    {
        // Arrange
        var result = Task.FromResult(Result<int>.Ok(5));
        var executed = false;

        // Act
        var returned = await result.OnSuccessAsync(async x =>
        {
            await Task.Delay(1);
            executed = true;
        });

        // Assert
        Assert.True(executed);
        Assert.True(returned.IsSuccess);
        Assert.Equal(5, returned.Value);
    }

    [Fact]
    public async Task OnFailureAsync_ShouldExecuteAction_WhenFailed()
    {
        // Arrange
        var error = Error.Validation("test");
        var result = Task.FromResult(Result<int>.Fail(error));
        var executed = false;

        // Act
        var returned = await result.OnFailureAsync(async e =>
        {
            await Task.Delay(1);
            executed = true;
        });

        // Assert
        Assert.True(executed);
        Assert.False(returned.IsSuccess);
    }

    #endregion

    #region Conversion Tests

    [Fact]
    public void ToResult_FromNullableClass_ShouldReturnOk_WhenNotNull()
    {
        // Arrange
        string? value = "test";

        // Act
        var result = value.ToResult(Error.NotFound("test"));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("test", result.Value);
    }

    [Fact]
    public void ToResult_FromNullableClass_ShouldReturnError_WhenNull()
    {
        // Arrange
        string? value = null;
        var error = Error.NotFound("test");

        // Act
        var result = value.ToResult(error);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(error, result.Error);
    }

    [Fact]
    public void ToResult_FromNullableStruct_ShouldReturnOk_WhenHasValue()
    {
        // Arrange
        int? value = 42;

        // Act
        var result = value.ToResult(Error.NotFound("test"));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void ToResult_FromNullableStruct_ShouldReturnError_WhenNull()
    {
        // Arrange
        int? value = null;
        var error = Error.NotFound("test");

        // Act
        var result = value.ToResult(error);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(error, result.Error);
    }

    [Fact]
    public void Unwrap_ShouldReturnValue_WhenSuccessful()
    {
        // Arrange
        var result = Result<int>.Ok(42);

        // Act
        var value = result.Unwrap();

        // Assert
        Assert.Equal(42, value);
    }

    [Fact]
    public void Unwrap_ShouldThrowException_WhenFailed()
    {
        // Arrange
        var result = Result<int>.Fail(Error.Validation("test"));

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => result.Unwrap());
    }

    [Fact]
    public void ValueOrDefault_ShouldReturnValue_WhenSuccessful()
    {
        // Arrange
        var result = Result<int>.Ok(42);

        // Act
        var value = result.ValueOrDefault(0);

        // Assert
        Assert.Equal(42, value);
    }

    [Fact]
    public void ValueOrDefault_ShouldReturnDefault_WhenFailed()
    {
        // Arrange
        var result = Result<int>.Fail(Error.Validation("test"));

        // Act
        var value = result.ValueOrDefault(99);

        // Assert
        Assert.Equal(99, value);
    }

    [Fact]
    public void ValueOr_ShouldReturnValue_WhenSuccessful()
    {
        // Arrange
        var result = Result<int>.Ok(42);

        // Act
        var value = result.ValueOr(() => 0);

        // Assert
        Assert.Equal(42, value);
    }

    [Fact]
    public void ValueOr_ShouldExecuteFactory_WhenFailed()
    {
        // Arrange
        var result = Result<int>.Fail(Error.Validation("test"));

        // Act
        var value = result.ValueOr(() => 99);

        // Assert
        Assert.Equal(99, value);
    }

    #endregion

    #region ToResult Conversion Tests

    [Fact]
    public void ToResult_FromResult_ShouldThrow_WhenSuccessful()
    {
        // Arrange
        var result = Result.Ok();

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => result.ToResult<int>());
    }

    [Fact]
    public void ToResult_FromResult_ShouldConvertError_WhenFailed()
    {
        // Arrange
        var error = Error.Validation("test");
        var result = Result.Fail(error);

        // Act
        var converted = result.ToResult<int>();

        // Assert
        Assert.False(converted.IsSuccess);
        Assert.Equal(error, converted.Error);
    }

    [Fact]
    public void ToResult_FromGenericResult_ShouldThrow_WhenSuccessful()
    {
        // Arrange
        var result = Result<int>.Ok(42);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => result.ToResult<int, string>());
    }

    #endregion
}

