using System.Text;
using FeatureLoom.Extensions;
using System.IO;
using System.Runtime.CompilerServices;

namespace Playground
{
    public static partial class MyJsonSerializer
    {
        public sealed class JsonUTF8StreamWriter : IJsonWriter
        {
            private Stream stream;
            private StreamWriter stringWriter;


            public JsonUTF8StreamWriter(Stream stream)
            {
                this.stream = stream;
                this.stringWriter = new StreamWriter(stream, Encoding.UTF8);
            }

            public override string ToString() => stream.ReadToString();


            static readonly byte[] NULL = "null".ToByteArray();
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WriteNullValue() => stream.Write(NULL, 0, NULL.Length);

            static readonly byte[] OPEN_OBJECT = "{".ToByteArray();
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void OpenObject() => stream.Write(OPEN_OBJECT, 0, OPEN_OBJECT.Length);

            static readonly byte[] CLOSE_OBJECT = "}".ToByteArray();
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void CloseObject() => stream.Write(CLOSE_OBJECT, 0, CLOSE_OBJECT.Length);

            static readonly byte[] TYPEINFO_PRE = "\"$type\":\"".ToByteArray();
            static readonly byte[] TYPEINFO_POST = "\",".ToByteArray();
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WriteTypeInfo(string typeName)
            {
                stream.Write(TYPEINFO_PRE, 0, TYPEINFO_PRE.Length);
                stringWriter.Write(typeName);
                stream.Write(TYPEINFO_POST, 0, TYPEINFO_POST.Length);
            }

            static readonly byte[] VALUEFIELDNAME = "\"$value\":".ToByteArray();
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WriteValueFieldName() => stream.Write(VALUEFIELDNAME, 0, VALUEFIELDNAME.Length);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WritePrimitiveValue<T>(T value) => stringWriter.Write(value.ToString());

            static readonly byte[] STRINGVALUE_PRE = "\"".ToByteArray();            
            static readonly byte[] STRINGVALUE_POST = "\"".ToByteArray();
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WriteStringValue(string str)
            {
                stream.Write(STRINGVALUE_PRE, 0,  STRINGVALUE_PRE.Length);
                stringWriter.WriteEscapedString(str);
                stream.Write(STRINGVALUE_POST, 0, STRINGVALUE_POST.Length);
            }

            static readonly byte[] REFOBJECT_PRE = "{\"$ref\":\"".ToByteArray();
            static readonly byte[] REFOBJECT_POST = "\"}".ToByteArray();
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WriteRefObject(string refPath)
            {
                stream.Write(REFOBJECT_PRE, 0, REFOBJECT_PRE.Length);
                stringWriter.Write(refPath);
                stream.Write(REFOBJECT_POST, 0, REFOBJECT_POST.Length);
            }

            static readonly byte[] OPENCOLLECTION = "[".ToByteArray();
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void OpenCollection() => stream.Write(OPENCOLLECTION, 0, OPENCOLLECTION.Length);

            static readonly byte[] CLOSECOLLECTION = "]".ToByteArray();
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void CloseCollection() => stream.Write(CLOSECOLLECTION, 0, CLOSECOLLECTION.Length);

            static readonly byte[] COMMA = ",".ToByteArray();
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WriteComma() => stream.Write(COMMA, 0, COMMA.Length);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WritePreparedString(string str) => stringWriter.Write(str);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteEscapedString(this StreamWriter writer, string str)
        {
            foreach (char c in str)
            {
                switch (c)
                {
                    case '\"': writer.Write("\\\""); break;
                    case '\\': writer.Write("\\\\"); break;
                    case '\b': writer.Write("\\b"); break;
                    case '\f': writer.Write("\\f"); break;
                    case '\n': writer.Write("\\n"); break;
                    case '\r': writer.Write("\\r"); break;
                    case '\t': writer.Write("\\t"); break;
                    default: writer.Write(c); break;
                }
            }
        }
    }
}
