using System;
using System.Threading;
using System.Threading.Tasks;

namespace FeatureLoom.Statemachines
{
    public sealed partial class Statemachine<T> where T : class
    {
        public readonly struct State
        {
            public readonly string name;
            public readonly Func<T, CancellationToken, Task<string>> action;

            public State(string name, Func<T, CancellationToken, Task<string>> action)
            {
                this.name = name;
                this.action = action;
            }

            public State(string name, Func<T, Task<string>> action)
            {
                this.name = name;
                this.action = (c, token)  => action(c);
            }

            public State(string name, Func<CancellationToken, Task<string>> action)
            {
                this.name = name;
                this.action = (c, token) => action(token);
            }

            public State(string name, Func<Task<string>> action)
            {
                this.name = name;
                this.action = (c, token) => action();
            }

            public static implicit operator State((string name, Func<T, CancellationToken, Task<string>> action) tuple) => new State(tuple.name, tuple.action);
            public static implicit operator State((string name, Func<T, Task<string>> action) tuple) => new State(tuple.name, tuple.action);
            public static implicit operator State((string name, Func<CancellationToken, Task<string>> action) tuple) => new State(tuple.name, tuple.action);
            public static implicit operator State((string name, Func<Task<string>> action) tuple) => new State(tuple.name, tuple.action);
        }

    }


        
}
