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
            /// Custom handlers that take precedence over the built-in ones. The first creator that
            /// supports a type determines how values of that type are written.
            /// Use the AddCustomTypeHandlerCreator overloads to add entries.
            /// </summary>
            public List<ITypeHandlerCreator> customTypeHandlerCreators = new List<ITypeHandlerCreator>();

            /// <summary>
            /// Determines how type names are written into "$type" members, unless a custom name was
            /// registered via <see cref="AddCustomTypeName(Type, string)"/> or set per type via
            /// <see cref="BaseTypeWriteSettings.SetCustomTypeName(string)"/> for the specific type.
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

            internal Dictionary<Type, string> customTypeNames = new();

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
            /// Adds or replaces a custom type name, which is written instead of the name that
            /// <see cref="typeNameFormat"/> would produce. Custom names take precedence over every
            /// other naming option, including <see cref="genericTypeNameFormat"/>. Only a per-type
            /// name set via <see cref="BaseTypeWriteSettings.SetCustomTypeName(string)"/> wins over
            /// an entry registered here.
            /// <para>
            /// Note that the JsonDeserializer keeps its own, independent name-to-type mapping. To
            /// read such JSON back, register the counterpart there via its AddCustomTypeName method.
            /// </para>
            /// </summary>
            /// <param name="type">The type to write the custom name for.</param>
            /// <param name="customTypeName">The name to write into the "$type" member.</param>
            public void AddCustomTypeName(Type type, string customTypeName)
            {
                if (type == null || customTypeName.EmptyOrNull()) return;
                customTypeNames[type] = customTypeName;
            }

            /// <summary>
            /// Adds or replaces a custom type name for <typeparamref name="T"/>.
            /// See <see cref="AddCustomTypeName(Type, string)"/>.
            /// </summary>
            public void AddCustomTypeName<T>(string customTypeName) => AddCustomTypeName(typeof(T), customTypeName);

            /// <summary>
            /// Removes all custom type name mappings.
            /// </summary>
            public void ClearCustomTypeNames() => customTypeNames.Clear();

            /// <summary>
            /// Adds a custom handler that writes values of <typeparamref name="T"/> instead of the
            /// built-in handler.
            /// </summary>
            /// <typeparam name="T">Type the handler is created for.</typeparam>
            /// <param name="category">
            /// How the written value is shaped, which the serializer needs to know to wrap it
            /// correctly, e.g. in type info objects.
            /// </param>
            /// <param name="creator">
            /// Builds the write action. It is called once per type and receives the extension API
            /// to write the value with.
            /// </param>
            /// <param name="onlyExactType">
            /// If true, the handler is only used for <typeparamref name="T"/> itself.
            /// If false, it is also used for types assignable to it.
            /// </param>
            public void AddCustomTypeHandlerCreator<T>(JsonDataTypeCategory category, Func<ExtensionApi, Action<T>> creator, bool onlyExactType = true)
            {
                customTypeHandlerCreators.Add(new TypeHandlerCreator<T>(category, creator, onlyExactType));
            }

            /// <summary>
            /// Adds a custom handler that writes values of <typeparamref name="T"/>, using an
            /// explicit predicate to decide which types it applies to.
            /// </summary>
            /// <typeparam name="T">Type the write action accepts.</typeparam>
            /// <param name="supportsType">Decides whether the handler is used for a given type.</param>
            /// <param name="category">
            /// How the written value is shaped, which the serializer needs to know to wrap it
            /// correctly, e.g. in type info objects.
            /// </param>
            /// <param name="creator">Builds the write action. It is called once per type.</param>
            public void AddCustomTypeHandlerCreator<T>(Func<Type, bool> supportsType, JsonDataTypeCategory category, Func<ExtensionApi, Action<T>> creator)
            {
                customTypeHandlerCreators.Add(new TypeHandlerCreator<T>(category, creator, supportsType));
            }

            /// <summary>
            /// Adds a fully custom handler creator implementation.
            /// </summary>
            /// <param name="creator">
            /// Decides which types it supports and builds their write action.
            /// </param>
            public void AddCustomTypeHandlerCreator(ITypeHandlerCreator creator)
            {
                customTypeHandlerCreators.Add(creator);
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
            /// Sets the name written into the "$type" member for this type, overruling both
            /// <see cref="Settings.typeNameFormat"/> and a globally registered custom name.
            /// </summary>
            /// <remarks>
            /// <para>
            /// Only supported on a concrete type, i.e. via <see cref="Settings.ConfigureType{T}"/>.
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
            private protected void ConfigureMemberInternal<TMember>(Type objType, string memberName, Action<MemberWriteSettings<TMember>> configureMemberSettings)
            {
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

        private readonly struct CompiledSettings
        {
            public readonly TypeInfoHandling typeInfoHandling;
            public readonly DataSelection dataSelection;
            public readonly ReferenceCheck referenceCheck;
            public readonly ReferenceFormat referenceFormat;
            public readonly TypeNameFormat typeNameFormat;
            public readonly TypeNameFormat genericTypeNameFormat;
            public readonly Dictionary<Type, string> customTypeNames;
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
                typeNameFormat = settings.typeNameFormat;
                genericTypeNameFormat = settings.genericTypeNameFormat ?? settings.typeNameFormat;
                customTypeNames = settings.customTypeNames.Count > 0 ? new Dictionary<Type, string>(settings.customTypeNames) : null;
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
            /// Resolves the member selection for <paramref name="typeSettings"/>, falling back to
            /// the global setting when the type does not override it.
            /// </summary>
            public DataSelection ResolveDataSelection(BaseTypeWriteSettings typeSettings)
                => typeSettings?.dataSelection ?? dataSelection;

            /// <summary>
            /// Resolves the type info handling for <paramref name="type"/>, falling back to the
            /// global setting when the type does not override it.
            /// </summary>
            public TypeInfoHandling ResolveTypeInfoHandling(Type type)
                => TryGetTypeSettings(type, out var typeSettings) ? typeSettings.typeInfoHandling ?? typeInfoHandling : typeInfoHandling;

            /// <summary>
            /// Resolves the enum representation for <paramref name="type"/>, falling back to the
            /// global setting when the type does not override it.
            /// </summary>
            public bool ResolveEnumAsString(Type type)
                => TryGetTypeSettings(type, out var typeSettings) ? typeSettings.enumAsString ?? enumAsString : enumAsString;

            /// <summary>
            /// Resolves the byte array representation for <paramref name="type"/>, falling back to
            /// the global setting when the type does not override it.
            /// </summary>
            public bool ResolveWriteByteArrayAsBase64String(Type type)
                => TryGetTypeSettings(type, out var typeSettings) ? typeSettings.writeByteArrayAsBase64String ?? writeByteArrayAsBase64String : writeByteArrayAsBase64String;

            /// <summary>
            /// Resolves the enumerable handling for <paramref name="type"/>, falling back to the
            /// global setting when the type does not override it.
            /// </summary>
            public bool ResolveTreatEnumerablesAsCollections(Type type)
                => TryGetTypeSettings(type, out var typeSettings) ? typeSettings.treatEnumerablesAsCollections ?? treatEnumerablesAsCollections : treatEnumerablesAsCollections;

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
