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
            var typeSettings = typeHandler.TypeSettings;
            DataSelection dataSelection = settings.ResolveDataSelection(typeSettings);

            var memberInfos = new List<MemberInfo>();
            if (dataSelection == DataSelection.PublicFieldsAndProperties)
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

                if (dataSelection == DataSelection.PublicAndPrivateFields_RemoveBackingFields)
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

            AddExplicitlyIncludedMembers(itemType, typeSettings, memberInfos, dataSelection);

            List<Action<T>> fieldValueWriters = new();
            bool allFieldsNoRefs = true;
            bool mergeCommas = !settings.indent;
            foreach (var memberInfo in memberInfos)
            {
                var memberSettings = GetMemberSettings(typeSettings, memberInfo);
                if (memberSettings?.member_ignore == true) continue;

                Type fieldType = GetFieldOrPropertyType(memberInfo);
                var fieldTypeHandler = GetCachedTypeWriterForMember(fieldType, memberSettings);
                allFieldsNoRefs &= NoRefTypesIncludingRuntimeTypes(fieldTypeHandler, fieldType);
                MethodInfo createMethod = typeof(JsonSerializer).GetMethod(nameof(CreateFieldValueWriter), BindingFlags.NonPublic | BindingFlags.Instance);
                MethodInfo genericCreateMethod = createMethod.MakeGenericMethod(itemType, fieldType);
                bool withLeadingComma = mergeCommas && fieldValueWriters.Count > 0;
                Action<T> writer = (Action<T>)genericCreateMethod.Invoke(this, new object[] { fieldTypeHandler, memberInfo, withLeadingComma, dataSelection, memberSettings });
                fieldValueWriters.Add(writer);
            }

            var fieldValueWritersArray = fieldValueWriters.ToArray();
            int fieldCount = fieldValueWritersArray.Length;
            var w = writer;

            // When mergeCommas is set, the separating comma is already part of the prepared field
            // name bytes of each field handler, so no extra write is needed per field.
            void WriteFields(T item)
            {
                if (fieldCount == 0) return;
                fieldValueWritersArray[0].Invoke(item);
                if (mergeCommas)
                {
                    for (int i = 1; i < fieldCount; i++) fieldValueWritersArray[i].Invoke(item);
                }
                else
                {
                    for (int i = 1; i < fieldCount; i++)
                    {
                        w.WriteComma();
                        fieldValueWritersArray[i].Invoke(item);
                    }
                }
            }

            typeHandler.SetItemWriter(CreateObjectItemWriter<T>(typeHandler, WriteFields), !allFieldsNoRefs);
        }

        private void CreateTypedComplexItemHandler_ForNullableStruct<T>(CachedTypeWriter typeHandler, Type itemType) where T : struct
        {
            var typeSettings = typeHandler.TypeSettings;
            DataSelection dataSelection = settings.ResolveDataSelection(typeSettings);

            var memberInfos = new List<MemberInfo>();
            if (dataSelection == DataSelection.PublicFieldsAndProperties)
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

            AddExplicitlyIncludedMembers(itemType, typeSettings, memberInfos, dataSelection);

            List<Action<T>> fieldValueWriters = new();
            bool allFieldsNoRefs = true;
            bool mergeCommas = !settings.indent;
            foreach (var memberInfo in memberInfos)
            {
                var memberSettings = GetMemberSettings(typeSettings, memberInfo);
                if (memberSettings?.member_ignore == true) continue;

                Type fieldType = GetFieldOrPropertyType(memberInfo);
                var fieldTypeHandler = GetCachedTypeWriterForMember(fieldType, memberSettings);
                allFieldsNoRefs &= NoRefTypesIncludingRuntimeTypes(fieldTypeHandler, fieldType);
                MethodInfo createMethod = typeof(JsonSerializer).GetMethod(nameof(CreateFieldValueWriter), BindingFlags.NonPublic | BindingFlags.Instance);
                MethodInfo genericCreateMethod = createMethod.MakeGenericMethod(itemType, fieldType);
                bool withLeadingComma = mergeCommas && fieldValueWriters.Count > 0;
                Action<T> writer = (Action<T>)genericCreateMethod.Invoke(this, new object[] { fieldTypeHandler, memberInfo, withLeadingComma, dataSelection, memberSettings });
                fieldValueWriters.Add(writer);
            }

            var fieldValueWritersArray = fieldValueWriters.ToArray();
            int fieldCount = fieldValueWritersArray.Length;
            var w = writer;

            void WriteFields(T? nullableItem)
            {
                if (fieldCount == 0) return;
                T item = nullableItem.Value;
                fieldValueWritersArray[0].Invoke(item);
                if (mergeCommas)
                {
                    for (int i = 1; i < fieldCount; i++) fieldValueWritersArray[i].Invoke(item);
                }
                else
                {
                    for (int i = 1; i < fieldCount; i++)
                    {
                        w.WriteComma();
                        fieldValueWritersArray[i].Invoke(item);
                    }
                }
            }

            typeHandler.SetItemWriter(CreateObjectItemWriter<T?>(typeHandler, WriteFields), !allFieldsNoRefs);
        }

        private Type GetFieldOrPropertyType(MemberInfo fieldOrPropertyInfo)
        {
            if (fieldOrPropertyInfo is FieldInfo fieldInfo) return fieldInfo.FieldType;
            else if (fieldOrPropertyInfo is PropertyInfo propertyInfo) return propertyInfo.PropertyType;
            throw new Exception("Not a FieldType or PropertyType");
        }

        /// <summary>
        /// Looks up the member settings configured for <paramref name="memberInfo"/> on its owning
        /// type. Compiler generated backing fields are also matched under their clean property
        /// name, so a member can be configured by the name the user knows it by.
        /// </summary>
        private BaseTypeWriteSettings GetMemberSettings(BaseTypeWriteSettings typeSettings, MemberInfo memberInfo)
        {
            if (typeSettings == null || typeSettings.memberSettingsDict.Count == 0) return null;

            if (typeSettings.memberSettingsDict.TryGetValue(memberInfo.Name, out var memberSettings)) return memberSettings;

            if (memberInfo.Name.TryExtract("<{Name}>k__BackingField", out string cleanedName) &&
                typeSettings.memberSettingsDict.TryGetValue(cleanedName, out memberSettings)) return memberSettings;

            return null;
        }

        /// <summary>
        /// Re-adds members that the JsonIgnoreAttribute filtering removed, but that were explicitly
        /// configured with SetIgnore(false). An explicit member setting always wins over the
        /// JsonIgnore/JsonInclude attributes, in both directions.
        /// </summary>
        private void AddExplicitlyIncludedMembers(Type itemType, BaseTypeWriteSettings typeSettings, List<MemberInfo> memberInfos, DataSelection dataSelection)
        {
            if (typeSettings == null || typeSettings.memberSettingsDict.Count == 0) return;

            // In these modes an auto property is represented by the property itself, in the others
            // by its compiler generated backing field.
            bool preferProperty = dataSelection == DataSelection.PublicFieldsAndProperties ||
                                  dataSelection == DataSelection.PublicAndPrivateFields_RemoveBackingFields;

            foreach (var entry in typeSettings.memberSettingsDict)
            {
                if (entry.Value.member_ignore != false) continue;
                if (memberInfos.Any(m => m.Name == entry.Key ||
                                         (m.Name.TryExtract("<{Name}>k__BackingField", out string cleaned) && cleaned == entry.Key))) continue;

                MemberInfo member = FindConfiguredMember(itemType, entry.Key, preferProperty);
                if (member != null) memberInfos.Add(member);
            }
        }

        /// <summary>
        /// Finds the member a configuration entry refers to, walking up the type hierarchy and
        /// resolving auto properties to the representation the current data selection uses.
        /// </summary>
        private MemberInfo FindConfiguredMember(Type itemType, string memberName, bool preferProperty)
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            string backingFieldName = $"<{memberName}>k__BackingField";

            for (Type t = itemType; t != null; t = t.BaseType)
            {
                if (!preferProperty)
                {
                    FieldInfo backingField = t.GetField(backingFieldName, flags);
                    if (backingField != null) return backingField;
                }

                FieldInfo field = t.GetField(memberName, flags);
                if (field != null) return field;

                PropertyInfo property = t.GetProperty(memberName, flags);
                if (property?.GetMethod != null) return property;

                if (preferProperty)
                {
                    FieldInfo backingField = t.GetField(backingFieldName, flags);
                    if (backingField != null) return backingField;
                }
            }

            return null;
        }

        private Action<T> CreateFieldValueWriter<T, V>(CachedTypeWriter fieldTypeHandler, MemberInfo memberInfo, bool withLeadingComma, DataSelection dataSelection, BaseTypeWriteSettings memberSettings)
        {
            string fieldName = memberInfo.Name;
            if (dataSelection == DataSelection.PublicAndPrivateFields_CleanBackingFields &&
                memberInfo.Name.StartsWith('<') &&
                memberInfo.Name.EndsWith(">k__BackingField"))
            {
                fieldName = fieldName.Substring("<", ">");
            }
            // An explicit name override always wins over the derived member name.
            if (memberSettings?.member_overrideName != null) fieldName = memberSettings.member_overrideName;
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
            // Value types (including Nullable<T>) are sealed, so the runtime type of the member
            // value can never deviate from the declared type. That makes the dynamic handler
            // lookup unnecessary and lets the value be written without boxing it.
            else if (expectedValueType.IsValueType || (!fieldTypeHandler.HandlerType?.IsNullable() ?? false))
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
                // Member overrides must survive a deviating runtime type, otherwise they would
                // silently stop applying exactly for the polymorphic values they matter most for.
                var resolveDeviating = CreateDeviatingWriterResolver(memberSettings);
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
                        if (valueType != expectedValueType) actualHandler = resolveDeviating(valueType);

                        actualHandler.WriteItem(value, fieldNameBytes);                        
                    }
                };
            }
        }

    }

}
