namespace Playground
{
    public static partial class MyJsonSerializer
    {
        public interface IJsonWriter
        {
            void CloseCollection();
            void CloseObject();
            void OpenCollection();
            void OpenObject();
            string ToString();
            void WriteComma();
            void WriteNullValue();
            void WritePreparedString(string str);
            void WritePrimitiveValue<T>(T value);
            void WriteRefObject(string refPath);
            void WriteStringValue(string str);
            void WriteTypeInfo(string typeName);
            void WriteValueFieldName();
        }
    }
}
