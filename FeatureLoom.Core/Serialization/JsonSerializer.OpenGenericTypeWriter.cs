using FeatureLoom.Extensions;
using System;

namespace FeatureLoom.Serialization;

public sealed partial class JsonSerializer
{
    /// <summary>
    /// Non-generic base of <see cref="CustomTypeWriterDefinition{T}"/>. Exists only so the
    /// serializer can apply a definition whose handled type is not known statically, which is
    /// the case for definitions closed by reflection from an open generic registration.
    /// </summary>
    public abstract class CustomTypeWriterDefinition
    {
        // Internal abstract on purpose: it keeps this base unusable as a direct extension point,
        // so CustomTypeWriterDefinition<T> stays the only way to define a custom writer.
        internal abstract void Apply(JsonSerializer serializer, CachedTypeWriter typeWriter, Type type);
    }

    /// <summary>
    /// Defines a custom writer for <typeparamref name="T"/> as a class instead of a lambda, so it
    /// can be declared for an open generic type: a definition
    /// <c>class MyWriter&lt;TItem&gt; : CustomTypeWriterDefinition&lt;My&lt;TItem&gt;&gt;</c> is
    /// registered as <c>typeof(MyWriter&lt;&gt;)</c> and closed once per constructed type.
    /// </summary>
    /// <typeparam name="T">The type this definition writes.</typeparam>
    /// <remarks>
    /// Implementations need a public parameterless constructor, because an instance is created
    /// per constructed type. Both the instantiation and <see cref="Prepare"/> happen once, when
    /// the type's writer is built, so neither is on the write path.
    /// </remarks>
    public abstract class CustomTypeWriterDefinition<T> : CustomTypeWriterDefinition
    {
        /// <summary>
        /// Builds the writer for <typeparamref name="T"/>. Called once per handled type.
        /// </summary>
        /// <param name="api">Preparation API used to build the writer.</param>
        /// <returns>The writer used for every value of the handled type.</returns>
        protected abstract CustomWriter<T> Prepare(WriterPreparationApi api);

        internal sealed override void Apply(JsonSerializer serializer, CachedTypeWriter typeWriter, Type type)
        {
            var customWriter = Prepare(new WriterPreparationApi(serializer));
            serializer.ApplyCustomWriter(typeWriter, customWriter, type);
        }
    }

    /// <summary>
    /// Registered custom writer that delegates to an already created definition instance.
    /// Used for the closed type registration, where no reflection is needed.
    /// </summary>
    internal sealed class DefinitionTypeWriterCreator<T> : ITypeHandlerCreator
    {
        readonly CustomTypeWriterDefinition<T> definition;

        internal DefinitionTypeWriterCreator(CustomTypeWriterDefinition<T> definition)
        {
            this.definition = definition ?? throw new ArgumentNullException(nameof(definition));
        }

        public bool SupportsType(Type type) => typeof(T) == type;

        public void CreateTypeHandler(JsonSerializer serializer, CachedTypeWriter typeWriter, Type type)
        {
            if (!type.IsAssignableTo(typeof(T))) throw new ArgumentException($"The custom writer for type {typeof(T).FullName} is not compatible with the actual item type {type.FullName}");

            definition.Apply(serializer, typeWriter, type);
        }
    }

    /// <summary>
    /// Registered custom writer for a generic type definition. It closes the definition class
    /// with the generic arguments of the constructed type and runs its preparation.
    /// This happens once per constructed type, when that type's writer is created.
    /// </summary>
    internal sealed class OpenGenericTypeWriterCreator : ITypeHandlerCreator
    {
        readonly Type genericTypeDefinition;
        readonly Type definitionTypeDefinition;

        internal OpenGenericTypeWriterCreator(Type genericTypeDefinition, Type definitionTypeDefinition)
        {
            if (genericTypeDefinition == null) throw new ArgumentNullException(nameof(genericTypeDefinition));
            if (definitionTypeDefinition == null) throw new ArgumentNullException(nameof(definitionTypeDefinition));
            if (!genericTypeDefinition.IsGenericTypeDefinition) throw new ArgumentException($"{genericTypeDefinition} is not a generic type definition, e.g. typeof(MyType<>).", nameof(genericTypeDefinition));
            if (!definitionTypeDefinition.IsGenericTypeDefinition) throw new ArgumentException($"{definitionTypeDefinition} is not a generic type definition, e.g. typeof(MyWriter<>).", nameof(definitionTypeDefinition));
            if (definitionTypeDefinition.IsAbstract) throw new ArgumentException($"{definitionTypeDefinition} is abstract and cannot be instantiated.", nameof(definitionTypeDefinition));
            if (!typeof(CustomTypeWriterDefinition).IsAssignableFrom(definitionTypeDefinition)) throw new ArgumentException($"{definitionTypeDefinition} does not derive from {typeof(CustomTypeWriterDefinition<>)}.", nameof(definitionTypeDefinition));

            // The generic arguments are passed on positionally, so the arities must match. Any
            // other mapping would have to be guessed and is rejected instead.
            int typeArity = genericTypeDefinition.GetGenericArguments().Length;
            int definitionArity = definitionTypeDefinition.GetGenericArguments().Length;
            if (typeArity != definitionArity) throw new ArgumentException($"{definitionTypeDefinition} has {definitionArity} generic parameter(s), but {genericTypeDefinition} has {typeArity}. They must match, because the generic arguments are passed on positionally.", nameof(definitionTypeDefinition));

            this.genericTypeDefinition = genericTypeDefinition;
            this.definitionTypeDefinition = definitionTypeDefinition;
        }

        public bool SupportsType(Type type) => type.IsConstructedGenericType && type.GetGenericTypeDefinition() == genericTypeDefinition;

        public void CreateTypeHandler(JsonSerializer serializer, CachedTypeWriter typeWriter, Type type)
        {
            if (!SupportsType(type)) throw new ArgumentException($"The custom writer definition {definitionTypeDefinition} is registered for {genericTypeDefinition} and cannot handle {type}.");

            Type closedDefinitionType;
            try
            {
                closedDefinitionType = definitionTypeDefinition.MakeGenericType(type.GetGenericArguments());
            }
            catch (ArgumentException e)
            {
                // Typically a generic constraint on the definition that the actual type arguments
                // do not satisfy. The raw message does not mention the involved types, so it is
                // wrapped in one that does.
                throw new ArgumentException($"The custom writer definition {definitionTypeDefinition} cannot be closed with the generic arguments of {type}. Check its generic constraints.", e);
            }

            Type handledType = GetHandledType(closedDefinitionType);
            if (handledType != type) throw new ArgumentException($"The custom writer definition {closedDefinitionType} writes {handledType}, but was used for {type}. It must derive from {typeof(CustomTypeWriterDefinition<>)} closed with its own generic parameters, e.g. MyWriter<T> : CustomTypeWriterDefinition<MyType<T>>.");

            var definition = (CustomTypeWriterDefinition)Activator.CreateInstance(closedDefinitionType);
            definition.Apply(serializer, typeWriter, type);
        }

        /// <summary>
        /// Returns the type argument the definition was closed with, i.e. the type it writes.
        /// </summary>
        static Type GetHandledType(Type definitionType)
        {
            for (Type current = definitionType; current != null; current = current.BaseType)
            {
                if (current.IsConstructedGenericType && current.GetGenericTypeDefinition() == typeof(CustomTypeWriterDefinition<>)) return current.GetGenericArguments()[0];
            }
            return null;
        }
    }
}
