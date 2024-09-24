using System;
using System.IO;

namespace FeatureLoom.Serialization;

public class Base64DecodingStream : Stream
{
    private readonly Stream _inputStream;
    private readonly byte[] _inputBuffer; // Buffer for Base64 input
    private readonly byte[] _outputBuffer;
    private int _outputBufferCount = 0;
    private int _outputBufferIndex = 0;
    private int _inputBufferIndex = 0;
    private int _inputBufferCount = 0;
    private bool _propagateDispose;

    // Mapping of Base64 characters to their corresponding 6-bit values
    private static readonly byte[] Base64Lookup = new byte[256];

    static Base64DecodingStream()
    {
        // Initialize the Base64 character lookup table
        for (int i = 0; i < Base64Lookup.Length; i++)
        {
            Base64Lookup[i] = 0xFF; // Invalid character
        }

        const string base64Chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/="; //incl. '=' for padding

        for (int i = 0; i < base64Chars.Length; i++)
        {
            Base64Lookup[base64Chars[i]] = (byte)i;
        }
    }

    public Base64DecodingStream(Stream inputStream, int bufferSize = 4096, bool propagateDispose = true)
    {
        _inputStream = inputStream ?? throw new ArgumentNullException(nameof(inputStream));
        _outputBuffer = new byte[bufferSize];
        _inputBuffer = new byte[bufferSize];
        _propagateDispose = propagateDispose;
    }

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

    public override int Read(byte[] buffer, int offset, int count)
    {
        int totalRead = 0;

        while (totalRead < count)
        {
            int bytesLeft = _outputBufferCount - _outputBufferIndex;

            // Fill the output buffer if it's empty
            if (bytesLeft == 0)
            {
                _outputBufferCount = 0;
                _outputBufferIndex = 0;

                if (!FillOutputBuffer()) break; // No more data to read
                bytesLeft = _outputBufferCount - _outputBufferIndex;
            }

            // Calculate how much we can read from the output buffer
            int bytesToCopy = Math.Min(count - totalRead, bytesLeft);
            Array.Copy(_outputBuffer, _outputBufferIndex, buffer, offset + totalRead, bytesToCopy);
            totalRead += bytesToCopy;
            _outputBufferIndex += bytesToCopy;
        }

        return totalRead;
    }

    public override int ReadByte()
    {
        int bytesLeft = _outputBufferCount - _outputBufferIndex;

        // Fill the output buffer if it's empty
        if (bytesLeft == 0)
        {
            _outputBufferCount = 0;
            _outputBufferIndex = 0;

            if (!FillOutputBuffer()) return -1; // No data to read
            bytesLeft = _outputBufferCount - _outputBufferIndex;
        }

        // Read a byte from the output buffer
        return _outputBuffer[_outputBufferIndex++];
    }

    private bool FillOutputBuffer()
    {
        // Reset input buffer for reading
        _inputBufferCount = _inputStream.Read(_inputBuffer, 0, _inputBuffer.Length);
        if (_inputBufferCount == 0)
        {
            return false; // End of stream reached
        }

        // Process the input buffer
        int validCharCount = 0;

        for (int i = 0; i < _inputBufferCount; i++)
        {
            byte value = Base64Lookup[_inputBuffer[i]];
            if (value != 0xFF) // Valid Base64 character
            {
                // Store the valid value directly in the input buffer
                _inputBuffer[validCharCount++] = value;
            }
        }
        
        _inputBufferCount = validCharCount;
        _inputBufferIndex = 0;

        // Decode full sets of 4 valid Base64 characters
        while (_inputBufferIndex + 4 <= _inputBufferCount)
        {
            DecodeInputBuffer();            
        }

        // Shift remaining valid characters to the beginning of the buffer
        if (_inputBufferIndex > 0)
        {
            Array.Copy(_inputBuffer, _inputBufferCount - _inputBufferIndex, _inputBuffer, 0, _inputBufferIndex);
        }

        return true; // Data processed successfully
    }

    private void DecodeInputBuffer()
    {
        byte byte0 = _inputBuffer[_inputBufferIndex++];
        byte byte1 = _inputBuffer[_inputBufferIndex++];
        byte byte2 = _inputBuffer[_inputBufferIndex++];
        byte byte3 = _inputBuffer[_inputBufferIndex++];

        // Combine the first four bytes from the input buffer
        int bufferValue = (byte0 << 18) |
                          (byte1 << 12) |
                          (byte2 << 6) |
                          (byte3);

        // Calculate the number of output bytes to write
        int byteCount = 3;
        if (byte2 == (byte)'=') byteCount--;
        if (byte3 == (byte)'=') byteCount--;

        // Fill the output buffer with the decoded bytes
        if (byteCount == 3)
        {
            _outputBuffer[_outputBufferCount++] = (byte)((bufferValue >> 16) & 0xFF);
            _outputBuffer[_outputBufferCount++] = (byte)((bufferValue >> 8) & 0xFF);
            _outputBuffer[_outputBufferCount++] = (byte)(bufferValue & 0xFF);
        }
        else if (byteCount == 2)
        {
            _outputBuffer[_outputBufferCount++] = (byte)((bufferValue >> 16) & 0xFF);
            _outputBuffer[_outputBufferCount++] = (byte)((bufferValue >> 8) & 0xFF);
        }
        else
        {
            _outputBuffer[_outputBufferCount++] = (byte)((bufferValue >> 16) & 0xFF);
        }               
    }

    public override void Flush()
    {
        // No implementation needed for a decoding stream
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (_propagateDispose) _inputStream.Dispose();
        }
        base.Dispose(disposing);
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        throw new NotSupportedException();
    }

    public override void SetLength(long value)
    {
        throw new NotSupportedException();
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        throw new NotImplementedException();
    }
}

