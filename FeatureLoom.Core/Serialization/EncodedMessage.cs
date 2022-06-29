using System;
using System.Collections.Generic;
using System.Text;

namespace FeatureLoom.Core.Serialization
{
    public readonly struct EncodedMessage
    {
        public readonly short EncoderId;
        public readonly byte[] Data;

        public EncodedMessage(short encoderId, byte[] data)
        {
            EncoderId = encoderId;
            Data = data;

            new ArraySegment<byte>(Data, 0, Data.Length);
        }
    }

    public class EncoderRegistry
    {
        

    }

    public interface IEncoder
    {
        short EncoderId { get; }
        EncodedMessage Encode<T>(T message);
        bool TryDecode<T>(EncodedMessage encodedMessage, out T message);        
        bool TryDecode<T>(ArraySegment<byte> data, out T message);
    }

}
