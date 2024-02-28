﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using FeatureLoom.Extensions;
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

            List<Action<T, ItemInfo>> fieldValueWriters = new();
            bool allFieldsPrimitive = true;
            foreach (var memberInfo in memberInfos)
            {
                Type fieldType = GetFieldOrPropertyType(memberInfo);
                var fieldTypeHandler = GetCachedTypeHandler(fieldType);
                allFieldsPrimitive &= fieldTypeHandler.IsPrimitive;
                MethodInfo createMethod = typeof(FeatureJsonSerializer).GetMethod(nameof(CreateFieldValueWriter), BindingFlags.NonPublic | BindingFlags.Instance);
                MethodInfo genericCreateMethod = createMethod.MakeGenericMethod(itemType, fieldType);
                Action<T, ItemInfo> writer = (Action<T, ItemInfo>)genericCreateMethod.Invoke(this, new object[] { fieldTypeHandler, memberInfo });
                fieldValueWriters.Add(writer);
            }

            bool isPrimitive = allFieldsPrimitive && !itemType.IsClass;
            if (fieldValueWriters.Count == 0)
            {
                if (isPrimitive)
                {
                    PrimitiveItemHandler<T> itemHandler = (complexItem) =>
                    {
                        writer.OpenObject();
                        if (settings.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo) writer.WritePreparedByteString(typeHandler.preparedTypeInfo);
                        writer.CloseObject();
                    };
                    typeHandler.SetItemHandler(itemHandler);
                }
                else
                {
                    ItemHandler<T> itemHandler = (complexItem, expectedType, itemInfo) =>
                    {
                        Type complexType = complexItem.GetType();
                        if (TryHandleItemAsRef(complexItem, itemInfo, complexType)) return;
                        writer.OpenObject();
                        if (TypeInfoRequired(complexType, expectedType)) writer.WritePreparedByteString(typeHandler.preparedTypeInfo);
                        writer.CloseObject();
                    };
                    typeHandler.SetItemHandler(itemHandler);
                }
            }
            else
            {
                if (isPrimitive)
                {
                    PrimitiveItemHandler<T> itemHandler = (complexItem) =>
                    {
                        writer.OpenObject();

                        if (settings.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo)
                        {
                            writer.WritePreparedByteString(typeHandler.preparedTypeInfo);
                            writer.WriteComma();
                        }
                        fieldValueWriters[0].Invoke(complexItem, null);
                        for (int i = 1; i < fieldValueWriters.Count; i++)
                        {
                            writer.WriteComma();
                            fieldValueWriters[i].Invoke(complexItem, null);
                        }

                        writer.CloseObject();
                    };
                    typeHandler.SetItemHandler(itemHandler);
                }
                else
                {
                    ItemHandler<T> itemHandler = (complexItem, expectedType, itemInfo) =>
                    {
                        Type complexType = complexItem.GetType();
                        if (TryHandleItemAsRef(complexItem, itemInfo, complexType)) return;

                        writer.OpenObject();

                        if (TypeInfoRequired(complexType, expectedType))
                        {
                            writer.WritePreparedByteString(typeHandler.preparedTypeInfo);
                            writer.WriteComma();
                        }
                        fieldValueWriters[0].Invoke(complexItem, itemInfo);
                        for (int i = 1; i < fieldValueWriters.Count; i++)
                        {
                            writer.WriteComma();
                            fieldValueWriters[i].Invoke(complexItem, itemInfo);
                        }

                        writer.CloseObject();
                    };
                    typeHandler.SetItemHandler(itemHandler);
                }
            }
        }

        private Type GetFieldOrPropertyType(MemberInfo fieldOrPropertyInfo)
        {
            if (fieldOrPropertyInfo is FieldInfo fieldInfo) return fieldInfo.FieldType;
            else if (fieldOrPropertyInfo is PropertyInfo propertyInfo) return propertyInfo.PropertyType;
            throw new Exception("Not a FieldType or PropertyType");
        }

        private Action<T, ItemInfo> CreateFieldValueWriter<T, V>(CachedTypeHandler fieldTypeHandler, MemberInfo memberInfo)
        {
            string fieldName = memberInfo.Name;
            if (settings.dataSelection == DataSelection.PublicAndPrivateFields_CleanBackingFields &&
                memberInfo.Name.StartsWith('<') &&
                memberInfo.Name.EndsWith(">k__BackingField"))
            {
                fieldName = fieldName.Substring("<", ">");
            }
            var fieldNameAndColonBytes = writer.PrepareFieldNameBytes(fieldName);
            var fieldNameBytes = JsonUTF8StreamWriter.PreparePrimitiveToBytes(fieldName);

            Type itemType = typeof(T);
            var parameter = Expression.Parameter(typeof(object));
            var castedParameter = Expression.Convert(parameter, itemType);
            var fieldAccess = memberInfo is FieldInfo field ? Expression.Field(castedParameter, field) : memberInfo is PropertyInfo property ? Expression.Property(castedParameter, property) : default;
            var lambda = Expression.Lambda<Func<T, V>>(fieldAccess, parameter);
            var getValue = lambda.Compile();

            Type expectedValueType = typeof(V);

            if (fieldTypeHandler.IsPrimitive)
            {
                return (parentItem, _) =>
                {
                    writer.WritePreparedByteString(fieldNameAndColonBytes);
                    V value = getValue(parentItem);
                    fieldTypeHandler.HandlePrimitiveItem(value);
                };
            }
            else
            {
                return (parentItem, parentInfo) =>
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

                        ItemInfo itemInfo = actualHandler.HandlerType.IsClass ? CreateItemInfoForClass(value, parentInfo, fieldNameBytes) : CreateItemInfoForStruct(parentInfo, fieldNameBytes);
                        actualHandler.HandleItem(value, itemInfo, expectedValueType);
                        itemInfoRecycler.ReturnItemInfo(itemInfo);
                    }
                };
            }
        }

    }

}
