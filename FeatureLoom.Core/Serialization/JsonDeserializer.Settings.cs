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

public sealed partial class JsonDeserializer
{
    public class Settings
    {
        internal Dictionary<Type, BaseTypeSettings> typeSettingsDict = new();

        public DataAccess dataAccess = DataAccess.PublicAndPrivateFields;        
        internal Dictionary<string, Type> customTypeNames = new();
        internal HashSet<Type> forbiddenTypes = new();

        public ReferenceResolutionMode referenceResolutionMode = ReferenceResolutionMode.OnlyPerType;
        public ProposedTypeMode proposedTypeMode = ProposedTypeMode.CheckWhereReasonable;
        public BackingFieldMode backingFieldMode = BackingFieldMode.TryBothNames;

        // If true, when custom type names are loaded into the proposed-type cache,
        // lower/upper-case variants are added too.
        public bool addCaseVariantsForCustomTypeNames = false;

        public int initialBufferSize = 1024 * 128;
        public bool castObjectArrayToCommonTypeArray = true;
        public bool rethrowExceptions = false;
        public bool logCatchedExceptions = true;
        public bool strict = false;
        public bool populateExistingMembers = true;
        public bool useStringCache = true;
        public int stringCacheBitSize = 12; //4096 entries
        public int stringCacheMaxLength = 128; //max string length to be cached
        public bool allowUninitializedObjectCreation = false;        

        public TypeWhitelistMode typeWhitelistMode = TypeWhitelistMode.Disabled;
        public HashSet<Type> allowedTypes = new();
        public HashSet<string> allowedNamespacePrefixes = new(StringComparer.Ordinal);        
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

        public static Settings Build(Action<Settings> configure)
        {
            var settings = new Settings();
            configure?.Invoke(settings);
            return settings;
        }

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

        public void ClearCustomTypeNames() => customTypeNames.Clear();

        public void AddCustomTypeName(string customTypeName, Type type)
        {
            customTypeNames[customTypeName] = type;
        }

        public void AddForbiddenType(Type type)
        {
            forbiddenTypes.Add(type);
        }

        public void ClearForbiddenTypes() => forbiddenTypes.Clear();

        

        public void AddAllowedType(Type type) => allowedTypes.Add(type);
        public void AddAllowedType<T>() => allowedTypes.Add(typeof(T));
        public void AddAllowedNamespacePrefix(string prefix)
        {
            if (!string.IsNullOrWhiteSpace(prefix)) allowedNamespacePrefixes.Add(prefix);
        }

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

        public enum TypeWhitelistMode
        {
            Disabled = 0,
            ForProposedTypesOnly = 1,
            ForAllNonIntrinsicTypes = 2
        }

        public enum ProposedTypeMode
        {
            /// <summary>
            /// Proposed types (e.g. from $type properties in JSON) are ignored and not used for deserialization. 
            /// This is the safest option when deserializing untrusted data, but also means that polymorphic deserialization based on type information in the JSON will not work.
            /// This option allows for the hightest performance.
            /// If primitive values and arrays are wrapped in a $type object, the deserializer will fail.
            /// </summary>
            Ignore = 0,
            /// <summary>
            /// Proposed types are ignored where they don't make any sense with the given target type, e.g. primitive values and sealed types, 
            /// but used where they can be applied, e.g. for deserializing into an interface, abstract class or base class.
            /// This option provides a good balance between performance and flexibility.            
            /// </summary>
            CheckWhereReasonable = 1,
            /// <summary>
            /// Proposed types are always checked and used for deserialization if they are present, regardless of the target type.
            /// This option has a significant performance impact, but rarely used in practice, as it allows for proposed types to be applied in situations 
            /// where they don't make much sense.
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
            OnlyPerType = 1,
            /// <summary>
            /// Indicates that the feature is enabled by default for all reference types, but can be disabled for specific types in TypeSettings.
            /// Performance overhead is higher but good as a starting point if you expect reference information in the JSON but are not sure for which types, 
            /// as it allows you to enable reference resolution for all reference types and then disable it for specific types where it is not needed to optimize performance.
            /// Strings are not included for performance reasons, though.
            /// </summary>
            EnabledByDefault = 2,
        }

        public enum BackingFieldMode
        { 
            TryBothNames = 0,
            TryBackingFieldNameOnly = 1,
            TryPropertyNameOnly = 2
        }
    }

    internal readonly struct MappedType
    {
        readonly public Type type;
        readonly public BaseTypeSettings typeSettings;

        public MappedType(Type type, BaseTypeSettings typeSettings)
        {
            this.type = type;
            this.typeSettings = typeSettings;
        }
    }

    public class BaseTypeSettings
    {
        internal MappedType? mappedType;
        internal LazyList<MappedType> multiOptionMappedTypes;
        internal bool? member_ignore = null;
        internal string member_overrideName = null;
        internal bool? member_useStringCache = null;
        
        internal DataAccess? dataAccess = null;
        internal BackingFieldMode? backingFieldMode = null;
        internal bool? enableReferenceResolution = null;
        internal bool? applyProposedTypes = null;
        internal bool? populateAsMember = null;        
        internal Delegate constructor = null;
        internal Delegate collectionConstructor = null;        
        internal object customTypeReader = null;
        internal LazyDictionary<string, BaseTypeSettings> memberSettingsDict = default;
    }

    public class GenericTypeSettings : BaseTypeSettings
    {
        protected Type genericType;

        public GenericTypeSettings(Type genericType)
        {
            this.genericType = genericType;
        }

        public void SetDataAccess(DataAccess dataAccess) => this.dataAccess = dataAccess;
        public void SetReferenceResolution(bool enable) => this.enableReferenceResolution = enable;
        public void SetProposedTypeHandling(bool applyProposedTypes) => this.applyProposedTypes = applyProposedTypes;
        public void SetPopulateAsMember(bool populate) => populateAsMember = populate;
        public void SetBackingFieldMode(BackingFieldMode mode) => this.backingFieldMode = mode;

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

    public class TypeSettings<T> : BaseTypeSettings
    {
        public void SetDataAccess(DataAccess dataAccess) => this.dataAccess = dataAccess;
        public void SetReferenceResolution(bool enable) => this.enableReferenceResolution = enable;
        public void SetProposedTypeHandling(bool applyProposedTypes) => this.applyProposedTypes = applyProposedTypes;
        public void SetPopulateAsMember(bool populate) => populateAsMember = populate;
        public void SetBackingFieldMode(BackingFieldMode mode) => this.backingFieldMode = mode;

        public void AddConstructor(Func<T> constructor) => this.constructor = constructor;
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
        public void AddUntypedCollectionConstructor(Func<IEnumerable, T> constructor)
        {
            Type type = typeof(T);
            if (!type.IsAssignableTo(typeof(IEnumerable)))
            {
                throw new Exception($"The provided collection constructor is not valid for type {TypeNameHelper.Shared.GetSimplifiedTypeName(type)}");
            }

            collectionConstructor = constructor;
        }

        public void SetCustomTypeReader(ICustomTypeReader<T> customTypeReader) => this.customTypeReader = customTypeReader;
        public void SetCustomTypeReader(Func<ExtensionApi, T, T> populateType) => SetCustomTypeReader(new CustomTypeReader<T>(populateType));
        public void SetCustomTypeReader(Func<ExtensionApi, T> readType) => SetCustomTypeReader(new CustomTypeReader<T>(readType));
        public void SetCustomTypeReader(Func<PreparationApi, Func<ExtensionApi, T, T>> readTypeCreator) => SetCustomTypeReader(new CustomTypeReader<T>(readTypeCreator));

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

    public class MemberSettings<T> : TypeSettings<T>
    {
        public void SetIgnore(bool ignore = true) => this.member_ignore = ignore;
        public void OverrideName(string alternateName) => this.member_overrideName = alternateName;

        public void SetUseStringCache(bool useStringCache)
        {
            this.member_useStringCache = useStringCache;
        }
    }


    private readonly struct CompiledSettings
    {
        public readonly DataAccess dataAccess;        
        public readonly Dictionary<string, Type> customTypeNames;
        public readonly HashSet<Type> forbiddenTypes;

        public readonly ReferenceResolutionMode referenceResolutionMode;
        public readonly ProposedTypeMode proposedTypeMode;
        public readonly BackingFieldMode backingFieldMode;
        public readonly bool addCaseVariantsForCustomTypeNames;

        public readonly int initialBufferSize;
        public readonly bool castObjectArrayToCommonTypeArray;
        public readonly bool rethrowExceptions;
        public readonly bool logCatchedExceptions;
        public readonly bool strict;
        public readonly bool populateExistingMembers;
        public readonly bool useStringCache;
        public readonly int stringCacheBitSize;
        public readonly int stringCacheMaxLength;
        public readonly bool allowUninitializedObjectCreation;
        public readonly bool anyUsesStringCache;
        public readonly bool anyAllowsProposedTypes;

        public readonly Settings.TypeWhitelistMode typeWhitelistMode;
        public readonly HashSet<Type> allowedTypes;
        public readonly HashSet<string> allowedNamespacePrefixes;

        public readonly Dictionary<Type, BaseTypeSettings> typeSettingsDict;

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
            for(int i=0; i<allTypeSettings.Count; i++)
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
            if (!anyTypeHasReferenceResolutionEnabled && referenceResolutionMode == ReferenceResolutionMode.OnlyPerType)
            {
                referenceResolutionMode = ReferenceResolutionMode.ForceDisabled;
            }
        }
    }

    public interface ICustomTypeReader<T>
    {
        void PrepareReader(PreparationApi api);
        T ReadValue(ExtensionApi api);
        T ReadValue(ExtensionApi api, T itemToPopulate);
        bool CanPopulateExistingValue { get; }
    }

    public class CustomTypeReader<T> : ICustomTypeReader<T>
    {
        Func<ExtensionApi, T> readValue;
        Func<ExtensionApi, T, T> populateValue;
        Func<PreparationApi, Func<ExtensionApi, T, T>> prepareReader;
        Func<T> construct;

        public CustomTypeReader(Func<PreparationApi, Func<ExtensionApi, T, T>> prepareReader)
        {
            if (prepareReader == null) throw new ArgumentNullException(nameof(prepareReader));
            this.prepareReader = prepareReader;            
        }

        public CustomTypeReader(Func<ExtensionApi, T, T> populateValue)
        {
            if (populateValue == null) throw new ArgumentNullException(nameof(populateValue));
            this.populateValue = populateValue;
        }

        public CustomTypeReader(Func<ExtensionApi, T> readValue)
        {
            if (readValue == null) throw new ArgumentNullException(nameof(readValue));
            this.readValue = readValue;
        }

        public bool CanPopulateExistingValue => populateValue != null;

        public void PrepareReader(PreparationApi api)
        {
            construct = api.GetContructor<T>();
            if (prepareReader != null)
            {
                populateValue = prepareReader(api);
                readValue = (api) => populateValue(api, construct());
            }
            else if (populateValue != null)
            {
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

        public T ReadValue(ExtensionApi api) => readValue(api);

        public T ReadValue(ExtensionApi api, T itemToPopulate) => populateValue(api, itemToPopulate);
    }



}
