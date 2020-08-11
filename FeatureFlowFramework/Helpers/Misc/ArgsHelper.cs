using FeatureFlowFramework.Helpers.Extensions;
using System;
using System.Collections.Generic;
using System.Text;

namespace FeatureFlowFramework.Helpers.Misc
{
    public class ArgsHelper
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

        void Parse()
        {
            foreach(var arg in args)
            {
                if(arg.StartsWith(bullet) && arg.Contains(assignment))
                {
                    var key = arg.Substring(bullet, assignment);
                    var value = arg.Substring(assignment);
                    if(!key.EmptyOrNull()) namedArgs[key] = value;
                }
            }
        }

        public void Update(string[] args)
        {
            this.args = args;
            namedArgs.Clear();
            Parse();
        }

        public int Count => args.Length;

        public string GetByIndex(int index) => args[index];
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

        public string GetByKey(string key) => namedArgs[key];
        public bool TryGetByKey(string key, out string value)
        {
            return namedArgs.TryGetValue(key, out value);
        }
    }
}
