using FeatureLoom.Extensions;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace FeatureLoom.Helpers
{
    /// <summary>
    /// Helps with handling of an argument list.
    /// </summary>
    public class ArgsHelper : IEnumerable<KeyValuePair<string, string>>
    {
        private List<KeyValuePair<string, string>> namedArgs = new List<KeyValuePair<string, string>>();        
        private readonly string bullet;
        private readonly string assignment;

        /// <summary>
        /// Parses an argument list based on a defined pattern by bullets and assignments.
        /// The string after a bullet is interpreted as a key, the value after an assignment is 
        /// interpreted as a value. If an element does not have bullet and key, the string is
        /// also interpreted as a value, but without a key.        
        /// Example: "-name=Teddy -friends Jim Carl" would be parsed to [("name", "Teddy"), ("friends", null), (null, "Jim"), (null, "Carl")].
        /// The name can be retreived by calling GetFirstByKey("name")
        /// The list of friends can be retrieved by calling GetAllAfterKey("friends");
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
        /// Extends the parsed list of argumnets with an additional key value pair.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public void Add(string key, string value)
        {
            namedArgs.Add(new KeyValuePair<string, string>(key, value));
        }

        /// <summary>
        /// The number of arguments
        /// </summary>
        public int Count => namedArgs.Count;

        /// <summary>
        /// Returns an argumnet value based on its index
        /// </summary>        
        /// <returns>The value part of the argument, null if not found</returns>
        public string GetByIndex(int index) => TryGetByIndex(index, out string value) ? value : null;

        /// <summary>
        /// Tries to get and convert an argument value.
        /// </summary>
        /// <typeparam name="T">The type to convert to</typeparam>
        /// <param name="index">The index of the argument</param>
        /// <param name="value">The converted argument value</param>
        /// <returns>True if successful</returns>
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
        /// Returns all values with the given key.
        /// </summary>
        /// <param name="key">The key to check for</param>
        /// <returns>The list of argument values</returns>
        public IEnumerable<string> GetAllByKey(string key) => namedArgs.Where(pair => pair.Key == key).Select(pair => pair.Value).ToArray();

        /// <summary>
        /// Returns all values with the given key and following values that do not have any key.
        /// Example: With "-name=Teddy -friends Jim Carl" GetAllAfterKey("friends") would return ["Jim", "Carl"].
        /// </summary>
        /// <param name="key">The key to check for</param>
        /// <returns>The list of argument values</returns>
        public IEnumerable<string> GetAllAfterKey(string key)
        {
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
        /// Returns the index of the given key.
        /// </summary>
        /// <param name="key">The key to be found</param>
        /// <param name="startIndex">The index, where to start the search</param>
        /// <returns>The index of the key, or -1 if not found</returns>
        public int FindIndexByKey(string key, int startIndex = 0)
        {
            for (int i=startIndex; i< namedArgs.Count; i++)
            {
                if (namedArgs[i].Key == key) return i;
            }
            return -1;
        }
        /// <summary>
        /// True if the key exists.
        /// </summary>
        /// <param name="key">The key to be found</param>
        /// <returns>True if the key exists, otherwise False</returns>
        public bool HasKey(string key) => FindIndexByKey(key, 0) != -1;        

        /// <summary>
        /// Returns first argumnet value with the given key
        /// </summary>        
        /// <returns>The value part of the argument, null if not found</returns>
        public string GetFirstByKey(string key) => TryGetFirstByKey(key, out string value) ? value : null;

        /// <summary>
        /// Tries to get and convert an argument value.
        /// </summary>
        /// <typeparam name="T">The type to convert to</typeparam>
        /// <param name="key">The key of the argument</param>
        /// <param name="value">The converted argument value</param>
        /// <returns>True if successful</returns>
        public bool TryGetFirstByKey<T>(string key, out T value)
        {
            if (namedArgs.TryFindFirst((KeyValuePair<string,string> pair) => pair.Key == key, out var argument))
            {
                if (argument.Value.TryConvertTo<string, T>(out value)) return true;
            }
            value = default;
            return false;
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
}