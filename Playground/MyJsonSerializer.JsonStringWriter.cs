using System.Runtime.CompilerServices;
using System.Text;

namespace Playground
{
    public static partial class MyJsonSerializer
    {
        public sealed class JsonStringWriter : IJsonWriter
        {
            private StringBuilder sb;

            public JsonStringWriter(int bufferSize)
            {
                this.sb = new StringBuilder(bufferSize);
            }

            public int UsedBuffer => sb.Length;

            public override string ToString() => sb.ToString();

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WriteNullValue() => sb.Append("null");

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void OpenObject() => sb.Append("{");

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void CloseObject() => sb.Append("}");

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WriteTypeInfo(string typeName) => sb.Append("\"$type\":\"").Append(typeName).Append("\",");

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WriteValueFieldName() => sb.Append("\"$value\":");

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WritePrimitiveValue<T>(T value) => sb.Append(value.ToString());

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WriteStringValue(string str) => sb.Append('\"').WriteEscapedString(str).Append('\"');

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WriteRefObject(string refPath) => sb.Append("{\"$ref\":\"").Append(refPath).Append("\"}");

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void OpenCollection() => sb.Append("[");

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void CloseCollection() => sb.Append("]");

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WriteComma() => sb.Append(",");

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WritePreparedString(string str) => sb.Append(str);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static StringBuilder WriteEscapedString(this StringBuilder sb, string str)
        {
            foreach (char c in str)
            {
                switch (c)
                {
                    case '\"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default: sb.Append(c); break;
                }
            }
            return sb;
        }
    }

}
