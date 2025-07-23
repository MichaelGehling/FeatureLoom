using FeatureLoom.Helpers;
using System;
using System.IO;
using Xunit;

namespace FeatureLoom.Helpers
{
    public class ReadOnlyMemoryStreamTests
    {
        [Fact]
        public void CanReadFromStream()
        {
            var data = new byte[] { 1, 2, 3, 4, 5 };
            var stream = new ReadOnlyMemoryStream(data);

            var buffer = new byte[3];
            int bytesRead = stream.Read(buffer, 0, buffer.Length);

            Assert.Equal(3, bytesRead);
            Assert.Equal(new byte[] { 1, 2, 3 }, buffer);
        }

        [Fact]
        public void CanSeekWithinStream()
        {
            var data = new byte[] { 1, 2, 3, 4, 5 };
            var stream = new ReadOnlyMemoryStream(data);

            stream.Seek(2, SeekOrigin.Begin);
            Assert.Equal(2, stream.Position);

            var buffer = new byte[2];
            int bytesRead = stream.Read(buffer, 0, buffer.Length);

            Assert.Equal(2, bytesRead);
            Assert.Equal(new byte[] { 3, 4 }, buffer);
        }

        [Fact]
        public void ThrowsExceptionWhenSeekingOutOfBounds()
        {
            var data = new byte[] { 1, 2, 3, 4, 5 };
            var stream = new ReadOnlyMemoryStream(data);

            Assert.Throws<IOException>(() => stream.Seek(-1, SeekOrigin.Begin));
            Assert.Throws<IOException>(() => stream.Seek(10, SeekOrigin.Begin));
        }

        [Fact]
        public void ThrowsExceptionWhenWritingToStream()
        {
            var data = new byte[] { 1, 2, 3, 4, 5 };
            var stream = new ReadOnlyMemoryStream(data);

            Assert.Throws<NotSupportedException>(() => stream.Write(new byte[1], 0, 1));
        }

        [Fact]
        public void CanReadSpanDirectly()
        {
            var data = new byte[] { 1, 2, 3, 4, 5 };
            var stream = new ReadOnlyMemoryStream(data);

            var buffer = new byte[3];
            int bytesRead = stream.Read(buffer.AsSpan());

            Assert.Equal(3, bytesRead);
            Assert.Equal(new byte[] { 1, 2, 3 }, buffer);
        }
    }
}