using FeatureLoom.Synchronization;
using System;
using System.Collections.Generic;
using System.Text;

namespace FeatureLoom.Helpers;

public static class ConsoleHelper
{
    static MicroLock consoleLock = new MicroLock();

    public static void UseLocked(Action consoleAction)
    {
        using (consoleLock.Lock())
        {
            consoleAction();
        }
    }

    public static void UseLocked(Action consoleAction, ConsoleColor? foreGroundColor, ConsoleColor? backGroundColor = null)
    {
        UseWithColors(consoleAction, foreGroundColor, backGroundColor);
    }

    public static void WriteLine(string text, ConsoleColor? foreGroundColor, ConsoleColor? backGroundColor = null)
    {
        UseWithColors(() => Console.WriteLine(text), foreGroundColor, backGroundColor);
    }

    public static void WriteLineToError(string text, ConsoleColor? foreGroundColor, ConsoleColor? backGroundColor = null)
    {
        UseWithColors(() => Console.Error.WriteLine(text), foreGroundColor, backGroundColor);
    }

    public static void WriteLine(string text)
    {
        UseLocked(() => Console.WriteLine(text));
    }

    public static void WriteLineToError(string text)
    {
        UseLocked(() => Console.Error.WriteLine(text));
    }

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

    public static string ReadLineLocked()
    {
        string input = string.Empty;
        UseLocked(() => input = Console.ReadLine());
        return input;
    }

    public static void ClearConsole()
    {
        UseLocked(() => Console.Clear());
    }

    private static bool? hasConsole;

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

