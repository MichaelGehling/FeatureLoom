﻿using FeatureLoom.Synchronization;
using Microsoft.Identity.Client;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using FeatureLoom.Extensions;
using System.Reflection;
using System.Collections;
using System.Runtime.CompilerServices;

namespace Playground
{
    public sealed partial class FeatureJsonDeserializer
    {
        MicroValueLock serializerLock = new MicroValueLock();
        const int BUFFER_SIZE = 1024 * 64;
        byte[] buffer = new byte[BUFFER_SIZE];
        int bufferPos = 0;
        int bufferFillLevel = 0;
        int bufferResetLevel = BUFFER_SIZE - (1024 * 8);
        long totalBytesRead = 0;
        Stream stream;

        Dictionary<Type, CachedTypeReader> typeReaderCache = new();

        static readonly FilterResult[] map_SkipWhitespaces = CreateFilterMap_SkipWhitespaces();
        static readonly FilterResult[] map_IsFieldEnd = CreateFilterMap_IsFieldEnd();
        static readonly FilterResult[] map_SkipWhitespacesUntilStringStarts = CreateFilterMap_SkipWhitespacesUntilStringStarts();
        static readonly FilterResult[] map_SkipCharsUntilStringEndsOrMultiByteChar = CreateFilterMap_SkipCharsUntilStringEndsOrMultiByteChar();
        static readonly FilterResult[] map_SkipWhitespacesUntilNumberStarts = CreateFilterMap_SkipWhitespacesUntilNumberStarts();
        static readonly FilterResult[] map_SkipFiguresUntilDecimalPointOrExponentOrNumberEnds = CreateFilterMap_SkipFiguresUntilDecimalPointOrExponentOrNumberEnds();
        static readonly FilterResult[] map_SkipFiguresUntilExponentOrNumberEnds = CreateFilterMap_SkipFiguresUntilExponentOrNumberEnds();
        static readonly FilterResult[] map_SkipFiguresUntilNumberEnds = CreateFilterMap_SkipFiguresUntilNumberEnds();

        static ulong[] exponentFactorMap = CreateExponentFactorMap();

        static ulong[] CreateExponentFactorMap()
        {
            ulong[] map = new ulong[21];
            ulong factor = 1;
            map[0] = factor;
            for (int i = 1; i < map.Length; i++)
            {
                factor *= 10;
                map[i] = factor;
            }
            return map;
        }

        public FeatureJsonDeserializer()
        {            
        }

        CachedTypeReader GetCachedTypeReader(Type itemType)
        {
            if (typeReaderCache.TryGetValue(itemType, out var cachedTypeReader)) return cachedTypeReader;
            else return CreateCachedTypeReader(itemType);
        }

        CachedTypeReader CreateCachedTypeReader(Type itemType)
        {
            CachedTypeReader cachedTypeReader = new CachedTypeReader(this);
            typeReaderCache[itemType] = cachedTypeReader;

            if (itemType == typeof(string)) cachedTypeReader.SetTypeReader(ReadStringValue, JsonDataTypeCategory.Primitive);
            else if (itemType == typeof(long)) cachedTypeReader.SetTypeReader(ReadLongValue, JsonDataTypeCategory.Primitive);
            else if (itemType == typeof(long?)) cachedTypeReader.SetTypeReader(ReadNullableLongValue, JsonDataTypeCategory.Primitive);
            else if (itemType == typeof(int)) cachedTypeReader.SetTypeReader(ReadIntValue, JsonDataTypeCategory.Primitive);
            else if (itemType == typeof(int?)) cachedTypeReader.SetTypeReader(ReadNullableIntValue, JsonDataTypeCategory.Primitive);
            else if (itemType == typeof(short)) cachedTypeReader.SetTypeReader(ReadShortValue, JsonDataTypeCategory.Primitive);
            else if (itemType == typeof(short?)) cachedTypeReader.SetTypeReader(ReadNullableShortValue, JsonDataTypeCategory.Primitive);
            else if (itemType == typeof(sbyte)) cachedTypeReader.SetTypeReader(ReadSbyteValue, JsonDataTypeCategory.Primitive);
            else if (itemType == typeof(sbyte?)) cachedTypeReader.SetTypeReader(ReadNullableSbyteValue, JsonDataTypeCategory.Primitive);
            else if (itemType == typeof(ulong)) cachedTypeReader.SetTypeReader(ReadUlongValue, JsonDataTypeCategory.Primitive);
            else if (itemType == typeof(ulong?)) cachedTypeReader.SetTypeReader(ReadNullableUlongValue, JsonDataTypeCategory.Primitive);
            else if (itemType == typeof(uint)) cachedTypeReader.SetTypeReader(ReadUintValue, JsonDataTypeCategory.Primitive);
            else if (itemType == typeof(uint?)) cachedTypeReader.SetTypeReader(ReadNullableUintValue, JsonDataTypeCategory.Primitive);
            else if (itemType == typeof(ushort)) cachedTypeReader.SetTypeReader(ReadUshortValue, JsonDataTypeCategory.Primitive);
            else if (itemType == typeof(ushort?)) cachedTypeReader.SetTypeReader(ReadNullableUshortValue, JsonDataTypeCategory.Primitive);
            else if (itemType == typeof(byte)) cachedTypeReader.SetTypeReader(ReadByteValue, JsonDataTypeCategory.Primitive);
            else if (itemType == typeof(byte?)) cachedTypeReader.SetTypeReader(ReadNullableByteValue, JsonDataTypeCategory.Primitive);
            else if (itemType == typeof(double)) cachedTypeReader.SetTypeReader(ReadDoubleValue, JsonDataTypeCategory.Primitive);
            else if (itemType == typeof(double?)) cachedTypeReader.SetTypeReader(ReadNullableDoubleValue, JsonDataTypeCategory.Primitive);
            else if (itemType == typeof(float)) cachedTypeReader.SetTypeReader(ReadFloatValue, JsonDataTypeCategory.Primitive);
            else if (itemType == typeof(float?)) cachedTypeReader.SetTypeReader(ReadNullableFloatValue, JsonDataTypeCategory.Primitive);
            else if (itemType == typeof(bool)) cachedTypeReader.SetTypeReader(ReadBoolValue, JsonDataTypeCategory.Primitive);
            else if (itemType == typeof(bool?)) cachedTypeReader.SetTypeReader(ReadNullableBoolValue, JsonDataTypeCategory.Primitive);
            else if (TryCreateEnumerableTypeReader(itemType, cachedTypeReader)) { }
            else throw new NotImplementedException();

            return cachedTypeReader;
        }

        private bool TryCreateEnumerableTypeReader(Type itemType, CachedTypeReader cachedTypeReader)
        {
            if (!itemType.TryGetTypeParamsOfGenericInterface(typeof(IEnumerable<>), out Type elementType)) return false;
            if (itemType.IsInterface) throw new NotImplementedException();
                        
            this.InvokeGenericMethod(nameof(CreateEnumerableTypeReader), new Type[] {itemType, elementType}, cachedTypeReader);

            return true;
        }

        private void CreateEnumerableTypeReader<T, E>(CachedTypeReader cachedTypeReader)
        {
            Type itemType = typeof(T);
            Type elementType = typeof(E);            
            var enumerableType = typeof(IEnumerable<>).MakeGenericType(elementType);

            var elementReaderType = typeof(ElementReader<>).MakeGenericType(elementType);
            var elementReaderConstructor = elementReaderType.GetConstructor(new[] { typeof(FeatureJsonDeserializer) });
            var elementReader = elementReaderConstructor.Invoke(new object[] { this });

            var constructor = itemType.GetConstructor(new Type[] { enumerableType });

            cachedTypeReader.SetTypeReader<T>(() =>
            {
                SkipWhiteSpaces();
                if (CurrentByte != '[') throw new Exception("Failed reading Array");
                if (!TryNextByte()) throw new Exception("Failed reading Array");
                T item = (T)constructor.Invoke(new object[] { elementReader });                
                if (CurrentByte != ']') throw new Exception("Failed reading Array");
                TryNextByte();
                return item;
            }, JsonDataTypeCategory.Array);
        }

        class ElementReader<T> : IEnumerable<T>, IEnumerator<T>
        {
            FeatureJsonDeserializer deserializer;
            CachedTypeReader reader;
            T current = default;

            public ElementReader(FeatureJsonDeserializer deserializer)
            {
                this.deserializer = deserializer;
                reader = deserializer.GetCachedTypeReader(typeof(T));
            }

            public T Current => current;

            object IEnumerator.Current => current;

            public bool MoveNext()
            {
                deserializer.SkipWhiteSpaces();
                if (deserializer.CurrentByte == ']') return false;                
                current = reader.ReadItem<T>();
                deserializer.SkipWhiteSpaces();
                if (deserializer.CurrentByte == ',') deserializer.TryNextByte();
                else if (deserializer.CurrentByte != ']') throw new Exception("Failed reading Array");
                return true;               
            }

            public void Reset()
            {
                current = default;
            }

            public void Dispose()
            {
                current = default;
            }

            public IEnumerator<T> GetEnumerator() => this;        

            IEnumerator IEnumerable.GetEnumerator() => this;
        }

        public string ReadStringValue()
        {
            if (!TryReadStringBytes(out var stringBytes)) throw new Exception("Failed reading string");
            return Encoding.UTF8.GetString(stringBytes.Array, stringBytes.Offset, stringBytes.Count);
        }

        public bool ReadBoolValue()
        {
            SkipWhiteSpaces();
            byte b = CurrentByte;
            if (b == 't' || b == 'T')
            {
                if (!TryNextByte()) throw new Exception("Failed reading boolean");
                b = CurrentByte;
                if (b != 'r' && b != 'R') throw new Exception("Failed reading boolean");

                if (!TryNextByte()) throw new Exception("Failed reading boolean");
                b = CurrentByte;
                if (b != 'u' && b != 'U') throw new Exception("Failed reading boolean");

                if (!TryNextByte()) throw new Exception("Failed reading boolean");
                b = CurrentByte;
                if (b != 'e' && b != 'E') throw new Exception("Failed reading boolean");

                if(TryNextByte() && map_IsFieldEnd[CurrentByte] != FilterResult.Found) throw new Exception("Failed reading boolean");
                return true;
            }
            else if (b == 'f' || b == 'F')
            {
                if (!TryNextByte()) throw new Exception("Failed reading boolean");
                b = CurrentByte;
                if (b != 'a' && b != 'A') throw new Exception("Failed reading boolean");

                if (!TryNextByte()) throw new Exception("Failed reading boolean");
                b = CurrentByte;
                if (b != 'l' && b != 'L') throw new Exception("Failed reading boolean");

                if (!TryNextByte()) throw new Exception("Failed reading boolean");
                b = CurrentByte;
                if (b != 's' && b != 'S') throw new Exception("Failed reading boolean");

                if (!TryNextByte()) throw new Exception("Failed reading boolean");
                b = CurrentByte;
                if (b != 'e' && b != 'E') throw new Exception("Failed reading boolean");

                if (TryNextByte() && map_IsFieldEnd[CurrentByte] != FilterResult.Found) throw new Exception("Failed reading boolean");
                return false;
            }
            else throw new Exception("Failed reading boolean");
        }

        public bool? ReadNullableBoolValue() => ReadBoolValue();

        public long ReadLongValue()
        {
            if (!TryReadNumberBytes(out var isNegative, out var integerBytes, out var decimalBytes, out var exponentBytes, out bool isExponentNegative)) throw new Exception("Failed reading number");
            if (decimalBytes.Array != null) throw new Exception("Decimal found for integer");

            ulong integerPart = BytesToInteger(integerBytes);
            long value = (long)integerPart;
            if (isNegative) value *= -1;

            if (exponentBytes.Array != null)
            {
                int exp = (int)BytesToInteger(exponentBytes);
                long expFactor = (long)exponentFactorMap[exp];
                if (isExponentNegative) value /= expFactor;
                else value *= expFactor;
            }

            return value;
        }
        public long? ReadNullableLongValue() => ReadLongValue();

        public int ReadIntValue()
        {
            long longValue = ReadLongValue();
            if (longValue > int.MaxValue || longValue < int.MinValue) throw new Exception("Value is out of bounds.");
            return (int)longValue;
        }
        public int? ReadNullableIntValue() => ReadIntValue();
        
        public short ReadShortValue()
        {
            long longValue = ReadLongValue();
            if (longValue > short.MaxValue || longValue < short.MinValue) throw new Exception("Value is out of bounds.");
            return (short)longValue;
        }
        public short? ReadNullableShortValue() => ReadShortValue();

        public sbyte ReadSbyteValue()
        {
            long longValue = ReadLongValue();
            if (longValue > sbyte.MaxValue || longValue < sbyte.MinValue) throw new Exception("Value is out of bounds.");
            return (sbyte)longValue;
        }
        public sbyte? ReadNullableSbyteValue() => ReadSbyteValue();

        public ulong ReadUlongValue()
        {
            if (!TryReadNumberBytes(out var isNegative, out var integerBytes, out var decimalBytes, out var exponentBytes, out bool isExponentNegative)) throw new Exception("Failed reading number");
            if (decimalBytes.Array != null) throw new Exception("Decimal found for integer");
            if (isNegative) throw new Exception("Value is out of bounds.");

            ulong value = BytesToInteger(integerBytes);

            if (exponentBytes.Array != null)
            {
                int exp = (int)BytesToInteger(exponentBytes);
                ulong expFactor = exponentFactorMap[exp];
                if (isExponentNegative) value /= expFactor;
                else value *= expFactor;
            }

            return value;
        }
        public ulong? ReadNullableUlongValue() => ReadUlongValue();

        public uint ReadUintValue()
        {
            ulong longValue = ReadUlongValue();
            if (longValue > uint.MaxValue) throw new Exception("Value is out of bounds.");
            return (uint)longValue;
        }
        public uint? ReadNullableUintValue() => ReadUintValue();

        public ushort ReadUshortValue()
        {
            ulong longValue = ReadUlongValue();
            if (longValue > ushort.MaxValue) throw new Exception("Value is out of bounds.");
            return (ushort)longValue;
        }
        public ushort? ReadNullableUshortValue() => ReadUshortValue();

        public byte ReadByteValue()
        {
            ulong longValue = ReadUlongValue();
            if (longValue > byte.MaxValue) throw new Exception("Value is out of bounds.");
            return (byte)longValue;
        }
        public byte? ReadNullableByteValue() => ReadByteValue();

        public double ReadDoubleValue()
        {
            if (!TryReadNumberBytes(out var isNegative, out var integerBytes, out var decimalBytes, out var exponentBytes, out bool isExponentNegative)) throw new Exception("Failed reading number");

            ulong integerPart = BytesToInteger(integerBytes);
            double decimalPart = BytesToInteger(decimalBytes);
            double value = decimalPart / exponentFactorMap[decimalBytes.Count];
            value += integerPart;
            if (isNegative) value *= -1;

            if (exponentBytes.Array != null)
            {
                int exp = (int)BytesToInteger(exponentBytes);
                int expFactor = (int)exponentFactorMap[exp];
                if (isExponentNegative) value /= expFactor;
                else value *= expFactor;
            }

            return value;
        }
        public double? ReadNullableDoubleValue() => ReadDoubleValue();

        public float ReadFloatValue() => (float)ReadDoubleValue();
        public float? ReadNullableFloatValue() => (float?)ReadDoubleValue();

        public bool TryDeserialize<T>(Stream stream, out T item, bool continueReading = true)
        {
            serializerLock.Enter();
            try
            {                
                this.stream = stream;
                /*if (!continueReading || bufferPos >= bufferFillLevel)
                {
                    bufferFillLevel = 0;
                    bufferPos = 0;
                }
                totalBytesRead = bufferFillLevel - bufferPos;
                */
                bufferFillLevel = stream.Read(buffer, 0, buffer.Length);
                var reader = GetCachedTypeReader(typeof(T));
                item = reader.ReadItem<T>();
                return true;
            }
            finally
            {
                serializerLock.Exit();
            }
        }

        private async Task<bool> TryReadToBuffer()
        {
            if (bufferFillLevel > bufferResetLevel)
            {
                bufferPos = 0;
                bufferFillLevel = 0;
            }
            int bytesRead = await stream.ReadAsync(buffer, bufferFillLevel, buffer.Length - bufferFillLevel);
            totalBytesRead += bytesRead;
            bufferFillLevel += bytesRead;
            return bufferFillLevel > bufferPos;
        }

        private bool TryNextByte()
        {
            if (++bufferPos < bufferFillLevel) return true;
            --bufferPos;
            return false;            
        }

        private byte CurrentByte => buffer[bufferPos];

        /*
        Action[] CreateMap_HandleNextToken()
        {
            Action[] map = new Action[256];
            for (byte i = 0; i < map.Length; i++)
            {
                if (i == ' ' || i == '\t' || i == '\n' || i == '\r') map[i] = null;
                else if (i >= '0' && i <= '9') map[i] = HandleNumber;
                else if (i == '\"') map[i] = HandleString;
                else if (i == 'N' || i == 'n') map[i] = HandleNull;
                else if (i == 't' || i == 'T' || i == 'f' || i == 'F') map[i] = HandleBool;
                else if (i == '{') map[i] = HandleObject;
                else if (i == '[') map[i] = HandleArray;
                else map[i] = HandleInvalidByte;
            }
            return map;
        }
        */

        ulong BytesToInteger(ArraySegment<byte> bytes)
        {
            ulong value = 0;
            value += (byte)(bytes[0] - (byte)'0');
            for (int i = 1; i < bytes.Count; i++)
            {
                value *= 10;
                value += (byte)(bytes[i] - (byte)'0');                
            }
            return value;
        }

        bool TryReadNumberBytes(out bool isNegative, out ArraySegment<byte> integerBytes, out ArraySegment<byte> decimalBytes, out ArraySegment<byte> exponentBytes, out bool isExponentNegative)
        {
            integerBytes = default;
            decimalBytes = default;
            exponentBytes = default;
            isNegative = false;
            isExponentNegative = false;

            // Skip whitespaces until number starts
            while (true)
            {
                byte b = CurrentByte;
                var result = map_SkipWhitespacesUntilNumberStarts[b];
                if (result == FilterResult.Found) break;
                else if (result == FilterResult.Skip) { if (!TryNextByte()) return false; }                
                else if (result == FilterResult.Unexpected) return false;
            }
            
            // Check if negative
            isNegative = CurrentByte == '-';
            if (isNegative && !TryNextByte()) return false;
            int startPos = bufferPos;

            bool couldNotSkip = false;
            // Read integer part
            while (true)
            {
                byte b = CurrentByte;
                FilterResult result = map_SkipFiguresUntilDecimalPointOrExponentOrNumberEnds[b];
                if (result == FilterResult.Skip) 
                {
                    if (!TryNextByte())
                    {
                        couldNotSkip = true;
                        break;
                    }
                }
                else if (result == FilterResult.Found) break;
                else if (result == FilterResult.Unexpected) return false;
            }
            int count = bufferPos - startPos;
            if (couldNotSkip) count++;
            integerBytes = new ArraySegment<byte>(buffer, startPos, count);

            if (CurrentByte == '.')
            {                
                TryNextByte();
                // Read decimal part
                startPos = bufferPos;
                while (true)
                {
                    byte b = CurrentByte;
                    FilterResult result = map_SkipFiguresUntilExponentOrNumberEnds[b];
                    if (result == FilterResult.Skip)
                    {
                        if (!TryNextByte())
                        {
                            couldNotSkip = true;
                            break;
                        }
                    }
                    else if (result == FilterResult.Found) break;
                    else if (result == FilterResult.Unexpected) return false;
                }
                count = bufferPos - startPos;
                if (couldNotSkip) count++;
                decimalBytes = new ArraySegment<byte>(buffer, startPos, count);
            }

            if (CurrentByte == 'e' || CurrentByte == 'E')
            {
                TryNextByte();
                // Read exponent part
                isExponentNegative = CurrentByte == '-';
                if (isExponentNegative) TryNextByte();
                startPos = bufferPos;
                while (true)
                {
                    byte b = CurrentByte;
                    FilterResult result = map_SkipFiguresUntilNumberEnds[b];
                    if (result == FilterResult.Skip)
                    {
                        if (!TryNextByte())
                        {
                            couldNotSkip = true;
                            break;
                        }
                    }
                    else if (result == FilterResult.Found) break;
                    else if (result == FilterResult.Unexpected) return false;
                }
                count = bufferPos - startPos;
                if (couldNotSkip) count++;
                exponentBytes = new ArraySegment<byte>(buffer, startPos, count);
            }

            return true;
        }

        bool TryReadStringBytes(out ArraySegment<byte> stringBytes)
        {
            stringBytes = default;

            // Skip whitespaces until string starts
            do
            {
                byte b = CurrentByte;
                var result = map_SkipWhitespacesUntilStringStarts[b];
                if (result == FilterResult.Found) break;
                else if (result == FilterResult.Unexpected) return false;
            } while (TryNextByte());
            
            int startPos = bufferPos+1;

            // Skip chars until string ends
            while (TryNextByte())
            {
                byte b = CurrentByte;
                FilterResult result = map_SkipCharsUntilStringEndsOrMultiByteChar[b];
                if (result == FilterResult.Skip) continue;
                else if (result == FilterResult.Found)
                {
                    if (b == (byte)'"')
                    {
                        stringBytes = new ArraySegment<byte>(buffer, startPos, bufferPos - startPos);
                        TryNextByte();
                        return true;
                    }
                    else if ((b & 0b11100000) == 0b11000000) // skip 1 byte
                    {
                        TryNextByte();
                    }
                    else if ((b & 0b11110000) == 0b11100000) // skip 2 bytes
                    {
                        TryNextByte();
                        TryNextByte();
                    }
                    else if ((b & 0b11111000) == 0b11110000) // skip 3 bytes
                    {
                        TryNextByte();
                        TryNextByte();
                        TryNextByte();
                    }
                }
                else return false;
            }
            return false;
        }

        void SkipWhiteSpaces()
        {
            do
            {
                var result = map_SkipWhitespaces[CurrentByte];
                if (result == FilterResult.Found) return;
            } while (TryNextByte());
        }
        
        bool CheckBytesRemaining(int numBytes)
        {
            return !(bufferPos + numBytes >= bufferFillLevel);
        }

        bool PeekNull()
        {
            if (!CheckBytesRemaining(3)) return false;            
            int peekPos = bufferPos;
            if (buffer[peekPos] != 'n' && buffer[peekPos] != 'N') return false;
            peekPos++;
            if (buffer[peekPos] != 'u' && buffer[peekPos] != 'U') return false;
            peekPos++;
            if (buffer[peekPos] != 'l' && buffer[peekPos] != 'L') return false;
            peekPos++;
            if (buffer[peekPos] != 'l' && buffer[peekPos] != 'L') return false;

            if (CheckBytesRemaining(4))
            {
                bufferPos += 4;
                if (map_IsFieldEnd[buffer[++peekPos]] != FilterResult.Found) return false;
            }
            else bufferPos += 3;

            return true;
        }

        enum FilterResult
        {
            Skip,
            Found,
            Unexpected
        }

        static FilterResult[] CreateFilterMap_SkipWhitespacesUntilBoolStarts()
        {
            FilterResult[] map = new FilterResult[256];
            for (int i = 0; i < map.Length; i++)
            {
                if (i == ' ' || i == '\t' || i == '\n' || i == '\r') map[i] = FilterResult.Skip;
                else if (i == 't' || i == 'T' || i == 'f' || i == 'T') map[i] = FilterResult.Found;
                else map[i] = FilterResult.Unexpected;
            }
            return map;
        }

        static FilterResult[] CreateFilterMap_SkipBoolCharsUntilEnds()
        {
            FilterResult[] map = new FilterResult[256];
            for (int i = 0; i < map.Length; i++)
            {
                if ("trueTRUEfalseFALSE".Contains((char)i)) map[i] = FilterResult.Skip;
                else if (i == ' ' || i == '\t' || i == '\n' || i == '\r') map[i] = FilterResult.Found;
                else if (i == ',' || i == ']' || i == '}') map[i] = FilterResult.Found;
                else map[i] = FilterResult.Unexpected;
            }
            return map;
        }

        static FilterResult[] CreateFilterMap_SkipWhitespacesUntilStringStarts()
        {
            FilterResult[] map = new FilterResult[256];
            for (int i = 0; i < map.Length; i++)
            {
                if (i == ' ' || i == '\t' || i == '\n' || i == '\r') map[i] = FilterResult.Skip;                
                else if (i == '\"') map[i] = FilterResult.Found;
                else map[i] = FilterResult.Unexpected;
            }
            return map;
        }

        static FilterResult[] CreateFilterMap_SkipCharsUntilStringEndsOrMultiByteChar()
        {
            FilterResult[] map = new FilterResult[256];
            for (int b = 0; b < map.Length; b++)
            {                
                if (b == '\"') map[b] = FilterResult.Found;
                else if ((b & 0b11100000) == 0b11000000) map[b] = FilterResult.Found;
                else if ((b & 0b11110000) == 0b11100000) map[b] = FilterResult.Found;
                else if ((b & 0b11111000) == 0b11110000) map[b] = FilterResult.Found;
                else if ((b & 0b10000000) == 0b00000000) map[b] = FilterResult.Skip;
                else map[b] = FilterResult.Unexpected;
            }
            return map;
        }

        static FilterResult[] CreateFilterMap_SkipWhitespacesUntilNumberStarts()
        {
            FilterResult[] map = new FilterResult[256];
            for (int i = 0; i < map.Length; i++)
            {
                if (i == ' ' || i == '\t' || i == '\n' || i == '\r') map[i] = FilterResult.Skip;
                else if (i >= '0' && i <= '9') map[i] = FilterResult.Found;
                else if (i >= '-') map[i] = FilterResult.Found;
                else map[i] = FilterResult.Unexpected;
            }
            return map;
        }

        static FilterResult[] CreateFilterMap_SkipFiguresUntilDecimalPointOrExponentOrNumberEnds()
        {
            FilterResult[] map = new FilterResult[256];
            for (int i = 0; i < map.Length; i++)
            {
                if (i >= '0' && i <= '9') map[i] = FilterResult.Skip;
                else if (i == '.') map[i] = FilterResult.Found;
                else if (i == 'e' || i == 'E') map[i] = FilterResult.Found;
                else if (i == ' ' || i == '\t' || i == '\n' || i == '\r') map[i] = FilterResult.Found;
                else if (i == ',' || i == ']' || i == '}') map[i] = FilterResult.Found;
                else map[i] = FilterResult.Unexpected;
            }
            return map;
        }

        static FilterResult[] CreateFilterMap_SkipFiguresUntilExponentOrNumberEnds()
        {
            FilterResult[] map = new FilterResult[256];
            for (int i = 0; i < map.Length; i++)
            {
                if (i >= '0' && i <= '9') map[i] = FilterResult.Skip;
                else if (i == 'e' || i == 'E') map[i] = FilterResult.Found;
                else if (i == ' ' || i == '\t' || i == '\n' || i == '\r') map[i] = FilterResult.Found;
                else if (i == ',' || i == ']' || i == '}') map[i] = FilterResult.Found;
                else map[i] = FilterResult.Unexpected;
            }
            return map;
        }

        static FilterResult[] CreateFilterMap_SkipFiguresUntilNumberEnds()
        {
            FilterResult[] map = new FilterResult[256];
            for (int i = 0; i < map.Length; i++)
            {
                if (i >= '0' && i <= '9') map[i] = FilterResult.Skip;
                else if (i == ' ' || i == '\t' || i == '\n' || i == '\r') map[i] = FilterResult.Found;
                else if (i == ',' || i == ']' || i == '}') map[i] = FilterResult.Found;
                else map[i] = FilterResult.Unexpected;
            }
            return map;
        }

        static FilterResult[] CreateFilterMap_SkipWhitespaces()
        {
            FilterResult[] map = new FilterResult[256];
            for (int i = 0; i < map.Length; i++)
            {
                if (i == ' ' || i == '\t' || i == '\n' || i == '\r') map[i] = FilterResult.Skip;
                else map[i] = FilterResult.Found;
            }
            return map;
        }

        static FilterResult[] CreateFilterMap_IsFieldEnd()
        {
            FilterResult[] map = new FilterResult[256];
            for (int i = 0; i < map.Length; i++)
            {
                if (i == ' ' || i == '\t' || i == '\n' || i == '\r') map[i] = FilterResult.Found;
                else if (i == ',' || i == ']' || i == '}') map[i] = FilterResult.Found;
                else map[i] = FilterResult.Unexpected;
            }
            return map;
        }
    }
}
