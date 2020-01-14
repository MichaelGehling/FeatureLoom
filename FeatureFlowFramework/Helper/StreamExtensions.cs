using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace FeatureFlowFramework.Helper
{
    public static class StreamExtensions
    {
        public static string ReadToString(this Stream stream)
        {
            StreamReader reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }

        public static Task<string> ReadToStringAsync(this Stream stream)
        {
            StreamReader reader = new StreamReader(stream);
            return reader.ReadToEndAsync();
        }

        public static Stream ToStream(this string str, Encoding encoding)
        {
            return new MemoryStream(encoding.GetBytes(str));
        }
    }
}