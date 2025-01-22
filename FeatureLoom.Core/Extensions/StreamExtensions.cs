using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace FeatureLoom.Extensions
{
    public static class StreamExtensions
    {
        public static string ReadToString(this Stream stream, Encoding encoding = null)
        {
            StreamReader reader = new StreamReader(stream, encoding ?? Encoding.UTF8);
            return reader.ReadToEnd();
        }

        public static Task<string> ReadToStringAsync(this Stream stream, Encoding encoding = null)
        {
            StreamReader reader = new StreamReader(stream, encoding ?? Encoding.UTF8);
            return reader.ReadToEndAsync();
        }

        public static Stream ToStream(this string str, Encoding encoding = null)
        {
            if (encoding == null) encoding = Encoding.UTF8;
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
                bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
                if (bytesRead < buffer.Length) finished = true;
                if (bytesRead > 0)
                {
                    if (data == null) data = new byte[bytesRead];
                    data = data.Combine(buffer, bytesRead);
                }
            }
            while (!finished);

            return data ?? Array.Empty<Byte>();
        }

        public static int GetSizeOfLeftData(this MemoryStream memoryStream)
        {
            return (int)(memoryStream.Length - memoryStream.Position);
        }

        public static int GetLeftCapacity(this MemoryStream memoryStream)
        {
            return (int)(memoryStream.Capacity - memoryStream.Length);
        }
    }
}