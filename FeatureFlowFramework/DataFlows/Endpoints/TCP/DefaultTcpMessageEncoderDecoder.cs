using FeatureFlowFramework.Helpers;
using FeatureFlowFramework.Helpers.Data;
using FeatureFlowFramework.Helpers.Extensions;
using FeatureFlowFramework.Services.DataStorage;
using FeatureFlowFramework.Services.Logging;
using FeatureFlowFramework.Services.MetaData;
using System;
using System.Text;

namespace FeatureFlowFramework.DataFlows.TCP
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

            if(this.config.charEncodingName != null) encoding = Encoding.GetEncoding(config.charEncodingName);
            if(config.headerStartMarker == null) config.headerStartMarker = "°>";
            headerStartMarkerBytes = encoding.GetBytes(config.headerStartMarker);
        }

        public byte[] Encode(object obj)
        {
            try
            {
                // 0 = undefined 1 = object 2 = string 3 = byte[]
                byte type = 0;
                if(!(obj is byte[]) && !(obj is string))
                {
                    if(type == 0) type = 1;
                    obj = obj.ToJson(Json.ComplexObjectsStructure_SerializerSettings);
                }

                if(obj is string strObj)
                {
                    if(type == 0) type = 2;
                    obj = encoding.GetBytes(strObj);
                }

                if(obj is byte[] byteObj)
                {
                    if(type == 0) type = 3;
                    byte[] lengthInfo = byteObj.Length.ToBytes(config.uselittleEndian);
                    int totalLength = headerStartMarkerBytes.Length + lengthInfo.Length + sizeof(byte) + byteObj.Length;

                    int writePos = 0;
                    byte[] buffer = new byte[totalLength];
                    Buffer.BlockCopy(headerStartMarkerBytes, 0, buffer, writePos, headerStartMarkerBytes.Length);
                    writePos += headerStartMarkerBytes.Length;

                    Buffer.BlockCopy(lengthInfo, 0, buffer, writePos, lengthInfo.Length);
                    writePos += lengthInfo.Length;

                    buffer[writePos] = type;
                    writePos += sizeof(byte);

                    Buffer.BlockCopy(byteObj, 0, buffer, writePos, byteObj.Length);
                    writePos += byteObj.Length;

                    return buffer;
                }
            }
            catch(Exception e)
            {
                Log.ERROR(this.GetHandle(), "Encoding failed!", e.ToString());
            }
            return null;
        }

        public DecodingResult Decode(byte[] buffer, int bufferFillState, ref int bufferReadPosition, out object decodedMessage, ref object intermediateData)
        {
            var restartBufferReadPosition = bufferReadPosition;
            int leftBytes;

            if(!TryFindStartMarker(buffer, bufferFillState, ref bufferReadPosition))
            {
                decodedMessage = null;
                return DecodingResult.Incomplete;
            }
            restartBufferReadPosition = bufferReadPosition;
            bufferReadPosition += headerStartMarkerBytes.Length;

            leftBytes = bufferFillState - bufferReadPosition;
            if(leftBytes < sizeof(int))
            {
                bufferReadPosition = restartBufferReadPosition;
                decodedMessage = null;
                return DecodingResult.Incomplete;
            }
            int payloadLength = buffer.ToInt32(bufferReadPosition, config.uselittleEndian);
            bufferReadPosition += sizeof(int);

            leftBytes = bufferFillState - bufferReadPosition;
            if(leftBytes < sizeof(char))
            {
                bufferReadPosition = restartBufferReadPosition;
                decodedMessage = null;
                return DecodingResult.Incomplete;
            }
            byte type = buffer[bufferReadPosition];
            bufferReadPosition += sizeof(byte);

            leftBytes = bufferFillState - bufferReadPosition;
            if(leftBytes < payloadLength)
            {
                bufferReadPosition = restartBufferReadPosition;
                decodedMessage = null;
                return DecodingResult.Incomplete;
            }

            try
            {
                switch(type)
                {
                    case 3:
                        byte[] byteResult = new byte[payloadLength];
                        Buffer.BlockCopy(buffer, bufferReadPosition, byteResult, 0, payloadLength);
                        decodedMessage = byteResult;
                        return DecodingResult.Complete;

                    case 2:
                        string stringResult = encoding.GetString(buffer, bufferReadPosition, payloadLength);
                        decodedMessage = stringResult;
                        return DecodingResult.Complete;

                    case 1:
                        string json = encoding.GetString(buffer, bufferReadPosition, payloadLength);
                        decodedMessage = Json.DeserializeFromJson(json, typeof(object), Json.ComplexObjectsStructure_SerializerSettings);
                        return DecodingResult.Complete;

                    default:
                        decodedMessage = null;
                        Log.WARNING(this.GetHandle(), $"Unknown type byte {type} in message buffer!");
                        return DecodingResult.Invalid;
                }
            }
            catch(Exception e)
            {
                Log.WARNING(this.GetHandle(), $"Decoding message payload failed!", e.ToString());
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
            while(!startMarkerFound && leftBytes >= headerStartMarkerBytes.Length)
            {
                bool differs = false;
                for(int i = 0; i < headerStartMarkerBytes.Length; i++)
                {
                    if(buffer[bufferReadPosition + i] != headerStartMarkerBytes[i])
                    {
                        differs = true;
                        continue;
                    }
                }
                if(differs) bufferReadPosition++;
                else startMarkerFound = true;
            }

            return startMarkerFound;
        }
    }
}