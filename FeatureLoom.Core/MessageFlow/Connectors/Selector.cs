﻿using FeatureLoom.Helpers;
using FeatureLoom.Synchronization;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FeatureLoom.MessageFlow
{
    public sealed class Selector<T> : IMessageSink<T>, IAlternativeMessageSource
    {
        private volatile bool multiMatch = false;
        private List<(Func<T, bool> predicate, SourceHelper sender)> options = null;
        private LazyValue<SourceHelper> alternativeSendingHelper;

        private MicroLock myLock = new MicroLock();
        
        public Type ConsumedMessageType => typeof(T);

        public Selector(bool multiMatch = false)
        {
            this.multiMatch = multiMatch;
        }

        public IMessageSource Else => alternativeSendingHelper.Obj;

        public void Post<M>(in M message)
        {
            var options = this.options;
            bool success = false;
            if (options != null)
            {
                foreach (var option in options)
                {
                    if (message is T msgT && option.predicate(msgT))
                    {
                        option.sender.Forward(in message);
                        success = true;
                    }
                    if (!multiMatch && success) return;
                }
            }
            if (!success) alternativeSendingHelper.ObjIfExists?.Forward(in message);
        }

        public void Post<M>(M message)
        {
            var options = this.options;
            bool success = false;
            if (options != null)
            {
                foreach (var option in options)
                {
                    if (message is T msgT && option.predicate(msgT))
                    {
                        option.sender.Forward(message);
                        success = true;
                    }
                    if (!multiMatch && success) return;
                }
            }
            if (!success) alternativeSendingHelper.ObjIfExists?.Forward(message);
        }

        public Task PostAsync<M>(M message)
        {
            var options = this.options;
            bool success = false;
            if (options != null)
            {
                Task[] tasks = null;
                if (multiMatch) tasks = new Task[options.Count];
                int i = 0;
                foreach (var option in options)
                {
                    if (message is T msgT && option.predicate(msgT))
                    {
                        Task task = option.sender.ForwardAsync(message);
                        success = true;
                        if (!multiMatch && success) return task;
                        else if (multiMatch) tasks[i++] = task;
                    }
                    else if (multiMatch) tasks[i++] = Task.CompletedTask;
                }
                if (multiMatch) return Task.WhenAll(tasks);
            }
            if (!success) return alternativeSendingHelper.ObjIfExists?.ForwardAsync(message);
            else return Task.CompletedTask;
        }

        public IMessageSource AddOption(Func<T, bool> predicate)
        {
            return InsertOptionAt(predicate, int.MaxValue);
        }

        public IMessageSource InsertOptionAt(Func<T, bool> predicate, int index)
        {
            (Func<T, bool> predicate, SourceHelper sender) newOption = (predicate, new SourceHelper());
            using (myLock.Lock())
            {
                var newOptions = new List<(Func<T, bool> predicate, SourceHelper sender)>();
                if (options != null) newOptions.AddRange(options);
                if (index <= newOptions.Count) newOptions.Insert(index, newOption);
                else newOptions.Add(newOption);
                options = newOptions;
            }
            return newOption.sender;
        }

        public int CountOptions
        {
            get
            {
                if (options != null) return options.Count;
                else return 0;
            }
        }

        public bool MultiMatch { get => multiMatch; set => multiMatch = value; }

        public IMessageSource GetOptionAt(int index)
        {
            using (myLock.Lock())
            {
                if (options != null && options.Count > index)
                {
                    return options[index].sender;
                }
                else return null;
            }
        }

        public bool RemoveOptionAt(int index)
        {
            using (myLock.Lock())
            {
                if (options.Count == 1 && index == 0)
                {
                    options = null;
                    return true;
                }
                else if (options.Count > index)
                {
                    var newOptions = new List<(Func<T, bool> predicate, SourceHelper sender)>();
                    if (options != null) newOptions.AddRange(options);
                    newOptions.RemoveAt(index);
                    options = newOptions;
                    return true;
                }
                else return false;
            }
        }

        public void ClearOptions()
        {
            using (myLock.Lock())
            {
                options = null;
            }
        }
    }
}