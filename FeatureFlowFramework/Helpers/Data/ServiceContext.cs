using FeatureFlowFramework.Helpers.Synchronization;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace FeatureFlowFramework.Helpers.Data
{

    public abstract class ServiceContext
    {
        static List<ServiceContext> contexts = new List<ServiceContext>();
        static FeatureLock contextsLock = new FeatureLock();
        static bool noContextSeperationPolicy = false;
        static public bool NoContextSeperationPolicy
        {
            get => noContextSeperationPolicy;
            set => noContextSeperationPolicy = value;
        }

        public static void UseCopyOfContexts()
        {
            if(NoContextSeperationPolicy) return;

            using(contextsLock.ForWriting())
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

            using(contextsLock.ForWriting())
            {
                foreach(var context in contexts)
                {
                    context.UseNew();
                }
            }
        }

        protected static void Register(ServiceContext context)
        {
            using(contextsLock.ForWriting())
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
