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
    /// String-encoded CLR types that can be recognized when deserializing a value through a multi-option mapping.
    /// </summary>
    [Flags]
    public enum StringValueMappings
    {
        /// <summary>No automatic string recognition.</summary>
        None = 0,
        /// <summary>Recognize canonical hyphenated GUID strings.</summary>
        Guid = 1,
        /// <summary>Recognize ISO-8601 date/time strings carrying a UTC designator or numeric offset.</summary>
        DateTimeOffset = 2,
        /// <summary>Recognize ISO-8601 date/time strings.</summary>
        DateTime = 4,
        /// <summary>Recognize invariant constant-format time spans.</summary>
        TimeSpan = 8,
        /// <summary>Recognize all supported default string mappings.</summary>
        All = Guid | DateTimeOffset | DateTime | TimeSpan
    }

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

        /// <summary>Controls how unmatched JSON object properties are handled by default.</summary>
        public UnknownFieldPolicy unknownFieldPolicy = UnknownFieldPolicy.Skip;

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
#if NET6_0_OR_GREATER
            AddCustomTypeName(typeof(DateOnly).Name, typeof(DateOnly));
            AddCustomTypeName(typeof(TimeOnly).Name, typeof(TimeOnly));
#endif
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
#if NET6_0_OR_GREATER
            AddCustomTypeName("date", typeof(DateOnly));
            AddCustomTypeName("time", typeof(TimeOnly));
#endif
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
        /// Configures settings for a concrete type that is only known at runtime.
        /// </summary>
        /// <param name="type">Configured type. Must not be a generic type definition.</param>
        /// <param name="configureTypeSettings">
        /// Callback that mutates or creates the type settings.
        /// If <see langword="null"/>, the type configuration is removed.
        /// </param>
        public void ConfigureType(Type type, Action<BaseTypeSettings> configureTypeSettings)
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

            if (!typeSettingsDict.TryGetValue(type, out BaseTypeSettings typeSettings) ||
                typeSettings is GenericTypeSettings)
            {
                typeSettings = (BaseTypeSettings)Activator.CreateInstance(typeof(TypeSettings<>).MakeGenericType(type));
            }
            configureTypeSettings(typeSettings);
            typeSettingsDict[type] = typeSettings;
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

        /// <summary>Optional field predicate used while selecting this mapping option.</summary>
        readonly public IMappingOptionFieldChecker fieldChecker;

        /// <summary>Optional whole-value predicate or converter used while selecting this mapping option.</summary>
        readonly public IMappingOptionValueChecker valueChecker;

        /// <summary>
        /// Initializes a mapped-type entry.
        /// </summary>
        /// <param name="type">Destination type.</param>
        /// <param name="typeSettings">Optional nested settings for destination type handling.</param>
        public MappedType(Type type, BaseTypeSettings typeSettings, IMappingOptionFieldChecker fieldChecker = null, IMappingOptionValueChecker valueChecker = null)
        {
            this.type = type;
            this.typeSettings = typeSettings;
            this.fieldChecker = fieldChecker;
            this.valueChecker = valueChecker;
        }
    }

    internal interface IMappingOptionFieldChecker
    {
        string FieldName { get; }
        Type FieldType { get; }
    }

    internal sealed class MappingOptionFieldChecker<TField> : IMappingOptionFieldChecker
    {
        internal readonly Func<TField, bool> predicate;
        public string FieldName { get; }
        public Type FieldType => typeof(TField);

        internal MappingOptionFieldChecker(string fieldName, Func<TField, bool> predicate)
        {
            FieldName = fieldName;
            this.predicate = predicate;
        }
    }

    internal interface IMappingOptionValueChecker
    {
        Type ValueType { get; }
        bool ProducesResult { get; }
        bool IsDefaultStringMapping { get; }
    }

    internal sealed class MappingOptionValuePredicate<TValue> : IMappingOptionValueChecker
    {
        internal readonly Func<TValue, bool> predicate;
        public Type ValueType => typeof(TValue);
        public bool ProducesResult => false;
        public bool IsDefaultStringMapping => false;

        internal MappingOptionValuePredicate(Func<TValue, bool> predicate)
        {
            this.predicate = predicate;
        }
    }

    internal sealed class MappingOptionValueConverter<TValue, TMap> : IMappingOptionValueChecker
    {
        internal readonly TryMapValue<TValue, TMap> converter;
        public Type ValueType => typeof(TValue);
        public bool ProducesResult => true;
        public bool IsDefaultStringMapping { get; }

        internal MappingOptionValueConverter(TryMapValue<TValue, TMap> converter, bool isDefaultStringMapping = false)
        {
            this.converter = converter;
            IsDefaultStringMapping = isDefaultStringMapping;
        }
    }

    /// <summary>
    /// Attempts to map a deserialized input value to a mapped result.
    /// </summary>
    /// <typeparam name="TValue">Input value type.</typeparam>
    /// <typeparam name="TMap">Mapped result type.</typeparam>
    /// <param name="value">Deserialized input value.</param>
    /// <param name="result">Produced mapped result when successful.</param>
    /// <returns><see langword="true"/> when a result was produced; otherwise <see langword="false"/>.</returns>
    public delegate bool TryMapValue<TValue, TMap>(TValue value, out TMap result);

    /// <summary>
    /// Shared storage for type/member settings consumed by the deserializer pipeline.
    /// </summary>
    public class BaseTypeSettings
    {
        internal bool isMerged;

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

        /// <summary>Type/member-level unknown-field behavior override.</summary>
        internal UnknownFieldPolicy? unknownFieldPolicy;

        /// <summary>Custom constructor delegate.</summary>
        internal Delegate constructor = null;

        /// <summary>Custom collection constructor delegate.</summary>
        internal Delegate collectionConstructor = null;

        /// <summary>Custom reader implementation/object.</summary>
        internal object customTypeReader = null;

        /// <summary>Suppresses configured custom readers for a locally prepared non-custom reader.</summary>
        internal bool suppressCustomTypeReader;

        /// <summary>Per-member configuration map by member name.</summary>
        internal LazyDictionary<string, BaseTypeSettings> memberSettingsDict = default;

        /// <summary>Configuration for elements or dictionary values in this container scope.</summary>
        internal BaseTypeSettings elementSettings;

        /// <summary>Element type for which <see cref="elementSettings"/> was configured.</summary>
        internal Type elementSettingsType;

        /// <summary>Policies inherited by this scope and all nested values during reader preparation.</summary>
        internal RecursiveReadSettings recursiveSettings;

        /// <summary>Resolved recursive context captured by the prepared reader.</summary>
        internal RecursiveReadSettings effectiveRecursiveSettings;

        /// <summary>Parser for dictionary JSON-object property names.</summary>
        internal IKeyParser keyParser;

        /// <summary>Sets data-access behavior for this type scope.</summary>
        public void SetDataAccess(DataAccess dataAccess) => this.dataAccess = dataAccess;

        /// <summary>Enables or disables reference resolution for this type scope.</summary>
        public void SetReferenceResolution(bool enable) => enableReferenceResolution = enable;

        /// <summary>Enables or disables proposed-type usage for this type scope.</summary>
        public void SetProposedTypeHandling(bool applyProposedTypes) => this.applyProposedTypes = applyProposedTypes;

        /// <summary>Enables or disables populate-existing-member behavior for this type scope.</summary>
        public void SetPopulateAsMember(bool populate) => populateAsMember = populate;

        /// <summary>Sets backing-field lookup mode for this type scope.</summary>
        public void SetBackingFieldMode(BackingFieldMode mode) => backingFieldMode = mode;

        /// <summary>Sets how unmatched JSON object properties are handled in this type scope.</summary>
        public void SetUnknownFieldPolicy(UnknownFieldPolicy policy) => unknownFieldPolicy = policy;

        /// <summary>
        /// Configures read policies inherited by this type scope and its complete value subtree.
        /// </summary>
        public void ConfigureRecursively(Action<RecursiveReadSettings> configure)
        {
            if (configure == null)
            {
                recursiveSettings = null;
                return;
            }
            recursiveSettings ??= new RecursiveReadSettings();
            configure(recursiveSettings);
        }

        internal BaseTypeSettings MergeOnto(BaseTypeSettings broaderSettings)
        {
            if (broaderSettings == null || isMerged) return this;

            var merged = new BaseTypeSettings
            {
                mappedType = mappedType ?? broaderSettings.mappedType,
                multiOptionMappedTypes = multiOptionMappedTypes.Count > 0 ? multiOptionMappedTypes : broaderSettings.multiOptionMappedTypes,
                member_ignore = member_ignore,
                member_overrideName = member_overrideName,
                member_useStringCache = member_useStringCache ?? broaderSettings.member_useStringCache,
                dataAccess = dataAccess ?? broaderSettings.dataAccess,
                backingFieldMode = backingFieldMode ?? broaderSettings.backingFieldMode,
                enableReferenceResolution = enableReferenceResolution ?? broaderSettings.enableReferenceResolution,
                applyProposedTypes = applyProposedTypes ?? broaderSettings.applyProposedTypes,
                populateAsMember = populateAsMember ?? broaderSettings.populateAsMember,
                unknownFieldPolicy = unknownFieldPolicy ?? broaderSettings.unknownFieldPolicy,
                constructor = constructor ?? broaderSettings.constructor,
                collectionConstructor = collectionConstructor ?? broaderSettings.collectionConstructor,
                customTypeReader = customTypeReader ?? broaderSettings.customTypeReader,
                suppressCustomTypeReader = suppressCustomTypeReader,
                elementSettings = elementSettings ?? broaderSettings.elementSettings?.AsInjectedFromBroaderSettings(),
                elementSettingsType = elementSettings != null ? elementSettingsType : broaderSettings.elementSettingsType,
                recursiveSettings = recursiveSettings?.MergeOnto(broaderSettings.recursiveSettings) ?? broaderSettings.recursiveSettings,
                effectiveRecursiveSettings = effectiveRecursiveSettings ?? broaderSettings.effectiveRecursiveSettings,
                keyParser = keyParser ?? broaderSettings.keyParser,
                isMerged = true
            };

            foreach (var entry in broaderSettings.memberSettingsDict)
            {
                if (ReferenceEquals(entry.Value, this)) continue;
                merged.memberSettingsDict[entry.Key] = entry.Value.AsInjectedFromBroaderSettings();
            }
            foreach (var entry in memberSettingsDict) merged.memberSettingsDict[entry.Key] = entry.Value;
            return merged;
        }

        BaseTypeSettings AsInjectedFromBroaderSettings()
        {
            if (isMerged) return this;

            var copy = new BaseTypeSettings
            {
                mappedType = mappedType,
                multiOptionMappedTypes = multiOptionMappedTypes,
                member_ignore = member_ignore,
                member_overrideName = member_overrideName,
                member_useStringCache = member_useStringCache,
                dataAccess = dataAccess,
                backingFieldMode = backingFieldMode,
                enableReferenceResolution = enableReferenceResolution,
                applyProposedTypes = applyProposedTypes,
                populateAsMember = populateAsMember,
                unknownFieldPolicy = unknownFieldPolicy,
                constructor = constructor,
                collectionConstructor = collectionConstructor,
                customTypeReader = customTypeReader,
                suppressCustomTypeReader = suppressCustomTypeReader,
                elementSettings = elementSettings?.AsInjectedFromBroaderSettings(),
                elementSettingsType = elementSettingsType,
                recursiveSettings = recursiveSettings,
                effectiveRecursiveSettings = effectiveRecursiveSettings,
                keyParser = keyParser,
                isMerged = true
            };
            foreach (var entry in memberSettingsDict)
            {
                if (ReferenceEquals(entry.Value, this)) continue;
                copy.memberSettingsDict[entry.Key] = entry.Value.AsInjectedFromBroaderSettings();
            }
            return copy;
        }

        internal BaseTypeSettings CopyWithCustomTypeReader(object customTypeReader)
        {
            var copy = AsInjectedFromBroaderSettings();
            copy.customTypeReader = customTypeReader;
            return copy;
        }

        private protected void ConfigureElementInternal<TElement>(Type containerType, Action<TypeSettings<TElement>> configureElementSettings)
        {
            if (configureElementSettings == null)
            {
                elementSettings = null;
                elementSettingsType = null;
                return;
            }

            if (!TryGetReadElementType(containerType, out Type actualElementType))
            {
                throw new Exception($"Type {TypeNameHelper.Shared.GetSimplifiedTypeName(containerType)} is not a container type, so its elements cannot be configured.");
            }
            if (actualElementType != typeof(TElement))
            {
                throw new Exception($"Type {TypeNameHelper.Shared.GetSimplifiedTypeName(containerType)} reads elements of type " +
                                    $"{TypeNameHelper.Shared.GetSimplifiedTypeName(actualElementType)}, not {TypeNameHelper.Shared.GetSimplifiedTypeName(typeof(TElement))}.");
            }

            var settings = new TypeSettings<TElement>();
            configureElementSettings(settings);
            elementSettings = settings;
            elementSettingsType = typeof(TElement);
        }

        static bool TryGetReadElementType(Type containerType, out Type elementType)
        {
            elementType = null;
            if (containerType == null) return false;
            if (containerType.IsArray && containerType.GetArrayRank() == 1)
            {
                elementType = containerType.GetElementType();
                return true;
            }
            if (containerType.TryGetTypeParamsOfGenericInterface(typeof(IDictionary<,>), out _, out Type valueType) ||
                containerType.TryGetTypeParamsOfGenericInterface(typeof(IReadOnlyDictionary<,>), out _, out valueType))
            {
                elementType = valueType;
                return true;
            }
            if (containerType.TryGetTypeParamsOfGenericInterface(typeof(IEnumerable<>), out elementType)) return true;
            if (containerType.ImplementsInterface(typeof(IEnumerable)))
            {
                elementType = typeof(object);
                return true;
            }
            return false;
        }

        private protected void ValidateObjectKeyType<TKey>(Type dictionaryType)
        {
            if (dictionaryType != null)
            {
                if (!(dictionaryType.TryGetTypeParamsOfGenericInterface(typeof(IDictionary<,>), out Type actualKeyType, out _) ||
                      dictionaryType.TryGetTypeParamsOfGenericInterface(typeof(IReadOnlyDictionary<,>), out actualKeyType, out _)))
                {
                    throw new Exception($"Type {TypeNameHelper.Shared.GetSimplifiedTypeName(dictionaryType)} is not a dictionary type, so its keys cannot be configured.");
                }
                if (actualKeyType != typeof(TKey))
                {
                    throw new Exception($"Type {TypeNameHelper.Shared.GetSimplifiedTypeName(dictionaryType)} reads keys of type " +
                                        $"{TypeNameHelper.Shared.GetSimplifiedTypeName(actualKeyType)}, not {TypeNameHelper.Shared.GetSimplifiedTypeName(typeof(TKey))}.");
                }
            }
        }

        private protected void ConfigureObjectKeyInternal<TKey>(Type dictionaryType, Func<BufferSegment, TKey> parseKey)
        {
            ValidateObjectKeyType<TKey>(dictionaryType);
            keyParser = parseKey == null ? null : new ObjectKeyParser<TKey>(parseKey);
        }
    }

    internal interface IKeyParser
    {
        Type KeyType { get; }
    }

    internal sealed class ObjectKeyParser<TKey> : IKeyParser
    {
        readonly Func<BufferSegment, TKey> parseKey;
        internal ObjectKeyParser(Func<BufferSegment, TKey> parseKey) => this.parseKey = parseKey;
        public Type KeyType => typeof(TKey);
        public TKey Parse(BufferSegment key) => parseKey(key);
    }

    /// <summary>
    /// Read policies inherited by a configured scope and all nested values. Type-bound mappings,
    /// constructors, custom readers, and member metadata are intentionally unavailable.
    /// </summary>
    public sealed class RecursiveReadSettings
    {
        internal DataAccess? dataAccess;
        internal BackingFieldMode? backingFieldMode;
        internal bool? enableReferenceResolution;
        internal bool? applyProposedTypes;
        internal bool? populateAsMember;
        internal bool? useStringCache;
        internal UnknownFieldPolicy? unknownFieldPolicy;

        public void SetDataAccess(DataAccess value) => dataAccess = value;
        public void SetBackingFieldMode(BackingFieldMode value) => backingFieldMode = value;
        public void SetReferenceResolution(bool value) => enableReferenceResolution = value;
        public void SetProposedTypeHandling(bool value) => applyProposedTypes = value;
        public void SetPopulateAsMember(bool value) => populateAsMember = value;
        public void SetUseStringCache(bool value) => useStringCache = value;
        public void SetUnknownFieldPolicy(UnknownFieldPolicy value) => unknownFieldPolicy = value;

        internal RecursiveReadSettings MergeOnto(RecursiveReadSettings outer)
        {
            if (outer == null) return this;
            var merged = new RecursiveReadSettings
            {
                dataAccess = dataAccess ?? outer.dataAccess,
                backingFieldMode = backingFieldMode ?? outer.backingFieldMode,
                enableReferenceResolution = enableReferenceResolution ?? outer.enableReferenceResolution,
                applyProposedTypes = applyProposedTypes ?? outer.applyProposedTypes,
                populateAsMember = populateAsMember ?? outer.populateAsMember,
                useStringCache = useStringCache ?? outer.useStringCache,
                unknownFieldPolicy = unknownFieldPolicy ?? outer.unknownFieldPolicy
            };
            return merged.HasSameValues(outer) ? outer : merged;
        }

        internal bool HasSameValues(RecursiveReadSettings other) =>
            other != null &&
            dataAccess == other.dataAccess &&
            backingFieldMode == other.backingFieldMode &&
            enableReferenceResolution == other.enableReferenceResolution &&
            applyProposedTypes == other.applyProposedTypes &&
            populateAsMember == other.populateAsMember &&
            useStringCache == other.useStringCache &&
            unknownFieldPolicy == other.unknownFieldPolicy;

        internal BaseTypeSettings ApplyBelow(BaseTypeSettings local)
        {
            var effective = new BaseTypeSettings
            {
                mappedType = local?.mappedType,
                multiOptionMappedTypes = local?.multiOptionMappedTypes ?? default,
                member_ignore = local?.member_ignore,
                member_overrideName = local?.member_overrideName,
                member_useStringCache = local?.member_useStringCache ?? useStringCache,
                dataAccess = local?.dataAccess ?? dataAccess,
                backingFieldMode = local?.backingFieldMode ?? backingFieldMode,
                enableReferenceResolution = local?.enableReferenceResolution ?? enableReferenceResolution,
                applyProposedTypes = local?.applyProposedTypes ?? applyProposedTypes,
                populateAsMember = local?.populateAsMember ?? populateAsMember,
                unknownFieldPolicy = local?.unknownFieldPolicy ?? unknownFieldPolicy,
                constructor = local?.constructor,
                collectionConstructor = local?.collectionConstructor,
                customTypeReader = local?.customTypeReader,
                suppressCustomTypeReader = local?.suppressCustomTypeReader ?? false,
                elementSettings = local?.elementSettings,
                elementSettingsType = local?.elementSettingsType,
                recursiveSettings = local?.recursiveSettings,
                effectiveRecursiveSettings = this,
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

        /// <summary>
        /// Sets a custom reader definition for every type constructed from this generic type definition.
        /// </summary>
        /// <param name="readerDefinition">
        /// Generic type definition deriving from <see cref="CustomTypeReaderDefinition{T}"/> with the same
        /// generic arity as the configured type and a public parameterless constructor. Pass
        /// <see langword="null"/> to remove the definition.
        /// </param>
        /// <remarks>
        /// The definition is closed, instantiated, and prepared once per constructed type. Exact constructed
        /// type settings take precedence, and derived types are not implicitly covered.
        /// </remarks>
        public void SetCustomTypeReader(Type readerDefinition)
        {
            customTypeReader = readerDefinition == null ? null : new OpenGenericTypeReaderDefinition(genericType, readerDefinition);
        }

        /// <summary>Configures settings for matching elements or dictionary values.</summary>
        public void ConfigureElement<TElement>(Action<TypeSettings<TElement>> configureElementSettings)
        {
            if (configureElementSettings == null)
            {
                elementSettings = null;
                elementSettingsType = null;
                return;
            }
            var settings = new TypeSettings<TElement>();
            configureElementSettings(settings);
            elementSettings = settings;
            elementSettingsType = typeof(TElement);
        }

        /// <summary>
        /// Configures how JSON object property names are parsed into keys for matching dictionary constructions.
        /// </summary>
        /// <typeparam name="TKey">Dictionary key type to which this configuration applies.</typeparam>
        /// <param name="parseKey">
        /// Parser receiving a non-owning view of the JSON object property name. Pass <see langword="null"/> to remove the configuration.
        /// </param>
        /// <remarks>
        /// This parser applies only when a dictionary is represented as a JSON object, for example
        /// <c>{"key-1":10}</c>. It is not used when a dictionary is represented as an array of key-value
        /// pairs, for example <c>[{"key":1,"value":10}]</c>; keys in that representation are JSON values
        /// and continue through the normal <typeparamref name="TKey"/> value reader.
        /// For an open-generic dictionary setting, the parser applies only to constructions whose key type
        /// exactly matches <typeparamref name="TKey"/>.
        /// </remarks>
        public void ConfigureObjectKey<TKey>(Func<BufferSegment, TKey> parseKey) => ConfigureObjectKeyInternal(null, parseKey);

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
        /// <summary>Configures settings for elements or dictionary values.</summary>
        public void ConfigureElement<TElement>(Action<TypeSettings<TElement>> configureElementSettings) =>
            ConfigureElementInternal(typeof(T), configureElementSettings);

        /// <summary>
        /// Configures how JSON object property names are parsed into dictionary keys.
        /// </summary>
        /// <typeparam name="TKey">The dictionary key type.</typeparam>
        /// <param name="parseKey">
        /// Parser receiving a non-owning view of the JSON object property name. Pass <see langword="null"/> to remove the configuration.
        /// </param>
        /// <remarks>
        /// This parser applies only when the dictionary is represented as a JSON object, for example
        /// <c>{"key-1":10}</c>. It is not used when the dictionary is represented as an array of key-value
        /// pairs, for example <c>[{"key":1,"value":10}]</c>; keys in that representation are JSON values
        /// and continue through the normal <typeparamref name="TKey"/> value reader.
        /// </remarks>
        /// <exception cref="Exception">
        /// Thrown when <typeparamref name="T"/> is not a dictionary or <typeparamref name="TKey"/> does not
        /// match its key type.
        /// </exception>
        public void ConfigureObjectKey<TKey>(Func<BufferSegment, TKey> parseKey) => ConfigureObjectKeyInternal(typeof(T), parseKey);

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
        /// Assigns a custom read-only preparation delegate and wraps it in <see cref="CustomTypeReader{T}"/>.
        /// </summary>
        public void SetCustomTypeReader(Func<PreparationApi, Func<ExtensionApi, T>> readTypeCreator) => SetCustomTypeReader(new CustomTypeReader<T>(readTypeCreator));

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

        /// <summary>
        /// Adds a candidate concrete mapping option with a typed field predicate.
        /// </summary>
        /// <typeparam name="TMap">Mapped implementation type option.</typeparam>
        /// <typeparam name="TField">Expected type of the checked JSON field.</typeparam>
        /// <param name="fieldName">JSON field whose value is checked when present.</param>
        /// <param name="predicate">Predicate selecting this option when it returns <see langword="true"/>.</param>
        /// <param name="configureInstanceTypeSettings">Optional nested configuration for mapped option behavior.</param>
        /// <remarks>
        /// A matching predicate selects this option immediately. A present field that cannot be read as
        /// <typeparamref name="TField"/>, or for which the predicate returns <see langword="false"/>, excludes
        /// this option. If the field is absent, normal field-name inference remains available for this option.
        /// </remarks>
        public void AddInstanceTypeMappingOption<TMap, TField>(
            string fieldName,
            Func<TField, bool> predicate,
            Action<TypeSettings<TMap>> configureInstanceTypeSettings = null) where TMap : T
        {
            if (string.IsNullOrWhiteSpace(fieldName)) throw new ArgumentException("The checker field name must not be null or empty.", nameof(fieldName));
            if (predicate == null) throw new ArgumentNullException(nameof(predicate));

            this.mappedType = default;
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
            var checker = new MappingOptionFieldChecker<TField>(fieldName, predicate);
            multiOptionMappedTypes.Add(new MappedType(instanceType, typeSettings, checker));
        }

        /// <summary>
        /// Adds a mapping option selected by a predicate over the complete JSON value.
        /// </summary>
        /// <typeparam name="TValue">Type used to read the input value.</typeparam>
        /// <typeparam name="TMap">Mapped implementation type option.</typeparam>
        /// <param name="predicate">Predicate selecting this option when it returns <see langword="true"/>.</param>
        /// <param name="configureInstanceTypeSettings">Optional nested configuration for mapped option behavior.</param>
        /// <remarks>
        /// The input is inspected as <typeparamref name="TValue"/>. On a match, the original JSON value is
        /// deserialized again through the normal prepared reader for <typeparamref name="TMap"/>.
        /// </remarks>
        public void AddInstanceTypeMappingValueOption<TValue, TMap>(
            Func<TValue, bool> predicate,
            Action<TypeSettings<TMap>> configureInstanceTypeSettings = null) where TMap : T
        {
            if (predicate == null) throw new ArgumentNullException(nameof(predicate));

            this.mappedType = default;
            TypeSettings<TMap> typeSettings = CreateMappingOptionSettings(configureInstanceTypeSettings);
            var checker = new MappingOptionValuePredicate<TValue>(predicate);
            multiOptionMappedTypes.Add(new MappedType(typeof(TMap), typeSettings, valueChecker: checker));
        }

        /// <summary>
        /// Adds a mapping option that can directly produce a result from the complete JSON value.
        /// </summary>
        /// <typeparam name="TValue">Type used to read the input value.</typeparam>
        /// <typeparam name="TMap">Mapped result type.</typeparam>
        /// <param name="converter">Converter returning <see langword="true"/> when it produced the result.</param>
        /// <remarks>
        /// A successful conversion returns its result directly and therefore does not invoke the normal
        /// <typeparamref name="TMap"/> reader afterward.
        /// </remarks>
        public void AddInstanceTypeMappingValueOption<TValue, TMap>(TryMapValue<TValue, TMap> converter) where TMap : T
        {
            if (converter == null) throw new ArgumentNullException(nameof(converter));

            this.mappedType = default;
            var checker = new MappingOptionValueConverter<TValue, TMap>(converter);
            multiOptionMappedTypes.Add(new MappedType(typeof(TMap), null, valueChecker: checker));
        }

        /// <summary>
        /// Adds opt-in recognition for CLR types that the default serializer represents as JSON strings.
        /// </summary>
        /// <param name="mappings">String-encoded types to recognize.</param>
        /// <remarks>
        /// Explicit whole-value mappings are always evaluated before these defaults. When both
        /// <see cref="StringValueMappings.DateTimeOffset"/> and <see cref="StringValueMappings.DateTime"/>
        /// are enabled, values carrying <c>Z</c> or a numeric offset are recognized as
        /// <see cref="DateTimeOffset"/> first. Unrecognized values remain strings.
        /// </remarks>
        /// <exception cref="ArgumentOutOfRangeException">Thrown for unsupported flags.</exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when an enabled recognized type is not assignable to <typeparamref name="T"/>.
        /// </exception>
        public void AddDefaultStringValueMappings(StringValueMappings mappings = StringValueMappings.All)
        {
            if ((mappings & ~StringValueMappings.All) != 0) throw new ArgumentOutOfRangeException(nameof(mappings));
            if (mappings == StringValueMappings.None) return;

            EnsureDefaultStringMappingCompatible(mappings, StringValueMappings.Guid, typeof(Guid));
            EnsureDefaultStringMappingCompatible(mappings, StringValueMappings.DateTimeOffset, typeof(DateTimeOffset));
            EnsureDefaultStringMappingCompatible(mappings, StringValueMappings.DateTime, typeof(DateTime));
            EnsureDefaultStringMappingCompatible(mappings, StringValueMappings.TimeSpan, typeof(TimeSpan));

            this.mappedType = default;
            bool TryConvert(string value, out T result)
            {
                object recognized = null;
                bool success = false;
                if ((mappings & StringValueMappings.Guid) != 0 &&
                    value.Length == 36 && System.Guid.TryParseExact(value, "D", out Guid guid))
                {
                    recognized = guid;
                    success = true;
                }
                else if ((mappings & StringValueMappings.DateTimeOffset) != 0 &&
                    HasDateTimeOffset(value) &&
                    System.DateTimeOffset.TryParseExact(value, DateTimeFormats, System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.RoundtripKind, out DateTimeOffset dateTimeOffset))
                {
                    recognized = dateTimeOffset;
                    success = true;
                }
                else if ((mappings & StringValueMappings.DateTime) != 0 &&
                    System.DateTime.TryParseExact(value, DateTimeFormats, System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.RoundtripKind, out DateTime dateTime))
                {
                    recognized = dateTime;
                    success = true;
                }
                else if ((mappings & StringValueMappings.TimeSpan) != 0 &&
                    System.TimeSpan.TryParseExact(value, "c", System.Globalization.CultureInfo.InvariantCulture, out TimeSpan timeSpan))
                {
                    recognized = timeSpan;
                    success = true;
                }

                result = success ? (T)recognized : default;
                return success;
            }

            var checker = new MappingOptionValueConverter<string, T>(TryConvert, true);
            multiOptionMappedTypes.Add(new MappedType(typeof(T), null, valueChecker: checker));
        }

        private static readonly string[] DateTimeFormats =
        {
            "yyyy-MM-dd'T'HH:mm:ssK",
            "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFK"
        };

        private static bool HasDateTimeOffset(string value)
        {
            if (value.Length < 20) return false;
            char last = value[value.Length - 1];
            if (last == 'Z') return true;
            return value.Length >= 25 && (value[value.Length - 6] == '+' || value[value.Length - 6] == '-');
        }

        private static void EnsureDefaultStringMappingCompatible(StringValueMappings mappings, StringValueMappings mapping, Type recognizedType)
        {
            if ((mappings & mapping) != 0 && !typeof(T).IsAssignableFrom(recognizedType))
            {
                throw new InvalidOperationException($"Recognized type {TypeNameHelper.Shared.GetSimplifiedTypeName(recognizedType)} is not assignable to {TypeNameHelper.Shared.GetSimplifiedTypeName(typeof(T))}.");
            }
        }

        private static TypeSettings<TMap> CreateMappingOptionSettings<TMap>(Action<TypeSettings<TMap>> configureInstanceTypeSettings)
        {
            if (configureInstanceTypeSettings == null) return null;
            var typeSettings = new TypeSettings<TMap>();
            configureInstanceTypeSettings(typeSettings);
            return typeSettings;
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

        /// <summary>Resolved global unknown-field behavior.</summary>
        public readonly UnknownFieldPolicy unknownFieldPolicy;

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
            unknownFieldPolicy = settings.unknownFieldPolicy;
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

            foreach (var entry in typeSettingsDict.ToArray())
            {
                Type type = entry.Key;
                if (!type.IsGenericType || type.IsGenericTypeDefinition) continue;
                if (typeSettingsDict.TryGetValue(type.GetGenericTypeDefinition(), out BaseTypeSettings genericSettings))
                {
                    typeSettingsDict[type] = entry.Value.MergeOnto(genericSettings);
                }
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

        /// <summary>Preparation delegate producing a read delegate.</summary>
        Func<PreparationApi, Func<ExtensionApi, T>> prepareValueReader;

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
        /// Initializes from a preparation delegate that returns a read delegate.
        /// </summary>
        /// <param name="prepareValueReader">Preparation callback.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="prepareValueReader"/> is <see langword="null"/>.</exception>
        public CustomTypeReader(Func<PreparationApi, Func<ExtensionApi, T>> prepareValueReader)
        {
            if (prepareValueReader == null) throw new ArgumentNullException(nameof(prepareValueReader));
            this.prepareValueReader = prepareValueReader;
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
            if (prepareValueReader != null)
            {
                readValue = prepareValueReader(api);
                populateValue = null;
            }
            else if (prepareReader != null)
            {
                var construct = constructor ?? api.GetConstructor<T>();
                populateValue = prepareReader(api);
                readValue = (api) => populateValue(api, construct());
            }
            else if (populateValue != null)
            {
                var construct = constructor ?? api.GetConstructor<T>();
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
