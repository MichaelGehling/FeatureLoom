using FeatureFlowFramework.DataFlows;
using FeatureFlowFramework.Helpers;
using FeatureFlowFramework.Helpers.Data;
using FeatureFlowFramework.Helpers.Extensions;
using FeatureFlowFramework.Services.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;

namespace FeatureFlowFramework.Services.Web
{
    public struct WebMessageWrapper<T> : IMessageWrapper<T>
    {
        public T message;
        public string messageType;

        public WebMessageWrapper(T message, string messageType)
        {
            this.message = message;
            this.messageType = messageType;
        }

        [JsonIgnore]
        public T TypedMessage { get => message; set => message = value; }

        public object Message { get => message; set { if(value is T vT) message = vT; else throw new Exception($"This MessageWrapper can only take objects of type {typeof(T)}!"); } }
    }

    public interface IWebMessageTranslator
    {
        bool TryTranslate(string json, out object message);

        bool TryTranslate<T>(T message, out string json);
    }

    public class DefaultWebMessageTranslator : IWebMessageTranslator
    {
        private readonly Dictionary<string, Func<JObject, object>> nameToFactory;
        private readonly Dictionary<Type, string> typeToName;

        public DefaultWebMessageTranslator(params (string name, Type type, Func<JObject, object> factory)[] nameTypeFactoryTuples)
        {
            this.nameToFactory = new Dictionary<string, Func<JObject, object>>();
            this.typeToName = new Dictionary<Type, string>();
            foreach(var (name, type, factory) in nameTypeFactoryTuples)
            {
                nameToFactory.Add(name, factory);
                typeToName.Add(type, name);
            }
        }

        public DefaultWebMessageTranslator(params (string name, Type type)[] nameToTypeName)
        {
            this.nameToFactory = new Dictionary<string, Func<JObject, object>>();
            this.typeToName = new Dictionary<Type, string>();
            foreach(var (name, type) in nameToTypeName)
            {
                try
                {
                    nameToFactory.Add(name, jObj => jObj.ToObject(type));
                    typeToName.Add(type, name);
                }
                catch(Exception e)
                {
                    Log.ERROR(this, $"Failed creating an factory function! Name:{name} TypeName:{type.Name}", e.ToString());
                }
            }
        }

        public DefaultWebMessageTranslator(params (string name, string typeName)[] nameToTypeName)
        {
            this.nameToFactory = new Dictionary<string, Func<JObject, object>>();
            this.typeToName = new Dictionary<Type, string>();
            foreach(var (name, typeName) in nameToTypeName)
            {
                try
                {
                    Type type = Type.GetType(typeName, true, true);
                    nameToFactory.Add(name, jObj => jObj.ToObject(type, Json.Default_Serializer));
                    typeToName.Add(type, name);
                }
                catch(Exception e)
                {
                    Log.ERROR(this, $"Failed creating an factory function! Name:{name} TypeName:{typeName}", e.ToString());
                }
            }
        }

        public bool TryTranslate(string json, out object message)
        {
            try
            {
                JObject jWrapper = JObject.Parse(json);
                var typeName = jWrapper.Value<string>("messageType");

                if(nameToFactory.TryGetValue(typeName, out Func<JObject, object> createMessage))
                {
                    JObject jMessage = jWrapper.GetValue("message") as JObject;
                    message = createMessage(jMessage);
                    return true;
                }
                else
                {
                    message = default;
                    return false;
                }
            }
            catch(Exception e)
            {
                Log.ERROR(this, "Failed deserializing message!", e.ToString());
                message = default;
                return false;
            }
        }

        public bool TryTranslate<T>(T message, out string json)
        {
            try
            {
                if(typeToName.TryGetValue(message.GetType(), out string typeName))
                {
                    var wrappedMessage = new WebMessageWrapper<T>(message, typeName);
                    json = wrappedMessage.ToJson();
                    return true;
                }
                else
                {
                    Log.WARNING(this, $"TypeName for message {message.GetType()} not available.");
                    json = null;
                    return false;
                }
            }
            catch(Exception e)
            {
                Log.ERROR(this, "Failed serializing message!", e.ToString());
                json = null;
                return false;
            }
        }
    }
}