using FeatureLoom.Collections;
using System;
using System.IO;
using System.Text;

namespace FeatureLoom.Serialization;

public class Base64EncodingStream : Stream
{
    private readonly Stream _outputStream;
    private readonly byte[] _inputBuffer = new byte[3]; // Buffer for input bytes (3 bytes = 4 Base64 chars)
    private int _inputBufferIndex = 0;

    private readonly byte[] _outputBuffer;
    private int _outputBufferIndex = 0;

    private bool _propagateFlush;
    private bool _propagateDispose;

    private static readonly byte[] Base64Chars = System.Text.Encoding.ASCII.GetBytes("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/");

    public Base64EncodingStream(Stream outputStream, int bufferSize = 4096, bool propagateFlush = true, bool propagateDispose = true)
    {
        _outputStream = outputStream ?? throw new ArgumentNullException(nameof(outputStream));
        _outputBuffer = new byte[bufferSize];
        _propagateFlush = propagateFlush;
        _propagateDispose = propagateDispose;
    }

    public override bool CanRead => false; // This stream only supports writing (encoding)
    public override bool CanSeek => false;
    public override bool CanWrite => true;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

    // Write the Base64-encoded data in chunks to the output buffer and flush it when full
    public override void Write(byte[] buffer, int offset, int count)
    {        
        int inputPos = offset;
        int maxInputPos = inputPos + count - 1;

        // Step 1: Fill remaining input buffer if partially full
        while (_inputBufferIndex > 0 && inputPos <= maxInputPos)
        {
            _inputBuffer[_inputBufferIndex++] = buffer[inputPos++];
            if (_inputBufferIndex == 3)
            {
                WriteBufferedBytesToBase64();
                _inputBufferIndex = 0;
            }
        }

        // Step 2: Process full chunks of 3 bytes directly (current byte + 2)
        while (inputPos + 2 <= maxInputPos)
        {            
            _inputBuffer[0] = buffer[inputPos++];
            _inputBuffer[1] = buffer[inputPos++];
            _inputBuffer[2] = buffer[inputPos++];
            _inputBufferIndex = 3;
            WriteBufferedBytesToBase64();
            _inputBufferIndex = 0;
        }

        // Step 3: Handle any remaining bytes (less than 3)
        while (inputPos <= maxInputPos)
        {
            _inputBuffer[_inputBufferIndex++] = buffer[inputPos++];
        }
    }

    private void WriteBufferedBytesToBase64()
    {
        // If the output buffer is full, write it to the underlying stream
        if (_outputBufferIndex + 4 >= _outputBuffer.Length)
        {
            _outputStream.Write(_outputBuffer, 0, _outputBufferIndex);
            _outputBufferIndex = 0;
        }

        int bufferValue = (_inputBuffer[0] << 16) & 0xFFFFFF;
        bufferValue |= (_inputBuffer[1] << 8);
        bufferValue |= _inputBuffer[2];

        // Encode 3 bytes into 4 Base64 bytes
        _outputBuffer[_outputBufferIndex++] = Base64Chars[(bufferValue >> 18) & 0x3F];
        _outputBuffer[_outputBufferIndex++] = Base64Chars[(bufferValue >> 12) & 0x3F];
        _outputBuffer[_outputBufferIndex++] = Base64Chars[(bufferValue >> 6) & 0x3F];
        _outputBuffer[_outputBufferIndex++] = Base64Chars[bufferValue & 0x3F];        
    }

    private void WriteRemainingBufferedBytesToBase64()
    {
        // If the output buffer is full, write it to the underlying stream
        if (_outputBufferIndex + 4 >= _outputBuffer.Length)
        {
            _outputStream.Write(_outputBuffer, 0, _outputBufferIndex);
            _outputBufferIndex = 0;
        }

        int bufferValue = (_inputBuffer[0] << 16) & 0xFFFFFF;
        if (_inputBufferIndex > 1) bufferValue |= (_inputBuffer[1] << 8);
        if (_inputBufferIndex > 2) bufferValue |= _inputBuffer[2];

        // Encode 3 bytes into 4 Base64 bytes
        _outputBuffer[_outputBufferIndex++] = Base64Chars[(bufferValue >> 18) & 0x3F];
        _outputBuffer[_outputBufferIndex++] = Base64Chars[(bufferValue >> 12) & 0x3F];
        _outputBuffer[_outputBufferIndex++] = _inputBufferIndex > 1 ? Base64Chars[(bufferValue >> 6) & 0x3F] : (byte)'=';
        _outputBuffer[_outputBufferIndex++] = _inputBufferIndex > 2 ? Base64Chars[bufferValue & 0x3F] : (byte)'=';
    }

    // Flush the buffer and ensure all Base64 data is written
    public override void Flush()
    {
        if (_inputBufferIndex > 0)
        {
            // Encode any remaining bytes in the input buffer
            WriteRemainingBufferedBytesToBase64();
            _inputBufferIndex = 0;
        }

        if (_outputBufferIndex > 0)
        {
            // Write any remaining Base64 data in the output buffer
            _outputStream.Write(_outputBuffer, 0, _outputBufferIndex);
            _outputBufferIndex = 0;
        }

        if (_propagateFlush) _outputStream.Flush();
    }

    // This stream only supports writing, not reading
    public override int Read(byte[] buffer, int offset, int count)
    {
        throw new NotSupportedException();
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        throw new NotSupportedException();
    }

    public override void SetLength(long value)
    {
        throw new NotSupportedException();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Flush(); // Ensure any remaining data is flushed
            if (_propagateDispose) _outputStream.Dispose();
        }
        base.Dispose(disposing);
    }
}

