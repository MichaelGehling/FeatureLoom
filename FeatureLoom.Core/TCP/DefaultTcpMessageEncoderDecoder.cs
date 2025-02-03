using FeatureLoom.Extensions;
using FeatureLoom.Helpers;
using FeatureLoom.Logging;
using FeatureLoom.MetaDatas;
using FeatureLoom.Serialization;
using FeatureLoom.Storages;
using System;
using System.Text;

namespace FeatureLoom.TCP
{
    public class DefaultTcpMessageEncoderDecoder : ITcpMessageEncoder, ITcpMessageDecoder
    {
        public class Config : Configuration
        {
            public string headerStartMarker = "°>";
            public string charEncodingName = "utf-8";
            public bool uselittleEndian = BitConverter.IsLittleEndian;
        }

        private Config config;

        private Encoding encoding = Encoding.UTF8;
        private byte[] headerStartMarkerBytes = null;        

        public DefaultTcpMessageEncoderDecoder(Config config = null)
        {
            config = config ?? new Config();
            this.config = config;

            if (this.config.charEncodingName != null) encoding = Encoding.GetEncoding(config.charEncodingName);
            if (config.headerStartMarker == null) config.headerStartMarker = "°>";
            headerStartMarkerBytes = encoding.GetBytes(config.headerStartMarker);
        }

        public byte[] Encode(object obj)
        {
            try
            {
                // 0 = undefined 1 = object 2 = string 3 = byte[]
                byte type = 0;
                if (!(obj is byte[]) && !(obj is string))
                {
                    if (type == 0) type = 1;
                    obj = JsonHelper.DefaultSerializer.Serialize(obj);                    
                }

                if (obj is string strObj)
                {
                    if (type == 0) type = 2;
                    obj = encoding.GetBytes(strObj);
                }

                if (obj is byte[] byteObj)
                {
                    if (type == 0) type = 3;
                    byte[] lengthInfo = byteObj.Length.ToBytes(config.uselittleEndian);
                    int totalLength = headerStartMarkerBytes.Length + lengthInfo.Length + sizeof(byte) + byteObj.Length;

                    int writePos = 0;
                    byte[] buffer = new byte[totalLength];
                    Array.Copy(headerStartMarkerBytes, 0, buffer, writePos, headerStartMarkerBytes.Length);
                    writePos += headerStartMarkerBytes.Length;

                    Array.Copy(lengthInfo, 0, buffer, writePos, lengthInfo.Length);
                    writePos += lengthInfo.Length;

                    buffer[writePos] = type;
                    writePos += sizeof(byte);

                    Array.Copy(byteObj, 0, buffer, writePos, byteObj.Length);
                    writePos += byteObj.Length;

                    return buffer;
                }
            }
            catch (Exception e)
            {
                OptLog.ERROR()?.Build("Encoding failed!", e.ToString());
            }
            return null;
        }

        public DecodingResult Decode(byte[] buffer, int bufferFillState, ref int bufferReadPosition, out object decodedMessage, ref object intermediateData)
        {
            var restartBufferReadPosition = bufferReadPosition;
            int leftBytes;

            if (!TryFindStartMarker(buffer, bufferFillState, ref bufferReadPosition))
            {
                bufferReadPosition = restartBufferReadPosition;
                decodedMessage = null;
                return DecodingResult.Incomplete;
            }
            restartBufferReadPosition = bufferReadPosition;
            bufferReadPosition += headerStartMarkerBytes.Length;

            leftBytes = bufferFillState - bufferReadPosition;
            if (leftBytes < sizeof(int))
            {
                bufferReadPosition = restartBufferReadPosition;
                decodedMessage = null;
                return DecodingResult.Incomplete;
            }
            int payloadLength = buffer.ToInt32(bufferReadPosition, config.uselittleEndian);
            bufferReadPosition += sizeof(int);

            leftBytes = bufferFillState - bufferReadPosition;
            if (leftBytes < sizeof(char))
            {
                bufferReadPosition = restartBufferReadPosition;
                decodedMessage = null;
                return DecodingResult.Incomplete;
            }
            byte type = buffer[bufferReadPosition];
            bufferReadPosition += sizeof(byte);

            leftBytes = bufferFillState - bufferReadPosition;
            if (leftBytes < payloadLength)
            {
                bufferReadPosition = restartBufferReadPosition;
                decodedMessage = null;
                return DecodingResult.Incomplete;
            }

            try
            {
                switch (type)
                {
                    case 3:
                        byte[] byteResult = new byte[payloadLength];
                        Array.Copy(buffer, bufferReadPosition, byteResult, 0, payloadLength);
                        decodedMessage = byteResult;
                        return DecodingResult.Complete;

                    case 2:
                        string stringResult = encoding.GetString(buffer, bufferReadPosition, payloadLength);
                        decodedMessage = stringResult;
                        return DecodingResult.Complete;

                    case 1:                        
                        if (!JsonHelper.DefaultDeserializer.TryDeserialize(new ByteSegment(buffer, bufferReadPosition, payloadLength), out decodedMessage)) throw new Exception("Failed deserializing json");
                        return DecodingResult.Complete;

                    default:
                        decodedMessage = null;
                        OptLog.WARNING()?.Build($"Unknown type byte {type.ToString()} in message buffer!");
                        return DecodingResult.Invalid;
                }
            }
            catch (Exception e)
            {
                OptLog.WARNING()?.Build($"Decoding message payload failed!", e.ToString());
                decodedMessage = null;
                return DecodingResult.Invalid;
            }
            finally
            {
                bufferReadPosition += payloadLength;
            }
        }

        private bool TryFindStartMarker(byte[] buffer, int bufferFillState, ref int bufferReadPosition)
        {
            bool startMarkerFound = false;
            int leftBytes = bufferFillState - bufferReadPosition;
            while (!startMarkerFound && leftBytes >= headerStartMarkerBytes.Length)
            {
                bool differs = false;
                for (int i = 0; i < headerStartMarkerBytes.Length; i++)
                {
                    if (buffer[bufferReadPosition + i] != headerStartMarkerBytes[i])
                    {
                        differs = true;
                        continue;
                    }
                }
                if (differs) bufferReadPosition++;
                else startMarkerFound = true;
            }

            return startMarkerFound;
        }
    }
}