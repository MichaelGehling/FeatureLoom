using FeatureLoom.Collections;
using System;
using System.Xml.Serialization;

namespace FeatureLoom.Serialization;

public sealed partial class JsonSerializer
{
    void CreateByteArrayWriter(CachedTypeWriter typeWriter)
    {
        var useBase64 = settings.ResolveWriteByteArrayAsBase64String(typeWriter.TypeSettings);
        var typeInfo = typeWriter.typeInfoHandling;

        typeWriter.SetItemWriter<byte[]>(BuildWriter(), false);

        Action<byte[], bool, ByteSegment> BuildWriter() => (typeInfo, useBase64) switch
        {
            (TypeInfoHandling.AddAllTypeInfo, true) => (item, _, _) => 
                { StartTypeInfoObject(typeWriter); writer.WriteBytesAsBase64(item); FinishTypeInfoObject(typeWriter); },
            (TypeInfoHandling.AddAllTypeInfo, false) => (item, _, _) => 
                { StartTypeInfoObject(typeWriter); writer.WriteBytesAsArray(item); FinishTypeInfoObject(typeWriter); },
            (TypeInfoHandling.AddDeviatingTypeInfo, true) => (item, dev, _) => 
                { if (dev) StartTypeInfoObject(typeWriter); writer.WriteBytesAsBase64(item); if (dev) FinishTypeInfoObject(typeWriter); },
            (TypeInfoHandling.AddDeviatingTypeInfo, false) => (item, dev, _) => 
                { if (dev) StartTypeInfoObject(typeWriter); writer.WriteBytesAsArray(item); if (dev) FinishTypeInfoObject(typeWriter); },
            (_, true) => (item, _, _) => writer.WriteBytesAsBase64(item),
            (_, false) => (item, _, _) => writer.WriteBytesAsArray(item)
        };
    }

    void CreateByteSegmentWriter(CachedTypeWriter typeWriter, bool nullable)
    {
        var useBase64 = settings.ResolveWriteByteArrayAsBase64String(typeWriter.TypeSettings);
        var typeInfo = typeWriter.typeInfoHandling;

        Action configureWriter = (nullable, typeInfo, useBase64) switch
        {
            (false, TypeInfoHandling.AddAllTypeInfo, true) => () => typeWriter.SetItemWriter<ByteSegment>((item, _, _) =>
                { StartTypeInfoObject(typeWriter); writer.WriteBytesAsBase64(item); FinishTypeInfoObject(typeWriter); }, false),
            (false, TypeInfoHandling.AddAllTypeInfo, false) => () => typeWriter.SetItemWriter<ByteSegment>((item, _, _) =>
                { StartTypeInfoObject(typeWriter); writer.WriteBytesAsArray(item); FinishTypeInfoObject(typeWriter); }, false),
            (false, TypeInfoHandling.AddDeviatingTypeInfo, true) => () => typeWriter.SetItemWriter<ByteSegment>((item, dev, _) =>
                { if (dev) StartTypeInfoObject(typeWriter); writer.WriteBytesAsBase64(item); if (dev) FinishTypeInfoObject(typeWriter); }, false),
            (false, TypeInfoHandling.AddDeviatingTypeInfo, false) => () => typeWriter.SetItemWriter<ByteSegment>((item, dev, _) =>
                { if (dev) StartTypeInfoObject(typeWriter); writer.WriteBytesAsArray(item); if (dev) FinishTypeInfoObject(typeWriter); }, false),
            (false, _, true) => () => typeWriter.SetItemWriter<ByteSegment>((item, _, _) => writer.WriteBytesAsBase64(item), false),
            (false, _, false) => () => typeWriter.SetItemWriter<ByteSegment>((item, _, _) => writer.WriteBytesAsArray(item), false),
            (true, TypeInfoHandling.AddAllTypeInfo, true) => () => typeWriter.SetItemWriter<ByteSegment?>((item, _, _) =>
                { StartTypeInfoObject(typeWriter); if (item.HasValue) writer.WriteBytesAsBase64(item.Value); else writer.WriteNullValue(); FinishTypeInfoObject(typeWriter); }, false),
            (true, TypeInfoHandling.AddAllTypeInfo, false) => () => typeWriter.SetItemWriter<ByteSegment?>((item, _, _) =>
                { StartTypeInfoObject(typeWriter); if (item.HasValue) writer.WriteBytesAsArray(item.Value); else writer.WriteNullValue(); FinishTypeInfoObject(typeWriter); }, false),
            (true, TypeInfoHandling.AddDeviatingTypeInfo, true) => () => typeWriter.SetItemWriter<ByteSegment?>((item, dev, _) =>
                { if (dev) StartTypeInfoObject(typeWriter); if (item.HasValue) writer.WriteBytesAsBase64(item.Value); else writer.WriteNullValue(); if (dev) FinishTypeInfoObject(typeWriter); }, false),
            (true, TypeInfoHandling.AddDeviatingTypeInfo, false) => () => typeWriter.SetItemWriter<ByteSegment?>((item, dev, _) =>
                { if (dev) StartTypeInfoObject(typeWriter); if (item.HasValue) writer.WriteBytesAsArray(item.Value); else writer.WriteNullValue(); if (dev) FinishTypeInfoObject(typeWriter); }, false),
            (true, _, true) => () => typeWriter.SetItemWriter<ByteSegment?>((item, _, _) => { if (item.HasValue) writer.WriteBytesAsBase64(item.Value); else writer.WriteNullValue(); }, false),
            (true, _, false) => () => typeWriter.SetItemWriter<ByteSegment?>((item, _, _) => { if (item.HasValue) writer.WriteBytesAsArray(item.Value); else writer.WriteNullValue(); }, false),
        };

        configureWriter();
    }

    void CreateByteArraySegmentWriter(CachedTypeWriter typeWriter, bool nullable)
    {
        var useBase64 = settings.ResolveWriteByteArrayAsBase64String(typeWriter.TypeSettings);
        var typeInfo = typeWriter.typeInfoHandling;

        Action configureWriter = (nullable, typeInfo, useBase64) switch
        {
            (false, TypeInfoHandling.AddAllTypeInfo, true) => () => typeWriter.SetItemWriter<ArraySegment<byte>>((item, _, _) =>
                { StartTypeInfoObject(typeWriter); writer.WriteBytesAsBase64(item); FinishTypeInfoObject(typeWriter); }, false),
            (false, TypeInfoHandling.AddAllTypeInfo, false) => () => typeWriter.SetItemWriter<ArraySegment<byte>>((item, _, _) =>
                { StartTypeInfoObject(typeWriter); writer.WriteBytesAsArray(item); FinishTypeInfoObject(typeWriter); }, false),
            (false, TypeInfoHandling.AddDeviatingTypeInfo, true) => () => typeWriter.SetItemWriter<ArraySegment<byte>>((item, dev, _) =>
                { if (dev) StartTypeInfoObject(typeWriter); writer.WriteBytesAsBase64(item); if (dev) FinishTypeInfoObject(typeWriter); }, false),
            (false, TypeInfoHandling.AddDeviatingTypeInfo, false) => () => typeWriter.SetItemWriter<ArraySegment<byte>>((item, dev, _) =>
                { if (dev) StartTypeInfoObject(typeWriter); writer.WriteBytesAsArray(item); if (dev) FinishTypeInfoObject(typeWriter); }, false),
            (false, _, true) => () => typeWriter.SetItemWriter<ArraySegment<byte>>((item, _, _) => writer.WriteBytesAsBase64(item), false),
            (false, _, false) => () => typeWriter.SetItemWriter<ArraySegment<byte>>((item, _, _) => writer.WriteBytesAsArray(item), false),
            (true, TypeInfoHandling.AddAllTypeInfo, true) => () => typeWriter.SetItemWriter<ArraySegment<byte>?>((item, _, _) =>
                { StartTypeInfoObject(typeWriter); if (item.HasValue) writer.WriteBytesAsBase64(item.Value); else writer.WriteNullValue(); FinishTypeInfoObject(typeWriter); }, false),
            (true, TypeInfoHandling.AddAllTypeInfo, false) => () => typeWriter.SetItemWriter<ArraySegment<byte>?>((item, _, _) =>
                { StartTypeInfoObject(typeWriter); if (item.HasValue) writer.WriteBytesAsArray(item.Value); else writer.WriteNullValue(); FinishTypeInfoObject(typeWriter); }, false),
            (true, TypeInfoHandling.AddDeviatingTypeInfo, true) => () => typeWriter.SetItemWriter<ArraySegment<byte>?>((item, dev, _) =>
                { if (dev) StartTypeInfoObject(typeWriter); if (item.HasValue) writer.WriteBytesAsBase64(item.Value); else writer.WriteNullValue(); if (dev) FinishTypeInfoObject(typeWriter); }, false),
            (true, TypeInfoHandling.AddDeviatingTypeInfo, false) => () => typeWriter.SetItemWriter<ArraySegment<byte>?>((item, dev, _) =>
                { if (dev) StartTypeInfoObject(typeWriter); if (item.HasValue) writer.WriteBytesAsArray(item.Value); else writer.WriteNullValue(); if (dev) FinishTypeInfoObject(typeWriter); }, false),
            (true, _, true) => () => typeWriter.SetItemWriter<ArraySegment<byte>?>((item, _, _) => { if (item.HasValue) writer.WriteBytesAsBase64(item.Value); else writer.WriteNullValue(); }, false),
            (true, _, false) => () => typeWriter.SetItemWriter<ArraySegment<byte>?>((item, _, _) => { if (item.HasValue) writer.WriteBytesAsArray(item.Value); else writer.WriteNullValue(); }, false),
        };

        configureWriter();
    }

}
