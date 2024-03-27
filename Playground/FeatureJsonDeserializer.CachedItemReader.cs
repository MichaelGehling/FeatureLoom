using System;
using FeatureLoom.Extensions;

namespace Playground
{
    public sealed partial class FeatureJsonDeserializer
    {
        sealed class CachedTypeReader
        {
            private FeatureJsonDeserializer deserializer;
            private Delegate itemReader;
            private Func<object> objectItemReader;
            private Type readerType;

            public CachedTypeReader(FeatureJsonDeserializer serializer)
            {
                this.deserializer = serializer;
            }

            public void SetTypeReader<T>(Func<T> typeReader, JsonDataTypeCategory category)
            {
                this.readerType = typeof(T);                                
                bool isNullable = readerType.IsNullable();
                Func<T> temp;
                if (isNullable)
                {
                    temp = () =>
                    {
                        deserializer.SkipWhiteSpaces();
                        if (deserializer.PeekNull()) return default;                            
                        return typeReader.Invoke();
                    };
                }
                else
                {
                    temp = typeReader;
                }
                this.itemReader = temp;
                this.objectItemReader = () => (object)temp.Invoke();
            }

            public T ReadItem<T>()
            {
                Type callType = typeof(T);
                if (callType == this.readerType)
                {
                    Func<T> typedItemReader = (Func<T>)itemReader;
                    return typedItemReader.Invoke();
                }
                else
                {
                    return (T)objectItemReader.Invoke();
                }
            }
        }
    }
}
