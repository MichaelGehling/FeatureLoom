using System;

namespace FeatureLoom.Serialization;

public sealed partial class JsonDeserializer
{
    /// <summary>
    /// Non-generic base for custom reader definitions resolved from open-generic registrations.
    /// </summary>
    public abstract class CustomTypeReaderDefinition
    {
        internal abstract object PrepareReader(PreparationApi api);
    }

    /// <summary>
    /// Defines a custom reader for <typeparamref name="T"/> as a class that can also be registered
    /// as an open-generic reader definition.
    /// </summary>
    /// <typeparam name="T">Type handled by this reader definition.</typeparam>
    /// <remarks>
    /// Open-generic implementations need a public parameterless constructor. They are closed,
    /// instantiated, and prepared once for each constructed type while its reader is prepared.
    /// </remarks>
    public abstract class CustomTypeReaderDefinition<T> : CustomTypeReaderDefinition
    {
        /// <summary>Creates the custom reader used for <typeparamref name="T"/>.</summary>
        /// <param name="api">Preparation-only API for composing the reader.</param>
        protected abstract ICustomTypeReader<T> Prepare(PreparationApi api);

        internal sealed override object PrepareReader(PreparationApi api) =>
            Prepare(api) ?? throw new Exception($"The custom reader definition {GetType()} returned null.");
    }

    internal sealed class OpenGenericTypeReaderDefinition
    {
        readonly Type genericTypeDefinition;
        readonly Type definitionTypeDefinition;

        internal OpenGenericTypeReaderDefinition(Type genericTypeDefinition, Type definitionTypeDefinition)
        {
            if (genericTypeDefinition == null) throw new ArgumentNullException(nameof(genericTypeDefinition));
            if (definitionTypeDefinition == null) throw new ArgumentNullException(nameof(definitionTypeDefinition));
            if (!genericTypeDefinition.IsGenericTypeDefinition) throw new ArgumentException($"{genericTypeDefinition} is not a generic type definition, e.g. typeof(MyType<>).", nameof(genericTypeDefinition));
            if (!definitionTypeDefinition.IsGenericTypeDefinition) throw new ArgumentException($"{definitionTypeDefinition} is not a generic type definition, e.g. typeof(MyReader<>).", nameof(definitionTypeDefinition));
            if (definitionTypeDefinition.IsAbstract) throw new ArgumentException($"{definitionTypeDefinition} is abstract and cannot be instantiated.", nameof(definitionTypeDefinition));
            if (!typeof(CustomTypeReaderDefinition).IsAssignableFrom(definitionTypeDefinition)) throw new ArgumentException($"{definitionTypeDefinition} does not derive from {typeof(CustomTypeReaderDefinition<>)}.", nameof(definitionTypeDefinition));

            int typeArity = genericTypeDefinition.GetGenericArguments().Length;
            int definitionArity = definitionTypeDefinition.GetGenericArguments().Length;
            if (typeArity != definitionArity) throw new ArgumentException($"{definitionTypeDefinition} has {definitionArity} generic parameter(s), but {genericTypeDefinition} has {typeArity}. They must match because generic arguments are passed positionally.", nameof(definitionTypeDefinition));

            var constructor = definitionTypeDefinition.GetConstructor(Type.EmptyTypes);
            if (constructor == null || !constructor.IsPublic) throw new ArgumentException($"{definitionTypeDefinition} must have a public parameterless constructor.", nameof(definitionTypeDefinition));

            this.genericTypeDefinition = genericTypeDefinition;
            this.definitionTypeDefinition = definitionTypeDefinition;
        }

        internal CustomTypeReaderDefinition CreateDefinition(Type type)
        {
            if (!type.IsConstructedGenericType || type.GetGenericTypeDefinition() != genericTypeDefinition)
            {
                throw new ArgumentException($"The custom reader definition {definitionTypeDefinition} is registered for {genericTypeDefinition} and cannot handle {type}.");
            }

            Type closedDefinitionType;
            try
            {
                closedDefinitionType = definitionTypeDefinition.MakeGenericType(type.GetGenericArguments());
            }
            catch (ArgumentException e)
            {
                throw new ArgumentException($"The custom reader definition {definitionTypeDefinition} cannot be closed with the generic arguments of {type}. Check its generic constraints.", e);
            }

            Type handledType = GetHandledType(closedDefinitionType);
            if (handledType != type)
            {
                throw new ArgumentException($"The custom reader definition {closedDefinitionType} reads {handledType}, but was used for {type}. It must derive from {typeof(CustomTypeReaderDefinition<>)} closed with its own generic parameters.");
            }

            return (CustomTypeReaderDefinition)Activator.CreateInstance(closedDefinitionType);
        }

        static Type GetHandledType(Type definitionType)
        {
            for (Type current = definitionType; current != null; current = current.BaseType)
            {
                if (current.IsConstructedGenericType && current.GetGenericTypeDefinition() == typeof(CustomTypeReaderDefinition<>)) return current.GetGenericArguments()[0];
            }
            return null;
        }
    }
}
