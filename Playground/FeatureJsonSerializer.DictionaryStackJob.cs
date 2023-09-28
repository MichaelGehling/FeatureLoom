using System;
using System.Collections.Generic;
using FeatureLoom.Helpers;
using FeatureLoom.Extensions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Playground
{
    public sealed partial class FeatureJsonSerializer
    {
        class DictionaryStackJob : StackJob
        {
            internal Type dictType;
            internal bool firstElement = true;
            internal byte[] currentFieldName;
            Func<DictionaryStackJob, bool> processor;
            IBox enumeratorBox;

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
            public void Init(Func<DictionaryStackJob, bool> processor, Type dictType)
            {
                this.processor = processor;
                this.dictType = dictType;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public override bool Process()
            {
                return processor.Invoke(this);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public override void Reset()
            {
                firstElement = true;
                enumeratorBox.Clear();
                processor = null;
                dictType = null;
                base.Reset();
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public override byte[] GetCurrentChildItemName()
            {
                return currentFieldName;
            }
        }

        private bool TryCreateDictionaryItemHandler(CachedTypeHandler typeHandler, Type itemType)
        {
            if (!itemType.TryGetTypeParamsOfGenericInterface(typeof(IDictionary<,>), out Type keyType, out Type valueType)) return false;
            if (!TryGetCachedStringValueWriter(keyType, out CachedStringValueWriter keyWriter)) return false;
            CachedTypeHandler valueHandler = GetCachedTypeHandler(valueType);

            string methodName = itemType.IsOfGenericType(typeof(Dictionary<,>)) ? nameof(CreateDictionaryItemHandler) : nameof(CreateIDictionaryItemHandler);
            MethodInfo createMethod = typeof(FeatureJsonSerializer).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
            MethodInfo genericCreateMethod = createMethod.MakeGenericMethod(itemType, keyType, valueType);
            genericCreateMethod.Invoke(this, new object[] { typeHandler, valueHandler, keyWriter });

            return true;
        }


        /// The code of this method must be the same as for CreateIDictionaryItemHandler. It was duplicated to avoid boxing of the Dictionary enumerator.
        private void CreateDictionaryItemHandler<T, K, V>(CachedTypeHandler typeHandler, CachedTypeHandler valueHandler, CachedStringValueWriter keyWriter) where T : Dictionary<K, V>
        {
            bool requiresItemNames = settings.RequiresItemNames;

            if (valueHandler.IsPrimitive)
            {
                ItemHandler<T> itemHandler = (dict, expectedType, parentJob) =>
                {
                    Type dictType = dict.GetType();
                    if (TryHandleItemAsRef(dict, parentJob, dictType)) return;

                    writer.OpenObject();
                    var enumerator = dict.GetEnumerator();

                    if (settings.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo ||
                        (settings.typeInfoHandling == TypeInfoHandling.AddDeviatingTypeInfo && expectedType != dictType))
                    {
                        writer.WritePreparedByteString(typeHandler.preparedTypeInfo);
                        writer.WriteComma();
                    }

                    if (enumerator.MoveNext())
                    {
                        KeyValuePair<K, V> pair = enumerator.Current;
                        keyWriter.WriteValueAsString(pair.Key);
                        writer.WriteColon();
                        valueHandler.HandleItem(pair.Value, valueHandler.HandlerType, parentJob);
                    }

                    while (enumerator.MoveNext())
                    {
                        writer.WriteComma();
                        KeyValuePair<K, V> pair = enumerator.Current;
                        keyWriter.WriteValueAsString(pair.Key);
                        writer.WriteColon();
                        valueHandler.HandleItem(pair.Value, valueHandler.HandlerType, parentJob);
                    }

                    writer.CloseObject();
                };
                typeHandler.SetItemHandler(itemHandler, false);
            }
            else
            {
                Func<DictionaryStackJob, bool> processor = job =>
                {
                    int beforeStackSize = jobStack.Count;
                    var enumeratorBox = job.GetEnumeratorBox<Dictionary<K, V>.Enumerator>();

                    if (job.firstElement)
                    {
                        job.firstElement = false;
                        writer.OpenObject();

                        if (settings.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo ||
                            (settings.typeInfoHandling == TypeInfoHandling.AddDeviatingTypeInfo && typeof(T) != job.dictType))
                        {
                            writer.WritePreparedByteString(typeHandler.preparedTypeInfo);
                            writer.WriteComma();
                        }

                        if (enumeratorBox.value.MoveNext())
                        {
                            WriteKeyAndValue(valueHandler, keyWriter, job, enumeratorBox.value.Current);
                            if (jobStack.Count != beforeStackSize) return false;
                        }
                    }

                    while (enumeratorBox.value.MoveNext())
                    {
                        writer.WriteComma();
                        WriteKeyAndValue(valueHandler, keyWriter, job, enumeratorBox.value.Current);
                        if (jobStack.Count != beforeStackSize) return false;
                    }

                    writer.CloseObject();

                    return true;
                };

                ItemHandler<T> itemHandler = (dict, expectedType, parentJob) =>
                {
                    if (dict == null)
                    {
                        writer.WriteNullValue();
                        return;
                    }

                    Type dictType = dict.GetType();
                    if (TryHandleItemAsRef(dict, parentJob, dictType)) return;

                    if (dict.Count == 0)
                    {                        
                        writer.OpenObject();
                        if (settings.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo ||
                            (settings.typeInfoHandling == TypeInfoHandling.AddDeviatingTypeInfo && expectedType != dictType))
                        {
                            writer.WritePreparedByteString(typeHandler.preparedTypeInfo);
                        }
                        writer.CloseObject();
                        return;
                    }
                    var job = dictionaryStackJobRecycler.GetJob(parentJob, requiresItemNames ? CreateItemName(parentJob) : null, dict);
                    job.Init(processor, dict.GetType());
                    job.SetEnumerator(dict.GetEnumerator());                    
                    AddJobToStack(job);
                };
                typeHandler.SetItemHandler(itemHandler, false);
            }

            void WriteKeyAndValue(CachedTypeHandler valueHandler, CachedStringValueWriter keyWriter, DictionaryStackJob job, KeyValuePair<K, V> pair)
            {
                keyWriter.WriteValueAsString(pair.Key);
                writer.WriteColon();

                V value = pair.Value;
                if (value == null)
                {
                    writer.WriteNullValue();
                }
                else
                {
                    Type valueType = value.GetType();
                    CachedTypeHandler actualHandler = valueHandler;
                    if (valueType != typeof(V)) actualHandler = GetCachedTypeHandler(valueType);
                    if (requiresItemNames) job.currentFieldName = writer.PrepareStringToBytes(pair.Key.ToString()); //TODO: Optimize with caching ?
                    actualHandler.HandleItem(pair.Value, typeof(V), job);
                }
            }
        }

        private void CreateIDictionaryItemHandler<T, K, V>(CachedTypeHandler typeHandler, CachedTypeHandler valueHandler, CachedStringValueWriter keyWriter) where T : IDictionary<K, V>
        {
            bool requiresItemNames = settings.RequiresItemNames;

            if (valueHandler.IsPrimitive)
            {
                ItemHandler<T> itemHandler = (dict, expectedType, parentJob) =>
                {
                    Type dictType = dict.GetType();
                    if (TryHandleItemAsRef(dict, parentJob, dictType)) return;

                    writer.OpenObject();
                    var enumerator = dict.GetEnumerator();

                    if (settings.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo ||
                        (settings.typeInfoHandling == TypeInfoHandling.AddDeviatingTypeInfo && expectedType != dictType))
                    {
                        writer.WritePreparedByteString(typeHandler.preparedTypeInfo);
                        writer.WriteComma();
                    }

                    if (enumerator.MoveNext())
                    {
                        KeyValuePair<K, V> pair = enumerator.Current;
                        keyWriter.WriteValueAsString(pair.Key);
                        writer.WriteColon();
                        valueHandler.HandleItem(pair.Value, valueHandler.HandlerType, parentJob);
                    }

                    while (enumerator.MoveNext())
                    {
                        writer.WriteComma();
                        KeyValuePair<K, V> pair = enumerator.Current;
                        keyWriter.WriteValueAsString(pair.Key);
                        writer.WriteColon();
                        valueHandler.HandleItem(pair.Value, valueHandler.HandlerType, parentJob);
                    }

                    writer.CloseObject();
                };
                typeHandler.SetItemHandler(itemHandler, false);
            }
            else
            {
                Func<DictionaryStackJob, bool> processor = job =>
                {
                    int beforeStackSize = jobStack.Count;
                    var enumeratorBox = job.GetEnumeratorBox<IEnumerator<KeyValuePair<K, V>>>();

                    if (job.firstElement)
                    {
                        job.firstElement = false;                        
                        writer.OpenObject();

                        if (settings.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo ||
                            (settings.typeInfoHandling == TypeInfoHandling.AddDeviatingTypeInfo && typeof(T) != job.dictType))
                        {
                            writer.WritePreparedByteString(typeHandler.preparedTypeInfo);
                            writer.WriteComma();
                        }

                        if (enumeratorBox.value.MoveNext())
                        {
                            WriteKeyAndValue(valueHandler, keyWriter, job, enumeratorBox.value.Current);
                            if (jobStack.Count != beforeStackSize) return false;
                        }
                    }

                    while (enumeratorBox.value.MoveNext())
                    {
                        writer.WriteComma();
                        WriteKeyAndValue(valueHandler, keyWriter, job, enumeratorBox.value.Current);
                        if (jobStack.Count != beforeStackSize) return false;
                    }

                    writer.CloseObject();

                    return true;
                };

                ItemHandler<T> itemHandler = (dict, expectedType, parentJob) =>
                {
                    if (dict == null)
                    {
                        writer.WriteNullValue();
                        return;
                    }
                    
                    Type dictType = dict.GetType();
                    if (TryHandleItemAsRef(dict, parentJob, dictType)) return;

                    if (dict.Count == 0)
                    {
                        writer.OpenObject();
                        if (settings.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo ||
                            (settings.typeInfoHandling == TypeInfoHandling.AddDeviatingTypeInfo && expectedType != dictType))
                        {
                            writer.WritePreparedByteString(typeHandler.preparedTypeInfo);
                        }
                        writer.CloseObject();
                        return;
                    }
                    var job = dictionaryStackJobRecycler.GetJob(parentJob, requiresItemNames ? CreateItemName(parentJob) : null, dict);
                    job.Init(processor, dict.GetType());
                    job.SetEnumerator(dict.GetEnumerator());
                    AddJobToStack(job);
                };
                typeHandler.SetItemHandler(itemHandler, false);
            }

            void WriteKeyAndValue(CachedTypeHandler valueHandler, CachedStringValueWriter keyWriter, DictionaryStackJob job, KeyValuePair<K, V> pair)
            {
                keyWriter.WriteValueAsString(pair.Key);
                writer.WriteColon();

                V value = pair.Value;
                if (value == null)
                {
                    writer.WriteNullValue();
                }
                else
                {
                    Type valueType = value.GetType();
                    CachedTypeHandler actualHandler = valueHandler;
                    if (valueType != typeof(V)) actualHandler = GetCachedTypeHandler(valueType);
                    if (requiresItemNames) job.currentFieldName = writer.PrepareStringToBytes(pair.Key.ToString()); //TODO: Optimize
                    actualHandler.HandleItem(pair.Value, typeof(V), job);
                }
            }
        }

    }

}
