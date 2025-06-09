using System;
using System.IO;
using System.Threading.Tasks;
using FeatureLoom.Helpers;
using Xunit;

namespace FeatureLoom.Helpers;

public class ConsoleHelperTests
{
    [Fact]
    public void WriteLine_WritesToConsoleOut()
    {
        var sw = new StringWriter();
        var originalOut = Console.Out;
        Console.SetOut(sw);

        try
        {
            ConsoleHelper.WriteLine("test output");
            Assert.Contains("test output", sw.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public void WriteLineToError_WritesToConsoleError()
    {
        var sw = new StringWriter();
        var originalErr = Console.Error;
        Console.SetError(sw);

        try
        {
            ConsoleHelper.WriteLineToError("error output");
            Assert.Contains("error output", sw.ToString());
        }
        finally
        {
            Console.SetError(originalErr);
        }
    }

    [Fact]
    public async Task WriteLineAsync_WritesToConsoleOut()
    {
        var sw = new StringWriter();
        var originalOut = Console.Out;
        Console.SetOut(sw);

        try
        {
            await ConsoleHelper.WriteLineAsync("async output");
            Assert.Contains("async output", sw.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public async Task WriteLineToErrorAsync_WritesToConsoleError()
    {
        var sw = new StringWriter();
        var originalErr = Console.Error;
        Console.SetError(sw);

        try
        {
            await ConsoleHelper.WriteLineToErrorAsync("async error");
            Assert.Contains("async error", sw.ToString());
        }
        finally
        {
            Console.SetError(originalErr);
        }
    }

    [Fact]
    public void UseLocked_ExecutesAction()
    {
        bool called = false;
        ConsoleHelper.UseLocked(() => called = true);
        Assert.True(called);
    }

    [Fact]
    public async Task UseLockedAsync_ExecutesAction()
    {
        bool called = false;
        await ConsoleHelper.UseLockedAsync(() => called = true);
        Assert.True(called);
    }

    [Fact]
    public async Task UseLockedAsync_ExecutesAsyncFunc()
    {
        bool called = false;
        await ConsoleHelper.UseLockedAsync(async () => { called = true; await Task.Yield(); });
        Assert.True(called);
    }

    [Fact]
    public void ReadLineLocked_ReadsInput()
    {
        var sr = new StringReader("input line");
        var originalIn = Console.In;
        Console.SetIn(sr);

        try
        {
            string result = ConsoleHelper.ReadLineLocked();
            Assert.Equal("input line", result);
        }
        finally
        {
            Console.SetIn(originalIn);
        }
    }

    [Fact]
    public async Task ReadLineLockedAsync_ReadsInput()
    {
        var sr = new StringReader("async input");
        var originalIn = Console.In;
        Console.SetIn(sr);

        try
        {
            string result = await ConsoleHelper.ReadLineLockedAsync();
            Assert.Equal("async input", result);
        }
        finally
        {
            Console.SetIn(originalIn);
        }
    }

    [Fact]
    public void CheckHasConsole_ReturnsBool()
    {
        bool result = ConsoleHelper.CheckHasConsole();
        Assert.IsType<bool>(result);
    }
}