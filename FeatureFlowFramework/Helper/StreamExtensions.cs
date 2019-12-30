using System.IO;
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
    }
}