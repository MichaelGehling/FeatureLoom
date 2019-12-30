using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FeatureFlowFramework.DataFlows
{
    public class Selector<T> : IDataFlowSink, IAlternativeDataFlow
    {
        private readonly bool multiMatch = false;
        private List<(Func<T, bool> predicate, DataFlowSourceHelper sender)> options = null;
        private readonly DataFlowSourceHelper alternativeSendingHelper = null;

        public Selector(bool multiMatch = false)
        {
            this.multiMatch = multiMatch;
        }

        public IDataFlowSource Else => throw new NotImplementedException();

        public void Post<M>(in M message)
        {
            var options = this.options;
            bool success = false;
            if(options != null)
            {
                foreach(var option in options)
                {
                    if(message is T msgT && option.predicate(msgT))
                    {
                        option.sender.Forward(message);
                        success = true;
                    }
                    if(!multiMatch && success) return;
                }
            }
            if(!success) alternativeSendingHelper?.Forward(message);
        }

        public Task PostAsync<M>(M message)
        {
            var options = this.options;
            bool success = false;
            if(options != null)
            {
                Task[] tasks = null;
                if(multiMatch) tasks = new Task[options.Count];
                int i = 0;
                foreach(var option in options)
                {
                    if(message is T msgT && option.predicate(msgT))
                    {
                        Task task = option.sender.ForwardAsync(message);
                        success = true;
                        if(!multiMatch && success) return task;
                        else if(multiMatch) tasks[i++] = task;
                    }
                    else if(multiMatch) tasks[i++] = Task.CompletedTask;
                }
                if(multiMatch) return Task.WhenAll(tasks);
            }
            if(!success) return alternativeSendingHelper?.ForwardAsync(message);
            else return Task.CompletedTask;
        }

        public IDataFlowSource AddOption(Func<T, bool> predicate)
        {
            return InsertOptionAt(predicate, int.MaxValue);
        }

        public IDataFlowSource InsertOptionAt(Func<T, bool> predicate, int index)
        {
            (Func<T, bool> predicate, DataFlowSourceHelper sender) newOption = (predicate, new DataFlowSourceHelper());
            lock(this)
            {
                var newOptions = new List<(Func<T, bool> predicate, DataFlowSourceHelper sender)>();
                if(options != null) newOptions.AddRange(options);
                if(index <= newOptions.Count) newOptions.Insert(index, newOption);
                else newOptions.Add(newOption);
                options = newOptions;
            }
            return newOption.sender;
        }

        public int CountOptions
        {
            get
            {
                if(options != null) return options.Count;
                else return 0;
            }
        }

        public IDataFlowSource GetOptionAt(int index)
        {
            if(options != null && options.Count > index)
            {
                return options[index].sender;
            }
            else return null;
        }

        public bool RemoveOptionAt(int index)
        {
            lock(this)
            {
                if(options.Count == 1 && index == 0)
                {
                    options = null;
                    return true;
                }
                else if(options.Count > index)
                {
                    var newOptions = new List<(Func<T, bool> predicate, DataFlowSourceHelper sender)>();
                    if(options != null) newOptions.AddRange(options);
                    newOptions.RemoveAt(index);
                    options = newOptions;
                    return true;
                }
                else return false;
            }
        }

        public void ClearOptions()
        {
            lock(this)
            {
                options = null;
            }
        }
    }
}
