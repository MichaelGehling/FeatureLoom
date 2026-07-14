using FeatureLoom.Collections;
using FeatureLoom.Helpers;
using System;
using System.Runtime.CompilerServices;
using System.Text;

namespace FeatureLoom.Extensions
{
    public static class StringBuilderExtensions
    {
        /// <summary>
        /// Appends an interpolated string directly to the <see cref="StringBuilder"/> buffer without
        /// creating an intermediate string. Each literal part and interpolated value is appended
        /// individually, so this behaves like a sequence of <see cref="StringBuilder.Append(string)"/>
        /// calls but with a more convenient interpolation syntax.
        /// </summary>
        /// <example>
        /// <code>sb.Append($"Hello {name}, you have {count} messages");</code>
        /// </example>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static StringBuilder Append(this StringBuilder sb, [InterpolatedStringHandlerArgument("sb")] ref StringBuilderInterpolationHandler handler)
        {
            return sb;
        }

        
        extension(StringBuilder)
        {
            /// <summary>
            /// Builds a string from an interpolated string using a thread-locally cached <see cref="StringBuilder"/>:
            /// a builder is rented from a thread-static slot (a new one is created on first use per thread, or on a
            /// reentrant nested call), the interpolated parts are appended directly into it, the final string is
            /// produced through <see cref="StringExtensions.BuildWithCache"/> (deduplicated via
            /// <see cref="StringInternCache"/>), and the builder is cleared and stored back in the slot for reuse.
            /// No intermediate string is allocated for the interpolation.
            /// Exposed as a static extension member so it can be called as <c>StringBuilder.BuildCachedString($"...")</c>.
            /// </summary>
            /// <example>
            /// <code>string s = StringBuilder.BuildCachedString($"Hello {name}, you have {count} messages");</code>
            /// </example>
            /// <param name="cache">Optional <see cref="StringInternCache"/> to use for deduplicating the resulting string. If null, the shared cache is used.</param>
            /// <returns>The (possibly cache-shared) resulting string.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static string BuildCachedString(ref PooledStringBuilderInterpolationHandler handler, StringInternCache cache = null)
            {
                StringBuilder sb = handler.Builder;
                string result = sb.BuildWithCache(cache);
                ReturnPooledBuilder(sb);
                return result;
            }

            /// <summary>
            /// Builds a string from an interpolated string using a thread-locally cached <see cref="StringBuilder"/>,
            /// without interning the result. A builder is rented from a thread-static slot (a new one is created on
            /// first use per thread, or on a reentrant nested call), the interpolated parts are appended directly
            /// into it, <see cref="object.ToString"/> produces the final string, and the builder is cleared and
            /// stored back in the slot for reuse. Prefer this over <see cref="BuildCachedString"/> for
            /// high-cardinality or unbounded unique strings, where interning only adds cache pressure without payoff.
            /// <para>
            /// NOTE: This is mainly beneficial on older frameworks (.NET Framework 4.8 / .NET Standard 2.0/2.1),
            /// where a plain <c>$"..."</c> compiles to <see cref="string.Format(string, object[])"/> and therefore
            /// allocates a params array and boxes value-type arguments. On .NET 6+ a plain interpolated string
            /// already uses a non-boxing, buffer-pooled handler, so this method is essentially equivalent to
            /// <c>$"..."</c> there and offers little benefit.
            /// </para>
            /// </summary>
            /// <example>
            /// <code>string s = StringBuilder.BuildString($"Hello {name}, you have {count} messages");</code>
            /// </example>
            /// <returns>The resulting string (always a freshly allocated instance).</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static string BuildString(ref PooledStringBuilderInterpolationHandler handler)
            {
                StringBuilder sb = handler.Builder;
                string result = sb.ToString();
                ReturnPooledBuilder(sb);
                return result;
            }
        }

        // Single thread-static StringBuilder slot instead of a full SharedPool<T>: this handler only ever
        // needs at most one builder per thread at a time, so a plain field avoids the stack/lock/delegate
        // overhead of the general-purpose pool. On rent, the slot is cleared out (so a reentrant call on
        // the same thread - e.g. building one interpolated string while formatting a value for another -
        // safely falls back to a fresh instance instead of aliasing the same builder). On return, the
        // builder is cleared and stored back for reuse; a reentrant fallback instance is simply discarded
        // to the GC as an ordinary short-lived object instead of forcing it into the slot.
        [ThreadStatic]
        static StringBuilder threadStaticBuilder;

        /// <summary>
        /// Rents a <see cref="StringBuilder"/> from the thread-local slot, or creates a new one if the
        /// slot is empty (e.g. first use on this thread, or a reentrant nested call).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static StringBuilder RentPooledBuilder()
        {
            StringBuilder sb = threadStaticBuilder;
            if (sb == null) return new StringBuilder();
            threadStaticBuilder = null;
            return sb;
        }

        /// <summary>
        /// Clears and returns a <see cref="StringBuilder"/> rented via <see cref="RentPooledBuilder"/> back
        /// to the thread-local slot for reuse.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void ReturnPooledBuilder(StringBuilder sb)
        {
            sb.Clear();
            threadStaticBuilder = sb;
        }
    }

    /// <summary>
    /// Interpolated string handler that appends the individual parts of an interpolated string
    /// straight into a target <see cref="StringBuilder"/>.
    /// </summary>
    [InterpolatedStringHandler]
    public ref struct StringBuilderInterpolationHandler
    {
        private readonly StringBuilder sb;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public StringBuilderInterpolationHandler(int literalLength, int formattedCount, StringBuilder sb)
        {
            this.sb = sb;
            if (literalLength > 0) sb.EnsureCapacity(sb.Length + literalLength);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AppendLiteral(string value) => sb.Append(value);

        // ----- generic value holes: {value}, {value:format}, {value,align}, {value,align:format} -----

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AppendFormatted<T>(T value) => AppendValue(value, null);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AppendFormatted<T>(T value, string format) => AppendValue(value, format);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AppendFormatted<T>(T value, int alignment) => AppendAligned(value, alignment, null);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AppendFormatted<T>(T value, int alignment, string format) => AppendAligned(value, alignment, format);

        // ----- string holes (dedicated overloads avoid going through the generic path) -----

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AppendFormatted(string value) => sb.Append(value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AppendFormatted(string value, int alignment = 0, string format = null) => AppendAligned(value, alignment, null);

#if NETSTANDARD2_1_OR_GREATER || NET
        // ----- ReadOnlySpan<char> holes: no allocation, appended directly (netstandard2.1+/net) -----

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AppendFormatted(ReadOnlySpan<char> value) => sb.Append(value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AppendFormatted(ReadOnlySpan<char> value, int alignment = 0, string format = null)
        {
            if (alignment == 0) { sb.Append(value); return; }
            int start = sb.Length;
            sb.Append(value);
            Pad(start, alignment);
        }
#endif

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AppendAligned<T>(T value, int alignment, string format)
        {
            if (alignment == 0) { AppendValue(value, format); return; }
            int start = sb.Length;
            AppendValue(value, format);
            Pad(start, alignment);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Pad(int start, int alignment)
        {
            int written = sb.Length - start;
            int pad = (alignment < 0 ? -alignment : alignment) - written;
            if (pad <= 0) return;
            if (alignment < 0) sb.Append(' ', pad);   // left aligned -> pad on the right
            else sb.Insert(start, " ", pad);           // right aligned -> pad on the left
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AppendValue<T>(T value, string format)
        {
            // Fast, boxing-free path for the common primitives and known char sources.
            if (format == null && TryAppendKnown(value)) return;

            if (value == null) return;

#if NET6_0_OR_GREATER
            // Formats value types straight into a stack buffer without boxing or a ToString() allocation.
            if (value is ISpanFormattable) { AppendSpanFormattable(value, format); return; }
#endif
            if (format != null && value is IFormattable formattable) { sb.Append(formattable.ToString(format, null)); return; }
            sb.Append(value.ToString());
        }

        // Appends value types via their dedicated StringBuilder.Append overloads using Unsafe.As instead of
        // boxing (typeof(T) comparisons are folded to constants by the JIT). Reference sources are matched by pattern.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryAppendKnown<T>(T value)
        {
            if (typeof(T) == typeof(int)) { sb.Append(Unsafe.As<T, int>(ref value)); return true; }
            if (typeof(T) == typeof(long)) { sb.Append(Unsafe.As<T, long>(ref value)); return true; }
            if (typeof(T) == typeof(double)) { sb.Append(Unsafe.As<T, double>(ref value)); return true; }
            if (typeof(T) == typeof(float)) { sb.Append(Unsafe.As<T, float>(ref value)); return true; }
            if (typeof(T) == typeof(decimal)) { sb.Append(Unsafe.As<T, decimal>(ref value)); return true; }
            if (typeof(T) == typeof(bool)) { sb.Append(Unsafe.As<T, bool>(ref value)); return true; }
            if (typeof(T) == typeof(char)) { sb.Append(Unsafe.As<T, char>(ref value)); return true; }
            if (typeof(T) == typeof(byte)) { sb.Append(Unsafe.As<T, byte>(ref value)); return true; }
            if (typeof(T) == typeof(sbyte)) { sb.Append(Unsafe.As<T, sbyte>(ref value)); return true; }
            if (typeof(T) == typeof(short)) { sb.Append(Unsafe.As<T, short>(ref value)); return true; }
            if (typeof(T) == typeof(ushort)) { sb.Append(Unsafe.As<T, ushort>(ref value)); return true; }
            if (typeof(T) == typeof(uint)) { sb.Append(Unsafe.As<T, uint>(ref value)); return true; }
            if (typeof(T) == typeof(ulong)) { sb.Append(Unsafe.As<T, ulong>(ref value)); return true; }
            if (typeof(T) == typeof(TextSegment)) { var s = Unsafe.As<T, TextSegment>(ref value); sb.Append(s.UnderlyingString, s.Offset, s.Count); return true; }
            if (typeof(T) == typeof(ArraySegment<char>)) { var s = Unsafe.As<T, ArraySegment<char>>(ref value); sb.Append(s.Array, s.Offset, s.Count); return true; }
            if (value is string str) { sb.Append(str); return true; }
            if (value is char[] chars) { sb.Append(chars); return true; }
            if (value is StringBuilder other) { sb.Append(other); return true; }
            return false;
        }

#if NET6_0_OR_GREATER
        private void AppendSpanFormattable<T>(T value, string format)
        {
            Span<char> buffer = stackalloc char[256];
            if (((ISpanFormattable)value).TryFormat(buffer, out int written, format, null)) sb.Append(buffer.Slice(0, written));
            else if (value is IFormattable formattable) sb.Append(formattable.ToString(format, null));
            else sb.Append(value.ToString());
        }
#endif
    }

    /// <summary>
    /// Interpolated string handler used by <see cref="StringBuilderExtensions.BuildCachedString"/> and
    /// <see cref="StringBuilderExtensions.BuildString"/>. It rents a <see cref="StringBuilder"/> from a
    /// thread-static slot (see <see cref="StringBuilderExtensions.RentPooledBuilder"/>) in its constructor and
    /// appends the interpolated parts directly into it, avoiding an intermediate string. The rented builder is
    /// exposed through <see cref="Builder"/> so the calling method can build the final string and return the
    /// builder to the slot.
    /// </summary>
    [InterpolatedStringHandler]
    public ref struct PooledStringBuilderInterpolationHandler
    {
        private StringBuilderInterpolationHandler inner;

        /// <summary>The <see cref="StringBuilder"/> rented from the pool that receives the appended parts.</summary>
        public StringBuilder Builder { get; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public PooledStringBuilderInterpolationHandler(int literalLength, int formattedCount)
        {
            Builder = StringBuilderExtensions.RentPooledBuilder();
            inner = new StringBuilderInterpolationHandler(literalLength, formattedCount, Builder);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AppendLiteral(string value) => inner.AppendLiteral(value);
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AppendFormatted<T>(T value) => inner.AppendFormatted(value);
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AppendFormatted<T>(T value, string format) => inner.AppendFormatted(value, format);
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AppendFormatted<T>(T value, int alignment) => inner.AppendFormatted(value, alignment);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AppendFormatted<T>(T value, int alignment, string format) => inner.AppendFormatted(value, alignment, format);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AppendFormatted(string value) => inner.AppendFormatted(value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AppendFormatted(string value, int alignment = 0, string format = null) => inner.AppendFormatted(value, alignment, format);

#if NETSTANDARD2_1_OR_GREATER || NET
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AppendFormatted(ReadOnlySpan<char> value) => inner.AppendFormatted(value);
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AppendFormatted(ReadOnlySpan<char> value, int alignment = 0, string format = null) => inner.AppendFormatted(value, alignment, format);
#endif
    }
}

#if !NET6_0_OR_GREATER
namespace System.Runtime.CompilerServices
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
    internal sealed class InterpolatedStringHandlerAttribute : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
    internal sealed class InterpolatedStringHandlerArgumentAttribute : Attribute
    {
        public InterpolatedStringHandlerArgumentAttribute(string argument) => Arguments = new[] { argument };

        public InterpolatedStringHandlerArgumentAttribute(params string[] arguments) => Arguments = arguments;

        public string[] Arguments { get; }
    }
}
#endif
