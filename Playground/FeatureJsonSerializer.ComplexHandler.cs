﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using FeatureLoom.Extensions;
using FeatureLoom.Helpers;
using Microsoft.AspNetCore.Server.Kestrel.Transport.Abstractions.Internal;

namespace Playground
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
                memberInfos.AddRange(itemType.GetFields(BindingFlags.Public | BindingFlags.Instance));
                memberInfos.AddRange(itemType.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(prop => prop.GetMethod != null));
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

            bool isPrimitive = allFieldsNoRefs && !itemType.IsClass;
            if (fieldValueWriters.Count == 0)
            {
                ItemHandler<T> itemHandler = (complexItem) =>
                {
                    // do nothing
                };

                typeHandler.SetItemHandler_Object(itemHandler, true, true);               
            }
            else
            {
                ItemHandler<T> itemHandler;
                if (allFieldsNoRefs)
                {
                    itemHandler = (complexItem) =>
                    {
                        fieldValueWriters[0].Invoke(complexItem);
                        for (int i = 1; i < fieldValueWriters.Count; i++)
                        {
                            writer.WriteComma();
                            fieldValueWriters[i].Invoke(complexItem);
                        }
                    };
                }
                else
                {
                    itemHandler = (complexItem) =>
                    {
                        fieldValueWriters[0].Invoke(complexItem);
                        for (int i = 1; i < fieldValueWriters.Count; i++)
                        {
                            writer.WriteComma();
                            fieldValueWriters[i].Invoke(complexItem);
                        }
                    };
                }
                typeHandler.SetItemHandler_Object(itemHandler, allFieldsNoRefs, false);
            }
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
            var parameter = Expression.Parameter(typeof(object));
            var castedParameter = Expression.Convert(parameter, itemType);
            var fieldAccess = memberInfo is FieldInfo field ? Expression.Field(castedParameter, field) : memberInfo is PropertyInfo property ? Expression.Property(castedParameter, property) : default;
            var lambda = Expression.Lambda<Func<T, V>>(fieldAccess, parameter);
            var getValue = lambda.Compile();

            Type expectedValueType = typeof(V);

            if (fieldTypeHandler.IsPrimitive)
            {
                return (parentItem) =>
                {
                    writer.WritePreparedByteString(fieldNameAndColonBytes);
                    V value = getValue(parentItem);
                    fieldTypeHandler.HandleItem(value, default);
                };
            }
            else
            {
                return (parentItem) =>
                {
                    writer.WritePreparedByteString(fieldNameAndColonBytes);
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
