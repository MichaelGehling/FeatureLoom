using System;
using System.Collections.Generic;
using System.Text;

namespace FeatureLoom.Serialization
{
    
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
    public class JsonIgnoreAttribute : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
    public class JsonIncludeAttribute : Attribute
    {
    }

    /// <summary>
    /// Marks a static method of a class or struct that configures how the type itself is serialized
    /// or deserialized. The method may be private.
    /// <para>
    /// <b>Important:</b> the configuration only takes effect if it is enabled in the settings of the
    /// serializer/deserializer: set <c>JsonSerializer.Settings.typeSelfConfigurationMode</c> or
    /// <c>JsonDeserializer.Settings.typeSelfConfigurationMode</c> to <see cref="TypeSelfConfigurationMode.Enabled"/>
    /// (or <see cref="TypeSelfConfigurationMode.EnabledKeepRefTrackingOff"/>). By default
    /// (<see cref="TypeSelfConfigurationMode.IgnoreButWarn"/>) it is ignored and a warning is logged once per type.
    /// Alternatively, <c>Settings.ApplyTypeSelfConfiguration&lt;T&gt;()</c> copies it into the settings, independent of the mode.
    /// </para>
    /// </summary>
    /// <remarks>
    /// <para>
    /// Valid signatures, where <c>T</c> is the declaring type:
    /// <c>static void M(JsonSerializer.TypeWriteSettings&lt;T&gt; settings)</c> for writing and
    /// <c>static void M(JsonDeserializer.TypeSettings&lt;T&gt; settings)</c> for reading.
    /// At most one method per side is allowed.
    /// </para>
    /// <para>
    /// The configuration behaves exactly like an entry created via <c>Settings.ConfigureType&lt;T&gt;()</c>,
    /// but has the lowest priority: anything configured in the settings (for the type or its generic
    /// type definition) overrides it per option. Like settings entries, it is not inherited by derived types.
    /// </para>
    /// <para>
    /// It is resolved once per closed type when its reader/writer is created, so it also works for generic
    /// types (declare the parameter as e.g. <c>TypeSettings&lt;MyType&lt;TItem&gt;&gt;</c>). It applies to
    /// <c>T?</c> for structs as well. An invalid signature throws when the type is first used.
    /// </para>
    /// <para>
    /// Limitation: custom type names and anything else the (de)serializer prepares upfront (e.g. proposed
    /// "$type" resolution, custom writers selected by type predicate) are only known once the type was used.
    /// Use <c>Settings.ApplyTypeSelfConfiguration&lt;T&gt;()</c> when these are needed.
    /// </para>
    /// </remarks>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class JsonTypeConfigurationAttribute : Attribute
    {
    }

    /// <summary>
    /// Determines whether configuration methods marked with <see cref="JsonTypeConfigurationAttribute"/> are applied.
    /// </summary>
    public enum TypeSelfConfigurationMode
    {
        /// <summary>
        /// Type-own configuration is never looked for. Use this to ignore it on purpose; it has no cost.
        /// </summary>
        Ignore,

        /// <summary>
        /// Type-own configuration is not applied, but a warning is logged once per type that has one.
        /// Checking costs one reflection lookup per type when its reader/writer is created, not per value.
        /// </summary>
        IgnoreButWarn,

        /// <summary>
        /// Type-own configuration is applied.
        /// Deserializer: reference resolution is not switched off completely upfront, because a type found
        /// later may enable it. That costs performance when no type actually needs reference resolution.
        /// Custom type names from type-own configuration are only known once the type was used, so they
        /// cannot be resolved from a "$type" before; apply the type configuration to the settings upfront
        /// (<c>Settings.ApplyTypeSelfConfiguration&lt;T&gt;()</c>) if that is required.
        /// Deserializer: the string cache is always created, because a type found later may request it.
        /// </summary>
        Enabled,

        /// <summary>
        /// Like <see cref="Enabled"/>, but on the deserializer reference resolution stays switched off
        /// completely if no settings entry needs it. A type-own configuration enabling reference resolution
        /// is then ignored, i.e. the type behaves differently than if the same configuration were in the settings.
        /// On the serializer this is identical to <see cref="Enabled"/>.
        /// </summary>
        EnabledKeepRefTrackingOff
    }

}
