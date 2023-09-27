using FeatureLoom.Extensions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Playground
{
    public sealed class LoopJsonDeserializer
    {
        private class Job
        {

        }
        
        enum State
        {
            Value,
            OpenObject,
            OpenCollection,
            StartString,
            StartNumber,
            SpecialValue,
            Finished,
        }

        int index = 0;
        string json;
        State state;
        char[] buffer = new char[4096];
        StringBuilder sb = new StringBuilder();
        int bufferIndex = 0;
        Type nextExpectedType;
        Type nextAnnouncedType;

        Stack<Job> jobStack = new Stack<Job>();

        /*
        public T Deserialize<T>(string json)
        {
            index = 0;          
            this.json = json;
        }

        private T HandleRootValue<T>()
        {
            state = State.Value;
            nextExpectedType = typeof(T);
            Type type = nextExpectedType;

            char c = SkipWhitespaces(ref index);
            if (c == '{') state = State.OpenObject;
            c = SkipWhitespaces(ref index);
            if (CompareChars(ref index, "\"$type\""))
            {
                c = SkipWhitespaces(ref index);
                if (c != ':') throw new Exception("Field $type had no trailing colon.");
                SkipWhitespaces(ref index);
                if (!TryReadStringValue(out string typeName)) throw new Exception("Field $type had no trailing string value.");
                c = SkipWhitespaces(ref index);
                if (c == ',') SkipWhitespaces(ref index);


            }

            if (type == typeof(string))
            {
                if (TryReadStringValue(out string value) && value is T result) return result;
                throw new Exception($"Value could not be deserialized as {nextExpectedType.FullName}");
            }
            if (nextExpectedType.IsPrimitive)
            {
                if (nextExpectedType == typeof(int))
                {
                    if (TryReadIntValue(out int value) && value is T result) return result;
                }
                else if (nextExpectedType == typeof(double))
                {
                    if (TryReadDoubleValue(out double value) && value is T result) return result;
                }
            }            
        }
        */
        /*
        private void HandleValue_()
        {
            bufferIndex = 0;
            char c = SkipWhitespaces();
            switch (c)
            {
                case '{': state = State.OpenObject; break;
                case '[': state = State.OpenCollection; break;
                case '"': state = State.StartString; break;
                case char _ when char.IsAsciiDigit(c):
                    buffer[bufferIndex++] = c;
                    state = State.StartNumber; 
                    break;
                case char _ when char.IsAsciiLetter(c):
                    buffer[bufferIndex++] = c;
                    state = State.SpecialValue; 
                    break;
                case '\0': state = State.Finished; break;
                default: throw new Exception($"Unhandled char {c} in state {state.ToName()}");
            }
            
        }
        */

        private char SkipWhitespaces(ref int index)
        {
            while (index < json.Length)
            {
                char c = json[index];
                if (char.IsWhiteSpace(c)) index++;
                return c;
            }
            return '\0';
        }

        private char CurrentChar(ref int index)
        {
            return json[index];
        }

        private char NextChar(ref int index)
        {
            while (index < json.Length)
            {
                char c = json[index++];
                return c;
            }
            return '\0';
        }

        private bool CompareChars(ref int index, string comparison)
        {
            if (index + comparison.Length > json.Length) return false;
            for(int i = 0; i < comparison.Length; i++)
            {
                if (comparison[i] != json[index + i]) return false;
            }
            index += comparison.Length;
            return true;
        }



        private bool TryReadStringValue(out string value)
        {
            value = default;
            int peekIndex = index;
            if (json[peekIndex++] != '"') return false;

            bool escaped = false;
            bool endFound = false;
            while(peekIndex < json.Length) 
            {
                char c = json[peekIndex++];
                if (!escaped)
                {
                    if (c == '\\')
                    {
                        escaped = true;
                        continue;
                    }
                    else if (c == '"')
                    {
                        endFound = true;
                        break;
                    }
                }
                
                escaped = false;
                sb.Append(c);
            }
            if (!endFound) return false;
            index = peekIndex;
            value = sb.ToString();
            sb.Clear();
            return true;
        }

        private bool TryReadIntValue(out int value)
        {
            value = default;
            int peekIndex = index;
            bool isNegative = json[peekIndex] == '-';
            if (isNegative) peekIndex++;

            if (!json[peekIndex].IsAsciiDigit()) return false;

            bool hasExponent = false;
            bool hasFractionalPart = false;
            int fractionalDigits = 0;
            value = 0;
            while (peekIndex < json.Length)
            {
                char c = json[peekIndex++];
                if (!c.IsAsciiDigit())
                {
                    if (c == '.') hasFractionalPart = true;
                    else if (c == 'e' || c == 'E') hasExponent = true;
                    break;
                }                
                value *= 10;
                value += c - '0';
            }

            if (hasFractionalPart)
            {
                while (peekIndex < json.Length)
                {
                    char c = json[peekIndex++];
                    if (!c.IsAsciiDigit())
                    {
                        if (c == 'e' || c == 'E') hasExponent = true;
                        break;
                    }
                    buffer[fractionalDigits++] = c;
                }
            }
            
            if (hasExponent)
            {
                bool negativeExponent = json[peekIndex] == '-';
                if (negativeExponent) peekIndex++;

                if (!json[peekIndex].IsAsciiDigit()) return false;

                int exponent = 0;
                while (peekIndex < json.Length)
                {
                    char c = json[peekIndex++];
                    if (!c.IsAsciiDigit()) break;
                    exponent *= 10;
                    exponent += c - '0';
                }
                if (negativeExponent)
                {
                    for (int i = 0; i < exponent; i++)
                    {
                        value /= 10;
                    }
                }
                else
                {
                    int usedFractionalDigits = 0;
                    for (int i = 0; i < exponent; i++)
                    {
                        value *= 10;
                        if (usedFractionalDigits < fractionalDigits)
                        {
                            value += buffer[usedFractionalDigits++] - '0';
                        }
                    }
                }
            }

            if (isNegative) value = -value;
            index = peekIndex;
            return true;
        }

        private bool TryReadDoubleValue(out double value)
        {
            value = default;
            int peekIndex = index;
            bool isNegative = json[peekIndex] == '-';
            if (isNegative) peekIndex++;

            if (!json[peekIndex].IsAsciiDigit()) return false;

            bool hasExponent = false;
            bool hasFractionalPart = false;
            value = 0;
            while (peekIndex < json.Length)
            {
                char c = json[peekIndex++];
                if (!c.IsAsciiDigit())
                {
                    if (c == '.') hasFractionalPart = true;
                    else if (c == 'e' || c == 'E') hasExponent = true;
                    break;
                }
                value *= 10;
                value += c - '0';
            }

            if (hasFractionalPart)
            {
                double fractionalPart = 0;
                double divisor = 1;
                while (peekIndex < json.Length)
                {
                    char c = json[peekIndex++];
                    if (!c.IsAsciiDigit())
                    {
                        if (c == 'e' || c == 'E') hasExponent = true;
                        break;
                    }
                    divisor *= 10;
                    fractionalPart *= 10;
                    fractionalPart += c - '0';
                }
                fractionalPart /= divisor;
                value += fractionalPart;
            }

            if (hasExponent)
            {
                bool negativeExponent = json[peekIndex] == '-';
                if (negativeExponent) peekIndex++;

                if (!json[peekIndex].IsAsciiDigit()) return false;

                int exponent = 0;
                while (peekIndex < json.Length)
                {
                    char c = json[peekIndex++];
                    if (!c.IsAsciiDigit()) break;
                    exponent *= 10;
                    exponent += c - '0';
                }
                if (negativeExponent)
                {
                    for (int i = 0; i < exponent; i++)
                    {
                        value /= 10;
                    }
                }
                else
                {
                    int usedFractionalDigits = 0;
                    for (int i = 0; i < exponent; i++)
                    {
                        value *= 10;                        
                    }
                }
            }

            if (isNegative) value = -value;
            index = peekIndex;
            return true;
        }
    }
}
