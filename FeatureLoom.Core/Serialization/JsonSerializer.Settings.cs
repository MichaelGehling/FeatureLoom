using FeatureLoom.Collections;
using FeatureLoom.Extensions;
using FeatureLoom.Helpers;
using FeatureLoom.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using static FeatureLoom.Serialization.JsonSerializer;

namespace FeatureLoom.Serialization
{

    public sealed partial class JsonSerializer
    {
        public class Settings
        {
            /// <summary>
            /// Determines when a "$type" member is written, so that the JsonDeserializer can
            /// restore the concrete type instead of the declared one.
            /// Can be overridden per type via <see cref="BaseTypeWriteSettings.SetTypeInfoHandling(TypeInfoHandling)"/>.
            /// </summary>
            public TypeInfoHandling typeInfoHandling = TypeInfoHandling.AddDeviatingTypeInfo;

            /// <summary>
            /// Determines which members of an object are written, e.g. public properties or all
            /// fields including private ones.
            /// Can be overridden per type via <see cref="BaseTypeWriteSettings.SetDataSelection(DataSelection)"/>.
            /// </summary>
            public DataSelection dataSelection = DataSelection.PublicAndPrivateFields_CleanBackingFields;

            /// <summary>
            /// Determines if and how repeated or circular object references are detected.
            /// See <see cref="ReferenceCheck"/> for the performance implications of each mode.
            /// </summary>
            public ReferenceCheck referenceCheck = ReferenceCheck.NoRefCheck;

            /// <summary>
            /// Determines how a detected reference is represented in the output. Only relevant if
            /// <see cref="referenceCheck"/> enables a ref writing mode.
            /// </summary>
            public ReferenceFormat referenceFormat = ReferenceFormat.JsonPath;

            /// <summary>
            /// Determines the shape used whenever a "$type" member is written. Independent of
            /// <see cref="typeInfoHandling"/>, which only decides *whether* it is written.
            /// </summary>
            public TypeInfoFormat typeInfoFormat = TypeInfoFormat.InlineForObjects;

            /// <summary>
            /// Determines which field name carries the payload of an array inside a type info
            /// envelope. Set to <see cref="ValueFieldName.Values"/> for consumers that expect the
            /// Newtonsoft.Json style "$values".
            /// </summary>
            public ValueFieldName arrayValueFieldName = ValueFieldName.Value;

            /// <summary>
            /// If true, enum values are written as their name instead of their numeric value.
            /// Can be overridden per enum type via <see cref="BaseTypeWriteSettings.SetEnumAsString(bool)"/>.
            /// </summary>
            public bool enumAsString = false;

            /// <summary>
            /// If true, any IEnumerable is written as a JSON array. If false, only types that also
            /// implement ICollection are, while other enumerables fall back to being written as
            /// objects. Set this to false to avoid enumerating lazy or infinite sequences.
            /// </summary>
            public bool treatEnumerablesAsCollections = true;

            /// <summary>
            /// Size in bytes of the write buffer. Output larger than this is flushed to the target
            /// stream in chunks of this size. Larger values reduce flushes at the cost of memory.
            /// </summary>
            public int writeBufferChunkSize = 64 * 1024;

            /// <summary>
            /// Initial size in bytes of the temporary buffer used for values that cannot be written
            /// directly into the main buffer, e.g. prepared field names.
            /// </summary>
            public int tempBufferSize = 8 * 1024;

            /// <summary>
            /// If true, the output is formatted with line breaks and indentation. This produces
            /// human readable but noticeably larger and slower output.
            /// </summary>
            public bool indent = false;

            /// <summary>
            /// Nesting depth up to which indentation grows. Deeper levels reuse the indentation of
            /// this depth, which bounds the size of the precomputed indentation lookup.
            /// Only relevant if <see cref="indent"/> is set.
            /// </summary>
            public int maxIndentationDepth = 50;

            /// <summary>
            /// Number of space characters added per nesting level.
            /// Only relevant if <see cref="indent"/> is set.
            /// </summary>
            public int indentationFactor = 2;

            /// <summary>
            /// If true, byte arrays and byte segments are written as a compact base64 string.
            /// If false, they are written as a JSON array of numbers.
            /// </summary>
            public bool writeByteArrayAsBase64String = true;

            /// <summary>
            /// Custom writers registered with a type predicate. They are matched by scanning, in
            /// registration order, and only if no writer was set for the concrete type itself.
            /// Filled by <see cref="TypeWriteSettings{T}.SetCustomTypeWriter(Func{WriterPreparationApi, CustomWriter{T}}, bool)"/>.
            /// </summary>
            internal List<ITypeHandlerCreator> customTypeHandlerCreators = new List<ITypeHandlerCreator>();

            /// <summary>
            /// Determines how type names are written into "$type" members, unless a custom name was
            /// set for the specific scope via
            /// <see cref="BaseTypeWriteSettings.SetCustomTypeName(string)"/>.
            /// </summary>
            public TypeNameFormat typeNameFormat = TypeNameFormat.Simplified;

            /// <summary>
            /// Optional separate format for generic types. If null, <see cref="typeNameFormat"/> is
            /// used for generic types as well. Set this to
            /// <see cref="TypeNameFormat.AssemblyQualified"/> to write the nested generic form
            /// understood by Newtonsoft.Json while keeping simple types in another format.
            /// Custom type names always take precedence over both settings.
            /// </summary>
            public TypeNameFormat? genericTypeNameFormat = null;

            /// <summary>
            /// Builds a new settings instance and applies a configuration callback.
            /// </summary>
            /// <param name="configure">Configuration action; may be <see langword="null"/>.</param>
            /// <returns>A configured settings instance.</returns>
            public static Settings Build(Action<Settings> configure)
            {
                var settings = new Settings();
                configure?.Invoke(settings);
                return settings;
            }

            /// <summary>
            /// Stores explicit type and generic-type configuration entries.
            /// </summary>
            internal Dictionary<Type, BaseTypeWriteSettings> typeSettingsDict = new();

            /// <summary>
            /// Configures write settings for a concrete type. Settings defined here override the
            /// corresponding global settings whenever a value of that type is written.
            /// </summary>
            /// <typeparam name="T">Configured type.</typeparam>
            /// <param name="configureTypeSettings">
            /// Callback that mutates or creates a <see cref="TypeWriteSettings{T}"/>.
            /// If <see langword="null"/>, the type configuration is removed.
            /// </param>
            public void ConfigureType<T>(Action<TypeWriteSettings<T>> configureTypeSettings)
            {
                Type type = typeof(T);
                if (configureTypeSettings == null)
                {
                    typeSettingsDict.Remove(type);
                    return;
                }

                if (!typeSettingsDict.TryGetValue(type, out BaseTypeWriteSettings existingSettings) ||
                    !(existingSettings is TypeWriteSettings<T> typeSettings))
                {
                    typeSettings = new TypeWriteSettings<T>();
                }
                typeSettings.ownerSettings = this;
                configureTypeSettings(typeSettings);
                typeSettingsDict[type] = typeSettings;
            }

            /// <summary>
            /// Configures write settings for a concrete type that is only known at runtime, e.g.
            /// when registering types discovered from a plugin or an assembly scan.
            /// </summary>
            /// <param name="type">Configured type. Must not be a generic type definition.</param>
            /// <param name="configureTypeSettings">
            /// Callback that mutates or creates the type settings.
            /// If <see langword="null"/>, the type configuration is removed.
            /// </param>
            /// <remarks>
            /// Only exposes the settings shared by all type scopes. The members that need the
            /// static type, e.g. ConfigureMember or SetCustomTypeWriter, are available on the
            /// generic <see cref="ConfigureType{T}"/> only.
            /// </remarks>
            public void ConfigureType(Type type, Action<BaseTypeWriteSettings> configureTypeSettings)
            {
                if (type == null) throw new ArgumentNullException(nameof(type));
                if (configureTypeSettings == null)
                {
                    typeSettingsDict.Remove(type);
                    return;
                }

                if (type.IsGenericTypeDefinition)
                {
                    throw new ArgumentException($"{TypeNameHelper.Shared.GetSimplifiedTypeName(type)} is a generic type definition. " +
                                                $"Use {nameof(ConfigureGenericType)}() instead.", nameof(type));
                }

                // The closed generic TypeWriteSettings<T> is created via reflection, so an entry
                // added here stays interchangeable with one added via ConfigureType<T>().
                if (!typeSettingsDict.TryGetValue(type, out BaseTypeWriteSettings typeSettings) ||
                    typeSettings is GenericTypeWriteSettings)
                {
                    typeSettings = (BaseTypeWriteSettings)Activator.CreateInstance(typeof(TypeWriteSettings<>).MakeGenericType(type));
                }
                typeSettings.ownerSettings = this;
                configureTypeSettings(typeSettings);
                typeSettingsDict[type] = typeSettings;
            }

            /// <summary>
            /// Configures write settings for a generic type definition, e.g. typeof(List&lt;&gt;).
            /// They apply to every constructed type built from that definition, unless a more
            /// specific configuration exists for the constructed type itself.
            /// </summary>
            /// <param name="genericTypeDefinition">Generic type definition.</param>
            /// <param name="configureTypeSettings">
            /// Callback that mutates or creates a <see cref="GenericTypeWriteSettings"/>.
            /// If <see langword="null"/>, the type configuration is removed.
            /// </param>
            public void ConfigureGenericType(Type genericTypeDefinition, Action<GenericTypeWriteSettings> configureTypeSettings)
            {
                if (genericTypeDefinition == null) throw new ArgumentNullException(nameof(genericTypeDefinition));
                if (!genericTypeDefinition.IsGenericTypeDefinition)
                {
                    throw new ArgumentException($"{TypeNameHelper.Shared.GetSimplifiedTypeName(genericTypeDefinition)} is not a generic type definition. " +
                                                $"Use {nameof(ConfigureType)}() for concrete types.", nameof(genericTypeDefinition));
                }

                if (configureTypeSettings == null)
                {
                    typeSettingsDict.Remove(genericTypeDefinition);
                    return;
                }

                if (!typeSettingsDict.TryGetValue(genericTypeDefinition, out BaseTypeWriteSettings existingSettings) ||
                    !(existingSettings is GenericTypeWriteSettings typeSettings))
                {
                    typeSettings = new GenericTypeWriteSettings(genericTypeDefinition);
                }
                configureTypeSettings(typeSettings);
                typeSettingsDict[genericTypeDefinition] = typeSettings;
            }
        }

        /// <summary>
        /// Shared storage for type and member write settings.
        /// </summary>
        /// <remarks>
        /// All override fields are nullable. A <see langword="null"/> value means "not configured",
        /// so the next broader scope (owning type, then global settings) decides.
        /// </remarks>
        public class BaseTypeWriteSettings
        {
            /// <summary>Type-level member selection override.</summary>
            internal DataSelection? dataSelection = null;

            /// <summary>Type-level type info handling override.</summary>
            internal TypeInfoHandling? typeInfoHandling = null;

            /// <summary>Type-level type info layout override.</summary>
            internal TypeInfoFormat? typeInfoFormat = null;

            /// <summary>Type-level payload field name override for the type info envelope.</summary>
            internal ValueFieldName? arrayValueFieldName = null;

            /// <summary>Type-level enum representation override.</summary>
            internal bool? enumAsString = null;

            /// <summary>Type-level byte array/segment representation override.</summary>
            internal bool? writeByteArrayAsBase64String = null;

            /// <summary>Type-level override for treating enumerables as collections.</summary>
            internal bool? treatEnumerablesAsCollections = null;

            /// <summary>Type-level custom type name override.</summary>
            internal string customTypeName = null;

            /// <summary>Member-level ignore flag override.</summary>
            internal bool? member_ignore = null;

            /// <summary>Member-level alternate name override.</summary>
            internal string member_overrideName = null;

            /// <summary>Per-member configuration map, keyed by member name.</summary>
            internal Dictionary<string, BaseTypeWriteSettings> memberSettingsDict = new();

            /// <summary>
            /// Configuration for the elements of a container type scope, or <see langword="null"/>.
            /// For a dictionary written as a JSON object this applies to its values; for lists,
            /// arrays and other sequences it applies to their elements.
            /// </summary>
            internal BaseTypeWriteSettings elementSettings = null;

            /// <summary>
            /// The element type <see cref="elementSettings"/> was configured for. Checked against
            /// the type actually written, which is only known once a generic type is constructed.
            /// </summary>
            internal Type elementSettingsType = null;

            /// <summary>
            /// Settings inherited by this type and every value below it in the object graph.
            /// They are applied while type writers are prepared, not materialized as member
            /// settings during configuration.
            /// </summary>
            internal RecursiveWriteSettings recursiveSettings = null;

            /// <summary>
            /// Type-level override for the JSON shape a dictionary is written in, or
            /// <see langword="null"/> to let the key type decide.
            /// </summary>
            internal DictionaryShape? dictionaryShape = null;

            /// <summary>
            /// Formats a dictionary key as a JSON property name. When set, the dictionary can be
            /// written as a JSON object regardless of its key type.
            /// </summary>
            internal IKeyFormatter keyFormatter = null;

            /// <summary>
            /// Custom writer registered for this type scope, or <see langword="null"/>. Found by a
            /// direct lookup, so it always beats the predicate registered handlers.
            /// </summary>
            internal ITypeHandlerCreator customTypeWriterCreator = null;

            /// <summary>
            /// The settings instance this configuration belongs to. Needed to register a custom
            /// writer that matches by predicate, since such a writer cannot live in the per-type map.
            /// </summary>
            internal Settings ownerSettings = null;

            /// <summary>
            /// True if this object is already the result of a merge with the general settings of a
            /// type, so <see cref="MergeOnto"/> must not be applied to it again. See there for why
            /// the merge is limited to one level.
            /// </summary>
            internal bool isMerged = false;

            /// <summary>
            /// True if this settings object says anything about how a value is written, as opposed
            /// to only member-level metadata (<see cref="member_ignore"/>, <see cref="member_overrideName"/>).
            /// Used to decide whether a member needs its own writer instead of the shared one.
            /// </summary>
            internal bool HasValueShapingOverrides =>
                dataSelection != null ||
                typeInfoHandling != null ||
                typeInfoFormat != null ||
                arrayValueFieldName != null ||
                enumAsString != null ||
                writeByteArrayAsBase64String != null ||
                treatEnumerablesAsCollections != null ||
                customTypeWriterCreator != null ||
                customTypeName != null ||
                elementSettings != null ||
                dictionaryShape != null ||
                keyFormatter != null ||
                memberSettingsDict.Count > 0;

            /// <summary>
            /// Configures defaults that apply to this type scope and recursively to nested values.
            /// Local type, member and element settings override these defaults per option.
            /// </summary>
            public void ConfigureRecursively(Action<RecursiveWriteSettings> configure)
            {
                if (configure == null)
                {
                    recursiveSettings = null;
                    return;
                }

                recursiveSettings ??= new RecursiveWriteSettings();
                configure(recursiveSettings);
            }

            /// <summary>
            /// Returns these context-local settings combined with <paramref name="generalSettings"/>,
            /// the settings configured for the type itself. Everything the local context does not
            /// say anything about is taken from the general settings, so configuring a type and
            /// then overriding a single aspect for one member no longer discards the rest.
            /// </summary>
            /// <remarks>
            /// <para>
            /// <see cref="memberSettingsDict"/> is merged per member name, with the local entry
            /// winning, but only for this one level: the result is flagged as
            /// <see cref="isMerged"/> and writers created below it do not merge again. That limit
            /// is required for termination. A self referencing type whose general settings
            /// configure the recursive member would otherwise re-inject that same member setting at
            /// every nesting level, and building the writer would never end.
            /// </para>
            /// <para>
            /// Returns a new object; neither input is modified.
            /// </para>
            /// </remarks>
            internal BaseTypeWriteSettings MergeOnto(BaseTypeWriteSettings generalSettings)
            {
                if (generalSettings == null || isMerged) return this;

                var merged = new BaseTypeWriteSettings
                {
                    dataSelection = dataSelection ?? generalSettings.dataSelection,
                    typeInfoHandling = typeInfoHandling ?? generalSettings.typeInfoHandling,
                    typeInfoFormat = typeInfoFormat ?? generalSettings.typeInfoFormat,
                    arrayValueFieldName = arrayValueFieldName ?? generalSettings.arrayValueFieldName,
                    enumAsString = enumAsString ?? generalSettings.enumAsString,
                    writeByteArrayAsBase64String = writeByteArrayAsBase64String ?? generalSettings.writeByteArrayAsBase64String,
                    treatEnumerablesAsCollections = treatEnumerablesAsCollections ?? generalSettings.treatEnumerablesAsCollections,
                    customTypeName = customTypeName ?? generalSettings.customTypeName,
                    customTypeWriterCreator = customTypeWriterCreator ?? generalSettings.customTypeWriterCreator,
                    elementSettings = elementSettings ?? generalSettings.elementSettings?.AsInjectedFromGeneralSettings(),
                    elementSettingsType = elementSettings != null ? elementSettingsType : generalSettings.elementSettingsType,
                    dictionaryShape = dictionaryShape ?? generalSettings.dictionaryShape,
                    keyFormatter = keyFormatter ?? generalSettings.keyFormatter,
                    recursiveSettings = recursiveSettings?.MergeOnto(generalSettings.recursiveSettings) ?? generalSettings.recursiveSettings,
                    member_ignore = member_ignore,
                    member_overrideName = member_overrideName,
                    ownerSettings = ownerSettings ?? generalSettings.ownerSettings,
                    isMerged = true
                };

                foreach (var entry in generalSettings.memberSettingsDict) merged.memberSettingsDict[entry.Key] = entry.Value.AsInjectedFromGeneralSettings();
                foreach (var entry in memberSettingsDict) merged.memberSettingsDict[entry.Key] = entry.Value;

                return merged;
            }

            /// <summary>
            /// Returns a copy of these member settings that will not be merged with the general
            /// settings of its type again.
            /// </summary>
            /// <remarks>
            /// Used for entries pulled in from a type's general settings by <see cref="MergeOnto"/>.
            /// Without the flag, a self referencing member would re-inject the very settings object
            /// that pulled it in, at every nesting level, and writer creation would never terminate.
            /// The copy keeps its own <see cref="memberSettingsDict"/>, which is user authored and
            /// therefore finite in depth.
            /// </remarks>
            private BaseTypeWriteSettings AsInjectedFromGeneralSettings()
            {
                if (isMerged) return this;

                var copy = new BaseTypeWriteSettings
                {
                    dataSelection = dataSelection,
                    typeInfoHandling = typeInfoHandling,
                    typeInfoFormat = typeInfoFormat,
                    arrayValueFieldName = arrayValueFieldName,
                    enumAsString = enumAsString,
                    writeByteArrayAsBase64String = writeByteArrayAsBase64String,
                    treatEnumerablesAsCollections = treatEnumerablesAsCollections,
                    customTypeName = customTypeName,
                    customTypeWriterCreator = customTypeWriterCreator,
                    elementSettings = elementSettings,
                    elementSettingsType = elementSettingsType,
                    dictionaryShape = dictionaryShape,
                    keyFormatter = keyFormatter,
                    recursiveSettings = recursiveSettings,
                    member_ignore = member_ignore,
                    member_overrideName = member_overrideName,
                    ownerSettings = ownerSettings,
                    isMerged = true
                };
                foreach (var entry in memberSettingsDict) copy.memberSettingsDict[entry.Key] = entry.Value;
                return copy;
            }

            /// <summary>
            /// Returns the subset of these settings that may be carried over to a value whose
            /// runtime type deviates from the declared one, or <see langword="null"/> if nothing
            /// remains. Used to keep a member/context override in effect for polymorphic values.
            /// </summary>
            /// <remarks>
            /// <para>
            /// Transferable are the type independent policy settings and the per member
            /// configuration: a derived type inherits the members of its base type, so a member
            /// rule stated for the declared type should keep applying to them. Entries naming a
            /// member the runtime type does not have simply never match, the same as a mistyped
            /// member name does today.
            /// </para>
            /// <para>
            /// Deliberately excluded: <see cref="customTypeName"/>, because the name configured for
            /// the declared type would mislabel the deviating one, and
            /// <see cref="customTypeWriterCreator"/>, because a custom writer is bound to the type
            /// it was declared for and cannot read a different one.
            /// </para>
            /// <para>
            /// Returns a copy, so the result can be handed to a writer without the risk of the
            /// original being mutated afterwards.
            /// </para>
            /// </remarks>
            internal BaseTypeWriteSettings GetTransferableSubset()
            {
                if (dataSelection == null &&
                    typeInfoHandling == null &&
                    typeInfoFormat == null &&
                    arrayValueFieldName == null &&
                    enumAsString == null &&
                    writeByteArrayAsBase64String == null &&
                    treatEnumerablesAsCollections == null &&
                    elementSettings == null &&
                    dictionaryShape == null &&
                    keyFormatter == null &&
                    memberSettingsDict.Count == 0) return null;

                var subset = new BaseTypeWriteSettings
                {
                    dataSelection = dataSelection,
                    typeInfoHandling = typeInfoHandling,
                    typeInfoFormat = typeInfoFormat,
                    arrayValueFieldName = arrayValueFieldName,
                    enumAsString = enumAsString,
                    writeByteArrayAsBase64String = writeByteArrayAsBase64String,
                    treatEnumerablesAsCollections = treatEnumerablesAsCollections,
                    elementSettings = elementSettings,
                    elementSettingsType = elementSettingsType,
                    dictionaryShape = dictionaryShape,
                    keyFormatter = keyFormatter,
                    ownerSettings = ownerSettings
                };
                foreach (var entry in memberSettingsDict) subset.memberSettingsDict[entry.Key] = entry.Value;
                return subset;
            }

            /// <summary>Sets which members are written for this type scope.</summary>
            public void SetDataSelection(DataSelection dataSelection) => this.dataSelection = dataSelection;
            /// <summary>
            /// Sets whether a "$type" member is written for values of this type scope.
            /// </summary>
            /// <remarks>
            /// Resolved once, when the writer for the type is created, so it applies to every
            /// value of that type regardless of the member it is written for.
            /// </remarks>
            public void SetTypeInfoHandling(TypeInfoHandling typeInfoHandling) => this.typeInfoHandling = typeInfoHandling;

            /// <summary>
            /// Sets how the "$type" member is laid out for values of this type scope, overriding
            /// <see cref="Settings.typeInfoFormat"/>.
            /// </summary>
            /// <remarks>
            /// Resolved once, when the writer for the type scope is created.
            /// </remarks>
            public void SetTypeInfoFormat(TypeInfoFormat typeInfoFormat) => this.typeInfoFormat = typeInfoFormat;

            /// <summary>
            /// Sets whether an array wrapped in a type info envelope uses "$value" or "$values" as
            /// payload field name, overriding <see cref="Settings.arrayValueFieldName"/>.
            /// </summary>
            public void SetArrayValueFieldName(ValueFieldName arrayValueFieldName) => this.arrayValueFieldName = arrayValueFieldName;

            /// <summary>
            /// Sets whether enum values of this type scope are written as their name instead of
            /// their numeric value.
            /// </summary>
            /// <remarks>
            /// Only affects enums written as values. Enums used as dictionary keys always follow
            /// the global <see cref="Settings.enumAsString"/> setting.
            /// </remarks>
            public void SetEnumAsString(bool enumAsString) => this.enumAsString = enumAsString;

            /// <summary>
            /// Sets whether byte arrays and byte segments of this type scope are written as a
            /// compact base64 string instead of a JSON array of numbers.
            /// </summary>
            /// <remarks>
            /// Applies to the byte array or segment type itself, so configure e.g.
            /// <c>ConfigureType&lt;byte[]&gt;(...)</c>, not the type that contains such a member.
            /// </remarks>
            public void SetWriteByteArrayAsBase64String(bool writeByteArrayAsBase64String) => this.writeByteArrayAsBase64String = writeByteArrayAsBase64String;

            /// <summary>
            /// Sets whether values of this type scope are written as a JSON array when they only
            /// implement IEnumerable without implementing ICollection. If set to false, such a type
            /// is written as an object instead, which avoids enumerating lazy or infinite sequences.
            /// </summary>
            public void SetTreatEnumerablesAsCollections(bool treatEnumerablesAsCollections) => this.treatEnumerablesAsCollections = treatEnumerablesAsCollections;

            /// <summary>
            /// Sets the name written into the "$type" member for this scope, overruling
            /// <see cref="Settings.typeNameFormat"/> and <see cref="Settings.genericTypeNameFormat"/>.
            /// </summary>
            /// <remarks>
            /// <para>
            /// Set on a type scope it applies to every value of that type. Set on a member scope
            /// via ConfigureMember it applies only to that member, which lets the same CLR type
            /// claim a different name depending on where it is written.
            /// </para>
            /// <para>
            /// Only supported on a concrete type, i.e. via <see cref="Settings.ConfigureType{T}"/>
            /// or <see cref="Settings.ConfigureType(Type, Action{BaseTypeWriteSettings})"/>.
            /// A generic type definition is rejected, because a single literal name cannot stay
            /// unique across its constructed types: List&lt;int&gt; and List&lt;string&gt; would both
            /// write it and could no longer be told apart when reading.
            /// </para>
            /// <para>
            /// The JsonDeserializer keeps its own, independent name-to-type mapping. To read such
            /// JSON back, register the counterpart there via its AddCustomTypeName method. This is
            /// what allows a payload to be decoded into a type other than the one it was written
            /// from.
            /// </para>
            /// </remarks>
            /// <param name="customTypeName">Name to write, or <see langword="null"/> to remove the override.</param>
            public virtual void SetCustomTypeName(string customTypeName) => this.customTypeName = customTypeName;

            /// <summary>
            /// Validates that <paramref name="memberName"/> exists on <paramref name="objType"/> and
            /// has the expected type, then stores the configured member settings.
            /// </summary>
            private protected void ConfigureMemberInternal<TMember>(Type objType, string memberName, Action<MemberWriteSettings<TMember>> configureMemberSettings)            {
                if (configureMemberSettings == null)
                {
                    memberSettingsDict.Remove(memberName);
                    return;
                }

                Type memberType = typeof(TMember);
                if (!objType.GetMember(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    .TryFindFirst(m => m.MemberType == MemberTypes.Field || m.MemberType == MemberTypes.Property, out MemberInfo member))
                {
                    throw new Exception($"Member '{memberName}' not found on type {TypeNameHelper.Shared.GetSimplifiedTypeName(objType)}");
                }
                if (member is PropertyInfo propertyInfo && propertyInfo.PropertyType != memberType)
                {
                    throw new Exception($"Member '{memberName}' on type {TypeNameHelper.Shared.GetSimplifiedTypeName(objType)} is of type {TypeNameHelper.Shared.GetSimplifiedTypeName(propertyInfo.PropertyType)}, not {TypeNameHelper.Shared.GetSimplifiedTypeName(memberType)}");
                }
                else if (member is FieldInfo fieldInfo && fieldInfo.FieldType != memberType)
                {
                    throw new Exception($"Member '{memberName}' on type {TypeNameHelper.Shared.GetSimplifiedTypeName(objType)} is of type {TypeNameHelper.Shared.GetSimplifiedTypeName(fieldInfo.FieldType)}, not {TypeNameHelper.Shared.GetSimplifiedTypeName(memberType)}");
                }

                var memberSettings = new MemberWriteSettings<TMember>();
                configureMemberSettings(memberSettings);
                memberSettingsDict[memberName] = memberSettings;
            }

            /// <summary>
            /// Validates that <paramref name="containerType"/> writes elements of
            /// <typeparamref name="TElement"/>, then stores the configured element settings.
            /// </summary>
            private protected void ConfigureElementInternal<TElement>(Type containerType, Action<TypeWriteSettings<TElement>> configureElementSettings)
            {
                if (configureElementSettings == null)
                {
                    elementSettings = null;
                    elementSettingsType = null;
                    return;
                }

                if (!TryGetWrittenElementType(containerType, out Type actualElementType))
                {
                    throw new Exception($"Type {TypeNameHelper.Shared.GetSimplifiedTypeName(containerType)} is not a container type, so its elements cannot be configured.");
                }
                if (actualElementType != typeof(TElement))
                {
                    throw new Exception($"Type {TypeNameHelper.Shared.GetSimplifiedTypeName(containerType)} writes elements of type " +
                                        $"{TypeNameHelper.Shared.GetSimplifiedTypeName(actualElementType)}, not {TypeNameHelper.Shared.GetSimplifiedTypeName(typeof(TElement))}.");
                }

                var settings = new TypeWriteSettings<TElement>();
                configureElementSettings(settings);
                elementSettings = settings;
                elementSettingsType = typeof(TElement);
            }

            /// <summary>
            /// Stores a formatter that turns keys of <typeparamref name="TKey"/> into JSON
            /// property names, after checking that the configured type is a dictionary with that
            /// key type.
            /// </summary>
            private protected void ConfigureKeyInternal<TKey>(Type containerType, IKeyFormatter formatter)
            {
                if (formatter == null)
                {
                    keyFormatter = null;
                    return;
                }

                if (containerType != null)
                {
                    if (!TryGetDictionaryKeyType(containerType, out Type actualKeyType))
                    {
                        throw new Exception($"Type {TypeNameHelper.Shared.GetSimplifiedTypeName(containerType)} is not a dictionary, so its keys cannot be configured.");
                    }
                    if (actualKeyType != typeof(TKey))
                    {
                        throw new Exception($"Type {TypeNameHelper.Shared.GetSimplifiedTypeName(containerType)} has keys of type " +
                                            $"{TypeNameHelper.Shared.GetSimplifiedTypeName(actualKeyType)}, not {TypeNameHelper.Shared.GetSimplifiedTypeName(typeof(TKey))}.");
                    }
                }

                keyFormatter = formatter;
            }

            /// <summary>
            /// Determines the key type of a dictionary, regardless of whether that key could be
            /// written as a JSON property name without a formatter.
            /// </summary>
            private protected static bool TryGetDictionaryKeyType(Type containerType, out Type keyType)
            {
                keyType = null;
                if (containerType == null) return false;

                return containerType.TryGetTypeParamsOfGenericInterface(typeof(IDictionary<,>), out keyType, out _) ||
                       containerType.TryGetTypeParamsOfGenericInterface(typeof(IReadOnlyDictionary<,>), out keyType, out _);
            }

            /// <summary>
            /// Sets the JSON shape this dictionary type is written in.
            /// </summary>
            /// <param name="shape">
            /// The shape to use. <see cref="DictionaryShape.Auto"/> restores the default, where the
            /// key type decides. There is deliberately no value that forces the object shape,
            /// because a key that cannot become a property name could not be written that way;
            /// configure a key formatter via <c>ConfigureKey</c> instead.
            /// </param>
            public void SetDictionaryShape(DictionaryShape shape) => dictionaryShape = shape;

            /// <summary>
            /// Determines the type of the values a container writes into its JSON array or object:
            /// the value type of a dictionary, otherwise the element type of the sequence.
            /// </summary>
            /// <remarks>
            /// A dictionary is only treated as such if it is written as a JSON object. When it is
            /// written as an array of key/value pairs instead - because its key cannot become a
            /// property name, or because that shape was requested - the written element is the
            /// KeyValuePair itself.
            /// </remarks>
            private bool TryGetWrittenElementType(Type containerType, out Type elementType)
            {
                elementType = null;
                if (containerType == null) return false;

                if (containerType.IsArray && containerType.GetArrayRank() == 1)
                {
                    elementType = containerType.GetElementType();
                    return true;
                }

                if ((containerType.TryGetTypeParamsOfGenericInterface(typeof(IDictionary<,>), out Type keyType, out Type valueType) ||
                     containerType.TryGetTypeParamsOfGenericInterface(typeof(IReadOnlyDictionary<,>), out keyType, out valueType)) &&
                    dictionaryShape != DictionaryShape.KeyValuePairArray &&
                    (CanWriteKeyAsPropertyName(keyType) || keyFormatter != null))
                {
                    elementType = valueType;
                    return true;
                }

                return containerType.TryGetTypeParamsOfGenericInterface(typeof(IEnumerable<>), out elementType);
            }
        }

        /// <summary>
        /// Write settings inherited by the configured type itself and all nested values. Only
        /// type-independent policies are available; settings bound to a specific member or CLR
        /// type cannot be propagated recursively.
        /// </summary>
        public sealed class RecursiveWriteSettings
        {
            internal DataSelection? dataSelection;
            internal TypeInfoHandling? typeInfoHandling;
            internal TypeInfoFormat? typeInfoFormat;
            internal ValueFieldName? arrayValueFieldName;
            internal bool? enumAsString;
            internal bool? writeByteArrayAsBase64String;
            internal bool? treatEnumerablesAsCollections;
            internal DictionaryShape? dictionaryShape;

            public void SetDataSelection(DataSelection value) => dataSelection = value;
            public void SetTypeInfoHandling(TypeInfoHandling value) => typeInfoHandling = value;
            public void SetTypeInfoFormat(TypeInfoFormat value) => typeInfoFormat = value;
            public void SetArrayValueFieldName(ValueFieldName value) => arrayValueFieldName = value;
            public void SetEnumAsString(bool value) => enumAsString = value;
            public void SetWriteByteArrayAsBase64String(bool value) => writeByteArrayAsBase64String = value;
            public void SetTreatEnumerablesAsCollections(bool value) => treatEnumerablesAsCollections = value;
            public void SetDictionaryShape(DictionaryShape value) => dictionaryShape = value;

            internal RecursiveWriteSettings MergeOnto(RecursiveWriteSettings outer)
            {
                if (outer == null) return this;

                var merged = new RecursiveWriteSettings
                {
                    dataSelection = dataSelection ?? outer.dataSelection,
                    typeInfoHandling = typeInfoHandling ?? outer.typeInfoHandling,
                    typeInfoFormat = typeInfoFormat ?? outer.typeInfoFormat,
                    arrayValueFieldName = arrayValueFieldName ?? outer.arrayValueFieldName,
                    enumAsString = enumAsString ?? outer.enumAsString,
                    writeByteArrayAsBase64String = writeByteArrayAsBase64String ?? outer.writeByteArrayAsBase64String,
                    treatEnumerablesAsCollections = treatEnumerablesAsCollections ?? outer.treatEnumerablesAsCollections,
                    dictionaryShape = dictionaryShape ?? outer.dictionaryShape
                };
                return merged.HasSameValues(outer) ? outer : merged;
            }

            internal bool HasSameValues(RecursiveWriteSettings other) =>
                other != null &&
                dataSelection == other.dataSelection &&
                typeInfoHandling == other.typeInfoHandling &&
                typeInfoFormat == other.typeInfoFormat &&
                arrayValueFieldName == other.arrayValueFieldName &&
                enumAsString == other.enumAsString &&
                writeByteArrayAsBase64String == other.writeByteArrayAsBase64String &&
                treatEnumerablesAsCollections == other.treatEnumerablesAsCollections &&
                dictionaryShape == other.dictionaryShape;

            internal BaseTypeWriteSettings ApplyBelow(BaseTypeWriteSettings local)
            {
                var effective = new BaseTypeWriteSettings
                {
                    dataSelection = local?.dataSelection ?? dataSelection,
                    typeInfoHandling = local?.typeInfoHandling ?? typeInfoHandling,
                    typeInfoFormat = local?.typeInfoFormat ?? typeInfoFormat,
                    arrayValueFieldName = local?.arrayValueFieldName ?? arrayValueFieldName,
                    enumAsString = local?.enumAsString ?? enumAsString,
                    writeByteArrayAsBase64String = local?.writeByteArrayAsBase64String ?? writeByteArrayAsBase64String,
                    treatEnumerablesAsCollections = local?.treatEnumerablesAsCollections ?? treatEnumerablesAsCollections,
                    customTypeName = local?.customTypeName,
                    customTypeWriterCreator = local?.customTypeWriterCreator,
                    elementSettings = local?.elementSettings,
                    elementSettingsType = local?.elementSettingsType,
                    dictionaryShape = local?.dictionaryShape ?? dictionaryShape,
                    keyFormatter = local?.keyFormatter,
                    recursiveSettings = local?.recursiveSettings,
                    member_ignore = local?.member_ignore,
                    member_overrideName = local?.member_overrideName,
                    ownerSettings = local?.ownerSettings,
                    isMerged = local?.isMerged ?? false
                };
                if (local != null)
                {
                    foreach (var entry in local.memberSettingsDict) effective.memberSettingsDict[entry.Key] = entry.Value;
                }
                return effective;
            }
        }

        /// <summary>
        /// Write settings for a generic type definition.
        /// </summary>
        public class GenericTypeWriteSettings : BaseTypeWriteSettings
        {
            /// <summary>Generic type definition this setting entry applies to.</summary>
            protected Type genericType;

            /// <summary>Creates write settings for a generic type definition.</summary>
            /// <param name="genericType">Generic type definition.</param>
            public GenericTypeWriteSettings(Type genericType)
            {
                this.genericType = genericType;
            }

            /// <summary>
            /// Sets a custom writer for every type constructed from this generic type definition,
            /// replacing the built-in writer.
            /// </summary>
            /// <param name="writerDefinition">
            /// Generic type definition of a <see cref="CustomTypeWriterDefinition{T}"/> implementation,
            /// e.g. <c>typeof(MyWriter&lt;&gt;)</c> for
            /// <c>class MyWriter&lt;T&gt; : CustomTypeWriterDefinition&lt;MyType&lt;T&gt;&gt;</c>.
            /// It must have the same number of generic parameters as the configured type definition
            /// and a public parameterless constructor.
            /// Pass <see langword="null"/> to remove a previously set writer.
            /// </param>
            /// <remarks>
            /// The definition is closed and instantiated once per constructed type, when that type's
            /// writer is created, so it is off the write path.
            /// Precedence: a writer set for a constructed type via
            /// <see cref="Settings.ConfigureType{T}"/> is found by direct lookup and therefore wins
            /// over the writer registered here. Derived types are not covered.
            /// </remarks>
            public void SetCustomTypeWriter(Type writerDefinition)
            {
                if (writerDefinition == null)
                {
                    customTypeWriterCreator = null;
                    return;
                }

                customTypeWriterCreator = new OpenGenericTypeWriterCreator(genericType, writerDefinition);
            }

            /// <summary>
            /// Configures write settings for one member of the generic type definition.
            /// </summary>
            /// <typeparam name="TMember">Expected member type.</typeparam>
            /// <param name="memberName">Member name.</param>
            /// <param name="configureMemberSettings">
            /// Callback to configure member settings. If <see langword="null"/>, existing member settings are removed.
            /// </param>
            public void ConfigureMember<TMember>(string memberName, Action<MemberWriteSettings<TMember>> configureMemberSettings)
                => ConfigureMemberInternal(genericType, memberName, configureMemberSettings);

            /// <summary>
            /// Configures write settings for the elements written by the constructed types of this
            /// generic type definition.
            /// </summary>
            /// <typeparam name="TElement">
            /// Expected element type: the value type for a dictionary written as a JSON object,
            /// otherwise the element type of the sequence.
            /// </typeparam>
            /// <param name="configureElementSettings">
            /// Callback to configure element settings. If <see langword="null"/>, existing element settings are removed.
            /// </param>
            /// <remarks>
            /// The element type cannot be verified against an open generic definition, because it
            /// is only known once the type is constructed. A constructed type whose elements are
            /// not <typeparamref name="TElement"/> simply keeps its unconfigured element writer.
            /// </remarks>
            public void ConfigureElement<TElement>(Action<TypeWriteSettings<TElement>> configureElementSettings)
            {
                if (configureElementSettings == null)
                {
                    elementSettings = null;
                    elementSettingsType = null;
                    return;
                }

                var settings = new TypeWriteSettings<TElement>();
                configureElementSettings(settings);
                elementSettings = settings;
                elementSettingsType = typeof(TElement);
            }

            /// <summary>
            /// Sets a formatter that turns the keys of the constructed dictionary types of this
            /// generic type definition into JSON property names.
            /// </summary>
            /// <typeparam name="TKey">Expected key type.</typeparam>
            /// <param name="formatKey">
            /// Formats a key. If <see langword="null"/>, an existing formatter is removed.
            /// </param>
            /// <remarks>
            /// The key type cannot be verified against an open generic definition. A constructed
            /// type whose keys are not <typeparamref name="TKey"/> keeps its default key handling.
            /// Like every key formatter, this one is write-only and has no deserializer counterpart.
            /// </remarks>
            public void ConfigureKey<TKey>(Func<TKey, string> formatKey)
                => ConfigureKeyInternal<TKey>(null, formatKey == null ? null : new StringKeyFormatter<TKey>(formatKey));

            /// <inheritdoc cref="ConfigureKey{TKey}(Func{TKey, string})"/>
            public void ConfigureKey<TKey>(Func<TKey, TextSegment> formatKey)
                => ConfigureKeyInternal<TKey>(null, formatKey == null ? null : new TextSegmentKeyFormatter<TKey>(formatKey));

#if !NETSTANDARD2_0
            /// <inheritdoc cref="ConfigureKey{TKey}(Func{TKey, string})"/>
            public void ConfigureKey<TKey>(KeyToSpan<TKey> formatKey)
                => ConfigureKeyInternal<TKey>(null, formatKey == null ? null : new SpanKeyFormatter<TKey>(formatKey));
#endif

            /// <summary>
            /// Not supported for a generic type definition. A single literal name cannot stay
            /// unique across the constructed types, so it has to be set on each concrete type via
            /// <see cref="Settings.ConfigureType{T}"/>.
            /// </summary>
            /// <exception cref="Exception">Always thrown, unless the name is <see langword="null"/>.</exception>
            public override void SetCustomTypeName(string customTypeName)
            {
                if (customTypeName == null)
                {
                    base.SetCustomTypeName(null);
                    return;
                }

                throw new Exception($"A custom type name cannot be set on the generic type definition {TypeNameHelper.Shared.GetSimplifiedTypeName(genericType)}, " +
                                    $"because all its constructed types would share the same name and could not be distinguished when reading. " +
                                    $"Use ConfigureType<T>() on each constructed type instead.");
            }
        }

        /// <summary>
        /// Write settings for one concrete type.
        /// </summary>
        /// <typeparam name="T">Configured type.</typeparam>
        public class TypeWriteSettings<T> : BaseTypeWriteSettings
        {
            /// <summary>
            /// Configures write settings for one member of <typeparamref name="T"/>.
            /// </summary>
            /// <typeparam name="TMember">Expected member type.</typeparam>
            /// <param name="memberName">Member name.</param>
            /// <param name="configureMemberSettings">
            /// Callback to configure member settings. If <see langword="null"/>, existing member settings are removed.
            /// </param>
            public void ConfigureMember<TMember>(string memberName, Action<MemberWriteSettings<TMember>> configureMemberSettings)
                => ConfigureMemberInternal(typeof(T), memberName, configureMemberSettings);

            /// <summary>
            /// Configures write settings for the elements <typeparamref name="T"/> writes, the same
            /// way <see cref="ConfigureMember{TMember}"/> configures a single member.
            /// </summary>
            /// <typeparam name="TElement">
            /// Expected element type: the value type for a dictionary written as a JSON object,
            /// otherwise the element type of the sequence.
            /// </typeparam>
            /// <param name="configureElementSettings">
            /// Callback to configure element settings. If <see langword="null"/>, existing element settings are removed.
            /// </param>
            /// <remarks>
            /// The settings apply to every element, including elements whose runtime type deviates
            /// from <typeparamref name="TElement"/>, as far as they are transferable (see
            /// <see cref="BaseTypeWriteSettings.GetTransferableSubset"/>).
            /// </remarks>
            public void ConfigureElement<TElement>(Action<TypeWriteSettings<TElement>> configureElementSettings)
                => ConfigureElementInternal(typeof(T), configureElementSettings);

            /// <summary>
            /// Sets a formatter that turns the keys of the dictionary type <typeparamref name="T"/>
            /// into JSON property names, which lets it be written as a JSON object even when its
            /// key type could not be written as a property name on its own.
            /// </summary>
            /// <typeparam name="TKey">Expected key type.</typeparam>
            /// <param name="formatKey">
            /// Formats a key. If <see langword="null"/>, an existing formatter is removed.
            /// The result is escaped by the serializer, but not checked for uniqueness: producing
            /// the same name for two keys yields a JSON object with duplicate property names.
            /// </param>
            /// <remarks>
            /// The formatter is write-only. The JsonDeserializer has no counterpart, so a
            /// dictionary with a formatted key of a type that is not natively supported as a key
            /// does not round-trip.
            /// </remarks>
            public void ConfigureKey<TKey>(Func<TKey, string> formatKey)
                => ConfigureKeyInternal<TKey>(typeof(T), formatKey == null ? null : new StringKeyFormatter<TKey>(formatKey));

            /// <inheritdoc cref="ConfigureKey{TKey}(Func{TKey, string})"/>
            public void ConfigureKey<TKey>(Func<TKey, TextSegment> formatKey)
                => ConfigureKeyInternal<TKey>(typeof(T), formatKey == null ? null : new TextSegmentKeyFormatter<TKey>(formatKey));

#if !NETSTANDARD2_0
            /// <inheritdoc cref="ConfigureKey{TKey}(Func{TKey, string})"/>
            public void ConfigureKey<TKey>(KeyToSpan<TKey> formatKey)
                => ConfigureKeyInternal<TKey>(typeof(T), formatKey == null ? null : new SpanKeyFormatter<TKey>(formatKey));
#endif

            /// <summary>
            /// Sets a custom writer for <typeparamref name="T"/>, replacing the built-in writer.
            /// </summary>
            /// <param name="prepare">
            /// Preparation step, called once per matching type. It builds the writer via the
            /// <see cref="WriterPreparationApi"/> and returns it; the returned writer is then used
            /// for every value of that type.
            /// Pass <see langword="null"/> to remove a previously set writer.
            /// </param>
            /// <param name="supportsType">
            /// Optional predicate that widens the writer to further types, e.g.
            /// <c>type => typeof(T).IsAssignableFrom(type)</c> for all subtypes, or an attribute
            /// check. <typeparamref name="T"/> itself is always covered, whether the predicate
            /// accepts it or not. Every accepted type must be assignable to <typeparamref name="T"/>.
            /// </param>
            /// <remarks>
            /// The predicate is only evaluated when a type's writer is created, so it is off the
            /// write path.
            /// Precedence: for <typeparamref name="T"/> itself the writer is found by direct
            /// lookup, so it wins over every predicate matched writer, independent of registration
            /// order. For the additionally matched types the first registered predicate wins.
            /// </remarks>
            public void SetCustomTypeWriter(Func<WriterPreparationApi, CustomWriter<T>> prepare, Func<Type, bool> supportsType)
            {
                if (prepare == null)
                {
                    customTypeWriterCreator = null;
                    return;
                }

                customTypeWriterCreator = new CustomTypeWriterCreator<T>(prepare);
                if (supportsType == null) return;

                // The predicate matches types other than T, which cannot be found by the per-type
                // lookup, so that part of the registration goes into the scanned list.
                if (ownerSettings == null) throw new Exception($"A custom type writer with a type predicate can only be set via {nameof(Settings)}.{nameof(Settings.ConfigureType)}<T>().");
                ownerSettings.customTypeHandlerCreators.Add(new CustomTypeWriterCreator<T>(prepare, supportsType));
            }

            /// <summary>
            /// Sets a custom writer for <typeparamref name="T"/>, replacing the built-in writer.
            /// </summary>
            /// <param name="prepare">
            /// Preparation step, called once per matching type. It builds the writer via the
            /// <see cref="WriterPreparationApi"/> and returns it; the returned writer is then used
            /// for every value of that type.
            /// Pass <see langword="null"/> to remove a previously set writer.
            /// </param>
            /// <param name="handlesDerivedTypes">
            /// If <see langword="true"/>, the writer is used for all types derived from <typeparamref name="T"/>, 
            /// otherwise only for exactly <typeparamref name="T"/>.
            /// </param>
            /// <remarks>
            /// The predicate is only evaluated when a type's writer is created, so it is off the
            /// write path.
            /// Precedence: for <typeparamref name="T"/> itself the writer is found by direct
            /// lookup, so it wins over every predicate matched writer, independent of registration
            /// order. For the additionally matched types the first registered predicate wins.
            /// </remarks>
            public void SetCustomTypeWriter(Func<WriterPreparationApi, CustomWriter<T>> prepare, bool handlesDerivedTypes = false)
            {
                if (handlesDerivedTypes)
                {
                    SetCustomTypeWriter(prepare, type => typeof(T).IsAssignableFrom(type));
                }
                else
                {
                    SetCustomTypeWriter(prepare, null);
                }
            }

            /// <summary>
            /// Sets a custom writer for <typeparamref name="T"/> from a definition instance,
            /// replacing the built-in writer.
            /// </summary>
            /// <param name="definition">
            /// The definition whose <c>Prepare</c> builds the writer. Called once, when the writer
            /// for <typeparamref name="T"/> is created.
            /// Pass <see langword="null"/> to remove a previously set writer.
            /// </param>
            /// <remarks>
            /// Equivalent to passing the preparation as a lambda, but the definition is a class and
            /// can therefore carry state and be reused for the generic registration
            /// <see cref="GenericTypeWriteSettings.SetCustomTypeWriter(Type)"/>.
            /// Only <typeparamref name="T"/> itself is covered; use the lambda overload to widen
            /// the writer to further types by predicate.
            /// </remarks>
            public void SetCustomTypeWriter(CustomTypeWriterDefinition<T> definition)
            {
                customTypeWriterCreator = definition == null ? null : new DefinitionTypeWriterCreator<T>(definition);
            }
        }

        /// <summary>
        /// Write settings for a single member of a configured type.
        /// </summary>
        /// <typeparam name="T">Member type.</typeparam>
        public class MemberWriteSettings<T> : TypeWriteSettings<T>
        {
            /// <summary>
            /// Excludes this member from the output, equivalent to a JsonIgnore attribute.
            /// </summary>
            /// <param name="ignore"><see langword="true"/> to skip this member.</param>
            public void SetIgnore(bool ignore = true) => this.member_ignore = ignore;

            /// <summary>
            /// Writes this member under a different name than the member name.
            /// </summary>
            /// <param name="alternateName">Name to write into the JSON object.</param>
            public void OverrideName(string alternateName) => this.member_overrideName = alternateName;
        }

        /// <summary>
        /// Determines which members of an object are written.
        /// </summary>
        /// <remarks>
        /// Independent of the selected mode, members marked with a JsonIgnoreAttribute are always
        /// omitted and members marked with a JsonIncludeAttribute are always written.
        /// </remarks>
        public enum DataSelection
        {
            /// <summary>
            /// All instance fields, public and private, including auto-property backing fields
            /// under their compiler generated name, e.g. "&lt;Name&gt;k__BackingField".
            /// </summary>
            PublicAndPrivateFields = 0,

            /// <summary>
            /// Like <see cref="PublicAndPrivateFields"/>, but auto-property backing fields are
            /// written under their clean property name, e.g. "Name". This keeps full state
            /// fidelity while producing conventional member names.
            /// </summary>
            PublicAndPrivateFields_CleanBackingFields = 1,

            /// <summary>
            /// Like <see cref="PublicAndPrivateFields"/>, but auto-property backing fields are
            /// omitted, so only explicitly declared fields are written.
            /// </summary>
            PublicAndPrivateFields_RemoveBackingFields = 2,

            /// <summary>
            /// Only public instance fields and readable public instance properties. This matches
            /// what other JSON serializers write by default, but it loses state that is only kept
            /// in private fields.
            /// </summary>
            PublicFieldsAndProperties = 3,
        }

        /// <summary>
        /// Determines if and how repeated or circular object references are detected while writing.
        /// <para>
        /// Performance note: the loop detecting modes are not a cheap middle ground between
        /// <see cref="NoRefCheck"/> and <see cref="AlwaysReplaceByRef"/>. They pay the full
        /// bookkeeping cost (item infos and, in <see cref="ReferenceFormat.JsonPath"/> mode, item
        /// names) but only shrink the output when a loop is actually present. On acyclic graphs
        /// <see cref="AlwaysReplaceByRef"/> is therefore typically faster, because writing repeated
        /// objects as refs saves more output than the tracking costs.
        /// See SerializeReferenceHandlingTest in FeatureLoom.PerformanceTests.
        /// </para>
        /// </summary>
        public enum ReferenceCheck
        {
            /// <summary>
            /// No reference tracking at all. Fastest option, but shared objects are written
            /// repeatedly and circular references cause an endless loop / stack overflow.
            /// Only use this if the object graph is known to be a tree.
            /// </summary>
            NoRefCheck = 0,

            /// <summary>
            /// Detects circular references and throws an exception instead of writing them.
            /// </summary>
            OnLoopThrowException = 1,

            /// <summary>
            /// Detects circular references and writes null in place of the looping object,
            /// breaking the cycle without adding any ref syntax to the output.
            /// </summary>
            OnLoopReplaceByNull = 2,

            /// <summary>
            /// Detects circular references and writes a ref (see <see cref="ReferenceFormat"/>) in
            /// place of the looping object. Repeated but non-circular occurrences are still written
            /// out in full.
            /// <para>
            /// Beware: on graphs without loops this produces exactly the same output as
            /// <see cref="NoRefCheck"/> while still paying the tracking overhead, which makes it
            /// the slowest mode in that case. Prefer <see cref="AlwaysReplaceByRef"/> unless
            /// duplicated objects really have to be materialized as separate instances.
            /// </para>
            /// </summary>
            OnLoopReplaceByRef = 3,

            /// <summary>
            /// Every object is written only once; each repeated occurrence becomes a ref (see
            /// <see cref="ReferenceFormat"/>). This preserves object identity on deserialization
            /// and usually shrinks the output noticeably, which often compensates the tracking cost.
            /// </summary>
            AlwaysReplaceByRef = 4
        }

        /// <summary>
        /// Determines how references to already serialized objects are represented.
        /// </summary>
        public enum ReferenceFormat
        {
            /// <summary>
            /// References are written as a JSONPath pointing at the location of the first
            /// occurrence, e.g. {"$ref":"$.Items[0]"}. This produces clean, human readable output
            /// and requires no additional members on the referenced objects, but it is specific to
            /// this serializer.
            /// <para>
            /// This format needs the name of every written item to build the paths, so it is
            /// slightly slower than <see cref="IdBased"/> even though it produces less output.
            /// </para>
            /// </summary>
            JsonPath = 0,

            /// <summary>
            /// References use the id based format established by System.Text.Json
            /// (ReferenceHandler.Preserve) and Newtonsoft.Json (PreserveReferencesHandling).
            /// Every reference tracked object gets an "$id" member and repeated occurrences are
            /// written as {"$ref":"1"}. Arrays are wrapped as {"$id":"1","$values":[...]}.
            /// Use this mode when the JSON has to be read by those serializers.
            /// <para>
            /// Writes a few more bytes than <see cref="JsonPath"/>, but is faster because no item
            /// names have to be tracked.
            /// </para>
            /// </summary>
            IdBased = 1
        }

        /// <summary>
        /// Determines when a "$type" member is written, so that the JsonDeserializer can restore
        /// the concrete type instead of the declared one.
        /// See <see cref="TypeNameFormat"/> for how the written name is built.
        /// </summary>
        public enum TypeInfoHandling
        {
            /// <summary>
            /// Never write type info. Smallest and fastest output, but polymorphic values are
            /// deserialized as their declared type.
            /// </summary>
            AddNoTypeInfo = 0,

            /// <summary>
            /// Write type info only where the actual type deviates from the expected one, i.e.
            /// where it is needed to restore the value correctly.
            /// </summary>
            AddDeviatingTypeInfo = 1,

            /// <summary>
            /// Always write type info, even when the actual type matches the expected one.
            /// Mainly useful for diagnostics or fully self-describing output.
            /// </summary>
            AddAllTypeInfo = 2,
        }

        /// <summary>
        /// Determines the shape used to write type info. Applies wherever a "$type" is written,
        /// no matter whether <see cref="TypeInfoHandling.AddAllTypeInfo"/> or
        /// <see cref="TypeInfoHandling.AddDeviatingTypeInfo"/> decided that.
        /// </summary>
        public enum TypeInfoFormat
        {
            /// <summary>
            /// Objects carry "$type" as their first member, e.g. {"$type":"X","A":1}, which is what
            /// Newtonsoft.Json and System.Text.Json expect. Values that have no members of their
            /// own, i.e. arrays and primitives, still need the envelope, e.g.
            /// {"$type":"X","$value":[1,2]}.
            /// </summary>
            InlineForObjects = 0,

            /// <summary>
            /// Every typed value is wrapped into an envelope, so objects become
            /// {"$type":"X","$value":{"A":1}} as well.
            /// </summary>
            /// <remarks>
            /// Gives type and payload a fixed, uniform position, which lets a consumer extract them
            /// separately without knowing the shape of the payload. Useful for foreign readers, but
            /// no longer understood by Newtonsoft.Json or System.Text.Json, which expect "$type"
            /// inline. The JsonDeserializer reads both formats.
            /// </remarks>
            AlwaysEnvelope = 1,

            /// <summary>
            /// Like <see cref="InlineForObjects"/>, but values that cannot carry an inline
            /// "$type", i.e. arrays and primitives, are written without any type info instead of
            /// being wrapped into an envelope.
            /// </summary>
            /// <remarks>
            /// Keeps the output a plain, structurally unchanged JSON document that any consumer can
            /// read, at the price of losing type info exactly where it cannot be expressed inline.
            /// A polymorphic array or primitive member then no longer round-trips to its original
            /// type. Note that the type info of the array's *elements* is unaffected, because
            /// elements are values of their own.
            /// </remarks>
            OnlyInlineForObjects = 2,
        }

        /// <summary>
        /// Selects the field name that carries the payload inside a type info envelope.
        /// The JsonDeserializer accepts both names.
        /// </summary>
        public enum ValueFieldName
        {
            /// <summary>Writes "$value", the uniform FeatureLoom keyword.</summary>
            Value = 0,

            /// <summary>
            /// Writes "$values", which is what Newtonsoft.Json uses for arrays.
            /// </summary>
            Values = 1,
        }

        /// <summary>
        /// Selects the JSON shape a dictionary is written in.
        /// </summary>
        public enum DictionaryShape
        {
            /// <summary>
            /// Writes a JSON object when the keys can become property names, which is the case for
            /// the built-in key types and for any key type that has a formatter configured via
            /// <see cref="TypeWriteSettings{T}.ConfigureKey{TKey}(Func{TKey, string})"/>.
            /// Otherwise an array of key/value pairs is written.
            /// </summary>
            Auto = 0,

            /// <summary>
            /// Always writes an array of <c>{"Key":...,"Value":...}</c> objects, even for keys that
            /// could become property names. Useful when the values of a key must survive as their
            /// own JSON value rather than as a string.
            /// </summary>
            KeyValuePairArray = 1,
        }

        /// <summary>
        /// Writes a dictionary key of a fixed key type as a JSON property name. Implemented per
        /// key representation so the formatter result can be written without an intermediate
        /// string where the representation allows it.
        /// </summary>
        internal interface IKeyFormatter
        {
            /// <summary>The key type this formatter accepts.</summary>
            Type KeyType { get; }

            /// <summary>
            /// Binds the formatter to a writer, producing the delegates a dictionary handler needs.
            /// Called once while the dictionary's writer is created, never per entry.
            /// </summary>
            void BindTo(JsonUTF8StreamWriter writer, CachedKeyWriter keyWriter);
        }

#if !NETSTANDARD2_0
        /// <summary>
        /// Formats a dictionary key as a character span, so a key can be written from an existing
        /// buffer without allocating a string. A delegate type is required because
        /// <c>Func&lt;TKey, ReadOnlySpan&lt;char&gt;&gt;</c> cannot be expressed.
        /// </summary>
        public delegate ReadOnlySpan<char> KeyToSpan<TKey>(TKey key);
#endif

        /// <summary>Key formatter producing a string.</summary>
        internal sealed class StringKeyFormatter<TKey> : IKeyFormatter
        {
            readonly Func<TKey, string> formatKey;

            internal StringKeyFormatter(Func<TKey, string> formatKey) => this.formatKey = formatKey;

            public Type KeyType => typeof(TKey);

            public void BindTo(JsonUTF8StreamWriter writer, CachedKeyWriter keyWriter)
            {
                var format = formatKey;
                keyWriter.SetWriterMethod<TKey>(key => writer.WritePrimitiveValueAsString(format(key)));
                keyWriter.SetWriterWithCopyMethod<TKey>(key => writer.WriteStringValueAsStringWithCopy(format(key)));
            }
        }

        /// <summary>Key formatter producing a text segment.</summary>
        internal sealed class TextSegmentKeyFormatter<TKey> : IKeyFormatter
        {
            readonly Func<TKey, TextSegment> formatKey;

            internal TextSegmentKeyFormatter(Func<TKey, TextSegment> formatKey) => this.formatKey = formatKey;

            public Type KeyType => typeof(TKey);

            public void BindTo(JsonUTF8StreamWriter writer, CachedKeyWriter keyWriter)
            {
                var format = formatKey;
                keyWriter.SetWriterMethod<TKey>(key => writer.WriteTextSegmentValue(format(key)));
                keyWriter.SetWriterWithCopyMethod<TKey>(key => writer.WriteTextSegmentValueAsStringWithCopy(format(key)));
            }
        }

#if !NETSTANDARD2_0
        /// <summary>Key formatter producing a character span.</summary>
        internal sealed class SpanKeyFormatter<TKey> : IKeyFormatter
        {
            readonly KeyToSpan<TKey> formatKey;

            internal SpanKeyFormatter(KeyToSpan<TKey> formatKey) => this.formatKey = formatKey;

            public Type KeyType => typeof(TKey);

            public void BindTo(JsonUTF8StreamWriter writer, CachedKeyWriter keyWriter)
            {
                var format = formatKey;
                keyWriter.SetWriterMethod<TKey>(key => writer.WriteStringValue(format(key)));
                keyWriter.SetWriterWithCopyMethod<TKey>(key => writer.WriteStringValueAsStringWithCopy(format(key)));
            }
        }
#endif

        /// <summary>
        /// Determines how the type name written into a "$type" member is built.
        /// The JsonDeserializer reads all of these formats.
        /// </summary>
        public enum TypeNameFormat
        {
            /// <summary>
            /// The simplified, human readable FeatureLoom format, e.g.
            /// "System.Collections.Generic.List&lt;System.String&gt;". Omits assembly information,
            /// which keeps the output short but requires the type to be findable in the loaded
            /// assemblies when reading it back.
            /// </summary>
            Simplified = 0,

            /// <summary>
            /// The plain CLR full name without assembly information, e.g. "MyApp.Models.Customer".
            /// For generic types this is the CLR form, e.g.
            /// "System.Collections.Generic.List`1[[System.String, System.Private.CoreLib]]".
            /// </summary>
            FullName = 1,

            /// <summary>
            /// The assembly qualified name using the short assembly name, e.g.
            /// "MyApp.Models.Customer, MyApp". This is the format written by Newtonsoft.Json when
            /// TypeNameHandling is enabled, so use it when the JSON has to be read by Newtonsoft.
            /// <para>
            /// Beware: because the name contains the assembly, output is not identical across
            /// target frameworks (e.g. corlib is "mscorlib" on .NET Framework and
            /// "System.Private.CoreLib" on .NET 8/10).
            /// </para>
            /// </summary>
            AssemblyQualified = 2
        }

        internal readonly struct CompiledSettings
        {
            public readonly TypeInfoHandling typeInfoHandling;
            public readonly DataSelection dataSelection;
            public readonly ReferenceCheck referenceCheck;
            public readonly ReferenceFormat referenceFormat;
            public readonly TypeInfoFormat typeInfoFormat;

            /// <summary>
            /// True if type info must be omitted where it would require an envelope, i.e. for
            /// arrays and primitives. See <see cref="TypeInfoFormat.OnlyInlineForObjects"/>.
            /// </summary>
            public readonly bool skipTypeInfoEnvelope;
            public readonly ValueFieldName arrayValueFieldName;
            public readonly TypeNameFormat typeNameFormat;
            public readonly TypeNameFormat genericTypeNameFormat;
            public readonly bool enumAsString;
            public readonly bool treatEnumerablesAsCollections;
            public readonly int writeBufferChunkSize;
            public readonly int tempBufferSize;
            public readonly bool indent;
            public readonly int maxIndentationDepth;
            public readonly int indentationFactor;
            public readonly ITypeHandlerCreator[] itemHandlerCreators;

            public readonly bool requiresItemNames;
            public readonly bool requiresItemInfos;
            public readonly bool writeItemIds;
            public readonly bool writeByteArrayAsBase64String = false;

            /// <summary>Compiled type/generic-type settings map.</summary>
            public readonly Dictionary<Type, BaseTypeWriteSettings> typeSettingsDict;

            /// <summary>
            /// True if no type specific settings exist at all, which lets the hot paths skip
            /// every per-type lookup.
            /// </summary>
            public readonly bool hasTypeSettings;

            public CompiledSettings(Settings settings)
            {
                typeInfoHandling = settings.typeInfoHandling;
                dataSelection = settings.dataSelection;
                referenceCheck = settings.referenceCheck;
                referenceFormat = settings.referenceFormat;
                typeInfoFormat = settings.typeInfoFormat;
                skipTypeInfoEnvelope = settings.typeInfoFormat == TypeInfoFormat.OnlyInlineForObjects;
                arrayValueFieldName = settings.arrayValueFieldName;
                typeNameFormat = settings.typeNameFormat;
                genericTypeNameFormat = settings.genericTypeNameFormat ?? settings.typeNameFormat;                
                enumAsString = settings.enumAsString;
                treatEnumerablesAsCollections = settings.treatEnumerablesAsCollections;
                writeBufferChunkSize = settings.writeBufferChunkSize;
                tempBufferSize = settings.tempBufferSize;
                indent = settings.indent;
                maxIndentationDepth = settings.maxIndentationDepth;
                indentationFactor = settings.indentationFactor;
                itemHandlerCreators = settings.customTypeHandlerCreators.Where(creator => creator != null).ToArray();

                // Item names are only needed to build JSONPath ref values. The id based format
                // identifies objects by an explicit "$id" member, so the path is irrelevant there.
                requiresItemNames = referenceFormat == ReferenceFormat.JsonPath &&
                                    (referenceCheck == ReferenceCheck.AlwaysReplaceByRef || referenceCheck == ReferenceCheck.OnLoopReplaceByRef);
                requiresItemInfos = referenceCheck != ReferenceCheck.NoRefCheck;
                writeItemIds = referenceFormat == ReferenceFormat.IdBased &&
                               (referenceCheck == ReferenceCheck.AlwaysReplaceByRef || referenceCheck == ReferenceCheck.OnLoopReplaceByRef);
                writeByteArrayAsBase64String = settings.writeByteArrayAsBase64String;

                typeSettingsDict = new Dictionary<Type, BaseTypeWriteSettings>(settings.typeSettingsDict);
                // A constructed type is a more specific configuration scope, not a replacement for
                // its generic definition. Pre-merge configured constructed entries once so all
                // later lookups return a stable settings object suitable for writer cache keys.
                foreach (var entry in settings.typeSettingsDict)
                {
                    Type type = entry.Key;
                    if (!type.IsConstructedGenericType) continue;
                    if (!settings.typeSettingsDict.TryGetValue(type.GetGenericTypeDefinition(), out var genericSettings)) continue;
                    typeSettingsDict[type] = entry.Value.MergeOnto(genericSettings);
                }
                hasTypeSettings = typeSettingsDict.Count > 0;
            }

            /// <summary>
            /// Looks up the settings configured for <paramref name="type"/>. Falls back to the
            /// settings of the generic type definition if the constructed type has none.
            /// </summary>
            /// <param name="type">Type to resolve settings for.</param>
            /// <param name="typeSettings">Resolved settings, or <see langword="null"/>.</param>
            /// <returns><see langword="true"/> if settings were found.</returns>
            public bool TryGetTypeSettings(Type type, out BaseTypeWriteSettings typeSettings)
            {
                typeSettings = null;
                if (!hasTypeSettings) return false;

                if (typeSettingsDict.TryGetValue(type, out typeSettings)) return true;

                // A nullable value type shares its configuration with the underlying type, so
                // ConfigureType<MyStruct>() also applies to MyStruct? members.
                Type underlyingType = Nullable.GetUnderlyingType(type);
                if (underlyingType != null && typeSettingsDict.TryGetValue(underlyingType, out typeSettings)) return true;

                if (type.IsConstructedGenericType &&
                    typeSettingsDict.TryGetValue(type.GetGenericTypeDefinition(), out typeSettings)) return true;

                typeSettings = null;
                return false;
            }

            /// <summary>
            /// True if any custom writer applies to <paramref name="type"/>, either set for the
            /// type itself or matched by a convention based handler. Used by optimizations that
            /// must not bypass a custom writer.
            /// </summary>
            public bool HasCustomWriterFor(Type type)
            {
                if (TryGetTypeSettings(type, out BaseTypeWriteSettings typeSettings) &&
                    typeSettings.customTypeWriterCreator != null) return true;

                foreach (var creator in itemHandlerCreators)
                {
                    if (creator.SupportsType(type)) return true;
                }
                return false;
            }

            /// <summary>
            /// Resolves the member selection for <paramref name="typeSettings"/>, falling back to
            /// the global setting when the type does not override it.
            /// </summary>
            public DataSelection ResolveDataSelection(BaseTypeWriteSettings typeSettings)
                => typeSettings?.dataSelection ?? dataSelection;

            /// <summary>
            /// Resolves the type info handling from already resolved <paramref name="typeSettings"/>,
            /// falling back to the global setting.
            /// </summary>
            public TypeInfoHandling ResolveTypeInfoHandling(BaseTypeWriteSettings typeSettings)
                => typeSettings?.typeInfoHandling ?? typeInfoHandling;

            /// <summary>
            /// Resolves the type info layout from already resolved <paramref name="typeSettings"/>,
            /// falling back to the global setting.
            /// </summary>
            public TypeInfoFormat ResolveTypeInfoFormat(BaseTypeWriteSettings typeSettings)
                => typeSettings?.typeInfoFormat ?? typeInfoFormat;

            /// <summary>
            /// Resolves the payload field name used inside a type info envelope for arrays, from
            /// already resolved <paramref name="typeSettings"/>, falling back to the global setting.
            /// </summary>
            public ValueFieldName ResolveArrayValueFieldName(BaseTypeWriteSettings typeSettings)
                => typeSettings?.arrayValueFieldName ?? arrayValueFieldName;

            /// <summary>
            /// Resolves the enum representation from already resolved <paramref name="typeSettings"/>,
            /// falling back to the global setting.
            /// </summary>
            public bool ResolveEnumAsString(BaseTypeWriteSettings typeSettings)
                => typeSettings?.enumAsString ?? enumAsString;

            /// <summary>
            /// Resolves the byte array representation from already resolved
            /// <paramref name="typeSettings"/>, falling back to the global setting.
            /// </summary>
            public bool ResolveWriteByteArrayAsBase64String(BaseTypeWriteSettings typeSettings)
                => typeSettings?.writeByteArrayAsBase64String ?? writeByteArrayAsBase64String;

            /// <summary>
            /// Resolves the enumerable handling from already resolved <paramref name="typeSettings"/>,
            /// falling back to the global setting.
            /// </summary>
            public bool ResolveTreatEnumerablesAsCollections(BaseTypeWriteSettings typeSettings)
                => typeSettings?.treatEnumerablesAsCollections ?? treatEnumerablesAsCollections;

            /// <summary>
            /// Returns the custom type name configured for exactly this type, or
            /// <see langword="null"/> if there is none.
            /// </summary>
            /// <remarks>
            /// Deliberately uses an exact type match instead of <see cref="TryGetTypeSettings"/>:
            /// a name is a literal, so it must not be inherited from the generic type definition
            /// or from the underlying type of a nullable, which would make several types share it.
            /// </remarks>
            public bool TryGetCustomTypeName(Type type, out string customTypeName)
            {
                customTypeName = null;
                if (!hasTypeSettings) return false;

                if (typeSettingsDict.TryGetValue(type, out var typeSettings)) customTypeName = typeSettings.customTypeName;
                return customTypeName != null;
            }

        }

    }

    
}
