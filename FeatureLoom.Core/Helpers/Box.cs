using System;

namespace FeatureLoom.Helpers;

/// <summary>
/// Non-generic interface for a value container ("box") that allows clearing, getting, and setting values in a type-safe way.
/// </summary>
public interface IBox
{
    /// <summary>
    /// Clears the contained value (sets it to default).
    /// </summary>
    void Clear();

    /// <summary>
    /// Gets the contained value, cast to the specified type.
    /// Throws an exception if T doesn't match with the Box's type.
    /// </summary>
    /// <typeparam name="T">The type to cast the value to.</typeparam>
    /// <returns>The contained value as type T.</returns>
    T GetValue<T>();

    /// <summary>
    /// Sets the contained value, casting from the specified type.
    /// Throws an exception if T doesn't match with the Box's type.
    /// </summary>
    /// <typeparam name="T">The type of the value to set.</typeparam>
    /// <param name="value">The value to set.</param>
    void SetValue<T>(T value);
}

/// <summary>
/// A generic value container ("box") that mimics the comparison behavior of Nullable&lt;T&gt;.
/// Supports equality and comparison with both Box&lt;T&gt; and T, as well as null.
/// </summary>
/// <typeparam name="T">The type of the value to contain.</typeparam>
public class Box<T> : IBox
{
    /// <summary>
    /// The contained value.
    /// </summary>
    public T value;

    /// <summary>
    /// Initializes a new, empty box (value is default).
    /// </summary>
    public Box()
    {
    }

    /// <summary>
    /// Initializes a new box with the specified value.
    /// </summary>
    /// <param name="value">The value to contain.</param>
    public Box(T value)
    {
        this.value = value;
    }

    /// <summary>
    /// Clears the contained value (sets it to default).
    /// </summary>
    public void Clear() => value = default;

    /// <summary>
    /// Implicit conversion from T to Box&lt;T&gt;.
    /// </summary>
    /// <param name="value">The value to box.</param>
    public static implicit operator Box<T>(T value) => new Box<T>(value);

    /// <summary>
    /// Determines whether this box is equal to another object.
    /// Returns true if the other object is a Box&lt;T&gt; or a T with the same value.
    /// </summary>
    /// <param name="obj">The object to compare to.</param>
    public override bool Equals(object obj)
    {
        if (ReferenceEquals(this, obj)) return true;
        if (obj is Box<T> otherBox) return Equals(value, otherBox.value);
        if (obj is T tValue) return Equals(value, tValue);
        return false;
    }

    /// <summary>
    /// Returns a hash code for the contained value.
    /// </summary>
    public override int GetHashCode()
    {
        return value == null ? 0 : value.GetHashCode();
    }

    /// <summary>
    /// Equality operator for two Box&lt;T&gt; instances.
    /// </summary>
    public static bool operator ==(Box<T> left, Box<T> right)
    {
        if (ReferenceEquals(left, right)) return true;
        if (ReferenceEquals(left, null) || ReferenceEquals(right, null)) return false;
        return Equals(left.value, right.value);
    }

    /// <summary>
    /// Inequality operator for two Box&lt;T&gt; instances.
    /// </summary>
    public static bool operator !=(Box<T> left, Box<T> right) => !(left == right);

    /// <summary>
    /// Equality operator for Box&lt;T&gt; and T.
    /// </summary>
    public static bool operator ==(Box<T> box, T value)
    {
        if (ReferenceEquals(box, null)) return value == null;
        return Equals(box.value, value);
    }

    /// <summary>
    /// Inequality operator for Box&lt;T&gt; and T.
    /// </summary>
    public static bool operator !=(Box<T> box, T value) => !(box == value);

    /// <summary>
    /// Equality operator for T and Box&lt;T&gt;.
    /// </summary>
    public static bool operator ==(T value, Box<T> box)
    {
        if (ReferenceEquals(box, null)) return value == null;
        return Equals(value, box.value);
    }

    /// <summary>
    /// Inequality operator for T and Box&lt;T&gt;.
    /// </summary>
    public static bool operator !=(T value, Box<T> box) => !(value == box);

    /// <summary>
    /// Returns the string representation of the contained value, or an empty string if the value is null.
    /// </summary>
    public override string ToString()
    {
        return value?.ToString() ?? string.Empty;
    }

    /// <summary>
    /// Gets the contained value, cast to the specified type.
    /// Throws an exception if the type does not match.
    /// </summary>
    /// <typeparam name="T1">The type to cast the value to.</typeparam>
    /// <returns>The contained value as type T1.</returns>
    public T1 GetValue<T1>()
    {
        if (value is T1 castedValue) return castedValue;
        throw new Exception($"Wrong type! Box has a value of type {typeof(T)}, while requested was a {typeof(T1)}");
    }

    /// <summary>
    /// Sets the contained value, casting from the specified type.
    /// Throws an exception if the type does not match.
    /// </summary>
    /// <typeparam name="T1">The type of the value to set.</typeparam>
    /// <param name="value">The value to set.</param>
    public void SetValue<T1>(T1 value)
    {
        if (value is T castedValue)
        {
            this.value = castedValue;
            return;
        }
        throw new Exception($"Wrong type! Box has a value of type {typeof(T)}, while set was a {typeof(T1)}");
    }
}