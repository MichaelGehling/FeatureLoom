using FeatureLoom.Helpers;
using FeatureLoom.Logging;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Playground
{

    public partial class LoopJsonSerializer
    {
        /*
        private Dictionary<Type, Action<object, JsonUTF8StreamWriter>> dictionaryKeyAsPropertySerializer = new Dictionary<Type, Action<object, JsonUTF8StreamWriter>>()
        {
            [typeof(string)] = (key, writer) => writer.WritePrimitiveValueAsString((string)key),
            [typeof(bool)] = (key, writer) => writer.WritePrimitiveValueAsString((bool)key),
            [typeof(char)] = (key, writer) => writer.WritePrimitiveValueAsString((char)key),
            [typeof(sbyte)] = (key, writer) => writer.WritePrimitiveValueAsString((sbyte)key),
            [typeof(short)] = (key, writer) => writer.WritePrimitiveValueAsString((short)key),
            [typeof(int)] = (key, writer) => writer.WritePrimitiveValueAsString((int)key),
            [typeof(long)] = (key, writer) => writer.WritePrimitiveValueAsString((long)key),
            [typeof(byte)] = (key, writer) => writer.WritePrimitiveValueAsString((byte)key),
            [typeof(ushort)] = (key, writer) => writer.WritePrimitiveValueAsString((ushort)key),
            [typeof(uint)] = (key, writer) => writer.WritePrimitiveValueAsString((uint)key),
            [typeof(ulong)] = (key, writer) => writer.WritePrimitiveValueAsString((ulong)key),
        };
        */
        public class Settings
        {
            public TypeInfoHandling typeInfoHandling = TypeInfoHandling.AddDeviatingTypeInfo;
            public DataSelection dataSelection = DataSelection.PublicAndPrivateFields_CleanBackingFields;
            public ReferenceCheck referenceCheck = ReferenceCheck.AlwaysReplaceByRef;
            public int bufferSize = -1;
            public bool enumAsString = false;
          /*  public List<string> dictionaryKeyTypesImplyingObjectNotation = new List<string>()
            {
                TypeNameHelper.GetSimplifiedTypeName(typeof(string)),
                TypeNameHelper.GetSimplifiedTypeName(typeof(bool)),
                TypeNameHelper.GetSimplifiedTypeName(typeof(char)),
                TypeNameHelper.GetSimplifiedTypeName(typeof(sbyte)),
                TypeNameHelper.GetSimplifiedTypeName(typeof(short)),
                TypeNameHelper.GetSimplifiedTypeName(typeof(int)),
                TypeNameHelper.GetSimplifiedTypeName(typeof(long)),
                TypeNameHelper.GetSimplifiedTypeName(typeof(byte)),
                TypeNameHelper.GetSimplifiedTypeName(typeof(ushort)),
                TypeNameHelper.GetSimplifiedTypeName(typeof(uint)),
                TypeNameHelper.GetSimplifiedTypeName(typeof(ulong)),
            };

            internal void Apply(LoopJsonSerializer serializer)
            {                
                foreach (string typeName in this.dictionaryKeyTypesImplyingObjectNotation)
                {
                    Type type = TypeNameHelper.GetTypeFromSimplifiedName(typeName);
                    if (type == null)
                    {
                        Log.WARNING($"Type {typeName} cannot be resolved. The type will not imply object notation when used as a dictionary key.");
                        continue;
                    }
                    serializer.dictionaryKeyAsPropertySerializer.TryAdd(type, (key, writer) => writer.WritePrimitiveValueAsString(key.ToString()));
                }
            }
          */
        }

        public enum DataSelection
        {
            PublicAndPrivateFields = 0,
            PublicAndPrivateFields_CleanBackingFields = 1,
            PublicFieldsAndProperties = 2,
        }

        public enum ReferenceCheck
        {
            NoRefCheck = 0,
            OnLoopThrowException = 1,
            OnLoopReplaceByNull = 2,
            OnLoopReplaceByRef = 3,
            AlwaysReplaceByRef = 4
        }

        public enum TypeInfoHandling
        {
            AddNoTypeInfo = 0,
            AddDeviatingTypeInfo = 1,
            AddAllTypeInfo = 2,
        } 
    }
}
