using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using FeatureLoom.Extensions;

namespace FeatureLoom.Serialization
{
    public sealed partial class FeatureJsonSerializer
    {

        private void CreateComplexItemHandler(CachedTypeHandler typeHandler, Type itemType)
        {
            MethodInfo createMethod = typeof(FeatureJsonSerializer).GetMethod(nameof(CreateTypedComplexItemHandler), BindingFlags.NonPublic | BindingFlags.Instance);
            MethodInfo genericCreateMethod = createMethod.MakeGenericMethod(itemType);
            genericCreateMethod.Invoke(this, new object[] { typeHandler, itemType});
        }

        private void CreateTypedComplexItemHandler<T>(CachedTypeHandler typeHandler, Type itemType)
        {
            var memberInfos = new List<MemberInfo>();
            if (settings.dataSelection == DataSelection.PublicFieldsAndProperties)
            {
                memberInfos.AddRange(itemType.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(prop => prop.GetMethod != null));
                memberInfos.AddRange(itemType.GetFields(BindingFlags.Public | BindingFlags.Instance));                
            }
            else
            {
                memberInfos.AddRange(itemType.GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance));
                Type t = itemType.BaseType;
                while (t != null)
                {
                    memberInfos.AddRange(t.GetFields(BindingFlags.NonPublic | BindingFlags.Instance).Where(baseField => !memberInfos.Any(field => field.Name == baseField.Name)));
                    t = t.BaseType;
                }
            }

            List<Action<T>> fieldValueWriters = new();
            bool allFieldsNoRefs = true;
            foreach (var memberInfo in memberInfos)
            {
                Type fieldType = GetFieldOrPropertyType(memberInfo);
                var fieldTypeHandler = GetCachedTypeHandler(fieldType);
                allFieldsNoRefs &= fieldTypeHandler.NoRefTypes;
                MethodInfo createMethod = typeof(FeatureJsonSerializer).GetMethod(nameof(CreateFieldValueWriter), BindingFlags.NonPublic | BindingFlags.Instance);
                MethodInfo genericCreateMethod = createMethod.MakeGenericMethod(itemType, fieldType);
                Action<T> writer = (Action<T>)genericCreateMethod.Invoke(this, new object[] { fieldTypeHandler, memberInfo });
                fieldValueWriters.Add(writer);
            }

            var fieldValueWritersArray = fieldValueWriters.ToArray();
            typeHandler.SetItemHandler_Object(fieldValueWritersArray, allFieldsNoRefs);
        }

        private Type GetFieldOrPropertyType(MemberInfo fieldOrPropertyInfo)
        {
            if (fieldOrPropertyInfo is FieldInfo fieldInfo) return fieldInfo.FieldType;
            else if (fieldOrPropertyInfo is PropertyInfo propertyInfo) return propertyInfo.PropertyType;
            throw new Exception("Not a FieldType or PropertyType");
        }

        private Action<T> CreateFieldValueWriter<T, V>(CachedTypeHandler fieldTypeHandler, MemberInfo memberInfo)
        {
            string fieldName = memberInfo.Name;
            if (settings.dataSelection == DataSelection.PublicAndPrivateFields_CleanBackingFields &&
                memberInfo.Name.StartsWith('<') &&
                memberInfo.Name.EndsWith(">k__BackingField"))
            {
                fieldName = fieldName.Substring("<", ">");
            }
            var fieldNameAndColonBytes = writer.PrepareFieldNameBytes(fieldName);
            var fieldNameBytes = new ArraySegment<byte>(JsonUTF8StreamWriter.PreparePrimitiveToBytes(fieldName));

            
            Type itemType = typeof(T);
            var parameter = Expression.Parameter(itemType, "param");

            var fieldAccess = memberInfo is FieldInfo field ? Expression.Field(parameter, field) :
                              memberInfo is PropertyInfo property ? Expression.Property(parameter, property) : null;
            var lambda = Expression.Lambda<Func<T, V>>(fieldAccess, parameter);
            var getValue = lambda.Compile();
            
            Type expectedValueType = typeof(V);

            if (writer.TryPreparePrimitiveWriteDelegate<V>(out var primitiveWriteDelegate))
            {
                return (parentItem) =>
                {
                    writer.WriteToBuffer(fieldNameAndColonBytes);
                    V value = getValue(parentItem);
                    primitiveWriteDelegate(value);
                };
            }
            else if (fieldTypeHandler.NoRefTypes)
            {
                return (parentItem) =>
                {
                    writer.WriteToBuffer(fieldNameAndColonBytes);
                    V value = getValue(parentItem);
                    fieldTypeHandler.HandleItem(value, default);
                };
            }
            else
            {
                return (parentItem) =>
                {
                    writer.WriteToBuffer(fieldNameAndColonBytes);
                    V value = getValue(parentItem);
                    if (value == null)
                    {
                        writer.WriteNullValue();
                    }
                    else
                    {
                        Type valueType = value.GetType();
                        CachedTypeHandler actualHandler = fieldTypeHandler;
                        if (valueType != expectedValueType) actualHandler = GetCachedTypeHandler(valueType);
                        
                        actualHandler.HandleItem(value, fieldNameBytes);                        
                    }
                };
            }
        }

    }

}
