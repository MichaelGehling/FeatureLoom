using FeatureLoom.Extensions;
using FeatureLoom.Logging;
using FeatureLoom.TCP;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FeatureLoom.TCP
{

    public class VariantStreamWriter : IGeneralMessageStreamWriter
    {
        public class Settings
        {
            //public string headerStartMarker = "^v^";
            public string headerStartMarker = "";
            public int maxStreamCopyBufferSize = 80 * 1024;
        }
        Settings settings;

        List<ISpecificMessageStreamWriter> writers = new List<ISpecificMessageStreamWriter>();
        MemoryStream memoryStream = new MemoryStream();
        BinaryWriter binaryWriter;
        byte[] startMarker;

        public VariantStreamWriter()
        {            
            settings= new Settings();

            startMarker = settings.headerStartMarker.ToByteArray();
            binaryWriter= new BinaryWriter(memoryStream);
        }

        public VariantStreamWriter(Settings settings = null, params ISpecificMessageStreamWriter[] writers)
        {   
            this.settings = settings;
            if (this.settings == null) this.settings = new Settings();

            startMarker = this.settings.headerStartMarker.ToByteArray();
            binaryWriter = new BinaryWriter(memoryStream);

            this.writers.AddRange(writers);
        }

        public void AddWriter(ISpecificMessageStreamWriter writer)
        {
            writers.Add(writer);
        }

        public void Dispose()
        {
            foreach(var writer in writers) 
            { 
                writer.Dispose(); 
            }
            binaryWriter.Dispose();
            memoryStream.Dispose();
        }

        public async Task WriteMessage<T>(T message, Stream stream, CancellationToken cancellationToken)
        {
            ISpecificMessageStreamWriter writer = null;
            byte[] typeInfo= null;
            for (int i= 0; i < writers.Count; i++)
            {
                if (!writers[i].CanWrite(message, out typeInfo)) continue;
                writer = writers[i];
                break;
            }
            if (writer == null) throw new Exception($"No writer is able to write message {message}!");
            if (typeInfo == null) typeInfo = Array.Empty<byte>();            
            if (typeInfo.Length > byte.MaxValue) throw new Exception($"TypeInfo is longer than {byte.MaxValue.ToString()} bytes!");

            binaryWriter.Write(startMarker);
            binaryWriter.Write((byte)typeInfo.Length);
            binaryWriter.Write(typeInfo);
            long lengthPos = memoryStream.Length;
            binaryWriter.Write((uint)0);
            long messagePos = memoryStream.Length;
            await writer.WriteMessage(message, memoryStream, cancellationToken).ConfigureAwait(false);
            long messageLength = memoryStream.Length - messagePos;
            memoryStream.Position = lengthPos;
            binaryWriter.Write((int)messageLength);

            memoryStream.Position = 0;
            await memoryStream.CopyToAsync(stream, (int)memoryStream.Length.ClampHigh(settings.maxStreamCopyBufferSize), cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);

            // Reset
            memoryStream.SetLength(0);
        }
    }
}
