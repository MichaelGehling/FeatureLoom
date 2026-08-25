using FeatureLoom.Collections;
using FeatureLoom.Extensions;
using System;
using System.Xml.Serialization;

namespace FeatureLoom.Serialization;

public sealed partial class JsonSerializer
{
    void CreateIntItemWriter(CachedTypeWriter typeWriter, bool nullable)
    {
        if (!nullable)
        {
            if (typeWriter.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo)
            {
                typeWriter.SetItemWriter<int>((item, _, _) =>
                {
                    StartTypeInfoObject(typeWriter);
                    writer.WriteIntValue(item);
                    FinishTypeInfoObject(typeWriter);
                }, false);
            }
            else if (typeWriter.typeInfoHandling == TypeInfoHandling.AddDeviatingTypeInfo)
            {
                typeWriter.SetItemWriter<int>((item, deviatingType, _) =>
                {
                    if (deviatingType) StartTypeInfoObject(typeWriter);
                    writer.WriteIntValue(item);
                    if (deviatingType) FinishTypeInfoObject(typeWriter);
                }, false);
            }
            else
            {
                typeWriter.SetItemWriter<int>((item, _, _) =>
                {
                    writer.WriteIntValue(item);
                }, false);
            }
        }
        else
        {
            if (typeWriter.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo)
            {
                typeWriter.SetItemWriter<int?>((item, _, _) =>
                {
                    StartTypeInfoObject(typeWriter);
                    if (item.HasValue) writer.WriteIntValue(item.Value);
                    else writer.WriteNullValue();
                    FinishTypeInfoObject(typeWriter);
                }, false);
            }
            else if (typeWriter.typeInfoHandling == TypeInfoHandling.AddDeviatingTypeInfo)
            {
                typeWriter.SetItemWriter<int?>((item, deviatingType, _) =>
                {
                    if (deviatingType) StartTypeInfoObject(typeWriter);
                    if (item.HasValue) writer.WriteIntValue(item.Value);
                    else writer.WriteNullValue();
                    if (deviatingType) FinishTypeInfoObject(typeWriter);
                }, false);
            }
            else
            {
                typeWriter.SetItemWriter<int?>((item, _, _) =>
                {
                    if (item.HasValue) writer.WriteIntValue(item.Value);
                    else writer.WriteNullValue();
                }, false);
            }
        }
    }

    void CreateUIntItemWriter(CachedTypeWriter typeWriter, bool nullable)
    {
        if (!nullable)
        {
            if (typeWriter.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo)
            {
                typeWriter.SetItemWriter<uint>((item, _, _) =>
                {
                    StartTypeInfoObject(typeWriter);
                    writer.WriteUintValue(item);
                    FinishTypeInfoObject(typeWriter);
                }, false);
            }
            else if (typeWriter.typeInfoHandling == TypeInfoHandling.AddDeviatingTypeInfo)
            {
                typeWriter.SetItemWriter<uint>((item, deviatingType, _) =>
                {
                    if (deviatingType) StartTypeInfoObject(typeWriter);
                    writer.WriteUintValue(item);
                    if (deviatingType) FinishTypeInfoObject(typeWriter);
                }, false);
            }
            else
            {
                typeWriter.SetItemWriter<uint>((item, _, _) =>
                {
                    writer.WriteUintValue(item);
                }, false);
            }
        }
        else
        {
            if (typeWriter.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo)
            {
                typeWriter.SetItemWriter<uint?>((item, _, _) =>
                {
                    StartTypeInfoObject(typeWriter);
                    if (item.HasValue) writer.WriteUintValue(item.Value);
                    else writer.WriteNullValue();
                    FinishTypeInfoObject(typeWriter);
                }, false);
            }
            else if (typeWriter.typeInfoHandling == TypeInfoHandling.AddDeviatingTypeInfo)
            {
                typeWriter.SetItemWriter<uint?>((item, deviatingType, _) =>
                {
                    if (deviatingType) StartTypeInfoObject(typeWriter);
                    if (item.HasValue) writer.WriteUintValue(item.Value);
                    else writer.WriteNullValue();
                    if (deviatingType) FinishTypeInfoObject(typeWriter);
                }, false);
            }
            else
            {
                typeWriter.SetItemWriter<uint?>((item, _, _) =>
                {
                    if (item.HasValue) writer.WriteUintValue(item.Value);
                    else writer.WriteNullValue();
                }, false);
            }
        }
    }

    void CreateLongItemWriter(CachedTypeWriter typeWriter, bool nullable)
    {
        if (!nullable)
        {
            if (typeWriter.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo)
            {
                typeWriter.SetItemWriter<long>((item, _, _) =>
                {
                    StartTypeInfoObject(typeWriter);
                    writer.WriteLongValue(item);
                    FinishTypeInfoObject(typeWriter);
                }, false);
            }
            else if (typeWriter.typeInfoHandling == TypeInfoHandling.AddDeviatingTypeInfo)
            {
                typeWriter.SetItemWriter<long>((item, deviatingType, _) =>
                {
                    if (deviatingType) StartTypeInfoObject(typeWriter);
                    writer.WriteLongValue(item);
                    if (deviatingType) FinishTypeInfoObject(typeWriter);
                }, false);
            }
            else
            {
                typeWriter.SetItemWriter<long>((item, _, _) =>
                {
                    writer.WriteLongValue(item);
                }, false);
            }
        }
        else
        {
            if (typeWriter.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo)
            {
                typeWriter.SetItemWriter<long?>((item, _, _) =>
                {
                    StartTypeInfoObject(typeWriter);
                    if (item.HasValue) writer.WriteLongValue(item.Value);
                    else writer.WriteNullValue();
                    FinishTypeInfoObject(typeWriter);
                }, false);
            }
            else if (typeWriter.typeInfoHandling == TypeInfoHandling.AddDeviatingTypeInfo)
            {
                typeWriter.SetItemWriter<long?>((item, deviatingType, _) =>
                {
                    if (deviatingType) StartTypeInfoObject(typeWriter);
                    if (item.HasValue) writer.WriteLongValue(item.Value);
                    else writer.WriteNullValue();
                    if (deviatingType) FinishTypeInfoObject(typeWriter);
                }, false);
            }
            else
            {
                typeWriter.SetItemWriter<long?>((item, _, _) =>
                {
                    if (item.HasValue) writer.WriteLongValue(item.Value);
                    else writer.WriteNullValue();
                }, false);
            }
        }
    }

    void CreateULongItemWriter(CachedTypeWriter typeWriter, bool nullable)
    {
        if (!nullable)
        {
            if (typeWriter.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo)
            {
                typeWriter.SetItemWriter<ulong>((item, _, _) =>
                {
                    StartTypeInfoObject(typeWriter);
                    writer.WriteUlongValue(item);
                    FinishTypeInfoObject(typeWriter);
                }, false);
            }
            else if (typeWriter.typeInfoHandling == TypeInfoHandling.AddDeviatingTypeInfo)
            {
                typeWriter.SetItemWriter<ulong>((item, deviatingType, _) =>
                {
                    if (deviatingType) StartTypeInfoObject(typeWriter);
                    writer.WriteUlongValue(item);
                    if (deviatingType) FinishTypeInfoObject(typeWriter);
                }, false);
            }
            else
            {
                typeWriter.SetItemWriter<ulong>((item, _, _) =>
                {
                    writer.WriteUlongValue(item);
                }, false);
            }
        }
        else
        {
            if (typeWriter.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo)
            {
                typeWriter.SetItemWriter<ulong?>((item, _, _) =>
                {
                    StartTypeInfoObject(typeWriter);
                    if (item.HasValue) writer.WriteUlongValue(item.Value);
                    else writer.WriteNullValue();
                    FinishTypeInfoObject(typeWriter);
                }, false);
            }
            else if (typeWriter.typeInfoHandling == TypeInfoHandling.AddDeviatingTypeInfo)
            {
                typeWriter.SetItemWriter<ulong?>((item, deviatingType, _) =>
                {
                    if (deviatingType) StartTypeInfoObject(typeWriter);
                    if (item.HasValue) writer.WriteUlongValue(item.Value);
                    else writer.WriteNullValue();
                    if (deviatingType) FinishTypeInfoObject(typeWriter);
                }, false);
            }
            else
            {
                typeWriter.SetItemWriter<ulong?>((item, _, _) =>
                {
                    if (item.HasValue) writer.WriteUlongValue(item.Value);
                    else writer.WriteNullValue();
                }, false);
            }
        }
    }

    void CreateShortItemWriter(CachedTypeWriter typeWriter, bool nullable)
    {
        if (!nullable)
        {
            if (typeWriter.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo)
            {
                typeWriter.SetItemWriter<short>((item, _, _) =>
                {
                    StartTypeInfoObject(typeWriter);
                    writer.WriteShortValue(item);
                    FinishTypeInfoObject(typeWriter);
                }, false);
            }
            else if (typeWriter.typeInfoHandling == TypeInfoHandling.AddDeviatingTypeInfo)
            {
                typeWriter.SetItemWriter<short>((item, deviatingType, _) =>
                {
                    if (deviatingType) StartTypeInfoObject(typeWriter);
                    writer.WriteShortValue(item);
                    if (deviatingType) FinishTypeInfoObject(typeWriter);
                }, false);
            }
            else
            {
                typeWriter.SetItemWriter<short>((item, _, _) =>
                {
                    writer.WriteShortValue(item);
                }, false);
            }
        }
        else
        {
            if (typeWriter.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo)
            {
                typeWriter.SetItemWriter<short?>((item, _, _) =>
                {
                    StartTypeInfoObject(typeWriter);
                    if (item.HasValue) writer.WriteShortValue(item.Value);
                    else writer.WriteNullValue();
                    FinishTypeInfoObject(typeWriter);
                }, false);
            }
            else if (typeWriter.typeInfoHandling == TypeInfoHandling.AddDeviatingTypeInfo)
            {
                typeWriter.SetItemWriter<short?>((item, deviatingType, _) =>
                {
                    if (deviatingType) StartTypeInfoObject(typeWriter);
                    if (item.HasValue) writer.WriteShortValue(item.Value);
                    else writer.WriteNullValue();
                    if (deviatingType) FinishTypeInfoObject(typeWriter);
                }, false);
            }
            else
            {
                typeWriter.SetItemWriter<short?>((item, _, _) =>
                {
                    if (item.HasValue) writer.WriteShortValue(item.Value);
                    else writer.WriteNullValue();
                }, false);
            }
        }
    }

    void CreateUShortItemWriter(CachedTypeWriter typeWriter, bool nullable)
    {
        if (!nullable)
        {
            if (typeWriter.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo)
            {
                typeWriter.SetItemWriter<ushort>((item, _, _) =>
                {
                    StartTypeInfoObject(typeWriter);
                    writer.WriteUshortValue(item);
                    FinishTypeInfoObject(typeWriter);
                }, false);
            }
            else if (typeWriter.typeInfoHandling == TypeInfoHandling.AddDeviatingTypeInfo)
            {
                typeWriter.SetItemWriter<ushort>((item, deviatingType, _) =>
                {
                    if (deviatingType) StartTypeInfoObject(typeWriter);
                    writer.WriteUshortValue(item);
                    if (deviatingType) FinishTypeInfoObject(typeWriter);
                }, false);
            }
            else
            {
                typeWriter.SetItemWriter<ushort>((item, _, _) =>
                {
                    writer.WriteUshortValue(item);
                }, false);
            }
        }
        else
        {
            if (typeWriter.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo)
            {
                typeWriter.SetItemWriter<ushort?>((item, _, _) =>
                {
                    StartTypeInfoObject(typeWriter);
                    if (item.HasValue) writer.WriteUshortValue(item.Value);
                    else writer.WriteNullValue();
                    FinishTypeInfoObject(typeWriter);
                }, false);
            }
            else if (typeWriter.typeInfoHandling == TypeInfoHandling.AddDeviatingTypeInfo)
            {
                typeWriter.SetItemWriter<ushort?>((item, deviatingType, _) =>
                {
                    if (deviatingType) StartTypeInfoObject(typeWriter);
                    if (item.HasValue) writer.WriteUshortValue(item.Value);
                    else writer.WriteNullValue();
                    if (deviatingType) FinishTypeInfoObject(typeWriter);
                }, false);
            }
            else
            {
                typeWriter.SetItemWriter<ushort?>((item, _, _) =>
                {
                    if (item.HasValue) writer.WriteUshortValue(item.Value);
                    else writer.WriteNullValue();
                }, false);
            }
        }
    }

    void CreateSByteItemWriter(CachedTypeWriter typeWriter, bool nullable)
    {
        if (!nullable)
        {
            if (typeWriter.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo)
            {
                typeWriter.SetItemWriter<sbyte>((item, _, _) =>
                {
                    StartTypeInfoObject(typeWriter);
                    writer.WriteSbyteValue(item);
                    FinishTypeInfoObject(typeWriter);
                }, false);
            }
            else if (typeWriter.typeInfoHandling == TypeInfoHandling.AddDeviatingTypeInfo)
            {
                typeWriter.SetItemWriter<sbyte>((item, deviatingType, _) =>
                {
                    if (deviatingType) StartTypeInfoObject(typeWriter);
                    writer.WriteSbyteValue(item);
                    if (deviatingType) FinishTypeInfoObject(typeWriter);
                }, false);
            }
            else
            {
                typeWriter.SetItemWriter<sbyte>((item, _, _) =>
                {
                    writer.WriteSbyteValue(item);
                }, false);
            }
        }
        else
        {
            if (typeWriter.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo)
            {
                typeWriter.SetItemWriter<sbyte?>((item, _, _) =>
                {
                    StartTypeInfoObject(typeWriter);
                    if (item.HasValue) writer.WriteSbyteValue(item.Value);
                    else writer.WriteNullValue();
                    FinishTypeInfoObject(typeWriter);
                }, false);
            }
            else if (typeWriter.typeInfoHandling == TypeInfoHandling.AddDeviatingTypeInfo)
            {
                typeWriter.SetItemWriter<sbyte?>((item, deviatingType, _) =>
                {
                    if (deviatingType) StartTypeInfoObject(typeWriter);
                    if (item.HasValue) writer.WriteSbyteValue(item.Value);
                    else writer.WriteNullValue();
                    if (deviatingType) FinishTypeInfoObject(typeWriter);
                }, false);
            }
            else
            {
                typeWriter.SetItemWriter<sbyte?>((item, _, _) =>
                {
                    if (item.HasValue) writer.WriteSbyteValue(item.Value);
                    else writer.WriteNullValue();
                }, false);
            }
        }
    }

    void CreateByteItemWriter(CachedTypeWriter typeWriter, bool nullable)
    {
        if (!nullable)
        {
            if (typeWriter.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo)
            {
                typeWriter.SetItemWriter<byte>((item, _, _) =>
                {
                    StartTypeInfoObject(typeWriter);
                    writer.WriteByteValue(item);
                    FinishTypeInfoObject(typeWriter);
                }, false);
            }
            else if (typeWriter.typeInfoHandling == TypeInfoHandling.AddDeviatingTypeInfo)
            {
                typeWriter.SetItemWriter<byte>((item, deviatingType, _) =>
                {
                    if (deviatingType) StartTypeInfoObject(typeWriter);
                    writer.WriteByteValue(item);
                    if (deviatingType) FinishTypeInfoObject(typeWriter);
                }, false);
            }
            else
            {
                typeWriter.SetItemWriter<byte>((item, _, _) =>
                {
                    writer.WriteByteValue(item);
                }, false);
            }
        }
        else
        {
            if (typeWriter.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo)
            {
                typeWriter.SetItemWriter<byte?>((item, _, _) =>
                {
                    StartTypeInfoObject(typeWriter);
                    if (item.HasValue) writer.WriteByteValue(item.Value);
                    else writer.WriteNullValue();
                    FinishTypeInfoObject(typeWriter);
                }, false);
            }
            else if (typeWriter.typeInfoHandling == TypeInfoHandling.AddDeviatingTypeInfo)
            {
                typeWriter.SetItemWriter<byte?>((item, deviatingType, _) =>
                {
                    if (deviatingType) StartTypeInfoObject(typeWriter);
                    if (item.HasValue) writer.WriteByteValue(item.Value);
                    else writer.WriteNullValue();
                    if (deviatingType) FinishTypeInfoObject(typeWriter);
                }, false);
            }
            else
            {
                typeWriter.SetItemWriter<byte?>((item, _, _) =>
                {
                    if (item.HasValue) writer.WriteByteValue(item.Value);
                    else writer.WriteNullValue();
                }, false);
            }
        }
    }

    void CreateDoubleItemWriter(CachedTypeWriter typeWriter, bool nullable)
    {
        if (!nullable)
        {
            if (typeWriter.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo)
            {
                typeWriter.SetItemWriter<double>((item, _, _) =>
                {
                    StartTypeInfoObject(typeWriter);
                    writer.WriteDoubleValue(item);
                    FinishTypeInfoObject(typeWriter);
                }, false);
            }
            else if (typeWriter.typeInfoHandling == TypeInfoHandling.AddDeviatingTypeInfo)
            {
                typeWriter.SetItemWriter<double>((item, deviatingType, _) =>
                {
                    if (deviatingType) StartTypeInfoObject(typeWriter);
                    writer.WriteDoubleValue(item);
                    if (deviatingType) FinishTypeInfoObject(typeWriter);
                }, false);
            }
            else
            {
                typeWriter.SetItemWriter<double>((item, _, _) =>
                {
                    writer.WriteDoubleValue(item);
                }, false);
            }
        }
        else
        {
            if (typeWriter.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo)
            {
                typeWriter.SetItemWriter<double?>((item, _, _) =>
                {
                    StartTypeInfoObject(typeWriter);
                    if (item.HasValue) writer.WriteDoubleValue(item.Value);
                    else writer.WriteNullValue();
                    FinishTypeInfoObject(typeWriter);
                }, false);
            }
            else if (typeWriter.typeInfoHandling == TypeInfoHandling.AddDeviatingTypeInfo)
            {
                typeWriter.SetItemWriter<double?>((item, deviatingType, _) =>
                {
                    if (deviatingType) StartTypeInfoObject(typeWriter);
                    if (item.HasValue) writer.WriteDoubleValue(item.Value);
                    else writer.WriteNullValue();
                    if (deviatingType) FinishTypeInfoObject(typeWriter);
                }, false);
            }
            else
            {
                typeWriter.SetItemWriter<double?>((item, _, _) =>
                {
                    if (item.HasValue) writer.WriteDoubleValue(item.Value);
                    else writer.WriteNullValue();
                }, false);
            }
        }
    }

    void CreateFloatItemWriter(CachedTypeWriter typeWriter, bool nullable)
    {
        if (!nullable)
        {
            if (typeWriter.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo)
            {
                typeWriter.SetItemWriter<float>((item, _, _) =>
                {
                    StartTypeInfoObject(typeWriter);
                    writer.WriteFloatValue(item);
                    FinishTypeInfoObject(typeWriter);
                }, false);
            }
            else if (typeWriter.typeInfoHandling == TypeInfoHandling.AddDeviatingTypeInfo)
            {
                typeWriter.SetItemWriter<float>((item, deviatingType, _) =>
                {
                    if (deviatingType) StartTypeInfoObject(typeWriter);
                    writer.WriteFloatValue(item);
                    if (deviatingType) FinishTypeInfoObject(typeWriter);
                }, false);
            }
            else
            {
                typeWriter.SetItemWriter<float>((item, _, _) =>
                {
                    writer.WriteFloatValue(item);
                }, false);
            }
        }
        else
        {
            if (typeWriter.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo)
            {
                typeWriter.SetItemWriter<float?>((item, _, _) =>
                {
                    StartTypeInfoObject(typeWriter);
                    if (item.HasValue) writer.WriteFloatValue(item.Value);
                    else writer.WriteNullValue();
                    FinishTypeInfoObject(typeWriter);
                }, false);
            }
            else if (typeWriter.typeInfoHandling == TypeInfoHandling.AddDeviatingTypeInfo)
            {
                typeWriter.SetItemWriter<float?>((item, deviatingType, _) =>
                {
                    if (deviatingType) StartTypeInfoObject(typeWriter);
                    if (item.HasValue) writer.WriteFloatValue(item.Value);
                    else writer.WriteNullValue();
                    if (deviatingType) FinishTypeInfoObject(typeWriter);
                }, false);
            }
            else
            {
                typeWriter.SetItemWriter<float?>((item, _, _) =>
                {
                    if (item.HasValue) writer.WriteFloatValue(item.Value);
                    else writer.WriteNullValue();
                }, false);
            }
        }
    }


    void CreateDecimalItemWriter(CachedTypeWriter typeWriter, bool nullable)
    {
        if (!nullable)
        {
            if (typeWriter.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo)
            {
                typeWriter.SetItemWriter<decimal>((item, _, _) =>
                {
                    StartTypeInfoObject(typeWriter);
                    writer.WriteDecimalValue(item);
                    FinishTypeInfoObject(typeWriter);
                }, false);
            }
            else if (typeWriter.typeInfoHandling == TypeInfoHandling.AddDeviatingTypeInfo)
            {
                typeWriter.SetItemWriter<decimal>((item, deviatingType, _) =>
                {
                    if (deviatingType) StartTypeInfoObject(typeWriter);
                    writer.WriteDecimalValue(item);
                    if (deviatingType) FinishTypeInfoObject(typeWriter);
                }, false);
            }
            else
            {
                typeWriter.SetItemWriter<decimal>((item, _, _) =>
                {
                    writer.WriteDecimalValue(item);
                }, false);
            }
        }
        else
        {
            if (typeWriter.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo)
            {
                typeWriter.SetItemWriter<decimal?>((item, _, _) =>
                {
                    StartTypeInfoObject(typeWriter);
                    if (item.HasValue) writer.WriteDecimalValue(item.Value);
                    else writer.WriteNullValue();
                    FinishTypeInfoObject(typeWriter);
                }, false);
            }
            else if (typeWriter.typeInfoHandling == TypeInfoHandling.AddDeviatingTypeInfo)
            {
                typeWriter.SetItemWriter<decimal?>((item, deviatingType, _) =>
                {
                    if (deviatingType) StartTypeInfoObject(typeWriter);
                    if (item.HasValue) writer.WriteDecimalValue(item.Value);
                    else writer.WriteNullValue();
                    if (deviatingType) FinishTypeInfoObject(typeWriter);
                }, false);
            }
            else
            {
                typeWriter.SetItemWriter<decimal?>((item, _, _) =>
                {
                    if (item.HasValue) writer.WriteDecimalValue(item.Value);
                    else writer.WriteNullValue();
                }, false);
            }
        }
    }

    void CreateCharItemWriter(CachedTypeWriter typeWriter, bool nullable)
    {
        if (!nullable)
        {
            if (typeWriter.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo)
            {
                typeWriter.SetItemWriter<char>((item, _, _) =>
                {
                    StartTypeInfoObject(typeWriter);
                    writer.WriteCharValue(item);
                    FinishTypeInfoObject(typeWriter);
                }, false);
            }
            else if (typeWriter.typeInfoHandling == TypeInfoHandling.AddDeviatingTypeInfo)
            {
                typeWriter.SetItemWriter<char>((item, deviatingType, _) =>
                {
                    if (deviatingType) StartTypeInfoObject(typeWriter);
                    writer.WriteCharValue(item);
                    if (deviatingType) FinishTypeInfoObject(typeWriter);
                }, false);
            }
            else
            {
                typeWriter.SetItemWriter<char>((item, _, _) =>
                {
                    writer.WriteCharValue(item);
                }, false);
            }
        }
        else
        {
            if (typeWriter.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo)
            {
                typeWriter.SetItemWriter<char?>((item, _, _) =>
                {
                    StartTypeInfoObject(typeWriter);
                    if (item.HasValue) writer.WriteCharValue(item.Value);
                    else writer.WriteNullValue();
                    FinishTypeInfoObject(typeWriter);
                }, false);
            }
            else if (typeWriter.typeInfoHandling == TypeInfoHandling.AddDeviatingTypeInfo)
            {
                typeWriter.SetItemWriter<char?>((item, deviatingType, _) =>
                {
                    if (deviatingType) StartTypeInfoObject(typeWriter);
                    if (item.HasValue) writer.WriteCharValue(item.Value);
                    else writer.WriteNullValue();
                    if (deviatingType) FinishTypeInfoObject(typeWriter);
                }, false);
            }
            else
            {
                typeWriter.SetItemWriter<char?>((item, _, _) =>
                {
                    if (item.HasValue) writer.WriteCharValue(item.Value);
                    else writer.WriteNullValue();
                }, false);
            }
        }
    }

    void CreateStringItemWriter(CachedTypeWriter typeWriter)
    {
        if (typeWriter.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo)
        {
            typeWriter.SetItemWriter<string>((item, _, _) =>
            {
                StartTypeInfoObject(typeWriter);
                writer.WriteStringValue(item);
                FinishTypeInfoObject(typeWriter);
            }, false);
        }
        else if (typeWriter.typeInfoHandling == TypeInfoHandling.AddDeviatingTypeInfo)
        {
            typeWriter.SetItemWriter<string>((item, deviatingType, _) =>
            {
                if (deviatingType) StartTypeInfoObject(typeWriter);
                writer.WriteStringValue(item);
                if (deviatingType) FinishTypeInfoObject(typeWriter);
            }, false);
        }
        else
        {
            typeWriter.SetItemWriter<string>((item, _, _) =>
            {
                writer.WriteStringValue(item);
            }, false);
        }
    }

    void CreateBoolItemWriter(CachedTypeWriter typeWriter, bool nullable)
    {
        if (!nullable)
        {
            if (typeWriter.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo)
            {
                typeWriter.SetItemWriter<bool>((item, _, _) =>
                {
                    StartTypeInfoObject(typeWriter);
                    writer.WriteBoolValue(item);
                    FinishTypeInfoObject(typeWriter);
                }, false);
            }
            else if (typeWriter.typeInfoHandling == TypeInfoHandling.AddDeviatingTypeInfo)
            {
                typeWriter.SetItemWriter<bool>((item, deviatingType, _) =>
                {
                    if (deviatingType) StartTypeInfoObject(typeWriter);
                    writer.WriteBoolValue(item);
                    if (deviatingType) FinishTypeInfoObject(typeWriter);
                }, false);
            }
            else
            {
                typeWriter.SetItemWriter<bool>((item, _, _) =>
                {
                    writer.WriteBoolValue(item);
                }, false);
            }
        }
        else
        {
            if (typeWriter.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo)
            {
                typeWriter.SetItemWriter<bool?>((item, _, _) =>
                {
                    StartTypeInfoObject(typeWriter);
                    if (item.HasValue) writer.WriteBoolValue(item.Value);
                    else writer.WriteNullValue();
                    FinishTypeInfoObject(typeWriter);
                }, false);
            }
            else if (typeWriter.typeInfoHandling == TypeInfoHandling.AddDeviatingTypeInfo)
            {
                typeWriter.SetItemWriter<bool?>((item, deviatingType, _) =>
                {
                    if (deviatingType) StartTypeInfoObject(typeWriter);
                    if (item.HasValue) writer.WriteBoolValue(item.Value);
                    else writer.WriteNullValue();
                    if (deviatingType) FinishTypeInfoObject(typeWriter);
                }, false);
            }
            else
            {
                typeWriter.SetItemWriter<bool?>((item, _, _) =>
                {
                    if (item.HasValue) writer.WriteBoolValue(item.Value);
                    else writer.WriteNullValue();
                }, false);
            }
        }

    }

    void CreateIntPtrItemWriter(CachedTypeWriter typeWriter, bool nullable)
    {
        if (!nullable)
        {
            if (typeWriter.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo)
            {
                typeWriter.SetItemWriter<nint>((item, _, _) =>
                {
                    StartTypeInfoObject(typeWriter);
                    writer.WriteIntPtrValue(item);
                    FinishTypeInfoObject(typeWriter);
                }, false);
            }
            else if (typeWriter.typeInfoHandling == TypeInfoHandling.AddDeviatingTypeInfo)
            {
                typeWriter.SetItemWriter<nint>((item, deviatingType, _) =>
                {
                    if (deviatingType) StartTypeInfoObject(typeWriter);
                    writer.WriteIntPtrValue(item);
                    if (deviatingType) FinishTypeInfoObject(typeWriter);
                }, false);
            }
            else
            {
                typeWriter.SetItemWriter<nint>((item, _, _) =>
                {
                    writer.WriteIntPtrValue(item);
                }, false);
            }
        }
        else
        {
            if (typeWriter.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo)
            {
                typeWriter.SetItemWriter<IntPtr?>((item, _, _) =>
                {
                    StartTypeInfoObject(typeWriter);
                    if (item.HasValue) writer.WriteIntPtrValue(item.Value);
                    else writer.WriteNullValue();
                    FinishTypeInfoObject(typeWriter);
                }, false);
            }
            else if (typeWriter.typeInfoHandling == TypeInfoHandling.AddDeviatingTypeInfo)
            {
                typeWriter.SetItemWriter<IntPtr?>((item, deviatingType, _) =>
                {
                    if (deviatingType) StartTypeInfoObject(typeWriter);
                    if (item.HasValue) writer.WriteIntPtrValue(item.Value);
                    else writer.WriteNullValue();
                    if (deviatingType) FinishTypeInfoObject(typeWriter);
                }, false);
            }
            else
            {
                typeWriter.SetItemWriter<IntPtr?>((item, _, _) =>
                {
                    if (item.HasValue) writer.WriteIntPtrValue(item.Value);
                    else writer.WriteNullValue();
                }, false);
            }
        }
    }

    void CreateUIntPtrItemWriter(CachedTypeWriter typeWriter, bool nullable)
    {
        if (!nullable)
        {
            if (typeWriter.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo)
            {
                typeWriter.SetItemWriter<nuint>((item, _, _) =>
                {
                    StartTypeInfoObject(typeWriter);
                    writer.WriteUintPtrValue(item);
                    FinishTypeInfoObject(typeWriter);
                }, false);
            }
            else if (typeWriter.typeInfoHandling == TypeInfoHandling.AddDeviatingTypeInfo)
            {
                typeWriter.SetItemWriter<nuint>((item, deviatingType, _) =>
                {
                    if (deviatingType) StartTypeInfoObject(typeWriter);
                    writer.WriteUintPtrValue(item);
                    if (deviatingType) FinishTypeInfoObject(typeWriter);
                }, false);
            }
            else
            {
                typeWriter.SetItemWriter<nuint>((item, _, _) =>
                {
                    writer.WriteUintPtrValue(item);
                }, false);
            }
        }
        else
        {
            if (typeWriter.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo)
            {
                typeWriter.SetItemWriter<UIntPtr?>((item, _, _) =>
                {
                    StartTypeInfoObject(typeWriter);
                    if (item.HasValue) writer.WriteUintPtrValue(item.Value);
                    else writer.WriteNullValue();
                    FinishTypeInfoObject(typeWriter);
                }, false);
            }
            else if (typeWriter.typeInfoHandling == TypeInfoHandling.AddDeviatingTypeInfo)
            {
                typeWriter.SetItemWriter<UIntPtr?>((item, deviatingType, _) =>
                {
                    if (deviatingType) StartTypeInfoObject(typeWriter);
                    if (item.HasValue) writer.WriteUintPtrValue(item.Value);
                    else writer.WriteNullValue();
                    if (deviatingType) FinishTypeInfoObject(typeWriter);
                }, false);
            }
            else
            {
                typeWriter.SetItemWriter<UIntPtr?>((item, _, _) =>
                {
                    if (item.HasValue) writer.WriteUintPtrValue(item.Value);
                    else writer.WriteNullValue();
                }, false);
            }
        }
    }

    void CreateGuidItemWriter(CachedTypeWriter typeWriter, bool nullable)
    {
        if (!nullable)
        {
            if (typeWriter.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo)
            {
                typeWriter.SetItemWriter<Guid>((item, _, _) =>
                {
                    StartTypeInfoObject(typeWriter);
                    writer.WriteGuidValue(item);
                    FinishTypeInfoObject(typeWriter);
                }, false);
            }
            else if (typeWriter.typeInfoHandling == TypeInfoHandling.AddDeviatingTypeInfo)
            {
                typeWriter.SetItemWriter<Guid>((item, deviatingType, _) =>
                {
                    if (deviatingType) StartTypeInfoObject(typeWriter);
                    writer.WriteGuidValue(item);
                    if (deviatingType) FinishTypeInfoObject(typeWriter);
                }, false);
            }
            else
            {
                typeWriter.SetItemWriter<Guid>((item, _, _) =>
                {
                    writer.WriteGuidValue(item);
                }, false);
            }
        }
        else
        {
            if (typeWriter.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo)
            {
                typeWriter.SetItemWriter<Guid?>((item, _, _) =>
                {
                    StartTypeInfoObject(typeWriter);
                    if (item.HasValue) writer.WriteGuidValue(item.Value);
                    else writer.WriteNullValue();
                    FinishTypeInfoObject(typeWriter);
                }, false);
            }
            else if (typeWriter.typeInfoHandling == TypeInfoHandling.AddDeviatingTypeInfo)
            {
                typeWriter.SetItemWriter<Guid?>((item, deviatingType, _) =>
                {
                    if (deviatingType) StartTypeInfoObject(typeWriter);
                    if (item.HasValue) writer.WriteGuidValue(item.Value);
                    else writer.WriteNullValue();
                    if (deviatingType) FinishTypeInfoObject(typeWriter);
                }, false);
            }
            else
            {
                typeWriter.SetItemWriter<Guid?>((item, _, _) =>
                {
                    if (item.HasValue) writer.WriteGuidValue(item.Value);
                    else writer.WriteNullValue();
                }, false);
            }
        }
    }

    void CreateDateTimeItemWriter(CachedTypeWriter typeWriter, bool nullable)
    {
        if (!nullable)
        {
            if (typeWriter.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo)
            {
                typeWriter.SetItemWriter<DateTime>((item, _, _) =>
                {
                    StartTypeInfoObject(typeWriter);
                    writer.WriteDateTimeValue(item);
                    FinishTypeInfoObject(typeWriter);
                }, false);
            }
            else if (typeWriter.typeInfoHandling == TypeInfoHandling.AddDeviatingTypeInfo)
            {
                typeWriter.SetItemWriter<DateTime>((item, deviatingType, _) =>
                {
                    if (deviatingType) StartTypeInfoObject(typeWriter);
                    writer.WriteDateTimeValue(item);
                    if (deviatingType) FinishTypeInfoObject(typeWriter);
                }, false);
            }
            else
            {
                typeWriter.SetItemWriter<DateTime>((item, _, _) =>
                {
                    writer.WriteDateTimeValue(item);
                }, false);
            }
        }
        else
        {
            if (typeWriter.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo)
            {
                typeWriter.SetItemWriter<DateTime?>((item, _, _) =>
                {
                    StartTypeInfoObject(typeWriter);
                    if (item.HasValue) writer.WriteDateTimeValue(item.Value);
                    else writer.WriteNullValue();
                    FinishTypeInfoObject(typeWriter);
                }, false);
            }
            else if (typeWriter.typeInfoHandling == TypeInfoHandling.AddDeviatingTypeInfo)
            {
                typeWriter.SetItemWriter<DateTime?>((item, deviatingType, _) =>
                {
                    if (deviatingType) StartTypeInfoObject(typeWriter);
                    if (item.HasValue) writer.WriteDateTimeValue(item.Value);
                    else writer.WriteNullValue();
                    if (deviatingType) FinishTypeInfoObject(typeWriter);
                }, false);
            }
            else
            {
                typeWriter.SetItemWriter<DateTime?>((item, _, _) =>
                {
                    if (item.HasValue) writer.WriteDateTimeValue(item.Value);
                    else writer.WriteNullValue();
                }, false);
            }
        }
    }

    void CreateDateTimeOffsetItemWriter(CachedTypeWriter typeWriter, bool nullable)
    {
        if (!nullable)
        {
            if (typeWriter.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo)
            {
                typeWriter.SetItemWriter<DateTimeOffset>((item, _, _) =>
                {
                    StartTypeInfoObject(typeWriter);
                    writer.WriteDateTimeOffsetValue(item);
                    FinishTypeInfoObject(typeWriter);
                }, false);
            }
            else if (typeWriter.typeInfoHandling == TypeInfoHandling.AddDeviatingTypeInfo)
            {
                typeWriter.SetItemWriter<DateTimeOffset>((item, deviatingType, _) =>
                {
                    if (deviatingType) StartTypeInfoObject(typeWriter);
                    writer.WriteDateTimeOffsetValue(item);
                    if (deviatingType) FinishTypeInfoObject(typeWriter);
                }, false);
            }
            else
            {
                typeWriter.SetItemWriter<DateTimeOffset>((item, _, _) =>
                {
                    writer.WriteDateTimeOffsetValue(item);
                }, false);
            }
        }
        else
        {
            if (typeWriter.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo)
            {
                typeWriter.SetItemWriter<DateTimeOffset?>((item, _, _) =>
                {
                    StartTypeInfoObject(typeWriter);
                    if (item.HasValue) writer.WriteDateTimeOffsetValue(item.Value);
                    else writer.WriteNullValue();
                    FinishTypeInfoObject(typeWriter);
                }, false);
            }
            else if (typeWriter.typeInfoHandling == TypeInfoHandling.AddDeviatingTypeInfo)
            {
                typeWriter.SetItemWriter<DateTimeOffset?>((item, deviatingType, _) =>
                {
                    if (deviatingType) StartTypeInfoObject(typeWriter);
                    if (item.HasValue) writer.WriteDateTimeOffsetValue(item.Value);
                    else writer.WriteNullValue();
                    if (deviatingType) FinishTypeInfoObject(typeWriter);
                }, false);
            }
            else
            {
                typeWriter.SetItemWriter<DateTimeOffset?>((item, _, _) =>
                {
                    if (item.HasValue) writer.WriteDateTimeOffsetValue(item.Value);
                    else writer.WriteNullValue();
                }, false);
            }
        }
    }

    void CreateTimeSpanItemWriter(CachedTypeWriter typeWriter, bool nullable)
    {
        if (!nullable)
        {
            if (typeWriter.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo)
            {
                typeWriter.SetItemWriter<TimeSpan>((item, _, _) =>
                {
                    StartTypeInfoObject(typeWriter);
                    writer.WriteTimeSpanValue(item);
                    FinishTypeInfoObject(typeWriter);
                }, false);
            }
            else if (typeWriter.typeInfoHandling == TypeInfoHandling.AddDeviatingTypeInfo)
            {
                typeWriter.SetItemWriter<TimeSpan>((item, deviatingType, _) =>
                {
                    if (deviatingType) StartTypeInfoObject(typeWriter);
                    writer.WriteTimeSpanValue(item);
                    if (deviatingType) FinishTypeInfoObject(typeWriter);
                }, false);
            }
            else
            {
                typeWriter.SetItemWriter<TimeSpan>((item, _, _) =>
                {
                    writer.WriteTimeSpanValue(item);
                }, false);
            }
        }
        else
        {
            if (typeWriter.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo)
            {
                typeWriter.SetItemWriter<TimeSpan?>((item, _, _) =>
                {
                    StartTypeInfoObject(typeWriter);
                    if (item.HasValue) writer.WriteTimeSpanValue(item.Value);
                    else writer.WriteNullValue();
                    FinishTypeInfoObject(typeWriter);
                }, false);
            }
            else if (typeWriter.typeInfoHandling == TypeInfoHandling.AddDeviatingTypeInfo)
            {
                typeWriter.SetItemWriter<TimeSpan?>((item, deviatingType, _) =>
                {
                    if (deviatingType) StartTypeInfoObject(typeWriter);
                    if (item.HasValue) writer.WriteTimeSpanValue(item.Value);
                    else writer.WriteNullValue();
                    if (deviatingType) FinishTypeInfoObject(typeWriter);
                }, false);
            }
            else
            {
                typeWriter.SetItemWriter<TimeSpan?>((item, _, _) =>
                {
                    if (item.HasValue) writer.WriteTimeSpanValue(item.Value);
                    else writer.WriteNullValue();
                }, false);
            }
        }
    }

    #if NET6_0_OR_GREATER
    void CreateDateOnlyItemWriter(CachedTypeWriter typeWriter, bool nullable)
    {
        if (!nullable)
        {
            if (typeWriter.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo)
            {
                typeWriter.SetItemWriter<DateOnly>((item, _, _) =>
                {
                    StartTypeInfoObject(typeWriter);
                    writer.WriteDateOnlyValue(item);
                    FinishTypeInfoObject(typeWriter);
                }, false);
            }
            else if (typeWriter.typeInfoHandling == TypeInfoHandling.AddDeviatingTypeInfo)
            {
                typeWriter.SetItemWriter<DateOnly>((item, deviatingType, _) =>
                {
                    if (deviatingType) StartTypeInfoObject(typeWriter);
                    writer.WriteDateOnlyValue(item);
                    if (deviatingType) FinishTypeInfoObject(typeWriter);
                }, false);
            }
            else
            {
                typeWriter.SetItemWriter<DateOnly>((item, _, _) =>
                {
                    writer.WriteDateOnlyValue(item);
                }, false);
            }
        }
        else
        {
            if (typeWriter.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo)
            {
                typeWriter.SetItemWriter<DateOnly?>((item, _, _) =>
                {
                    StartTypeInfoObject(typeWriter);
                    if (item.HasValue) writer.WriteDateOnlyValue(item.Value);
                    else writer.WriteNullValue();
                    FinishTypeInfoObject(typeWriter);
                }, false);
            }
            else if (typeWriter.typeInfoHandling == TypeInfoHandling.AddDeviatingTypeInfo)
            {
                typeWriter.SetItemWriter<DateOnly?>((item, deviatingType, _) =>
                {
                    if (deviatingType) StartTypeInfoObject(typeWriter);
                    if (item.HasValue) writer.WriteDateOnlyValue(item.Value);
                    else writer.WriteNullValue();
                    if (deviatingType) FinishTypeInfoObject(typeWriter);
                }, false);
            }
            else
            {
                typeWriter.SetItemWriter<DateOnly?>((item, _, _) =>
                {
                    if (item.HasValue) writer.WriteDateOnlyValue(item.Value);
                    else writer.WriteNullValue();
                }, false);
            }
        }
    }

    void CreateTimeOnlyItemWriter(CachedTypeWriter typeWriter, bool nullable)
    {
        if (!nullable)
        {
            if (typeWriter.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo)
            {
                typeWriter.SetItemWriter<TimeOnly>((item, _, _) =>
                {
                    StartTypeInfoObject(typeWriter);
                    writer.WriteTimeOnlyValue(item);
                    FinishTypeInfoObject(typeWriter);
                }, false);
            }
            else if (typeWriter.typeInfoHandling == TypeInfoHandling.AddDeviatingTypeInfo)
            {
                typeWriter.SetItemWriter<TimeOnly>((item, deviatingType, _) =>
                {
                    if (deviatingType) StartTypeInfoObject(typeWriter);
                    writer.WriteTimeOnlyValue(item);
                    if (deviatingType) FinishTypeInfoObject(typeWriter);
                }, false);
            }
            else
            {
                typeWriter.SetItemWriter<TimeOnly>((item, _, _) =>
                {
                    writer.WriteTimeOnlyValue(item);
                }, false);
            }
        }
        else
        {
            if (typeWriter.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo)
            {
                typeWriter.SetItemWriter<TimeOnly?>((item, _, _) =>
                {
                    StartTypeInfoObject(typeWriter);
                    if (item.HasValue) writer.WriteTimeOnlyValue(item.Value);
                    else writer.WriteNullValue();
                    FinishTypeInfoObject(typeWriter);
                }, false);
            }
            else if (typeWriter.typeInfoHandling == TypeInfoHandling.AddDeviatingTypeInfo)
            {
                typeWriter.SetItemWriter<TimeOnly?>((item, deviatingType, _) =>
                {
                    if (deviatingType) StartTypeInfoObject(typeWriter);
                    if (item.HasValue) writer.WriteTimeOnlyValue(item.Value);
                    else writer.WriteNullValue();
                    if (deviatingType) FinishTypeInfoObject(typeWriter);
                }, false);
            }
            else
            {
                typeWriter.SetItemWriter<TimeOnly?>((item, _, _) =>
                {
                    if (item.HasValue) writer.WriteTimeOnlyValue(item.Value);
                    else writer.WriteNullValue();
                }, false);
            }
        }
    }
#endif

    void CreateJsonFragmentItemWriter(CachedTypeWriter typeWriter, bool nullable)
    {
        if (!nullable)
        {
            if (typeWriter.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo)
            {
                typeWriter.SetItemWriter<JsonFragment>((item, _, _) =>
                {
                    StartTypeInfoObject(typeWriter);
                    writer.WriteJsonFragmentValue(item);
                    FinishTypeInfoObject(typeWriter);
                }, false);
            }
            else if (typeWriter.typeInfoHandling == TypeInfoHandling.AddDeviatingTypeInfo)
            {
                typeWriter.SetItemWriter<JsonFragment>((item, deviatingType, _) =>
                {
                    if (deviatingType) StartTypeInfoObject(typeWriter);
                    writer.WriteJsonFragmentValue(item);
                    if (deviatingType) FinishTypeInfoObject(typeWriter);
                }, false);
            }
            else
            {
                typeWriter.SetItemWriter<JsonFragment>((item, _, _) =>
                {
                    writer.WriteJsonFragmentValue(item);
                }, false);
            }
        }
        else
        {
            if (typeWriter.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo)
            {
                typeWriter.SetItemWriter<JsonFragment?>((item, _, _) =>
                {
                    StartTypeInfoObject(typeWriter);
                    if (item.HasValue) writer.WriteJsonFragmentValue(item.Value);
                    else writer.WriteNullValue();
                    FinishTypeInfoObject(typeWriter);
                }, false);
            }
            else if (typeWriter.typeInfoHandling == TypeInfoHandling.AddDeviatingTypeInfo)
            {
                typeWriter.SetItemWriter<JsonFragment?>((item, deviatingType, _) =>
                {
                    if (deviatingType) StartTypeInfoObject(typeWriter);
                    if (item.HasValue) writer.WriteJsonFragmentValue(item.Value);
                    else writer.WriteNullValue();
                    if (deviatingType) FinishTypeInfoObject(typeWriter);
                }, false);
            }
            else
            {
                typeWriter.SetItemWriter<JsonFragment?>((item, _, _) =>
                {
                    if (item.HasValue) writer.WriteJsonFragmentValue(item.Value);
                    else writer.WriteNullValue();
                }, false);
            }
        }
    }

    void CreateTextSegmentItemWriter(CachedTypeWriter typeWriter, bool nullable)
    {
        if (!nullable)
        {
            if (typeWriter.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo)
            {
                typeWriter.SetItemWriter<TextSegment>((item, _, _) =>
                {
                    StartTypeInfoObject(typeWriter);
                    writer.WriteTextSegmentValue(item);
                    FinishTypeInfoObject(typeWriter);
                }, false);
            }
            else if (typeWriter.typeInfoHandling == TypeInfoHandling.AddDeviatingTypeInfo)
            {
                typeWriter.SetItemWriter<TextSegment>((item, deviatingType, _) =>
                {
                    if (deviatingType) StartTypeInfoObject(typeWriter);
                    writer.WriteTextSegmentValue(item);
                    if (deviatingType) FinishTypeInfoObject(typeWriter);
                }, false);
            }
            else
            {
                typeWriter.SetItemWriter<TextSegment>((item, _, _) =>
                {
                    writer.WriteTextSegmentValue(item);
                }, false);
            }
        }
        else
        {
            if (typeWriter.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo)
            {
                typeWriter.SetItemWriter<TextSegment?>((item, _, _) =>
                {
                    StartTypeInfoObject(typeWriter);
                    if (item.HasValue) writer.WriteTextSegmentValue(item.Value);
                    else writer.WriteNullValue();
                    FinishTypeInfoObject(typeWriter);
                }, false);
            }
            else if (typeWriter.typeInfoHandling == TypeInfoHandling.AddDeviatingTypeInfo)
            {
                typeWriter.SetItemWriter<TextSegment?>((item, deviatingType, _) =>
                {
                    if (deviatingType) StartTypeInfoObject(typeWriter);
                    if (item.HasValue) writer.WriteTextSegmentValue(item.Value);
                    else writer.WriteNullValue();
                    if (deviatingType) FinishTypeInfoObject(typeWriter);
                }, false);
            }
            else
            {
                typeWriter.SetItemWriter<TextSegment?>((item, _, _) =>
                {
                    if (item.HasValue) writer.WriteTextSegmentValue(item.Value);
                    else writer.WriteNullValue();
                }, false);
            }
        }
    }

    void CreateUriItemWriter(CachedTypeWriter typeWriter)
    {
        if (typeWriter.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo)
        {
            typeWriter.SetItemWriter<Uri>((item, _, _) =>
            {
                StartTypeInfoObject(typeWriter);
                writer.WriteUriValue(item);
                FinishTypeInfoObject(typeWriter);
            }, false);
        }
        else if (typeWriter.typeInfoHandling == TypeInfoHandling.AddDeviatingTypeInfo)
        {
            typeWriter.SetItemWriter<Uri>((item, deviatingType, _) =>
            {
                if (deviatingType) StartTypeInfoObject(typeWriter);
                writer.WriteUriValue(item);
                if (deviatingType) FinishTypeInfoObject(typeWriter);
            }, false);
        }
        else
        {
            typeWriter.SetItemWriter<Uri>((item, _, _) =>
            {
                writer.WriteUriValue(item);
            }, false);
        }
    }

    private void CreateEnumItemHandler<T>(CachedTypeWriter typeHandler, bool nullable) where T : struct, Enum
    {
        // Captured once at setup, so the per-item check is a well-predicted branch on a readonly
        // local instead of a settings lookup. Spelling out all string/int combinations separately
        // would double the number of variants below without a measurable benefit.
        bool asString = settings.ResolveEnumAsString(typeHandler.TypeSettings);

        if (!nullable)
        {
            if (typeHandler.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo)
            {
                typeHandler.SetItemWriter<T>((item, _, _) =>
                {
                    StartTypeInfoObject(typeHandler);
                    if (asString) writer.WritePreparedStringValue(item.ToUtf8Name());
                    else writer.WriteIntValue(item.ToInt());
                    FinishTypeInfoObject(typeHandler);
                }, false);
            }
            else if (typeHandler.typeInfoHandling == TypeInfoHandling.AddDeviatingTypeInfo)
            {
                typeHandler.SetItemWriter<T>((item, deviatingType, _) =>
                {
                    if (deviatingType) StartTypeInfoObject(typeHandler);
                    if (asString) writer.WritePreparedStringValue(item.ToUtf8Name());
                    else writer.WriteIntValue(item.ToInt());
                    if (deviatingType) FinishTypeInfoObject(typeHandler);
                }, false);
            }
            else
            {
                typeHandler.SetItemWriter<T>((item, _, _) =>
                {
                    if (asString) writer.WritePreparedStringValue(item.ToUtf8Name());
                    else writer.WriteIntValue(item.ToInt());
                }, false);
            }
        }
        else
        {
            if (typeHandler.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo)
            {
                typeHandler.SetItemWriter<T?>((item, _, _) =>
                {
                    StartTypeInfoObject(typeHandler);
                    if (!item.HasValue) writer.WriteNullValue();
                    else if (asString) writer.WritePreparedStringValue(item.Value.ToUtf8Name());
                    else writer.WriteIntValue(item.Value.ToInt());
                    FinishTypeInfoObject(typeHandler);
                }, false);
            }
            else if (typeHandler.typeInfoHandling == TypeInfoHandling.AddDeviatingTypeInfo)
            {
                typeHandler.SetItemWriter<T?>((item, deviatingType, _) =>
                {
                    if (deviatingType) StartTypeInfoObject(typeHandler);
                    if (!item.HasValue) writer.WriteNullValue();
                    else if (asString) writer.WritePreparedStringValue(item.Value.ToUtf8Name());
                    else writer.WriteIntValue(item.Value.ToInt());
                    if (deviatingType) FinishTypeInfoObject(typeHandler);
                }, false);
            }
            else
            {
                typeHandler.SetItemWriter<T?>((item, _, _) =>
                {
                    if (!item.HasValue) writer.WriteNullValue();
                    else if (asString) writer.WritePreparedStringValue(item.Value.ToUtf8Name());
                    else writer.WriteIntValue(item.Value.ToInt());
                }, false);
            }
        }
    }
}
