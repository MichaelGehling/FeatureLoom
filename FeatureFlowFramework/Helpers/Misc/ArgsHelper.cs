using FeatureLoom.Helpers.Extensions;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace FeatureLoom.Helpers.Misc
{
    public class ArgsHelper : IEnumerable<KeyValuePair<string, string>> 
    {
        string[] args;        
        Dictionary<string, string> namedArgs = new Dictionary<string, string>();
        readonly char bullet;
        readonly char assignment;

        public ArgsHelper(string[] args, char bullet = '-', char assignment = '=')
        {
            this.args = args;
            this.bullet = bullet;
            this.assignment = assignment;

            Parse();
        }

        void Parse(bool overwriteExisting = true)
        {
            foreach(var arg in args)
            {
                if(arg.StartsWith(bullet) && arg.Contains(assignment))
                {
                    var key = arg.Substring(bullet.ToString(), assignment.ToString());
                    var value = arg.Substring(assignment.ToString());
                    if(!key.EmptyOrNull())
                    {
                        Add(key, value, overwriteExisting);                        
                    }
                }
            }
        }

        public void Add(string key, string value, bool overwriteExisting = true)
        {
            if(overwriteExisting || !namedArgs.ContainsKey(key)) namedArgs[key] = value;
        }

        public void Update(string[] args, bool clearAll = true, bool overwriteExisting = true)
        {
            this.args = args;
            if (clearAll) namedArgs.Clear();
            Parse(overwriteExisting);
        }

        public int Count => args.Length;

        public string GetByIndex(int index) => TryGetByIndex(index, out string value) ? value : null;
        public bool TryGetByIndex(int index, out string value)
        {
            if (index <= args.Length && index >= 0)
            {
                value = args[index];
                return true;
            }
            else
            {
                value = default;
                return false;
            }
        }

        public string GetByKey(string key) => TryGetByKey(key, out string value) ? value : null;
        public bool TryGetByKey(string key, out string value)
        {
            return namedArgs.TryGetValue(key, out value);
        }

        public IEnumerator<KeyValuePair<string, string>> GetEnumerator()
        {
            return ((IEnumerable<KeyValuePair<string, string>>)namedArgs).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable<KeyValuePair<string, string>>)namedArgs).GetEnumerator();
        }
    }
}
