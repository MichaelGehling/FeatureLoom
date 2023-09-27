using FeatureLoom.Collections;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Xml;

namespace Playground
{
    public sealed partial class FeatureJsonSerializer
    {
        interface IStackJobRecycler
        {
            void RecycleJob(StackJob job);
        }

        class StackJobRecycler<T> : IStackJobRecycler where T : StackJob, new()
        {
            bool postponeRecycling;
            Pool<T> pool = new Pool<T>(() => new T(), job => job.Reset(), 100, false);
            List<T> postponedJobs = new List<T>();
            
            public StackJobRecycler(FeatureJsonSerializer serializer)
            {
                postponeRecycling = serializer.settings.referenceCheck == ReferenceCheck.AlwaysReplaceByRef;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void RecycleJob(StackJob job)
            {
                if (!(job is T castedJob)) return;

                if (postponeRecycling) postponedJobs.Add(castedJob);
                else pool.Return(castedJob);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void RecyclePostponedJobs()
            {
                if (postponedJobs.Count == 0) return;

                for(int i = 0; i < postponedJobs.Count; i++)
                {
                    pool.Return(postponedJobs[i]);
                }
                postponedJobs.Clear();
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public T GetJob(StackJob parentJob, byte[] itemName, object objItem)
            {
                T job = pool.Take();
                job.recycler = this;
                job.parentJob = parentJob;
                job.itemName = itemName;
                job.objItem = objItem;
                return job;
            }
        }

    }
}
