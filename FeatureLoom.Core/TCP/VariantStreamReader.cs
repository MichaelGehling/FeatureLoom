using FeatureLoom.Extensions;
using FeatureLoom.Synchronization;
using FeatureLoom.TCP;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace FeatureLoom.TCP
{
    public class VariantStreamReader : IGeneralMessageStreamReader
    {
        public class Settings
        {
            //public string headerStartMarker = "^v^";
            public string headerStartMarker = "";
            public int maxStreamCopyBufferSize = 80 * 1024;
        }
        Settings settings;

        List<ISpecificMessageStreamReader> readers = new List<ISpecificMessageStreamReader>();
        MemoryStream memoryStream = new MemoryStream();
        BinaryReader binaryReader;
        byte[] startMarker;

        public VariantStreamReader()
        {
            this.settings = new Settings();
            startMarker = this.settings.headerStartMarker.ToByteArray();
            memoryStream = new MemoryStream(settings.maxStreamCopyBufferSize);
            binaryReader = new BinaryReader(memoryStream);
        }

        public VariantStreamReader(Settings settings = null, params ISpecificMessageStreamReader[] readers)
        {
            this.settings = settings;
            if (this.settings == null) this.settings = new Settings();

            startMarker = this.settings.headerStartMarker.ToByteArray();            
            memoryStream = new MemoryStream(this.settings.maxStreamCopyBufferSize);
            binaryReader = new BinaryReader(memoryStream);

            this.readers.AddRange(readers);
        }

        public async Task<object> ReadMessage(Stream stream, CancellationToken cancellationToken)
        {
            if (!startMarker.EmptyOrNull() && !await FindStartMarker(stream, cancellationToken).ConfiguredAwait()) return null;
            
            int typeInfoLength = await ReadTypeInfoLength(stream, cancellationToken).ConfiguredAwait();
            if (typeInfoLength < 0) return null;

            var reader = await FindMessageReader(typeInfoLength, stream, cancellationToken).ConfiguredAwait();
            memoryStream.Position += typeInfoLength;

            int messageLength = await ReadMessageLength(stream, cancellationToken).ConfiguredAwait();
            if (messageLength < 0) return null;
            int messageEndPos = (int)memoryStream.Position + messageLength;
            
            if (reader == null)
            {
                byte[] bytes = memoryStream.GetBuffer().CopySection((int)memoryStream.Position, messageLength);
                memoryStream.Position = messageEndPos;
                return bytes;
            }

            if (!await EnsureNextChunkInMemoryStream(stream, messageLength, cancellationToken).ConfiguredAwait()) return null;
            object message = await reader.ReadMessage(memoryStream, messageLength, cancellationToken).ConfiguredAwait();
            memoryStream.Position = messageEndPos;
            return message;
        }

        private async Task<int> ReadMessageLength(Stream stream, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested) return -1;

            if (!await EnsureNextChunkInMemoryStream(stream, sizeof(int), cancellationToken).ConfiguredAwait()) return -1;
            int messageLength = binaryReader.ReadInt32();
            return messageLength;
        }

        private async Task<ISpecificMessageStreamReader> FindMessageReader(int typeInfoLength,Stream stream, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested) return null;
            
            if (!await EnsureNextChunkInMemoryStream(stream, typeInfoLength, cancellationToken).ConfiguredAwait()) return null;
            var buffer = memoryStream.GetBuffer();
            foreach(var reader in readers)
            {
                if (reader.CanRead(buffer, (int)memoryStream.Position, typeInfoLength)) return reader;
            }
            
            //byte[] typeInfo = buffer.Slice((int)memoryStream.Position, typeInfoLength);
            //Log.ERROR($"No SpecificMessageStreamReader can handle message with typeInfo [{typeInfo.GetStringOrNull() ?? typeInfo.AllItemsToString(", ")}].");
            return null;
        }

        private async Task<int> ReadTypeInfoLength(Stream stream, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested) return -1;

            if (!await EnsureNextChunkInMemoryStream(stream, 1, cancellationToken).ConfiguredAwait()) return -1;
            int typeInfoLength = memoryStream.ReadByte();        
            return typeInfoLength;
        }

        private async Task<bool> FindStartMarker(Stream stream, CancellationToken cancellationToken)
        {
            int nextChunkSize = startMarker.Length;
            while (true)
            {
                if (cancellationToken.IsCancellationRequested) return false;
                
                if (!await EnsureNextChunkInMemoryStream(stream, nextChunkSize, cancellationToken).ConfiguredAwait()) return false;

                if (cancellationToken.IsCancellationRequested) return false;
                
                int startMarkerPos = 0;
                while(memoryStream.GetSizeOfLeftData() > 0)
                {
                    if (binaryReader.ReadByte() != startMarker[startMarkerPos++]) 
                    {
                        startMarkerPos = 0;
                        continue;
                    }
                    if (startMarkerPos >= startMarker.Length) return true;
                }
            }            
        }

        private async Task<bool> EnsureNextChunkInMemoryStream(Stream stream, int nextChunkSize, CancellationToken cancellationToken)
        {
            while (memoryStream.GetSizeOfLeftData() < nextChunkSize)
            {
                EnsureFreeMemoryStreamCapacity(nextChunkSize);
                int numBytesRead = await stream.ReadAsync(memoryStream.GetBuffer(), (int)memoryStream.Length, memoryStream.GetLeftCapacity(), cancellationToken).ConfiguredAwait();
                if (numBytesRead > 0)
                {
                    var pos = memoryStream.Position;
                    memoryStream.Write(memoryStream.GetBuffer(), (int)memoryStream.Length, numBytesRead);
                    memoryStream.Position = pos;
                }
                if (cancellationToken.IsCancellationRequested) return false;
            }
            return true;
        }

        private void EnsureFreeMemoryStreamCapacity(int minCapacityLeft)
        {
            int capacityLeft = memoryStream.GetLeftCapacity();
            int missingCapacity = minCapacityLeft - capacityLeft;
            if (missingCapacity <= 0) return;

            if (memoryStream.Position > missingCapacity)
            {
                var buffer = memoryStream.GetBuffer();
                Array.Copy(buffer, (int)memoryStream.Position, buffer, 0, memoryStream.GetSizeOfLeftData());
            }
            else
            {
                memoryStream.Capacity = memoryStream.Capacity + missingCapacity;
            }
        }

        public void Dispose()
        {
            foreach (var reader in readers)
            {
                reader.Dispose();
            }
            memoryStream.Dispose();
        }
    }
}
