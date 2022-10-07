using FeatureLoom.Extensions;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace FeatureLoom.Helpers
{
    public class ArgsHelper : IEnumerable<KeyValuePair<string, string>>
    {
        private List<KeyValuePair<string, string>> namedArgs = new List<KeyValuePair<string, string>>();        
        private readonly string bullet;
        private readonly string assignment;

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
                if (arg.StartsWith(bullet))
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

        public void Add(string key, string value)
        {
            namedArgs.Add(new KeyValuePair<string, string>(key, value));
        }

        public int Count => namedArgs.Count;

        public string GetByIndex(int index) => TryGetByIndex(index, out string value) ? value : null;

        public bool TryGetByIndex(int index, out string value)
        {
            if (index < namedArgs.Count && index >= 0)
            {
                value = namedArgs[index].Value;
                return true;
            }
            else
            {
                value = default;
                return false;
            }
        }

        public string[] GetAllByKey(string key) => namedArgs.Where(pair => pair.Key == key).Select(pair => pair.Value).ToArray();        

        public int FindIndexByKey(string key, int startIndex = 0)
        {
            for (int i=startIndex; i< namedArgs.Count; i++)
            {
                if (namedArgs[i].Key == key) return i;
            }
            return -1;
        }        

        public string GetFirstByKey(string key) => TryGetFirstByKey(key, out string value) ? value : null;

        public bool TryGetFirstByKey(string key, out string value)
        {
            if (namedArgs.TryFindFirst((KeyValuePair<string,string> pair) => pair.Key == key, out var keyValue))
            {
                value = keyValue.Value;
                return true;
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