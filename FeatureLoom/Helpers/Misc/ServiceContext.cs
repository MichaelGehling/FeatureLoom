using FeatureLoom.Helpers.Synchronization;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace FeatureLoom.Helpers.Misc
{

    public abstract class ServiceContext
    {
        static List<ServiceContext> contexts = new List<ServiceContext>();
        static FeatureLock contextsLock = new FeatureLock();
        static bool noContextSeperationPolicy = false;

        /// <summary>
        /// If set to true, it will remove a small performance overhead for looking up service context.
        /// This can make sense if an application is performance critical and uses many service calls.
        /// But then, each service will stick to a single service context instance,
        /// even if UseCopyOfContext() or UseNewContext() is called. 
        /// (Default is NoContextSeperationPolicy=false)
        /// </summary>
        static public bool NoContextSeperationPolicy
        {
            get => noContextSeperationPolicy;
            set => noContextSeperationPolicy = value;
        }

        public static void UseCopyOfContexts()
        {
            if(NoContextSeperationPolicy) return;

            using(contextsLock.Lock())
            {
                foreach(var context in contexts)
                {
                    context.UseCopy();
                }
            }
        }

        public static void UseNewContexts()
        {
            if(NoContextSeperationPolicy) return;

            using(contextsLock.Lock())
            {
                foreach(var context in contexts)
                {
                    context.UseNew();
                }
            }
        }

        protected static void Register(ServiceContext context)
        {
            using(contextsLock.Lock())
            {
                contexts.Add(context);
            }
        }

        abstract public void UseCopy();
        abstract public void UseNew();
    }

    public class ServiceContext<T> : ServiceContext where T : class, IServiceContextData, new()
    {
        T defaultContextData;
        AsyncLocal<T> contextData = new AsyncLocal<T>();

        public ServiceContext()
        {
            defaultContextData = new T();
            contextData.Value = defaultContextData;
            Register(this);
        }

        public T Data
        {
            get
            {
                if(NoContextSeperationPolicy) return defaultContextData;
                else
                {
                    if(contextData.Value == null) contextData.Value = defaultContextData;
                    return contextData.Value;
                }
            }
        }

        public override void UseCopy()
        {
            if(NoContextSeperationPolicy) return;

            if(contextData.Value == null) contextData.Value = defaultContextData.Copy() as T;
            else contextData.Value = contextData.Value.Copy() as T;
        }

        public override void UseNew()
        {
            if(NoContextSeperationPolicy) return;

            contextData.Value = new T();            
        }
    }

    public interface IServiceContextData
    {
        IServiceContextData Copy();
    }
}
