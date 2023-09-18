using FeatureLoom.DependencyInversion;
using FeatureLoom.Synchronization;
using System;
using System.Collections.Generic;
using System.Text;

namespace FeatureLoom.Collections
{
    public sealed class Pool<T>  where T : class
    {
        Stack<T> stack = new Stack<T>();        
        FeatureLock myLock;        
        Func<T> take;
        Action<T> ret;

        public Pool(Func<T> create, Action<T> reset = null, int maxSize = 100, bool threadSafe = true)
        {
            if (threadSafe)
            {
                myLock = new FeatureLock();
                take = () =>
                {
                    using (myLock.Lock())
                    {
                        if (stack.Count > 0) return stack.Pop();                        
                        else return create();
                    }
                };

                ret = obj =>
                {
                    using (myLock.Lock())
                    {
                        if (stack.Count < maxSize)
                        {
                            reset?.Invoke(obj);
                            stack.Push(obj);
                        }
                    }
                };
            }
            else if (reset != null)
            {
                take = () =>
                {
                    if (stack.Count > 0) return stack.Pop();
                    else return create();
                };

                ret = obj =>
                {
                    if (stack.Count < maxSize)
                    {
                        reset?.Invoke(obj);
                        stack.Push(obj);
                    }
                };
            }
            else
            {
                take = () =>
                {
                    if (stack.Count > 0) return stack.Pop();
                    else return create();
                };

                ret = obj =>
                {
                    if (stack.Count < maxSize)
                    {                        
                        stack.Push(obj);
                    }
                };
            }
        }

        public T Take() => take();

        public void Return(T obj) => ret(obj);
    }
}
