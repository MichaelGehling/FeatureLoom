using FeatureLoom.Synchronization;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace FeatureLoom.Helpers;

/// <summary>
/// Provides thread-safe and optionally colorized console operations, supporting both synchronous and asynchronous usage.
/// </summary>
public static class ConsoleHelper
{
    // Lock to ensure thread-safe console access (supports sync and async).
    static FeatureLock consoleLock = new();

    /// <summary>
    /// Executes a console action within a lock to ensure thread safety.
    /// </summary>
    /// <param name="consoleAction">The action to execute.</param>
    public static void UseLocked(Action consoleAction)
    {
        using (consoleLock.Lock())
        {
            consoleAction();
        }
    }

    /// <summary>
    /// Asynchronously executes a console action within a lock to ensure thread safety.
    /// </summary>
    /// <param name="consoleAction">The action to execute.</param>
    public static async Task UseLockedAsync(Action consoleAction)
    {
        using (await consoleLock.LockAsync())
        {
            consoleAction();
        }
    }

    /// <summary>
    /// Asynchronously executes an asynchronous console action within a lock to ensure thread safety.
    /// </summary>
    /// <param name="consoleAction">The asynchronous action to execute.</param>
    public static async Task UseLockedAsync(Func<Task> consoleAction)
    {
        using (await consoleLock.LockAsync())
        {
            await consoleAction();
        }
    }

    /// <summary>
    /// Executes a console action with optional foreground and background colors, within a lock.
    /// </summary>
    /// <param name="consoleAction">The action to execute.</param>
    /// <param name="foreGroundColor">Optional foreground color.</param>
    /// <param name="backGroundColor">Optional background color.</param>
    public static void UseLocked(Action consoleAction, ConsoleColor? foreGroundColor, ConsoleColor? backGroundColor = null)
    {
        UseWithColors(consoleAction, foreGroundColor, backGroundColor);
    }

    /// <summary>
    /// Asynchronously executes a console action with optional colors, within a lock.
    /// </summary>
    public static Task UseLockedAsync(Action consoleAction, ConsoleColor? foreGroundColor, ConsoleColor? backGroundColor = null)
    {
        return UseWithColorsAsync(consoleAction, foreGroundColor, backGroundColor);
    }

    /// <summary>
    /// Asynchronously executes an asynchronous console action with optional colors, within a lock.
    /// </summary>
    public static Task UseLockedAsync(Func<Task> consoleAction, ConsoleColor? foreGroundColor, ConsoleColor? backGroundColor = null)
    {
        return UseWithColorsAsync(consoleAction, foreGroundColor, backGroundColor);
    }

    /// <summary>
    /// Writes a line to the console with optional colors, within a lock.
    /// </summary>
    public static void WriteLine(string text, ConsoleColor? foreGroundColor, ConsoleColor? backGroundColor = null)
    {
        UseWithColors(() => Console.Out.WriteLine(text), foreGroundColor, backGroundColor);
    }

    /// <summary>
    /// Asynchronously writes a line to the console with optional colors, within a lock.
    /// </summary>
    public static Task WriteLineAsync(string text, ConsoleColor? foreGroundColor, ConsoleColor? backGroundColor = null)
    {
        return UseWithColorsAsync(() => Console.Out.WriteLineAsync(text), foreGroundColor, backGroundColor);
    }

    /// <summary>
    /// Writes a line to the error stream with optional colors, within a lock.
    /// </summary>
    public static void WriteLineToError(string text, ConsoleColor? foreGroundColor, ConsoleColor? backGroundColor = null)
    {
        UseWithColors(() => Console.Error.WriteLine(text), foreGroundColor, backGroundColor);
    }

    /// <summary>
    /// Asynchronously writes a line to the error stream with optional colors, within a lock.
    /// </summary>
    public static Task WriteLineToErrorAsync(string text, ConsoleColor? foreGroundColor, ConsoleColor? backGroundColor = null)
    {
        return UseWithColorsAsync(() => Console.Error.WriteLineAsync(text), foreGroundColor, backGroundColor);
    }

    /// <summary>
    /// Writes a line to the console within a lock.
    /// </summary>
    public static void WriteLine(string text)
    {
        UseLocked(() => Console.Out.WriteLine(text));
    }

    /// <summary>
    /// Asynchronously writes a line to the console within a lock.
    /// </summary>
    public static Task WriteLineAsync(string text)
    {
        return UseLockedAsync(() => Console.Out.WriteLineAsync(text));
    }

    /// <summary>
    /// Writes a line to the error stream within a lock.
    /// </summary>
    public static void WriteLineToError(string text)
    {
        UseLocked(() => Console.Error.WriteLine(text));
    }

    /// <summary>
    /// Asynchronously writes a line to the error stream within a lock.
    /// </summary>
    public static Task WriteLineToErrorAsync(string text)
    {
        return UseLockedAsync(() => Console.Error.WriteLineAsync(text));
    }

    // Executes a console action with color changes, restoring colors after execution.
    private static void UseWithColors(Action consoleAction, ConsoleColor? foreGroundColor, ConsoleColor? backGroundColor)
    {
        using (consoleLock.Lock())
        {
            var oldBgColor = Console.BackgroundColor;
            var oldFgColor = Console.ForegroundColor;
            if (backGroundColor.HasValue) Console.BackgroundColor = backGroundColor.Value;
            if (foreGroundColor.HasValue) Console.ForegroundColor = foreGroundColor.Value;

            consoleAction();

            Console.BackgroundColor = oldBgColor;
            Console.ForegroundColor = oldFgColor;
        }
    }

    // Asynchronously executes a console action with color changes, restoring colors after execution.
    private static async Task UseWithColorsAsync(Action consoleAction, ConsoleColor? foreGroundColor, ConsoleColor? backGroundColor)
    {
        using (await consoleLock.LockAsync())
        {
            var oldBgColor = Console.BackgroundColor;
            var oldFgColor = Console.ForegroundColor;
            if (backGroundColor.HasValue) Console.BackgroundColor = backGroundColor.Value;
            if (foreGroundColor.HasValue) Console.ForegroundColor = foreGroundColor.Value;

            consoleAction();

            Console.BackgroundColor = oldBgColor;
            Console.ForegroundColor = oldFgColor;
        }
    }

    // Asynchronously executes an async console action with color changes, restoring colors after execution.
    private static async Task UseWithColorsAsync(Func<Task> consoleAction, ConsoleColor? foreGroundColor, ConsoleColor? backGroundColor)
    {
        using (await consoleLock.LockAsync())
        {
            var oldBgColor = Console.BackgroundColor;
            var oldFgColor = Console.ForegroundColor;
            if (backGroundColor.HasValue) Console.BackgroundColor = backGroundColor.Value;
            if (foreGroundColor.HasValue) Console.ForegroundColor = foreGroundColor.Value;

            await consoleAction();

            Console.BackgroundColor = oldBgColor;
            Console.ForegroundColor = oldFgColor;
        }
    }

    /// <summary>
    /// Reads a line from the console within a lock to ensure thread safety.
    /// </summary>
    /// <returns>The input string.</returns>
    public static string ReadLineLocked()
    {
        string input = string.Empty;
        UseLocked(() => input = Console.ReadLine());
        return input;
    }

    /// <summary>
    /// Asynchronously reads a line from the console within a lock to ensure thread safety.
    /// </summary>
    /// <returns>The input string.</returns>
    public static async Task<string> ReadLineLockedAsync()
    {
        string input = string.Empty;
        await UseLockedAsync(async () => input = await Console.In.ReadLineAsync());
        return input;
    }

    /// <summary>
    /// Clears the console within a lock to ensure thread safety.
    /// </summary>
    public static void ClearConsole()
    {
        UseLocked(() => Console.Clear());
    }

    /// <summary>
    /// Asynchronously clears the console within a lock to ensure thread safety.
    /// </summary>
    public static Task ClearConsoleAsync()
    {
        return UseLockedAsync(() => Console.Clear());
    }

    private static bool? hasConsole;

    /// <summary>
    /// Checks if a console is available for the current process, with optional cache reset.
    /// </summary>
    /// <param name="resetCachedResult">If true, resets the cached result and checks again.</param>
    /// <returns>True if a console is available, otherwise false.</returns>
    public static bool CheckHasConsole(bool resetCachedResult = false)
    {
        if (resetCachedResult) hasConsole = null;
        else if (hasConsole.HasValue) return hasConsole.Value;

        try
        {
            _ = Console.WindowHeight;
            hasConsole = true;
        }
        catch
        {
            hasConsole = false;
        }

        return hasConsole.Value;
    }
}

