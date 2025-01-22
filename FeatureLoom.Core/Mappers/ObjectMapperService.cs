using FeatureLoom.Collections;
using FeatureLoom.Extensions;
using FeatureLoom.Helpers;
using FeatureLoom.MessageFlow;
using FeatureLoom.Synchronization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FeatureLoom.Mappers
{
    public class ObjectMapperService
    {
        MicroLock mappingConvertersLock = new MicroLock();
        Dictionary<Type, IMappingConverter> mappingConverters = new Dictionary<Type, IMappingConverter>();
        List<WeakReference<DynamicMultiMappingConverter>> multiMappingConverters = new List<WeakReference<DynamicMultiMappingConverter>>();

        public void AddConversion<Tin, Tout>(Func<Tin, Tout> convertFunc)
        {
            var newMappingConverter = new MappingConverter<Tin, Tout>(convertFunc);
            using (mappingConvertersLock.Lock())
            {
                LazyList<WeakReference<DynamicMultiMappingConverter>> invalidRefs = new LazyList<WeakReference<DynamicMultiMappingConverter>>();
                if (mappingConverters.TryGetValue(typeof(Tin), out var oldMappingConverter))
                {
                    mappingConverters[typeof(Tin)] = newMappingConverter;                    
                    foreach (var multiMappingConverterRef in multiMappingConverters)
                    {
                        if (multiMappingConverterRef.TryGetTarget(out var multiMappingConverter)) multiMappingConverter.CheckToReplaceConverter(oldMappingConverter, newMappingConverter);
                        else invalidRefs.Add(multiMappingConverterRef);
                    }
                }
                else
                {
                    mappingConverters[typeof(Tin)] = newMappingConverter;
                    foreach (var multiMappingConverterRef in multiMappingConverters)
                    {
                        if (multiMappingConverterRef.TryGetTarget(out var multiMappingConverter))  multiMappingConverter.CheckToAddNewConverter(newMappingConverter);
                        else invalidRefs.Add(multiMappingConverterRef);
                    }
                }

                foreach(var invalidRef in invalidRefs)
                {
                    multiMappingConverters.Remove(invalidRef);
                }
            }
        }

        public void RemoveConversion(Type conversionInputType)
        {
            using (mappingConvertersLock.Lock())
            {
                LazyList<WeakReference<DynamicMultiMappingConverter>> invalidRefs = new LazyList<WeakReference<DynamicMultiMappingConverter>>();
                if (mappingConverters.TryGetValue(conversionInputType, out var oldMappingConverter))
                {
                    mappingConverters.Remove(conversionInputType);
                    foreach (var multiMappingConverterRef in multiMappingConverters)
                    {
                        if (multiMappingConverterRef.TryGetTarget(out var multiMappingConverter)) multiMappingConverter.CheckToRemoveConverter(oldMappingConverter);
                        else invalidRefs.Add(multiMappingConverterRef);
                    }
                }

                foreach (var invalidRef in invalidRefs)
                {
                    multiMappingConverters.Remove(invalidRef);
                }
            }
        }

        public IMessageFlowConnection CreateMultiMappingConverter(bool forwardUnmappedMessages = true, params Type[] restrictToInputTypes)
        {
            using (mappingConvertersLock.Lock())
            {
                IMappingConverter[] converters;
                if (restrictToInputTypes.EmptyOrNull()) converters = mappingConverters.Values.ToArray();
                else converters = mappingConverters.Values.Where(c => restrictToInputTypes.Contains(c.InType)).ToArray();
                DynamicMultiMappingConverter newMultiConverter = new DynamicMultiMappingConverter(restrictToInputTypes, forwardUnmappedMessages, converters);
                var newMultiConverterRef = new WeakReference<DynamicMultiMappingConverter>(newMultiConverter);
                this.multiMappingConverters.Add(newMultiConverterRef);
                return newMultiConverter;
            }
        }

        interface IMappingConverter
        {
            Type InType { get; }
            Type OutType { get; }
            bool TryConvertAndForwardMessage<T>(T message, SourceHelper sink);
            bool TryConvertAndForwardMessage<T>(in T message, SourceHelper sink);
            Task<bool> TryConvertAndForwardMessageAsync<T>(T message, SourceHelper sink);
        }

        class MappingConverter<Tin,Tout> : IMappingConverter
        {
            public Type InType => typeof(Tin);
            public Type OutType => typeof(Tout);
            Func<Tin, Tout> convertFunc;

            public MappingConverter(Func<Tin, Tout> convertFunc)
            {
                this.convertFunc = convertFunc;
            }

            public bool TryConvertAndForwardMessage<T>(T message, SourceHelper sink)
            {
                if (!(message is Tin convertable)) return false;
                
                Tout converted = convertFunc(convertable);
                sink.Forward(converted);
                return true;                
            }

            public bool TryConvertAndForwardMessage<T>(in T message, SourceHelper sink)
            {
                if (!(message is Tin convertable)) return false;

                Tout converted = convertFunc(convertable);
                sink.Forward(in converted);
                return true;
            }

            public async Task<bool> TryConvertAndForwardMessageAsync<T>(T message, SourceHelper sink)
            {
                if (!(message is Tin convertable)) return false;

                Tout converted = convertFunc(convertable);
                await sink.ForwardAsync(converted).ConfigureAwait(false);
                return true;
            }
        }

        class DynamicMultiMappingConverter : IMessageFlowConnection, IMessageSource, IMessageSink
        {
            Type[] inputTypes;
            bool forwardUnmappedMessages;
            IMappingConverter[] converters;
            SourceHelper sourceHelper = new SourceHelper();

            internal DynamicMultiMappingConverter(Type[] inputTypes, bool forwardUnmappedMessages, IMappingConverter[] converters)
            {
                this.inputTypes = inputTypes;
                this.forwardUnmappedMessages = forwardUnmappedMessages;
                this.converters = converters;
            }

            internal void CheckToAddNewConverter(IMappingConverter newConverter)
            {                
                if (!inputTypes.EmptyOrNull() && !inputTypes.Contains(newConverter.InType)) return;

                // Will be locked by ObjectMapper's mappingConvertersLock
                IMappingConverter[] newConverters = new IMappingConverter[converters.Length + 1];
                newConverters[0] = newConverter;
                converters.CopyTo(newConverters, 1);
                converters = newConverters;
            }

            internal void CheckToRemoveConverter(IMappingConverter oldConverter)
            {
                // Will be locked by ObjectMapper's mappingConvertersLock
                int index = converters.IndexOf(oldConverter);
                if (index == -1) return;
                var newConverters = converters.RemoveIndexFromCopy(index);
                converters = newConverters;
            }

            internal void CheckToReplaceConverter(IMappingConverter oldConverter, IMappingConverter newConverter)
            {
                // Will be locked by ObjectMapper's mappingConvertersLock
                int index = converters.IndexOf(oldConverter);
                if (index == -1)
                {
                    CheckToAddNewConverter(newConverter);
                    return;
                }
                converters[index] = newConverter;
            }

            public void Post<M>(in M message)
            {
                var currentConverters = converters;
                for(int i=0; i < currentConverters.Length; i++)
                {
                    if (currentConverters[i].TryConvertAndForwardMessage(in message, sourceHelper)) return;
                }
                if (forwardUnmappedMessages) sourceHelper.Forward(in message);
            }

            public void Post<M>(M message)
            {
                var currentConverters = converters;
                for (int i = 0; i < currentConverters.Length; i++)
                {
                    if (currentConverters[i].TryConvertAndForwardMessage(message, sourceHelper)) return;
                }
                if (forwardUnmappedMessages) sourceHelper.Forward(message);
            }

            public async Task PostAsync<M>(M message)
            {
                var currentConverters = converters;
                for (int i = 0; i < currentConverters.Length; i++)
                {
                    if (await currentConverters[i].TryConvertAndForwardMessageAsync(message, sourceHelper).ConfigureAwait(false)) return;
                }
                if (forwardUnmappedMessages) await sourceHelper.ForwardAsync(message).ConfigureAwait(false);
            }

            public int CountConnectedSinks => ((IMessageSource)sourceHelper).CountConnectedSinks;

            public void ConnectTo(IMessageSink sink, bool weakReference = false)
            {
                ((IMessageSource)sourceHelper).ConnectTo(sink, weakReference);
            }

            public IMessageSource ConnectTo(IMessageFlowConnection sink, bool weakReference = false)
            {
                return ((IMessageSource)sourceHelper).ConnectTo(sink, weakReference);
            }

            public void DisconnectAll()
            {
                ((IMessageSource)sourceHelper).DisconnectAll();
            }

            public void DisconnectFrom(IMessageSink sink)
            {
                ((IMessageSource)sourceHelper).DisconnectFrom(sink);
            }

            public IMessageSink[] GetConnectedSinks()
            {
                return ((IMessageSource)sourceHelper).GetConnectedSinks();
            }            
        }
    }

    

}
