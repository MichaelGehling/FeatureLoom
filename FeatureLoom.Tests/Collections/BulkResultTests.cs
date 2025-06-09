using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace FeatureLoom.Collections;

public class BulkResultTests
{
    [Fact]
    public void BulkResult_AddsSuccessAndErrorAndWarning()
    {
        var result = new BulkResult();
        result.AddSuccess("success1");
        result.AddError("error1");
        result.AddWarning("warning1");

        Assert.Equal(1, result.SuccessCount);
        Assert.Equal(1, result.ErrorCount);
        Assert.Equal(1, result.WarningCount);

        Assert.Contains("success1", result.SuccessDescriptions);
        Assert.Contains("error1", result.ErrorDescriptions);
        Assert.Contains("warning1", result.WarningDescriptions);
    }

    [Fact]
    public void BulkResult_ImplicitSuccessWhenNoResults()
    {
        var result = new BulkResult();
        Assert.Equal(1, result.SuccessCount);
        Assert.Empty(result.ErrorDescriptions);
        Assert.Empty(result.WarningDescriptions);
    }

    [Fact]
    public void BulkResult_OnlyErrorsOrWarnings_NoImplicitSuccess()
    {
        var result = new BulkResult();
        result.AddError("error");
        Assert.Equal(0, result.SuccessCount);

        var result2 = new BulkResult();
        result2.AddWarning("warning");
        Assert.Equal(0, result2.SuccessCount);
    }

    [Fact]
    public void BulkResult_LockingDisabled_DoesNotThrow()
    {
        var result = new BulkResult { LockingDisabled = true };
        result.AddSuccess("success");
        result.AddError("error");
        result.AddWarning("warning");
        Assert.Equal(1, result.SuccessCount);
        Assert.Equal(1, result.ErrorCount);
        Assert.Equal(1, result.WarningCount);
    }

    [Fact]
    public void BulkResultT_AddsSuccessErrorWarningAndExtractsDescriptions()
    {
        var result = new BulkResult<int>();
        result.AddSuccess(42, "success");
        result.AddError(13, "error");
        result.AddWarning(7, "warning");

        Assert.Equal(1, result.SuccessCount);
        Assert.Equal(1, result.ErrorCount);
        Assert.Equal(1, result.WarningCount);

        Assert.Contains("success", result.SuccessDescriptions);
        Assert.Contains("error", result.ErrorDescriptions);
        Assert.Contains("warning", result.WarningDescriptions);

        var values = result.GetResultValues(ignoreSuccesses: false, ignoreWarnings: false, ignoreErrors: false).ToArray();
        Assert.Contains(42, values);
        Assert.Contains(13, values);
        Assert.Contains(7, values);
    }

    [Fact]
    public void BulkResultT_ExtractDescriptionFunc_Works()
    {
        var result = new BulkResult<int>();
        result.AddSuccess(99, v => $"Value is {v}");

        Assert.Contains("Value is 99", result.SuccessDescriptions);
    }

    [Fact]
    public void BulkResultT_LockingDisabled_DoesNotThrow()
    {
        var result = new BulkResult<int> { LockingDisabled = true };
        result.AddSuccess(1, "success");
        result.AddError(2, "error");
        result.AddWarning(3, "warning");
        Assert.Equal(1, result.SuccessCount);
        Assert.Equal(1, result.ErrorCount);
        Assert.Equal(1, result.WarningCount);
    }

    [Fact]
    public void BulkResultT_ImplicitSuccessWhenNoResults()
    {
        var result = new BulkResult<int>();
        Assert.Equal(1, result.SuccessCount);
        Assert.Empty(result.ErrorDescriptions);
        Assert.Empty(result.WarningDescriptions);
    }
}

