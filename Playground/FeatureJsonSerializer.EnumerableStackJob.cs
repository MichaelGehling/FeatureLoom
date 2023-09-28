using FeatureLoom.Helpers;
using System;
using System.Runtime.CompilerServices;

namespace Playground
{
    public sealed partial class FeatureJsonSerializer
    {
        class EnumerableStackJob : StackJob
        {
            IBox enumeratorBox;
            internal Type collectionType;
            internal int currentIndex;
            internal FeatureJsonSerializer serializer;
            Func<EnumerableStackJob, bool> processor;
            internal bool writeTypeInfo;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal void SetEnumerator<T>(T enumerator)
            {
                if (!(enumeratorBox is Box<T> castedBox)) enumeratorBox = new Box<T>(enumerator);
                else castedBox.value = enumerator;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal Box<T> GetEnumeratorBox<T>()
            {
                return enumeratorBox as Box<T>;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Init(FeatureJsonSerializer serializer, Func<EnumerableStackJob, bool> processor, object collection, bool writeTypeInfo)
            {
                this.processor = processor;
                this.serializer = serializer;
                this.collectionType = collection.GetType();
                this.writeTypeInfo = writeTypeInfo;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public override bool Process()
            {
                return processor.Invoke(this);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public override void Reset()
            {
                enumeratorBox.Clear();
                processor = null;
                currentIndex = 0;
                collectionType = null;
                base.Reset();
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public override byte[] GetCurrentChildItemName()
            {
                return serializer.writer.PrepareCollectionIndexName(currentIndex);
            }
        }
    }

    

}
