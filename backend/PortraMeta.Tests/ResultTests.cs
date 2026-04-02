using PortraMeta.Core.Models;

namespace PortraMeta.Tests;

public class ResultTests
{
    [Fact]
    public void GenericOk_ReturnsSuccessWithData()
    {
        var result = Result<string>.Ok("hello");

        Assert.True(result.Success);
        Assert.Equal("hello", result.Data);
        Assert.Null(result.Error);
    }

    [Fact]
    public void GenericFail_ReturnsFailureWithError()
    {
        var result = Result<string>.Fail("something went wrong");

        Assert.False(result.Success);
        Assert.Null(result.Data);
        Assert.Equal("something went wrong", result.Error);
    }

    [Fact]
    public void NonGenericOk_ReturnsSuccess()
    {
        var result = Result.Ok();

        Assert.True(result.Success);
        Assert.Null(result.Error);
    }

    [Fact]
    public void NonGenericFail_ReturnsFailureWithError()
    {
        var result = Result.Fail("error");

        Assert.False(result.Success);
        Assert.Equal("error", result.Error);
    }
}
