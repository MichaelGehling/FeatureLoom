using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using FeatureLoom.Extensions;
using FeatureLoom.Collections;

namespace FeatureLoom.Serialization
{
    public sealed partial class JsonSerializer
    {

        private void CreateComplexItemHandler(CachedTypeWriter typeHandler, Type itemType, bool isNullableStruct)
        {            
            if (isNullableStruct)
            {
                MethodInfo createMethod = typeof(JsonSerializer).GetMethod(nameof(CreateTypedComplexItemHandler_ForNullableStruct), BindingFlags.NonPublic | BindingFlags.Instance);
                MethodInfo genericCreateMethod = createMethod.MakeGenericMethod(itemType);
                genericCreateMethod.Invoke(this, new object[] { typeHandler, itemType });
            }
            else
            {
                MethodInfo createMethod = typeof(JsonSerializer).GetMethod(nameof(CreateTypedComplexItemHandler), BindingFlags.NonPublic | BindingFlags.Instance);
                MethodInfo genericCreateMethod = createMethod.MakeGenericMethod(itemType);
                genericCreateMethod.Invoke(this, new object[] { typeHandler, itemType });
            }
            
        }

        private void CreateTypedComplexItemHandler<T>(CachedTypeWriter typeHandler, Type itemType)
        {
            var memberInfos = new List<MemberInfo>();
            if (settings.dataSelection == DataSelection.PublicFieldsAndProperties)
            {
                memberInfos.AddRange(itemType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(prop => prop.GetMethod != null && !prop.IsDefined(typeof(JsonIgnoreAttribute), true)));
                memberInfos.AddRange(itemType.GetFields(BindingFlags.Public | BindingFlags.Instance)
                    .Where(field =>!field.IsDefined(typeof(JsonIgnoreAttribute), true)));

                // Also take private fields and properties with JsonIncludeAttribute
                memberInfos.AddRange(itemType.GetProperties(BindingFlags.NonPublic | BindingFlags.Instance)
                    .Where(prop => prop.GetMethod != null && prop.IsDefined(typeof(JsonIncludeAttribute), true)));
                memberInfos.AddRange(itemType.GetFields(BindingFlags.NonPublic | BindingFlags.Instance)
                    .Where(field => field.IsDefined(typeof(JsonIncludeAttribute), true)));
                Type t = itemType.BaseType;
                while (t != null)
                {
                    memberInfos.AddRange(t.GetProperties(BindingFlags.NonPublic | BindingFlags.Instance)
                        .Where(baseProp => baseProp.GetMethod != null && baseProp.IsDefined(typeof(JsonIncludeAttribute), true) && !memberInfos.Any(field => field.Name == baseProp.Name)));
                    memberInfos.AddRange(t.GetFields(BindingFlags.NonPublic | BindingFlags.Instance)
                        .Where(baseField => baseField.IsDefined(typeof(JsonIncludeAttribute), true) && !memberInfos.Any(field => field.Name == baseField.Name)));                    
                    t = t.BaseType;
                }
                memberInfos = memberInfos.Where(member => !member.Name.StartsWith("<") || !member.Name.EndsWith(">k__BackingField")).ToList();


            }
            else
            {
                memberInfos.AddRange(itemType.GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)
                    .Where(field => !field.IsDefined(typeof(JsonIgnoreAttribute), true)));
                Type t = itemType.BaseType;
                while (t != null)
                {
                    memberInfos.AddRange(t.GetFields(BindingFlags.NonPublic | BindingFlags.Instance)
                        .Where(baseField => !baseField.IsDefined(typeof(JsonIgnoreAttribute), true) && !memberInfos.Any(field => field.Name == baseField.Name)));
                    t = t.BaseType;
                }

                // Also take public and private properties with JsonIncludeAttribute
                memberInfos.AddRange(itemType.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    .Where(prop => prop.GetMethod != null && prop.IsDefined(typeof(JsonIncludeAttribute), true)));
                t = itemType.BaseType;
                while (t != null)
                {
                    memberInfos.AddRange(t.GetProperties(BindingFlags.NonPublic | BindingFlags.Instance)
                        .Where(baseProp => baseProp.GetMethod != null && 
                                           baseProp.IsDefined(typeof(JsonIncludeAttribute), true) && 
                                           !memberInfos.Any(field => field.Name == baseProp.Name)));
                    t = t.BaseType;
                }

                if (settings.dataSelection == DataSelection.PublicAndPrivateFields_RemoveBackingFields)
                {
                    memberInfos = memberInfos.Where(member => !member.Name.StartsWith("<") || !member.Name.EndsWith(">k__BackingField")).ToList();
                }
                else
                {
                    var backingFieldNames = memberInfos.Select<MemberInfo, (string cleanedName, string backingName)?> (m => m.Name.TryExtract("<{Name}>k__BackingField", out string cleanedName) ? (cleanedName, m.Name) : null)
                                                        .Where(name => name != null);
                    if (backingFieldNames.Any()) 
                    {
                        // remove properties whose backing fields are already available
                        memberInfos = memberInfos.Where(m => !backingFieldNames.Any(names => names.Value.cleanedName == m.Name)).ToList();

                        // remove backing fields whose properties have the JsonIgnoreAttribute
                        var ignoredProperties = new List<MemberInfo>();
                        ignoredProperties.AddRange(itemType.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                            .Where(prop => prop.GetMethod != null && prop.IsDefined(typeof(JsonIgnoreAttribute), true)));
                        t = itemType.BaseType;
                        while (t != null)
                        {
                            ignoredProperties.AddRange(t.GetProperties(BindingFlags.NonPublic | BindingFlags.Instance)
                                .Where(baseProp => baseProp.GetMethod != null &&
                                                   baseProp.IsDefined(typeof(JsonIgnoreAttribute), true)));
                            t = t.BaseType;
                        }
                        memberInfos = memberInfos.Where(m => !backingFieldNames.TryFindFirst(names => m.Name == names.Value.backingName, out var names) ||
                                                             !ignoredProperties.Any(prop => prop.Name == names.Value.cleanedName)).ToList();
                    }
                }
            }

            List<Action<T>> fieldValueWriters = new();
            bool allFieldsNoRefs = true;
            bool mergeCommas = !settings.indent;
            foreach (var memberInfo in memberInfos)
            {
                Type fieldType = GetFieldOrPropertyType(memberInfo);
                var fieldTypeHandler = GetCachedTypeWriter(fieldType);
                allFieldsNoRefs &= fieldTypeHandler.NoRefTypes;
                MethodInfo createMethod = typeof(JsonSerializer).GetMethod(nameof(CreateFieldValueWriter), BindingFlags.NonPublic | BindingFlags.Instance);
                MethodInfo genericCreateMethod = createMethod.MakeGenericMethod(itemType, fieldType);
                bool withLeadingComma = mergeCommas && fieldValueWriters.Count > 0;
                Action<T> writer = (Action<T>)genericCreateMethod.Invoke(this, new object[] { fieldTypeHandler, memberInfo, withLeadingComma });
                fieldValueWriters.Add(writer);
            }

            var fieldValueWritersArray = fieldValueWriters.ToArray();
            typeHandler.SetItemHandler_Object(fieldValueWritersArray, allFieldsNoRefs, mergeCommas);
        }

        private void CreateTypedComplexItemHandler_ForNullableStruct<T>(CachedTypeWriter typeHandler, Type itemType) where T : struct
        {
            var memberInfos = new List<MemberInfo>();
            if (settings.dataSelection == DataSelection.PublicFieldsAndProperties)
            {
                memberInfos.AddRange(itemType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(prop => prop.GetMethod != null && !prop.IsDefined(typeof(JsonIgnoreAttribute), true)));
                memberInfos.AddRange(itemType.GetFields(BindingFlags.Public | BindingFlags.Instance)
                    .Where(field => !field.IsDefined(typeof(JsonIgnoreAttribute), true)));
            }
            else
            {
                memberInfos.AddRange(itemType.GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)
                    .Where(field => !field.IsDefined(typeof(JsonIgnoreAttribute), true)));
                Type t = itemType.BaseType;
                while (t != null)
                {
                    memberInfos.AddRange(t.GetFields(BindingFlags.NonPublic | BindingFlags.Instance)
                        .Where(baseField => !baseField.IsDefined(typeof(JsonIgnoreAttribute), true) && !memberInfos.Any(field => field.Name == baseField.Name)));
                    t = t.BaseType;
                }
            }

            List<Action<T>> fieldValueWriters = new();
            bool allFieldsNoRefs = true;
            bool mergeCommas = !settings.indent;
            foreach (var memberInfo in memberInfos)
            {
                Type fieldType = GetFieldOrPropertyType(memberInfo);
                var fieldTypeHandler = GetCachedTypeWriter(fieldType);
                allFieldsNoRefs &= fieldTypeHandler.NoRefTypes;
                MethodInfo createMethod = typeof(JsonSerializer).GetMethod(nameof(CreateFieldValueWriter), BindingFlags.NonPublic | BindingFlags.Instance);
                MethodInfo genericCreateMethod = createMethod.MakeGenericMethod(itemType, fieldType);
                bool withLeadingComma = mergeCommas && fieldValueWriters.Count > 0;
                Action<T> writer = (Action<T>)genericCreateMethod.Invoke(this, new object[] { fieldTypeHandler, memberInfo, withLeadingComma });
                fieldValueWriters.Add(writer);
            }

            var fieldValueWritersArray = fieldValueWriters.ToArray();
            typeHandler.SetItemHandler_Object_ForNullableStruct(fieldValueWritersArray, allFieldsNoRefs, mergeCommas);
        }

        private Type GetFieldOrPropertyType(MemberInfo fieldOrPropertyInfo)
        {
            if (fieldOrPropertyInfo is FieldInfo fieldInfo) return fieldInfo.FieldType;
            else if (fieldOrPropertyInfo is PropertyInfo propertyInfo) return propertyInfo.PropertyType;
            throw new Exception("Not a FieldType or PropertyType");
        }

        private Action<T> CreateFieldValueWriter<T, V>(CachedTypeWriter fieldTypeHandler, MemberInfo memberInfo, bool withLeadingComma)
        {
            string fieldName = memberInfo.Name;
            if (settings.dataSelection == DataSelection.PublicAndPrivateFields_CleanBackingFields &&
                memberInfo.Name.StartsWith('<') &&
                memberInfo.Name.EndsWith(">k__BackingField"))
            {
                fieldName = fieldName.Substring("<", ">");
            }
            var fieldNameAndColonBytes = writer.PrepareFieldNameBytes(fieldName);
            if (withLeadingComma)
            {
                // Merge the separating comma into the prepared bytes so that comma, field name and
                // colon are emitted with a single buffer copy instead of an extra WriteComma call.
                var withComma = new byte[fieldNameAndColonBytes.Length + 1];
                withComma[0] = (byte)',';
                Array.Copy(fieldNameAndColonBytes, 0, withComma, 1, fieldNameAndColonBytes.Length);
                fieldNameAndColonBytes = withComma;
            }
            var fieldNameBytes = new ByteSegment(JsonUTF8StreamWriter.PreparePrimitiveToBytes(fieldName), true);


            Type itemType = typeof(T);
            var parameter = Expression.Parameter(itemType, "param");

            var fieldAccess = memberInfo is FieldInfo field ? Expression.Field(parameter, field) :
                              memberInfo is PropertyInfo property ? Expression.Property(parameter, property) : null;

            Type expectedValueType = typeof(V);

            var writerConst = Expression.Constant(writer);
            var writeFieldName = Expression.Call(writerConst, JsonUTF8StreamWriter.WritePreparedBytesMethod, Expression.Constant(fieldNameAndColonBytes));

            // Where possible, the field name write and the value write are fused into a single
            // compiled delegate. This removes the nested delegate invocations (getter delegate plus
            // value writer delegate) from the hot path and lets the JIT inline the writer calls.
            if (writer.TryGetPrimitiveWriteMethod<V>(out MethodInfo primitiveWriteMethod))
            {
                var body = Expression.Block(writeFieldName, Expression.Call(writerConst, primitiveWriteMethod, fieldAccess));
                return Expression.Lambda<Action<T>>(body, parameter).Compile();
            }
            else if (!fieldTypeHandler.HandlerType?.IsNullable() ?? false)
            {
                MethodInfo writeItemMethod = typeof(CachedTypeWriter)
                    .GetMethod(nameof(CachedTypeWriter.WriteItem), BindingFlags.Public | BindingFlags.Instance)
                    .MakeGenericMethod(expectedValueType);
                var body = Expression.Block(writeFieldName,
                    Expression.Call(Expression.Constant(fieldTypeHandler), writeItemMethod, fieldAccess, Expression.Default(typeof(ByteSegment))));
                return Expression.Lambda<Action<T>>(body, parameter).Compile();
            }
            else
            {
                var getValue = Expression.Lambda<Func<T, V>>(fieldAccess, parameter).Compile();
                return (parentItem) =>
                {
                    writer.WritePreparedBytes(fieldNameAndColonBytes);
                    V value = getValue(parentItem);
                    if (value == null)
                    {
                        writer.WriteNullValue();
                    }
                    else
                    {
                        Type valueType = value.GetType();
                        CachedTypeWriter actualHandler = fieldTypeHandler;
                        if (valueType != expectedValueType) actualHandler = GetCachedTypeWriter(valueType);

                        actualHandler.WriteItem(value, fieldNameBytes);                        
                    }
                };
            }
        }

    }

}
