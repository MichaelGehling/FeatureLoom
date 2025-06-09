using FeatureLoom.Extensions;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace FeatureLoom.Helpers;

/// <summary>
/// Helps with handling and parsing of an argument list.
/// Supports both named and positional arguments, quoted values, and case-insensitive key lookups.
/// </summary>
public class ArgsHelper : IEnumerable<KeyValuePair<string, string>>
{
    private List<KeyValuePair<string, string>> namedArgs = new List<KeyValuePair<string, string>>();
    private readonly string bullet;
    private readonly string assignment;

    /// <summary>
    /// Parses an argument list based on a defined pattern by bullets and assignments.
    /// The string after a bullet is interpreted as a key (case-insensitive), the value after an assignment is 
    /// interpreted as a value. If an element does not have bullet and key, the string is
    /// also interpreted as a value, but without a key.
    /// Example: "-name=Teddy -friends Jim Carl" would be parsed to [("name", "Teddy"), ("friends", ""), ("", "Jim"), ("", "Carl")].
    /// The name can be retrieved by calling GetFirstByKey("name").
    /// The list of friends can be retrieved by calling GetAllAfterKey("friends").
    /// Key lookups are case-insensitive.
    /// </summary>
    /// <param name="args">List of arguments</param>
    /// <param name="bullet">Char used as bullet, usually '-' or '/'</param>
    /// <param name="assignment">Char used as assignment, usually '=' or ':'</param>
    public ArgsHelper(string[] args, char bullet = '-', char assignment = '=')
    {
        this.bullet = bullet.ToString();
        this.assignment = assignment.ToString();

        Parse(args);
    }

    /// <summary>
    /// Parses an argument string into an argument list, supporting quoted values and case-insensitive key lookups.
    /// Quoted values (single or double quotes) are preserved as a single argument and quotes are removed.
    /// </summary>
    /// <param name="argsString">The argument string (e.g., as typed on a command line).</param>
    /// <param name="bullet">Char used as bullet, usually '-' or '/'</param>
    /// <param name="assignment">Char used as assignment, usually '=' or ':'</param>
    public ArgsHelper(string argsString, char bullet = '-', char assignment = '=')
        : this(SplitArgs(argsString), bullet, assignment)
    {
    }

    /// <summary>
    /// Parses the provided arguments into key-value pairs, normalizing keys to lower-case for case-insensitive lookup.
    /// </summary>
    /// <param name="args">The argument array to parse.</param>
    private void Parse(string[] args)
    {
        foreach (var arg in args)
        {
            if (arg.StartsWith(bullet) && arg.Contains(assignment))
            {
                var key = arg.Substring(bullet, assignment);
                var value = arg.Substring(assignment);
                Add(key, value);
            }
            else if (arg.StartsWith(bullet))
            {
                var key = arg.Substring(bullet);
                Add(key, "");
            }
            else
            {
                Add("", arg);
            }
        }
    }

    /// <summary>
    /// Extends the parsed list of arguments with an additional key-value pair.
    /// Keys are normalized to lower-case for case-insensitive lookup.
    /// </summary>
    /// <param name="key">The argument key (case-insensitive).</param>
    /// <param name="value">The argument value.</param>
    public void Add(string key, string value)
    {
        namedArgs.Add(new KeyValuePair<string, string>(key?.ToLowerInvariant() ?? "", value));
    }

    /// <summary>
    /// The number of parsed arguments.
    /// </summary>
    public int Count => namedArgs.Count;

    /// <summary>
    /// Returns an argument value based on its index.
    /// </summary>
    /// <param name="index">The index of the argument.</param>
    /// <returns>The value part of the argument, or null if not found.</returns>
    public string GetByIndex(int index) => TryGetByIndex(index, out string value) ? value : null;

    /// <summary>
    /// Tries to get and convert an argument value by index.
    /// </summary>
    /// <typeparam name="T">The type to convert to.</typeparam>
    /// <param name="index">The index of the argument.</param>
    /// <param name="value">The converted argument value.</param>
    /// <returns>True if successful.</returns>
    public bool TryGetByIndex<T>(int index, out T value)
    {
        if (index < namedArgs.Count && index >= 0)
        {
            string valueStr = namedArgs[index].Value;
            if (valueStr.TryConvertTo<string, T>(out value)) return true;
        }

        value = default;
        return false;
    }

    /// <summary>
    /// Returns all values with the given key (case-insensitive).
    /// </summary>
    /// <param name="key">The key to check for (case-insensitive).</param>
    /// <returns>The list of argument values.</returns>
    public IEnumerable<string> GetAllByKey(string key) =>
        namedArgs.Where(pair => pair.Key == (key?.ToLowerInvariant() ?? "")).Select(pair => pair.Value).ToArray();

    /// <summary>
    /// Returns all values with the given key (case-insensitive) and following values that do not have any key.
    /// Example: With "-name=Teddy -friends Jim Carl" GetAllAfterKey("friends") would return ["Jim", "Carl"].
    /// </summary>
    /// <param name="key">The key to check for (case-insensitive).</param>
    /// <returns>The list of argument values.</returns>
    public IEnumerable<string> GetAllAfterKey(string key)
    {
        key = key?.ToLowerInvariant() ?? "";
        List<string> values = new List<string>();
        int index = 0;
        while (index >= 0 && index < namedArgs.Count)
        {
            index = namedArgs.FindIndex(index, p => p.Key == key);
            if (index < 0) break;
            if (namedArgs[index].Value != "") values.Add(namedArgs[index].Value);
            while (++index < namedArgs.Count && namedArgs[index].Key == "" && namedArgs[index].Value != "") values.Add(namedArgs[index].Value);
        }
        return values;
    }

    /// <summary>
    /// Returns the index of the given key (case-insensitive).
    /// </summary>
    /// <param name="key">The key to be found (case-insensitive).</param>
    /// <param name="startIndex">The index where to start the search.</param>
    /// <returns>The index of the key, or -1 if not found.</returns>
    public int FindIndexByKey(string key, int startIndex = 0)
    {
        key = key?.ToLowerInvariant() ?? "";
        for (int i = startIndex; i < namedArgs.Count; i++)
        {
            if (namedArgs[i].Key == key) return i;
        }
        return -1;
    }

    /// <summary>
    /// True if the key exists (case-insensitive).
    /// </summary>
    /// <param name="key">The key to be found (case-insensitive).</param>
    /// <returns>True if the key exists, otherwise False.</returns>
    public bool HasKey(string key) => FindIndexByKey(key, 0) != -1;

    /// <summary>
    /// Returns the first argument value with the given key (case-insensitive).
    /// </summary>
    /// <param name="key">The key to check for (case-insensitive).</param>
    /// <returns>The value part of the argument, or null if not found.</returns>
    public string GetFirstByKey(string key) => TryGetFirstByKey(key, out string value) ? value : null;

    /// <summary>
    /// Tries to get and convert the first argument value with the given key (case-insensitive).
    /// </summary>
    /// <typeparam name="T">The type to convert to.</typeparam>
    /// <param name="key">The key of the argument (case-insensitive).</param>
    /// <param name="value">The converted argument value.</param>
    /// <returns>True if successful.</returns>
    public bool TryGetFirstByKey<T>(string key, out T value)
    {
        key = key?.ToLowerInvariant() ?? "";
        if (namedArgs.TryFindFirst((KeyValuePair<string, string> pair) => pair.Key == key, out var argument))
        {
            if (argument.Value.TryConvertTo<string, T>(out value)) return true;
        }
        value = default;
        return false;
    }

    /// <summary>
    /// Splits a single argument string into an array of arguments, supporting quoted values.
    /// Quoted values (single or double quotes) are preserved as a single argument and quotes are removed.
    /// </summary>
    /// <param name="args">The argument string.</param>
    /// <returns>An array of parsed arguments.</returns>
    private static string[] SplitArgs(string args)
    {
        if (string.IsNullOrWhiteSpace(args)) return Array.Empty<string>();
        var result = new List<string>();
        int i = 0;
        while (i < args.Length)
        {
            // Skip whitespace
            while (i < args.Length && char.IsWhiteSpace(args[i])) i++;
            if (i >= args.Length) break;

            char quoteChar = '\0';
            if (args[i] == '"' || args[i] == '\'')
            {
                quoteChar = args[i];
                i++;
            }

            var sb = new System.Text.StringBuilder();
            bool inQuotes = quoteChar != '\0';
            while (i < args.Length)
            {
                if (inQuotes)
                {
                    if (args[i] == quoteChar)
                    {
                        i++; // skip closing quote
                        break;
                    }
                    // Handle escaped quote
                    if (args[i] == '\\' && i + 1 < args.Length && args[i + 1] == quoteChar)
                    {
                        sb.Append(quoteChar);
                        i += 2;
                        continue;
                    }
                    sb.Append(args[i]);
                    i++;
                }
                else
                {
                    if (char.IsWhiteSpace(args[i]))
                    {
                        break;
                    }
                    // Start of quoted section inside an argument
                    if (args[i] == '"' || args[i] == '\'')
                    {
                        quoteChar = args[i];
                        inQuotes = true;
                        i++;
                        continue;
                    }
                    sb.Append(args[i]);
                    i++;
                }
            }
            result.Add(sb.ToString());
        }
        return result.ToArray();
    }

    public IEnumerator<KeyValuePair<string, string>> GetEnumerator()
    {
        return ((IEnumerable<KeyValuePair<string, string>>)namedArgs).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return ((IEnumerable)namedArgs).GetEnumerator();
    }
}