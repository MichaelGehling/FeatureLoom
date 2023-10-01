using System;
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
        class ComplexStackJob : StackJob
        {
            internal object complexItem;
            internal int currentIndex = 0;
            internal byte[] currentFieldName;
            internal Func<ComplexStackJob, bool> processor;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Init(Func<ComplexStackJob, bool> processor, object complexItem)
            {
                this.processor = processor;
                this.complexItem = complexItem;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public override byte[] GetCurrentChildItemName()
            {
                return currentFieldName;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public override bool Process()
            {
                return processor.Invoke(this);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public override void Reset()
            {
                complexItem = null;
                currentIndex = 0;
                processor = null;
                base.Reset();
            }
        }

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

            List<Action<T, ComplexStackJob>> fieldValueWriters = new();
            bool allFieldsPrimitive = true;
            foreach (var memberInfo in memberInfos)
            {
                Type fieldType = GetFieldOrPropertyType(memberInfo);
                var fieldTypeHandler = GetCachedTypeHandler(fieldType);
                allFieldsPrimitive &= fieldTypeHandler.IsPrimitive;
                MethodInfo createMethod = typeof(FeatureJsonSerializer).GetMethod(nameof(CreateFieldValueWriter), BindingFlags.NonPublic | BindingFlags.Instance);
                MethodInfo genericCreateMethod = createMethod.MakeGenericMethod(itemType, fieldType);
                Action<T, ComplexStackJob> writer = (Action<T, ComplexStackJob>)genericCreateMethod.Invoke(this, new object[] { fieldTypeHandler, memberInfo });
                fieldValueWriters.Add(writer);
            }

            if (fieldValueWriters.Count == 0)
            {
                ItemHandler<T> itemHandler = (complexItem, expectedType, parentJob) =>
                {
                    if (complexItem == null)
                    {
                        writer.WriteNullValue();
                        return;
                    }

                    Type complexType = complexItem.GetType();
                    if (TryHandleItemAsRef(complexItem, parentJob, complexType)) return;
                    writer.OpenObject();
                    if (settings.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo ||
                        (settings.typeInfoHandling == TypeInfoHandling.AddDeviatingTypeInfo && expectedType != complexType))
                    {
                        writer.WritePreparedByteString(typeHandler.preparedTypeInfo);                        
                    }
                    writer.CloseObject();
                };
                bool isPrimitive = !itemType.IsClass || itemType.IsSealed;
                typeHandler.SetItemHandler(itemHandler, isPrimitive);
            }
            else if (allFieldsPrimitive)
            {
                ItemHandler<T> itemHandler = (complexItem, expectedType, parentJob) =>
                {
                    if (complexItem == null)
                    {
                        writer.WriteNullValue();
                        return;
                    }

                    Type complexType = complexItem.GetType();
                    if (TryHandleItemAsRef(complexItem, parentJob, complexType)) return;

                    writer.OpenObject();

                    if (settings.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo ||
                        (settings.typeInfoHandling == TypeInfoHandling.AddDeviatingTypeInfo && expectedType != complexType))
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
                bool isPrimitive = !itemType.IsClass;
                typeHandler.SetItemHandler(itemHandler, isPrimitive);
            }
            else
            {
                Func<ComplexStackJob, bool> processor = job =>
                {
                    int beforeStackSize = jobStack.Count;

                    T complexItem = (T)job.complexItem;
                    Type complexItemType = complexItem.GetType();

                    if (job.currentIndex == 0)
                    {
                        writer.OpenObject();

                        if (settings.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo ||
                            (settings.typeInfoHandling == TypeInfoHandling.AddDeviatingTypeInfo && typeof(T) != complexItemType))
                        {
                            writer.WritePreparedByteString(typeHandler.preparedTypeInfo);
                            writer.WriteComma();
                        }

                        fieldValueWriters[job.currentIndex].Invoke(complexItem, job);

                        job.currentIndex++;
                        if (jobStack.Count != beforeStackSize) return false;
                    }

                    while (job.currentIndex < fieldValueWriters.Count)
                    {
                        writer.WriteComma();
                        fieldValueWriters[job.currentIndex].Invoke(complexItem, job);

                        job.currentIndex++;
                        if (jobStack.Count != beforeStackSize) return false;
                    }

                    writer.CloseObject();

                    return true;
                };

                bool requiresItemNames = settings.RequiresItemNames;

                ItemHandler <T> itemHandler = (complexItem, expectedType, parentJob) =>
                {
                    if (complexItem == null)
                    {
                        writer.WriteNullValue();
                        return;
                    }

                    Type complexItemType = complexItem.GetType();
                    if (TryHandleItemAsRef(complexItem, parentJob, complexItemType)) return;

                    var job = complexStackJobRecycler.GetJob(parentJob, requiresItemNames ? CreateItemName(parentJob) : null, complexItem);
                    job.Init(processor, complexItem);
                    AddJobToStack(job);
                };
                typeHandler.SetItemHandler(itemHandler, false);
            }
        }

        private Type GetFieldOrPropertyType(MemberInfo fieldOrPropertyInfo)
        {
            if (fieldOrPropertyInfo is FieldInfo fieldInfo) return fieldInfo.FieldType;
            else if (fieldOrPropertyInfo is PropertyInfo propertyInfo) return propertyInfo.PropertyType;
            throw new Exception("Not a FieldType or PropertyType");
        }

        private Action<T, ComplexStackJob> CreateFieldValueWriter<T, V>(CachedTypeHandler fieldTypeHandler, MemberInfo memberInfo)
        {
            string fieldName = memberInfo.Name;
            if (settings.dataSelection == DataSelection.PublicAndPrivateFields_CleanBackingFields &&
                memberInfo.Name.StartsWith('<') &&
                memberInfo.Name.EndsWith(">k__BackingField"))
            {
                fieldName = fieldName.Substring("<", ">");
            }
            var fieldNameAndColonBytes = writer.PrepareFieldNameBytes(fieldName);
            var fieldNameBytes = writer.PreparePrimitiveToBytes(fieldName);

            Type itemType = typeof(T);
            var parameter = Expression.Parameter(typeof(object));
            var castedParameter = Expression.Convert(parameter, itemType);
            var fieldAccess = memberInfo is FieldInfo field ? Expression.Field(castedParameter, field) : memberInfo is PropertyInfo property ? Expression.Property(castedParameter, property) : default;
            var lambda = Expression.Lambda<Func<T, V>>(fieldAccess, parameter);
            var getValue = lambda.Compile();

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
                return (parentItem, parentJob) =>
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
                        if (valueType != typeof(V)) actualHandler = GetCachedTypeHandler(valueType);
                        parentJob.currentFieldName = fieldNameBytes;
                        fieldTypeHandler.HandleItem(value, typeof(V), parentJob);
                    }
                };
            }
        }

    }

}
