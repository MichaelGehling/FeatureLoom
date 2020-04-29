using System;
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

        public static async Task<byte[]> ReadToByteArrayAsync(this Stream stream, int bufferSize)
        {
            byte[] buffer = new byte[bufferSize];
            int bytesRead = 0;
            bool finished = false;
            byte[] data = null;

            do
            {
                bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                if(bytesRead == 0) finished = true;
                else if(bytesRead < buffer.Length)
                {
                    buffer = buffer.AsSpan(0, bytesRead).ToArray();
                    finished = true;
                }
                if(data == null) data = buffer;
                else data = data.Combine(buffer);
            }
            while(!finished);

            return data;
        }
    }
}