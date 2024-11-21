using FeatureLoom.Helpers;
using System;

namespace FeatureLoom.Serialization;

public sealed partial class FeatureJsonDeserializer
{
    public struct UndoReadHandle : IDisposable
    {
        private FeatureJsonDeserializer deserializer;
        private int startBufferPos;
        private bool undoReading;

        public bool SetUndoReading(bool undo) => this.undoReading = undo;

        public UndoReadHandle(FeatureJsonDeserializer deserializer, bool initUndo) : this()
        {
            this.deserializer = deserializer;
            undoReading = initUndo;
            startBufferPos = deserializer.bufferPos;
            deserializer.peekStack.Push(deserializer.bufferPos);
        }

        public EquatableByteSegment GetReadBytes() => new EquatableByteSegment(deserializer.buffer, startBufferPos, deserializer.bufferPos - startBufferPos);

        public void Dispose()
        {
            if (undoReading) deserializer.bufferPos = deserializer.peekStack.Pop();
            else deserializer.peekStack.Pop();
        }
    }
}
