using FeatureLoom.Collections;
using FeatureLoom.Extensions;
using FeatureLoom.Helpers;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using static FeatureLoom.Serialization.JsonDeserializer;
using static FeatureLoom.Serialization.JsonDeserializer.Settings;

namespace FeatureLoom.Serialization;

/// <summary>
/// JSON deserializer implementation.
/// </summary>
public sealed partial class JsonDeserializer
{
    /// <summary>
    /// Defines global and type-specific deserialization behavior.
    /// </summary>
    /// <remarks>
    /// This class combines:
    /// <list type="bullet">
    /// <item><description>Global toggles (strictness, buffering, caching, reference/proposed-type handling).</description></item>
    /// <item><description>Security controls (forbidden types and whitelist policies).</description></item>
    /// <item><description>Per-type and per-member overrides via <see cref="ConfigureType{T}(Action{TypeSettings{T}})"/> and <see cref="ConfigureGenericType(Type, Action{GenericTypeSettings})"/>.</description></item>
    /// </list>
    /// </remarks>
    public class Settings
    {
        /// <summary>
        /// Stores explicit type and generic-type configuration entries.
        /// </summary>
        internal Dictionary<Type, BaseTypeSettings> typeSettingsDict = new();

        /// <summary>
        /// Controls which fields/properties are considered during read/write access.
        /// </summary>
        public DataAccess dataAccess = DataAccess.PublicAndPrivateFields;

        /// <summary>
        /// Maps custom type name aliases (for example from <c>$type</c>) to CLR types.
        /// </summary>
        internal Dictionary<string, Type> customTypeNames = new();

        /// <summary>
        /// Stores types that are blocked from type-based materialization.
        /// </summary>
        internal HashSet<Type> forbiddenTypes = new();

        /// <summary>
        /// Controls when reference tracking and <c>$ref</c>-based resolution are active.
        /// </summary>
        public ReferenceResolutionMode referenceResolutionMode = ReferenceResolutionMode.DisabledByDefault;

        /// <summary>
        /// Controls when proposed runtime types (for example from <c>$type</c>) are evaluated.
        /// </summary>
        public ProposedTypeMode proposedTypeMode = ProposedTypeMode.CheckWhereReasonable;

        /// <summary>
        /// Controls how compiler backing fields are matched during member lookup.
        /// </summary>
        public BackingFieldMode backingFieldMode = BackingFieldMode.TryBothNames;

        /// <summary>
        /// If <see langword="true"/>, lower/upper-case variants are also inserted into proposed-type caches
        /// when custom type names are loaded.
        /// </summary>
        public bool addCaseVariantsForCustomTypeNames = false;

        /// <summary>
        /// Initial parser buffer size in bytes.
        /// </summary>
        /// <remarks>
        /// During compilation this value is clamped to at least 16 KB. The default is 128 KB, but it may grow when needed.
        /// </remarks>
        public int initialBufferSize = 1024 * 128;

        /// <summary>
        /// If <see langword="true"/>, object arrays (object[]) may be converted to a common typed array when possible.
        /// </summary>
        public bool castObjectArrayToCommonTypeArray = true;

        /// <summary>
        /// If <see langword="true"/>, exceptions are rethrown after handling, otherwise the deserialization simply fails and returns false.
        /// </summary>
        public bool rethrowExceptions = false;

        /// <summary>
        /// If <see langword="true"/>, caught exceptions are logged.
        /// </summary>
        public bool logCatchedExceptions = true;

        /// <summary>
        /// Enables stricter parsing/validation behavior.
        /// </summary>
        public bool strict = false;

        /// <summary>
        /// If <see langword="true"/>, existing member instances may be populated instead of always replaced.        
        /// </summary>
        /// <remarks>
        /// This only affects normal deserialization behaviour, but not explicit population via TryPopulate().
        /// </remarks>
        public bool populateExistingMembers = true;

        /// <summary>
        /// Enables the internal string cache optimization.
        /// </summary>
        public bool useStringCache = true;

        /// <summary>
        /// Bit size of the string cache (entry count is 2^N).
        /// </summary>
        /// <example>
        /// Value 12 results in 4096 cache slots.
        /// </example>
        public int stringCacheBitSize = 12; // 4096 entries

        /// <summary>
        /// Maximum string length eligible for string-cache insertion.
        /// </summary>
        public int stringCacheMaxLength = 128;

        /// <summary>
        /// If <see langword="true"/>, object creation may use uninitialized-instance paths. 
        /// This is only possible if dataAccess is set to DataAccess.PublicAndPrivateFields.
        /// </summary>
        public bool allowUninitializedObjectCreation = false;

        /// <summary>
        /// Controls whether type-whitelist checks are disabled or enforced.
        /// </summary>
        public TypeWhitelistMode typeWhitelistMode = TypeWhitelistMode.Disabled;

        /// <summary>
        /// Explicitly allowed types for whitelist checks.
        /// </summary>
        internal HashSet<Type> allowedTypes = new();

        /// <summary>
        /// Allowed namespace prefixes for whitelist checks (ordinal comparison).
        /// </summary>
        internal HashSet<string> allowedNamespacePrefixes = new(StringComparer.Ordinal);

        /// <summary>
        /// Creates a settings instance with default mappings, forbidden-type list, and common type-name aliases.
        /// </summary>
        public Settings()
        {
            ConfigureType<IEnumerable>(ts => ts.SetInstanceTypeMapping<List<object>>());
            ConfigureType<ICollection>(ts => ts.SetInstanceTypeMapping<List<object>>());
            ConfigureType<IList>(ts => ts.SetInstanceTypeMapping<List<object>>());

            ConfigureGenericType(typeof(IEnumerable<>), ts => ts.SetInstanceTypeMapping(typeof(List<>)));
            ConfigureGenericType(typeof(ICollection<>), ts => ts.SetInstanceTypeMapping(typeof(List<>)));
            ConfigureGenericType(typeof(IReadOnlyCollection<>), ts => ts.SetInstanceTypeMapping(typeof(List<>)));
            ConfigureGenericType(typeof(IList<>), ts => ts.SetInstanceTypeMapping(typeof(List<>)));
            ConfigureGenericType(typeof(IReadOnlyList<>), ts => ts.SetInstanceTypeMapping(typeof(List<>)));
            ConfigureGenericType(typeof(IDictionary<,>), ts => ts.SetInstanceTypeMapping(typeof(Dictionary<,>)));
            ConfigureGenericType(typeof(IReadOnlyDictionary<,>), ts => ts.SetInstanceTypeMapping(typeof(Dictionary<,>)));
            ConfigureGenericType(typeof(ISet<>), ts => ts.SetInstanceTypeMapping(typeof(HashSet<>)));
            ConfigureGenericType(typeof(IProducerConsumerCollection<>), ts => ts.SetInstanceTypeMapping(typeof(ConcurrentQueue<>)));

            AddDefaultForbiddenTypes();

            AddDefaultCustomTypeNames();
            AddCSharpKeywordTypeNames();
            AddCommonCrossLanguageTypeNames();
        }

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
        /// Adds a defensive default set of forbidden runtime/OS/reflection/delegate related types.
        /// </summary>
        private void AddDefaultForbiddenTypes()
        {
            // Reflection / runtime metadata
            AddForbiddenType(typeof(Type));
            AddForbiddenType(typeof(System.Reflection.Assembly));
            AddForbiddenType(typeof(System.Reflection.Module));
            AddForbiddenType(typeof(System.Reflection.MemberInfo));
            AddForbiddenType(typeof(System.Reflection.MethodInfo));
            AddForbiddenType(typeof(System.Reflection.ConstructorInfo));
            AddForbiddenType(typeof(System.Reflection.FieldInfo));
            AddForbiddenType(typeof(System.Reflection.PropertyInfo));
            AddForbiddenType(typeof(System.Reflection.EventInfo));

            // Delegate / expression trees
            AddForbiddenType(typeof(Delegate));
            AddForbiddenType(typeof(MulticastDelegate));
            AddForbiddenType(typeof(System.Linq.Expressions.Expression));
            AddForbiddenType(typeof(System.Linq.Expressions.LambdaExpression));
            AddForbiddenType(typeof(System.Linq.Expressions.Expression<>)); // generic definition

            // Process / OS interaction
            AddForbiddenType(typeof(System.Diagnostics.Process));
            AddForbiddenType(typeof(System.Diagnostics.ProcessStartInfo));

            // File system handles/abstractions
            AddForbiddenType(typeof(System.IO.FileSystemInfo));
            AddForbiddenType(typeof(System.IO.FileInfo));
            AddForbiddenType(typeof(System.IO.DirectoryInfo));
            AddForbiddenType(typeof(System.IO.DriveInfo));

            // Threading primitives
            AddForbiddenType(typeof(System.Threading.Thread));
        }

        /// <summary>
        /// Adds short CLR type-name aliases (for example <c>Int32</c>, <c>String</c>) to the custom type-name map.
        /// </summary>
        public void AddDefaultCustomTypeNames()
        {
            // existing short CLR names
            AddCustomTypeName(typeof(string).Name, typeof(string));
            AddCustomTypeName(typeof(long).Name, typeof(long));
            AddCustomTypeName(typeof(ulong).Name, typeof(ulong));
            AddCustomTypeName(typeof(int).Name, typeof(int));
            AddCustomTypeName(typeof(uint).Name, typeof(uint));
            AddCustomTypeName(typeof(short).Name, typeof(short));
            AddCustomTypeName(typeof(ushort).Name, typeof(ushort));
            AddCustomTypeName(typeof(byte).Name, typeof(byte));
            AddCustomTypeName(typeof(sbyte).Name, typeof(sbyte));
            AddCustomTypeName(typeof(bool).Name, typeof(bool));
            AddCustomTypeName(typeof(char).Name, typeof(char));
            AddCustomTypeName(typeof(float).Name, typeof(float));
            AddCustomTypeName(typeof(double).Name, typeof(double));
            AddCustomTypeName(typeof(decimal).Name, typeof(decimal));
            AddCustomTypeName(typeof(DateTime).Name, typeof(DateTime));
            AddCustomTypeName(typeof(TimeSpan).Name, typeof(TimeSpan));
            AddCustomTypeName(typeof(Guid).Name, typeof(Guid));
            AddCustomTypeName(typeof(DateTimeOffset).Name, typeof(DateTimeOffset));
            AddCustomTypeName(typeof(Uri).Name, typeof(Uri));
            AddCustomTypeName(typeof(byte[]).Name, typeof(byte[]));
        }

        /// <summary>
        /// Adds C# keyword aliases (for example <c>int</c>, <c>string</c>) to the custom type-name map.
        /// </summary>
        public void AddCSharpKeywordTypeNames()
        {
            AddCustomTypeName("string", typeof(string));
            AddCustomTypeName("long", typeof(long));
            AddCustomTypeName("ulong", typeof(ulong));
            AddCustomTypeName("int", typeof(int));
            AddCustomTypeName("uint", typeof(uint));
            AddCustomTypeName("short", typeof(short));
            AddCustomTypeName("ushort", typeof(ushort));
            AddCustomTypeName("byte", typeof(byte));
            AddCustomTypeName("sbyte", typeof(sbyte));
            AddCustomTypeName("bool", typeof(bool));
            AddCustomTypeName("char", typeof(char));
            AddCustomTypeName("float", typeof(float));
            AddCustomTypeName("double", typeof(double));
            AddCustomTypeName("decimal", typeof(decimal));
        }

        /// <summary>
        /// Adds common non-language-specific aliases used in JSON or cross-language systems.
        /// </summary>
        public void AddCommonCrossLanguageTypeNames()
        {
            AddCustomTypeName("boolean", typeof(bool));
            AddCustomTypeName("datetime", typeof(DateTime));
            AddCustomTypeName("timestamp", typeof(DateTimeOffset));
            AddCustomTypeName("duration", typeof(TimeSpan));
            AddCustomTypeName("uuid", typeof(Guid));
            AddCustomTypeName("uri", typeof(Uri));
            AddCustomTypeName("bytes", typeof(byte[]));
            AddCustomTypeName("binary", typeof(byte[]));
        }

        /// <summary>
        /// Removes all custom type-name mappings.
        /// </summary>
        public void ClearCustomTypeNames() => customTypeNames.Clear();

        /// <summary>
        /// Adds or replaces a custom type-name mapping.
        /// </summary>
        /// <param name="customTypeName">Alias text as it appears in payload type metadata.</param>
        /// <param name="type">Target CLR type for the alias.</param>
        public void AddCustomTypeName(string customTypeName, Type type)
        {
            customTypeNames[customTypeName] = type;
        }

        /// <summary>
        /// Adds a type to the forbidden-type set.
        /// </summary>
        /// <param name="type">Type to block from type-based materialization.</param>
        public void AddForbiddenType(Type type)
        {
            forbiddenTypes.Add(type);
        }

        /// <summary>
        /// Clears all forbidden-type entries.
        /// </summary>
        public void ClearForbiddenTypes() => forbiddenTypes.Clear();

        /// <summary>
        /// Adds a type to the whitelist set.
        /// </summary>
        /// <param name="type">Allowed type.</param>
        public void AddAllowedType(Type type) => allowedTypes.Add(type);

        /// <summary>
        /// Adds a type to the whitelist set.
        /// </summary>
        /// <typeparam name="T">Allowed type.</typeparam>
        public void AddAllowedType<T>() => allowedTypes.Add(typeof(T));

        /// <summary>
        /// Adds a namespace prefix to the whitelist prefix set.
        /// </summary>
        /// <param name="prefix">Namespace prefix checked with ordinal comparison.</param>
        public void AddAllowedNamespacePrefix(string prefix)
        {
            if (!string.IsNullOrWhiteSpace(prefix)) allowedNamespacePrefixes.Add(prefix);
        }

        /// <summary>
        /// Configures settings for a concrete type.
        /// </summary>
        /// <typeparam name="T">Configured target type.</typeparam>
        /// <param name="configureTypeSettings">
        /// Callback that mutates or creates a <see cref="TypeSettings{T}"/>.
        /// If <see langword="null"/>, the type configuration is removed.
        /// </param>
        public void ConfigureType<T>(Action<TypeSettings<T>> configureTypeSettings)
        {
            Type type = typeof(T);
            if (configureTypeSettings == null)
            {
                typeSettingsDict.Remove(type);
                return;
            }

            if (typeSettingsDict.TryGetValue(type, out BaseTypeSettings existingSettings) &&
                existingSettings is TypeSettings<T> typeSettings)
            {
                configureTypeSettings(typeSettings);
                typeSettingsDict[type] = typeSettings;
            }
            else
            {
                typeSettings = new TypeSettings<T>();
                configureTypeSettings(typeSettings);
                typeSettingsDict[type] = typeSettings;
            }
        }

        /// <summary>
        /// Configures settings for a generic type definition.
        /// </summary>
        /// <param name="genericTypeDefinition">Generic type definition (for example <c>typeof(IEnumerable&lt;&gt;)</c>).</param>
        /// <param name="configureTypeSettings">
        /// Callback that mutates or creates a <see cref="GenericTypeSettings"/>.
        /// If <see langword="null"/>, the type configuration is removed.
        /// </param>
        public void ConfigureGenericType(Type genericTypeDefinition, Action<GenericTypeSettings> configureTypeSettings)
        {
            if (configureTypeSettings == null)
            {
                typeSettingsDict.Remove(genericTypeDefinition);
                return;
            }

            if (typeSettingsDict.TryGetValue(genericTypeDefinition, out BaseTypeSettings existingSettings) &&
                existingSettings is GenericTypeSettings typeSettings)
            {
                configureTypeSettings(typeSettings);
                typeSettingsDict[genericTypeDefinition] = typeSettings;
                return;
            }
            else
            {
                typeSettings = new GenericTypeSettings(genericTypeDefinition);
                configureTypeSettings(typeSettings);
                typeSettingsDict[genericTypeDefinition] = typeSettings;
            }
        }

        /// <summary>
        /// Controls whitelist enforcement level.
        /// </summary>
        public enum TypeWhitelistMode
        {
            /// <summary>No whitelist checks are performed.</summary>
            Disabled = 0,

            /// <summary>Whitelist checks apply only to proposed payload types.</summary>
            ForProposedTypesOnly = 1,

            /// <summary>Whitelist checks apply to all non-intrinsic types.</summary>
            ForAllNonIntrinsicTypes = 2
        }

        /// <summary>
        /// Controls how payload-proposed types are processed.
        /// </summary>
        public enum ProposedTypeMode
        {
            /// <summary>
            /// Proposed types are ignored entirely.
            /// This is the safest and typically fastest mode, but disables polymorphic type selection from payload metadata.
            /// </summary>
            Ignore = 0,

            /// <summary>
            /// Proposed types are used only in situations where they are meaningful (for example interface/abstract/base targets).
            /// </summary>
            CheckWhereReasonable = 1,

            /// <summary>
            /// Proposed types are always evaluated.
            /// This provides maximum flexibility but with higher overhead.
            /// </summary>
            CheckAlways = 2
        }

        public enum ReferenceResolutionMode
        {
            /// <summary>
            /// Disables reference resolution completely. Even if enabled in TypeSettings it will be ignored.
            /// This reduces the performance overhead and should be used if the input JSON is not expected to contain any reference information (e.g. no $ref properties).
            /// </summary>
            ForceDisabled = 0,
            /// <summary>
            /// Enables reference resolution only for types where it is explicitly enabled in TypeSettings. 
            /// For other types, reference information in the JSON will be ignored.
            /// This can be a good option to limit the performance overhead of reference resolution to only the types where it is actually needed, 
            /// while still allowing for reference resolution in those cases.
            /// </summary>
            DisabledByDefault = 1,
            /// <summary>
            /// Indicates that the feature is enabled by default for all reference types, but can be disabled for specific types in TypeSettings.
            /// Performance overhead is higher but good as a starting point if you expect reference information in the JSON but are not sure for which types, 
            /// as it allows you to enable reference resolution for all reference types and then disable it for specific types where it is not needed to optimize performance.
            /// Strings are not included for performance reasons, though.
            /// </summary>
            EnabledByDefault = 2,
        }

        /// <summary>
        /// Controls how property/backing-field name probing is performed.
        /// </summary>
        public enum BackingFieldMode
        {
            /// <summary>Try both property name and compiler backing-field conventions.</summary>
            TryBothNames = 0,

            /// <summary>Try only backing-field naming conventions.</summary>
            TryBackingFieldNameOnly = 1,

            /// <summary>Try only property names.</summary>
            TryPropertyNameOnly = 2
        }
    }

    /// <summary>
    /// Represents one mapped destination type plus optional nested settings for that mapped type.
    /// </summary>
    internal readonly struct MappedType
    {
        /// <summary>
        /// Destination CLR type used for instantiation.
        /// </summary>
        readonly public Type type;

        /// <summary>
        /// Optional additional settings scoped to <see cref="type"/>.
        /// </summary>
        readonly public BaseTypeSettings typeSettings;

        /// <summary>
        /// Initializes a mapped-type entry.
        /// </summary>
        /// <param name="type">Destination type.</param>
        /// <param name="typeSettings">Optional nested settings for destination type handling.</param>
        public MappedType(Type type, BaseTypeSettings typeSettings)
        {
            this.type = type;
            this.typeSettings = typeSettings;
        }
    }

    /// <summary>
    /// Shared storage for type/member settings consumed by the deserializer pipeline.
    /// </summary>
    public class BaseTypeSettings
    {
        /// <summary>Single mapped destination type.</summary>
        internal MappedType? mappedType;

        /// <summary>Multiple candidate mapped destination types.</summary>
        internal LazyList<MappedType> multiOptionMappedTypes;

        /// <summary>Member-level ignore flag override.</summary>
        internal bool? member_ignore = null;

        /// <summary>Member-level alternate name override.</summary>
        internal string member_overrideName = null;

        /// <summary>Member-level string-cache usage override.</summary>
        internal bool? member_useStringCache = null;

        /// <summary>Type/member-level data-access override.</summary>
        internal DataAccess? dataAccess = null;

        /// <summary>Type/member-level backing-field mode override.</summary>
        internal BackingFieldMode? backingFieldMode = null;

        /// <summary>Type/member-level reference-resolution override.</summary>
        internal bool? enableReferenceResolution = null;

        /// <summary>Type/member-level proposed-type handling override.</summary>
        internal bool? applyProposedTypes = null;

        /// <summary>Type/member-level populate-existing-members behavior override.</summary>
        internal bool? populateAsMember = null;

        /// <summary>Custom constructor delegate.</summary>
        internal Delegate constructor = null;

        /// <summary>Custom collection constructor delegate.</summary>
        internal Delegate collectionConstructor = null;

        /// <summary>Custom reader implementation/object.</summary>
        internal object customTypeReader = null;

        /// <summary>Per-member configuration map by member name.</summary>
        internal LazyDictionary<string, BaseTypeSettings> memberSettingsDict = default;
    }

    /// <summary>
    /// Settings for generic type definitions.
    /// </summary>
    public class GenericTypeSettings : BaseTypeSettings
    {
        /// <summary>
        /// Generic type definition this setting entry applies to.
        /// </summary>
        protected Type genericType;

        /// <summary>
        /// Creates settings for a generic type definition.
        /// </summary>
        /// <param name="genericType">Generic type definition.</param>
        public GenericTypeSettings(Type genericType)
        {
            this.genericType = genericType;
        }

        /// <summary>Sets data-access behavior.</summary>
        public void SetDataAccess(DataAccess dataAccess) => this.dataAccess = dataAccess;

        /// <summary>Enables or disables reference resolution for this type scope.</summary>
        public void SetReferenceResolution(bool enable) => this.enableReferenceResolution = enable;

        /// <summary>Enables or disables proposed-type usage for this type scope.</summary>
        public void SetProposedTypeHandling(bool applyProposedTypes) => this.applyProposedTypes = applyProposedTypes;

        /// <summary>Enables or disables populate-existing-member behavior for this type scope.</summary>
        public void SetPopulateAsMember(bool populate) => populateAsMember = populate;

        /// <summary>Sets backing-field lookup mode for this type scope.</summary>
        public void SetBackingFieldMode(BackingFieldMode mode) => this.backingFieldMode = mode;

        /// <summary>
        /// Configures settings for one member on the configured generic type definition.
        /// </summary>
        /// <typeparam name="TMember">Expected member type.</typeparam>
        /// <param name="memberName">Member name.</param>
        /// <param name="configureMemberSettings">
        /// Callback to configure member settings.
        /// If <see langword="null"/>, existing member settings are removed.
        /// </param>
        /// <exception cref="Exception">
        /// Thrown if the member is not found or its runtime type does not match <typeparamref name="TMember"/>.
        /// </exception>
        public void ConfigureMember<TMember>(string memberName, Action<MemberSettings<TMember>> configureMemberSettings)
        {
            if (configureMemberSettings == null)
            {
                memberSettingsDict.Remove(memberName);
                return;
            }

            Type objType = genericType;
            Type memberType = typeof(TMember);
            if (!objType.GetMember(memberName)
                .TryFindFirst(member => member.MemberType == MemberTypes.Field || member.MemberType == MemberTypes.Property,
                out MemberInfo member))
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

            var memberSettings = new MemberSettings<TMember>();
            configureMemberSettings(memberSettings);
            memberSettingsDict[memberName] = memberSettings;
        }

        /// <summary>
        /// Sets a single generic instance type mapping.
        /// </summary>
        /// <param name="genericInstanceTypeDefinition">Mapped generic instance type definition.</param>
        /// <param name="configureGenericInstanceTypeSettings">Optional nested configuration for mapped type behavior.</param>
        /// <exception cref="Exception">Thrown if the mapping is not assignable/compatible.</exception>
        public void SetInstanceTypeMapping(Type genericInstanceTypeDefinition, Action<GenericTypeSettings> configureGenericInstanceTypeSettings = null)
        {
            this.multiOptionMappedTypes.Clear(); // clear multi option mappings if they exist, as we are now adding a single mapping

            Type type = this.genericType;
            if (!genericInstanceTypeDefinition.IsOfGenericType(type)) throw new Exception($"{TypeNameHelper.Shared.GetSimplifiedTypeName(type)} is not implemented by {TypeNameHelper.Shared.GetSimplifiedTypeName(genericInstanceTypeDefinition)}");
            GenericTypeSettings typeSettings = null;
            if (configureGenericInstanceTypeSettings != null)
            {
                typeSettings = new GenericTypeSettings(genericInstanceTypeDefinition);
                configureGenericInstanceTypeSettings(typeSettings);
            }
            this.mappedType = new MappedType(genericInstanceTypeDefinition, typeSettings);
        }
    }

    /// <summary>
    /// Settings for one concrete type.
    /// </summary>
    /// <typeparam name="T">Configured type.</typeparam>
    public class TypeSettings<T> : BaseTypeSettings
    {
        /// <summary>Sets data-access behavior.</summary>
        public void SetDataAccess(DataAccess dataAccess) => this.dataAccess = dataAccess;

        /// <summary>Enables or disables reference resolution for this type.</summary>
        public void SetReferenceResolution(bool enable) => this.enableReferenceResolution = enable;

        /// <summary>Enables or disables proposed-type usage for this type.</summary>
        public void SetProposedTypeHandling(bool applyProposedTypes) => this.applyProposedTypes = applyProposedTypes;

        /// <summary>Enables or disables populate-existing-member behavior for this type.</summary>
        public void SetPopulateAsMember(bool populate) => populateAsMember = populate;

        /// <summary>Sets backing-field lookup mode for this type.</summary>
        public void SetBackingFieldMode(BackingFieldMode mode) => this.backingFieldMode = mode;

        /// <summary>
        /// Sets a custom constructor for <typeparamref name="T"/>.
        /// </summary>
        /// <param name="constructor">Factory delegate used to create instances.</param>
        public void AddConstructor(Func<T> constructor) => this.constructor = constructor;

        /// <summary>
        /// Sets a typed collection constructor for collection-like target types.
        /// </summary>
        /// <typeparam name="TElem">Element type consumed by the constructor input enumerable.</typeparam>
        /// <param name="constructor">Delegate creating <typeparamref name="T"/> from typed items.</param>
        /// <exception cref="Exception">Thrown if <typeparamref name="T"/> is not assignable to <see cref="IEnumerable{T}"/> of <typeparamref name="TElem"/>.</exception>
        public void AddCollectionConstructor<TElem>(Func<IEnumerable<TElem>, T> constructor)
        {
            Type type = typeof(T);
            Type elemType = typeof(TElem);
            if (!type.IsAssignableTo(typeof(IEnumerable<TElem>)))
            {
                throw new Exception($"The provided collection constructor is not valid for type {TypeNameHelper.Shared.GetSimplifiedTypeName(type)} and element type {TypeNameHelper.Shared.GetSimplifiedTypeName(elemType)}");
            }

            collectionConstructor = constructor;
        }

        /// <summary>
        /// Sets an untyped collection constructor for collection-like target types.
        /// </summary>
        /// <param name="constructor">Delegate creating <typeparamref name="T"/> from untyped items.</param>
        /// <exception cref="Exception">Thrown if <typeparamref name="T"/> is not assignable to <see cref="IEnumerable"/>.</exception>
        public void AddUntypedCollectionConstructor(Func<IEnumerable, T> constructor)
        {
            Type type = typeof(T);
            if (!type.IsAssignableTo(typeof(IEnumerable)))
            {
                throw new Exception($"The provided collection constructor is not valid for type {TypeNameHelper.Shared.GetSimplifiedTypeName(type)}");
            }

            collectionConstructor = constructor;
        }

        /// <summary>
        /// Assigns a custom reader implementation for this type.
        /// </summary>
        public void SetCustomTypeReader(ICustomTypeReader<T> customTypeReader) => this.customTypeReader = customTypeReader;

        /// <summary>
        /// Assigns a custom populate delegate and wraps it in <see cref="CustomTypeReader{T}"/>.
        /// </summary>
        public void SetCustomTypeReader(Func<ExtensionApi, T, T> populateType) => SetCustomTypeReader(new CustomTypeReader<T>(populateType));

        /// <summary>
        /// Assigns a custom read delegate and wraps it in <see cref="CustomTypeReader{T}"/>.
        /// </summary>
        public void SetCustomTypeReader(Func<ExtensionApi, T> readType) => SetCustomTypeReader(new CustomTypeReader<T>(readType));

        /// <summary>
        /// Assigns a custom reader-preparation delegate and wraps it in <see cref="CustomTypeReader{T}"/>.
        /// </summary>
        public void SetCustomTypeReader(Func<PreparationApi, Func<ExtensionApi, T, T>> readTypeCreator) => SetCustomTypeReader(new CustomTypeReader<T>(readTypeCreator));

        /// <summary>
        /// Configures settings for one member on <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="TMember">Expected member type.</typeparam>
        /// <param name="memberName">Member name.</param>
        /// <param name="configureMemberSettings">
        /// Callback to configure member settings.
        /// If <see langword="null"/>, existing member settings are removed.
        /// </param>
        /// <exception cref="Exception">
        /// Thrown if the member is not found or its runtime type does not match <typeparamref name="TMember"/>.
        /// </exception>
        public void ConfigureMember<TMember>(string memberName, Action<MemberSettings<TMember>> configureMemberSettings)
        {
            if (configureMemberSettings == null)
            {
                memberSettingsDict.Remove(memberName);
                return;
            }

            Type objType = typeof(T);
            Type memberType = typeof(TMember);
            if (!objType.GetMember(memberName)
                .TryFindFirst(member => member.MemberType == MemberTypes.Field || member.MemberType == MemberTypes.Property,
                out MemberInfo member))
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

            var memberSettings = new MemberSettings<TMember>();
            configureMemberSettings(memberSettings);
            memberSettingsDict[memberName] = memberSettings;
        }

        /// <summary>
        /// Sets a single concrete type mapping for <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="TMap">Mapped implementation type.</typeparam>
        /// <param name="configureInstanceTypeSettings">Optional nested configuration for mapped type behavior.</param>
        /// <exception cref="Exception">Thrown if <typeparamref name="TMap"/> is not compatible with <typeparamref name="T"/>.</exception>
        public void SetInstanceTypeMapping<TMap>(Action<TypeSettings<TMap>> configureInstanceTypeSettings = null) where TMap : T
        {
            this.multiOptionMappedTypes.Clear(); // clear multi option mappings if they exist, as we are now adding a single mapping

            Type instanceType = typeof(TMap);
            Type type = typeof(T);
            if (!instanceType.IsAssignableTo(type))
            {
                throw new Exception($"{TypeNameHelper.Shared.GetSimplifiedTypeName(type)} is not implemented by {TypeNameHelper.Shared.GetSimplifiedTypeName(instanceType)}");
            }
            TypeSettings<TMap> typeSettings = null;
            if (configureInstanceTypeSettings != null)
            {
                typeSettings = new TypeSettings<TMap>();
                configureInstanceTypeSettings(typeSettings);
            }
            this.mappedType = new MappedType(instanceType, typeSettings);
        }

        /// <summary>
        /// Adds one candidate concrete mapping option (multi-option mode) for <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="TMap">Mapped implementation type option.</typeparam>
        /// <param name="configureInstanceTypeSettings">Optional nested configuration for mapped option behavior.</param>
        /// <exception cref="Exception">Thrown if <typeparamref name="TMap"/> is not compatible with <typeparamref name="T"/>.</exception>
        public void AddInstanceTypeMappingOption<TMap>(Action<TypeSettings<TMap>> configureInstanceTypeSettings = null) where TMap : T
        {
            this.mappedType = default; // clear single mapping if it exists, as we are now adding multiple options

            Type instanceType = typeof(TMap);
            Type type = typeof(T);
            if (!instanceType.IsAssignableTo(type))
            {
                throw new Exception($"{TypeNameHelper.Shared.GetSimplifiedTypeName(type)} is not implemented by {TypeNameHelper.Shared.GetSimplifiedTypeName(instanceType)}");
            }
            TypeSettings<TMap> typeSettings = null;
            if (configureInstanceTypeSettings != null)
            {
                typeSettings = new TypeSettings<TMap>();
                configureInstanceTypeSettings(typeSettings);
            }
            multiOptionMappedTypes.Add(new MappedType(instanceType, typeSettings));
        }
    }

    /// <summary>
    /// Member-level settings.
    /// </summary>
    /// <typeparam name="T">Member type.</typeparam>
    public class MemberSettings<T> : TypeSettings<T>
    {
        /// <summary>
        /// Marks a member to be ignored or considered.
        /// </summary>
        /// <param name="ignore"><see langword="true"/> to ignore this member; otherwise <see langword="false"/>.</param>
        public void SetIgnore(bool ignore = true) => this.member_ignore = ignore;

        /// <summary>
        /// Overrides the serialized/deserialized member name for this member.
        /// </summary>
        /// <param name="alternateName">Alternate name used in payload matching.</param>
        public void OverrideName(string alternateName) => this.member_overrideName = alternateName;

        /// <summary>
        /// Enables or disables string-cache usage for this specific member.
        /// </summary>
        /// <param name="useStringCache"><see langword="true"/> to use string caching for this member.</param>
        public void SetUseStringCache(bool useStringCache)
        {
            this.member_useStringCache = useStringCache;
        }
    }

    /// <summary>
    /// Immutable runtime snapshot of <see cref="Settings"/> used by the active deserializer.
    /// </summary>
    private readonly struct CompiledSettings
    {
        /// <summary>Resolved global data-access mode.</summary>
        public readonly DataAccess dataAccess;

        /// <summary>Compiled custom type-name map.</summary>
        public readonly Dictionary<string, Type> customTypeNames;

        /// <summary>Compiled forbidden-type set.</summary>
        public readonly HashSet<Type> forbiddenTypes;

        /// <summary>Resolved global reference-resolution mode.</summary>
        public readonly ReferenceResolutionMode referenceResolutionMode;

        /// <summary>Resolved proposed-type mode.</summary>
        public readonly ProposedTypeMode proposedTypeMode;

        /// <summary>Resolved backing-field mode.</summary>
        public readonly BackingFieldMode backingFieldMode;

        /// <summary>Whether type-name cache should include case variants.</summary>
        public readonly bool addCaseVariantsForCustomTypeNames;

        /// <summary>Resolved initial buffer size.</summary>
        public readonly int initialBufferSize;

        /// <summary>Resolved cast-to-common-array setting.</summary>
        public readonly bool castObjectArrayToCommonTypeArray;

        /// <summary>Resolved rethrow-exceptions setting.</summary>
        public readonly bool rethrowExceptions;

        /// <summary>Resolved log-caught-exceptions setting.</summary>
        public readonly bool logCatchedExceptions;

        /// <summary>Resolved strict-mode setting.</summary>
        public readonly bool strict;

        /// <summary>Resolved populate-existing-members setting.</summary>
        public readonly bool populateExistingMembers;

        /// <summary>Resolved global string-cache setting.</summary>
        public readonly bool useStringCache;

        /// <summary>Resolved string-cache bit size.</summary>
        public readonly int stringCacheBitSize;

        /// <summary>Resolved string-cache maximum string length.</summary>
        public readonly int stringCacheMaxLength;

        /// <summary>Resolved uninitialized-object-creation setting.</summary>
        public readonly bool allowUninitializedObjectCreation;

        /// <summary>
        /// Indicates whether any compiled type/member path uses string cache.
        /// </summary>
        public readonly bool anyUsesStringCache;

        /// <summary>
        /// Indicates whether proposed types are allowed anywhere in compiled settings.
        /// </summary>
        public readonly bool anyAllowsProposedTypes;

        /// <summary>Resolved whitelist mode.</summary>
        public readonly Settings.TypeWhitelistMode typeWhitelistMode;

        /// <summary>Compiled allowed-type set.</summary>
        public readonly HashSet<Type> allowedTypes;

        /// <summary>Compiled allowed-namespace-prefix set.</summary>
        public readonly HashSet<string> allowedNamespacePrefixes;

        /// <summary>Deep-cloned type-settings map.</summary>
        public readonly Dictionary<Type, BaseTypeSettings> typeSettingsDict;

        /// <summary>
        /// Compiles a mutable <see cref="Settings"/> instance into an immutable runtime snapshot.
        /// </summary>
        /// <param name="settings">Source settings.</param>
        /// <exception cref="Exception">Thrown when deep-cloning type settings fails.</exception>
        public CompiledSettings(Settings settings)
        {
            dataAccess = settings.dataAccess;
            customTypeNames = new(settings.customTypeNames);
            forbiddenTypes = new(settings.forbiddenTypes);

            referenceResolutionMode = settings.referenceResolutionMode;

            proposedTypeMode = settings.proposedTypeMode;
            backingFieldMode = settings.backingFieldMode;
            addCaseVariantsForCustomTypeNames = settings.addCaseVariantsForCustomTypeNames;

            initialBufferSize = settings.initialBufferSize.ClampLow(1024 * 16); // minimum 16KB buffer size to avoid too many resizes for larger JSON inputs
            castObjectArrayToCommonTypeArray = settings.castObjectArrayToCommonTypeArray;
            rethrowExceptions = settings.rethrowExceptions;
            logCatchedExceptions = settings.logCatchedExceptions;
            strict = settings.strict;
            populateExistingMembers = settings.populateExistingMembers;
            useStringCache = settings.useStringCache;
            stringCacheBitSize = settings.stringCacheBitSize;
            stringCacheMaxLength = settings.stringCacheMaxLength;
            allowUninitializedObjectCreation = settings.allowUninitializedObjectCreation;

            typeWhitelistMode = settings.typeWhitelistMode;
            allowedTypes = new(settings.allowedTypes);
            allowedNamespacePrefixes = new(settings.allowedNamespacePrefixes, StringComparer.Ordinal);

            if (!settings.typeSettingsDict.TryCloneDeep(out typeSettingsDict))
            {
                throw new Exception("Failed to clone type settings dictionary.");
            }

            bool anyTypeHasReferenceResolutionEnabled = false;
            anyUsesStringCache = this.useStringCache;
            anyAllowsProposedTypes = proposedTypeMode != ProposedTypeMode.Ignore;
            List<BaseTypeSettings> allTypeSettings = typeSettingsDict.Values.ToList();
            for (int i = 0; i < allTypeSettings.Count; i++)
            {
                var typeSettings = allTypeSettings[i];
                if (typeSettings.enableReferenceResolution == true) anyTypeHasReferenceResolutionEnabled = true;
                if (typeSettings.member_useStringCache == true) anyUsesStringCache = true;
                if (typeSettings.applyProposedTypes == true) anyAllowsProposedTypes = true;
                if (anyTypeHasReferenceResolutionEnabled && anyUsesStringCache && anyAllowsProposedTypes) break;

                if (typeSettings.memberSettingsDict.Count > 0)
                {
                    allTypeSettings.AddRange(typeSettings.memberSettingsDict.Values);
                }
            }
            if (!anyTypeHasReferenceResolutionEnabled && referenceResolutionMode == ReferenceResolutionMode.DisabledByDefault)
            {
                referenceResolutionMode = ReferenceResolutionMode.ForceDisabled;
            }
        }
    }

    /// <summary>
    /// Contract for custom type readers used by deserialization extensions.
    /// </summary>
    /// <typeparam name="T">Target type handled by the reader.</typeparam>
    public interface ICustomTypeReader<T>
    {
        /// <summary>
        /// Performs one-time initialization before read operations.
        /// </summary>
        /// <param name="api">Preparation API for constructor/metadata access.</param>
        void PrepareReader(PreparationApi api);

        /// <summary>
        /// Reads a new value instance.
        /// </summary>
        /// <param name="api">Read-time extension API.</param>
        /// <returns>Read value.</returns>
        T ReadValue(ExtensionApi api);

        /// <summary>
        /// Populates and returns an existing instance.
        /// </summary>
        /// <param name="api">Read-time extension API.</param>
        /// <param name="itemToPopulate">Existing instance to populate.</param>
        /// <returns>Populated instance.</returns>
        T ReadValue(ExtensionApi api, T itemToPopulate);

        /// <summary>
        /// Gets whether this reader supports populating an existing instance.
        /// </summary>
        bool CanPopulateExistingValue { get; }
    }

    /// <summary>
    /// Default adapter implementation for delegate-based custom type readers.
    /// </summary>
    /// <typeparam name="T">Handled target type.</typeparam>
    public class CustomTypeReader<T> : ICustomTypeReader<T>
    {
        /// <summary>Delegate that reads and returns a new value.</summary>
        Func<ExtensionApi, T> readValue;

        /// <summary>Delegate that populates an existing value.</summary>
        Func<ExtensionApi, T, T> populateValue;

        /// <summary>Preparation delegate producing a populate delegate.</summary>
        Func<PreparationApi, Func<ExtensionApi, T, T>> prepareReader;

        /// <summary>Cached constructor delegate for creating new instances.</summary>
        Func<T> constructor;

        public void SetConstructor(Func<T> constructor) => this.constructor = constructor;

        /// <summary>
        /// Initializes from a preparation delegate that returns a populate delegate.
        /// </summary>
        /// <param name="prepareReader">Preparation callback.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="prepareReader"/> is <see langword="null"/>.</exception>
        public CustomTypeReader(Func<PreparationApi, Func<ExtensionApi, T, T>> prepareReader)
        {
            if (prepareReader == null) throw new ArgumentNullException(nameof(prepareReader));
            this.prepareReader = prepareReader;
        }

        /// <summary>
        /// Initializes from a populate delegate.
        /// </summary>
        /// <param name="populateValue">Populate callback.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="populateValue"/> is <see langword="null"/>.</exception>
        public CustomTypeReader(Func<ExtensionApi, T, T> populateValue)
        {
            if (populateValue == null) throw new ArgumentNullException(nameof(populateValue));
            this.populateValue = populateValue;
        }

        /// <summary>
        /// Initializes from a read delegate.
        /// </summary>
        /// <param name="readValue">Read callback.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="readValue"/> is <see langword="null"/>.</exception>
        public CustomTypeReader(Func<ExtensionApi, T> readValue)
        {
            if (readValue == null) throw new ArgumentNullException(nameof(readValue));
            this.readValue = readValue;
        }

        /// <summary>
        /// Gets whether this reader can populate an existing instance.
        /// </summary>
        public bool CanPopulateExistingValue => populateValue != null;

        /// <summary>
        /// Initializes internal delegates and constructor before reads.
        /// </summary>
        /// <param name="api">Preparation API.</param>
        /// <exception cref="Exception">Thrown if no valid read/populate delegate is available.</exception>
        public void PrepareReader(PreparationApi api)
        {            
            if (prepareReader != null)
            {
                var construct = constructor ?? api.GetContructor<T>();
                populateValue = prepareReader(api);
                readValue = (api) => populateValue(api, construct());
            }
            else if (populateValue != null)
            {
                var construct = constructor ?? api.GetContructor<T>();
                readValue = (api) => populateValue(api, construct());
            }
            else if (readValue != null)
            {
                populateValue = null;
            }
            else
            {
                throw new Exception("No valid reader function provided.");
            }
        }

        /// <summary>
        /// Reads and returns a value using the configured read delegate.
        /// </summary>
        public T ReadValue(ExtensionApi api) => readValue(api);

        /// <summary>
        /// Populates and returns the provided instance using the configured populate delegate.
        /// </summary>
        public T ReadValue(ExtensionApi api, T itemToPopulate) => populateValue(api, itemToPopulate);
    }
}
