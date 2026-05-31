using FeatureLoom.Collections;
using System;
using System.Xml.Serialization;

namespace FeatureLoom.Serialization;

public sealed partial class JsonSerializer
{
    void CreateIntItemWriter(CachedTypeWriter typeWriter, bool nullable)
    {
        if (!nullable)
        {
            if (settings.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo)
            {
                typeWriter.SetItemWriter<int>((item, _, _) =>
                {
                    StartTypeInfoObject(typeWriter.preparedTypeInfo);
                    writer.WriteIntValue(item);
                    FinishTypeInfoObject();
                }, false);
            }
            else if (settings.typeInfoHandling == TypeInfoHandling.AddDeviatingTypeInfo)
            {
                typeWriter.SetItemWriter<int>((item, deviatingType, _) =>
                {
                    if (deviatingType) StartTypeInfoObject(typeWriter.preparedTypeInfo);
                    writer.WriteIntValue(item);
                    if (deviatingType) FinishTypeInfoObject();
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
            if (settings.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo)
            {
                typeWriter.SetItemWriter<int?>((item, _, _) =>
                {
                    StartTypeInfoObject(typeWriter.preparedTypeInfo);
                    if (item.HasValue) writer.WriteIntValue(item.Value);
                    else writer.WriteNullValue();
                    FinishTypeInfoObject();
                }, false);
            }
            else if (settings.typeInfoHandling == TypeInfoHandling.AddDeviatingTypeInfo)
            {
                typeWriter.SetItemWriter<int?>((item, deviatingType, _) =>
                {
                    if (deviatingType) StartTypeInfoObject(typeWriter.preparedTypeInfo);
                    if (item.HasValue) writer.WriteIntValue(item.Value);
                    else writer.WriteNullValue();
                    if (deviatingType) FinishTypeInfoObject();
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
            if (settings.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo)
            {
                typeWriter.SetItemWriter<uint>((item, _, _) =>
                {
                    StartTypeInfoObject(typeWriter.preparedTypeInfo);
                    writer.WriteUintValue(item);
                    FinishTypeInfoObject();
                }, false);
            }
            else if (settings.typeInfoHandling == TypeInfoHandling.AddDeviatingTypeInfo)
            {
                typeWriter.SetItemWriter<uint>((item, deviatingType, _) =>
                {
                    if (deviatingType) StartTypeInfoObject(typeWriter.preparedTypeInfo);
                    writer.WriteUintValue(item);
                    if (deviatingType) FinishTypeInfoObject();
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
            if (settings.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo)
            {
                typeWriter.SetItemWriter<uint?>((item, _, _) =>
                {
                    StartTypeInfoObject(typeWriter.preparedTypeInfo);
                    if (item.HasValue) writer.WriteUintValue(item.Value);
                    else writer.WriteNullValue();
                    FinishTypeInfoObject();
                }, false);
            }
            else if (settings.typeInfoHandling == TypeInfoHandling.AddDeviatingTypeInfo)
            {
                typeWriter.SetItemWriter<uint?>((item, deviatingType, _) =>
                {
                    if (deviatingType) StartTypeInfoObject(typeWriter.preparedTypeInfo);
                    if (item.HasValue) writer.WriteUintValue(item.Value);
                    else writer.WriteNullValue();
                    if (deviatingType) FinishTypeInfoObject();
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
            if (settings.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo)
            {
                typeWriter.SetItemWriter<long>((item, _, _) =>
                {
                    StartTypeInfoObject(typeWriter.preparedTypeInfo);
                    writer.WriteLongValue(item);
                    FinishTypeInfoObject();
                }, false);
            }
            else if (settings.typeInfoHandling == TypeInfoHandling.AddDeviatingTypeInfo)
            {
                typeWriter.SetItemWriter<long>((item, deviatingType, _) =>
                {
                    if (deviatingType) StartTypeInfoObject(typeWriter.preparedTypeInfo);
                    writer.WriteLongValue(item);
                    if (deviatingType) FinishTypeInfoObject();
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
            if (settings.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo)
            {
                typeWriter.SetItemWriter<long?>((item, _, _) =>
                {
                    StartTypeInfoObject(typeWriter.preparedTypeInfo);
                    if (item.HasValue) writer.WriteLongValue(item.Value);
                    else writer.WriteNullValue();
                    FinishTypeInfoObject();
                }, false);
            }
            else if (settings.typeInfoHandling == TypeInfoHandling.AddDeviatingTypeInfo)
            {
                typeWriter.SetItemWriter<long?>((item, deviatingType, _) =>
                {
                    if (deviatingType) StartTypeInfoObject(typeWriter.preparedTypeInfo);
                    if (item.HasValue) writer.WriteLongValue(item.Value);
                    else writer.WriteNullValue();
                    if (deviatingType) FinishTypeInfoObject();
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
            if (settings.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo)
            {
                typeWriter.SetItemWriter<ulong>((item, _, _) =>
                {
                    StartTypeInfoObject(typeWriter.preparedTypeInfo);
                    writer.WriteUlongValue(item);
                    FinishTypeInfoObject();
                }, false);
            }
            else if (settings.typeInfoHandling == TypeInfoHandling.AddDeviatingTypeInfo)
            {
                typeWriter.SetItemWriter<ulong>((item, deviatingType, _) =>
                {
                    if (deviatingType) StartTypeInfoObject(typeWriter.preparedTypeInfo);
                    writer.WriteUlongValue(item);
                    if (deviatingType) FinishTypeInfoObject();
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
            if (settings.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo)
            {
                typeWriter.SetItemWriter<ulong?>((item, _, _) =>
                {
                    StartTypeInfoObject(typeWriter.preparedTypeInfo);
                    if (item.HasValue) writer.WriteUlongValue(item.Value);
                    else writer.WriteNullValue();
                    FinishTypeInfoObject();
                }, false);
            }
            else if (settings.typeInfoHandling == TypeInfoHandling.AddDeviatingTypeInfo)
            {
                typeWriter.SetItemWriter<ulong?>((item, deviatingType, _) =>
                {
                    if (deviatingType) StartTypeInfoObject(typeWriter.preparedTypeInfo);
                    if (item.HasValue) writer.WriteUlongValue(item.Value);
                    else writer.WriteNullValue();
                    if (deviatingType) FinishTypeInfoObject();
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
            if (settings.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo)
            {
                typeWriter.SetItemWriter<short>((item, _, _) =>
                {
                    StartTypeInfoObject(typeWriter.preparedTypeInfo);
                    writer.WriteShortValue(item);
                    FinishTypeInfoObject();
                }, false);
            }
            else if (settings.typeInfoHandling == TypeInfoHandling.AddDeviatingTypeInfo)
            {
                typeWriter.SetItemWriter<short>((item, deviatingType, _) =>
                {
                    if (deviatingType) StartTypeInfoObject(typeWriter.preparedTypeInfo);
                    writer.WriteShortValue(item);
                    if (deviatingType) FinishTypeInfoObject();
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
            if (settings.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo)
            {
                typeWriter.SetItemWriter<short?>((item, _, _) =>
                {
                    StartTypeInfoObject(typeWriter.preparedTypeInfo);
                    if (item.HasValue) writer.WriteShortValue(item.Value);
                    else writer.WriteNullValue();
                    FinishTypeInfoObject();
                }, false);
            }
            else if (settings.typeInfoHandling == TypeInfoHandling.AddDeviatingTypeInfo)
            {
                typeWriter.SetItemWriter<short?>((item, deviatingType, _) =>
                {
                    if (deviatingType) StartTypeInfoObject(typeWriter.preparedTypeInfo);
                    if (item.HasValue) writer.WriteShortValue(item.Value);
                    else writer.WriteNullValue();
                    if (deviatingType) FinishTypeInfoObject();
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
            if (settings.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo)
            {
                typeWriter.SetItemWriter<ushort>((item, _, _) =>
                {
                    StartTypeInfoObject(typeWriter.preparedTypeInfo);
                    writer.WriteUshortValue(item);
                    FinishTypeInfoObject();
                }, false);
            }
            else if (settings.typeInfoHandling == TypeInfoHandling.AddDeviatingTypeInfo)
            {
                typeWriter.SetItemWriter<ushort>((item, deviatingType, _) =>
                {
                    if (deviatingType) StartTypeInfoObject(typeWriter.preparedTypeInfo);
                    writer.WriteUshortValue(item);
                    if (deviatingType) FinishTypeInfoObject();
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
            if (settings.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo)
            {
                typeWriter.SetItemWriter<ushort?>((item, _, _) =>
                {
                    StartTypeInfoObject(typeWriter.preparedTypeInfo);
                    if (item.HasValue) writer.WriteUshortValue(item.Value);
                    else writer.WriteNullValue();
                    FinishTypeInfoObject();
                }, false);
            }
            else if (settings.typeInfoHandling == TypeInfoHandling.AddDeviatingTypeInfo)
            {
                typeWriter.SetItemWriter<ushort?>((item, deviatingType, _) =>
                {
                    if (deviatingType) StartTypeInfoObject(typeWriter.preparedTypeInfo);
                    if (item.HasValue) writer.WriteUshortValue(item.Value);
                    else writer.WriteNullValue();
                    if (deviatingType) FinishTypeInfoObject();
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
            if (settings.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo)
            {
                typeWriter.SetItemWriter<sbyte>((item, _, _) =>
                {
                    StartTypeInfoObject(typeWriter.preparedTypeInfo);
                    writer.WriteSbyteValue(item);
                    FinishTypeInfoObject();
                }, false);
            }
            else if (settings.typeInfoHandling == TypeInfoHandling.AddDeviatingTypeInfo)
            {
                typeWriter.SetItemWriter<sbyte>((item, deviatingType, _) =>
                {
                    if (deviatingType) StartTypeInfoObject(typeWriter.preparedTypeInfo);
                    writer.WriteSbyteValue(item);
                    if (deviatingType) FinishTypeInfoObject();
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
            if (settings.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo)
            {
                typeWriter.SetItemWriter<sbyte?>((item, _, _) =>
                {
                    StartTypeInfoObject(typeWriter.preparedTypeInfo);
                    if (item.HasValue) writer.WriteSbyteValue(item.Value);
                    else writer.WriteNullValue();
                    FinishTypeInfoObject();
                }, false);
            }
            else if (settings.typeInfoHandling == TypeInfoHandling.AddDeviatingTypeInfo)
            {
                typeWriter.SetItemWriter<sbyte?>((item, deviatingType, _) =>
                {
                    if (deviatingType) StartTypeInfoObject(typeWriter.preparedTypeInfo);
                    if (item.HasValue) writer.WriteSbyteValue(item.Value);
                    else writer.WriteNullValue();
                    if (deviatingType) FinishTypeInfoObject();
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
            if (settings.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo)
            {
                typeWriter.SetItemWriter<byte>((item, _, _) =>
                {
                    StartTypeInfoObject(typeWriter.preparedTypeInfo);
                    writer.WriteByteValue(item);
                    FinishTypeInfoObject();
                }, false);
            }
            else if (settings.typeInfoHandling == TypeInfoHandling.AddDeviatingTypeInfo)
            {
                typeWriter.SetItemWriter<byte>((item, deviatingType, _) =>
                {
                    if (deviatingType) StartTypeInfoObject(typeWriter.preparedTypeInfo);
                    writer.WriteByteValue(item);
                    if (deviatingType) FinishTypeInfoObject();
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
            if (settings.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo)
            {
                typeWriter.SetItemWriter<byte?>((item, _, _) =>
                {
                    StartTypeInfoObject(typeWriter.preparedTypeInfo);
                    if (item.HasValue) writer.WriteByteValue(item.Value);
                    else writer.WriteNullValue();
                    FinishTypeInfoObject();
                }, false);
            }
            else if (settings.typeInfoHandling == TypeInfoHandling.AddDeviatingTypeInfo)
            {
                typeWriter.SetItemWriter<byte?>((item, deviatingType, _) =>
                {
                    if (deviatingType) StartTypeInfoObject(typeWriter.preparedTypeInfo);
                    if (item.HasValue) writer.WriteByteValue(item.Value);
                    else writer.WriteNullValue();
                    if (deviatingType) FinishTypeInfoObject();
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
            if (settings.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo)
            {
                typeWriter.SetItemWriter<double>((item, _, _) =>
                {
                    StartTypeInfoObject(typeWriter.preparedTypeInfo);
                    writer.WriteDoubleValue(item);
                    FinishTypeInfoObject();
                }, false);
            }
            else if (settings.typeInfoHandling == TypeInfoHandling.AddDeviatingTypeInfo)
            {
                typeWriter.SetItemWriter<double>((item, deviatingType, _) =>
                {
                    if (deviatingType) StartTypeInfoObject(typeWriter.preparedTypeInfo);
                    writer.WriteDoubleValue(item);
                    if (deviatingType) FinishTypeInfoObject();
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
            if (settings.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo)
            {
                typeWriter.SetItemWriter<double?>((item, _, _) =>
                {
                    StartTypeInfoObject(typeWriter.preparedTypeInfo);
                    if (item.HasValue) writer.WriteDoubleValue(item.Value);
                    else writer.WriteNullValue();
                    FinishTypeInfoObject();
                }, false);
            }
            else if (settings.typeInfoHandling == TypeInfoHandling.AddDeviatingTypeInfo)
            {
                typeWriter.SetItemWriter<double?>((item, deviatingType, _) =>
                {
                    if (deviatingType) StartTypeInfoObject(typeWriter.preparedTypeInfo);
                    if (item.HasValue) writer.WriteDoubleValue(item.Value);
                    else writer.WriteNullValue();
                    if (deviatingType) FinishTypeInfoObject();
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
            if (settings.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo)
            {
                typeWriter.SetItemWriter<float>((item, _, _) =>
                {
                    StartTypeInfoObject(typeWriter.preparedTypeInfo);
                    writer.WriteFloatValue(item);
                    FinishTypeInfoObject();
                }, false);
            }
            else if (settings.typeInfoHandling == TypeInfoHandling.AddDeviatingTypeInfo)
            {
                typeWriter.SetItemWriter<float>((item, deviatingType, _) =>
                {
                    if (deviatingType) StartTypeInfoObject(typeWriter.preparedTypeInfo);
                    writer.WriteFloatValue(item);
                    if (deviatingType) FinishTypeInfoObject();
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
            if (settings.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo)
            {
                typeWriter.SetItemWriter<float?>((item, _, _) =>
                {
                    StartTypeInfoObject(typeWriter.preparedTypeInfo);
                    if (item.HasValue) writer.WriteFloatValue(item.Value);
                    else writer.WriteNullValue();
                    FinishTypeInfoObject();
                }, false);
            }
            else if (settings.typeInfoHandling == TypeInfoHandling.AddDeviatingTypeInfo)
            {
                typeWriter.SetItemWriter<float?>((item, deviatingType, _) =>
                {
                    if (deviatingType) StartTypeInfoObject(typeWriter.preparedTypeInfo);
                    if (item.HasValue) writer.WriteFloatValue(item.Value);
                    else writer.WriteNullValue();
                    if (deviatingType) FinishTypeInfoObject();
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
            if (settings.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo)
            {
                typeWriter.SetItemWriter<decimal>((item, _, _) =>
                {
                    StartTypeInfoObject(typeWriter.preparedTypeInfo);
                    writer.WriteDecimalValue(item);
                    FinishTypeInfoObject();
                }, false);
            }
            else if (settings.typeInfoHandling == TypeInfoHandling.AddDeviatingTypeInfo)
            {
                typeWriter.SetItemWriter<decimal>((item, deviatingType, _) =>
                {
                    if (deviatingType) StartTypeInfoObject(typeWriter.preparedTypeInfo);
                    writer.WriteDecimalValue(item);
                    if (deviatingType) FinishTypeInfoObject();
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
            if (settings.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo)
            {
                typeWriter.SetItemWriter<decimal?>((item, _, _) =>
                {
                    StartTypeInfoObject(typeWriter.preparedTypeInfo);
                    if (item.HasValue) writer.WriteDecimalValue(item.Value);
                    else writer.WriteNullValue();
                    FinishTypeInfoObject();
                }, false);
            }
            else if (settings.typeInfoHandling == TypeInfoHandling.AddDeviatingTypeInfo)
            {
                typeWriter.SetItemWriter<decimal?>((item, deviatingType, _) =>
                {
                    if (deviatingType) StartTypeInfoObject(typeWriter.preparedTypeInfo);
                    if (item.HasValue) writer.WriteDecimalValue(item.Value);
                    else writer.WriteNullValue();
                    if (deviatingType) FinishTypeInfoObject();
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
            if (settings.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo)
            {
                typeWriter.SetItemWriter<char>((item, _, _) =>
                {
                    StartTypeInfoObject(typeWriter.preparedTypeInfo);
                    writer.WriteCharValue(item);
                    FinishTypeInfoObject();
                }, false);
            }
            else if (settings.typeInfoHandling == TypeInfoHandling.AddDeviatingTypeInfo)
            {
                typeWriter.SetItemWriter<char>((item, deviatingType, _) =>
                {
                    if (deviatingType) StartTypeInfoObject(typeWriter.preparedTypeInfo);
                    writer.WriteCharValue(item);
                    if (deviatingType) FinishTypeInfoObject();
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
            if (settings.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo)
            {
                typeWriter.SetItemWriter<char?>((item, _, _) =>
                {
                    StartTypeInfoObject(typeWriter.preparedTypeInfo);
                    if (item.HasValue) writer.WriteCharValue(item.Value);
                    else writer.WriteNullValue();
                    FinishTypeInfoObject();
                }, false);
            }
            else if (settings.typeInfoHandling == TypeInfoHandling.AddDeviatingTypeInfo)
            {
                typeWriter.SetItemWriter<char?>((item, deviatingType, _) =>
                {
                    if (deviatingType) StartTypeInfoObject(typeWriter.preparedTypeInfo);
                    if (item.HasValue) writer.WriteCharValue(item.Value);
                    else writer.WriteNullValue();
                    if (deviatingType) FinishTypeInfoObject();
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
        if (settings.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo)
        {
            typeWriter.SetItemWriter<string>((item, _, _) =>
            {
                StartTypeInfoObject(typeWriter.preparedTypeInfo);
                writer.WriteStringValue(item);
                FinishTypeInfoObject();
            }, false);
        }
        else if (settings.typeInfoHandling == TypeInfoHandling.AddDeviatingTypeInfo)
        {
            typeWriter.SetItemWriter<string>((item, deviatingType, _) =>
            {
                if (deviatingType) StartTypeInfoObject(typeWriter.preparedTypeInfo);
                writer.WriteStringValue(item);
                if (deviatingType) FinishTypeInfoObject();
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
            if (settings.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo)
            {
                typeWriter.SetItemWriter<bool>((item, _, _) =>
                {
                    StartTypeInfoObject(typeWriter.preparedTypeInfo);
                    writer.WriteBoolValue(item);
                    FinishTypeInfoObject();
                }, false);
            }
            else if (settings.typeInfoHandling == TypeInfoHandling.AddDeviatingTypeInfo)
            {
                typeWriter.SetItemWriter<bool>((item, deviatingType, _) =>
                {
                    if (deviatingType) StartTypeInfoObject(typeWriter.preparedTypeInfo);
                    writer.WriteBoolValue(item);
                    if (deviatingType) FinishTypeInfoObject();
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
            if (settings.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo)
            {
                typeWriter.SetItemWriter<bool?>((item, _, _) =>
                {
                    StartTypeInfoObject(typeWriter.preparedTypeInfo);
                    if (item.HasValue) writer.WriteBoolValue(item.Value);
                    else writer.WriteNullValue();
                    FinishTypeInfoObject();
                }, false);
            }
            else if (settings.typeInfoHandling == TypeInfoHandling.AddDeviatingTypeInfo)
            {
                typeWriter.SetItemWriter<bool?>((item, deviatingType, _) =>
                {
                    if (deviatingType) StartTypeInfoObject(typeWriter.preparedTypeInfo);
                    if (item.HasValue) writer.WriteBoolValue(item.Value);
                    else writer.WriteNullValue();
                    if (deviatingType) FinishTypeInfoObject();
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
            if (settings.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo)
            {
                typeWriter.SetItemWriter<nint>((item, _, _) =>
                {
                    StartTypeInfoObject(typeWriter.preparedTypeInfo);
                    writer.WriteIntPtrValue(item);
                    FinishTypeInfoObject();
                }, false);
            }
            else if (settings.typeInfoHandling == TypeInfoHandling.AddDeviatingTypeInfo)
            {
                typeWriter.SetItemWriter<nint>((item, deviatingType, _) =>
                {
                    if (deviatingType) StartTypeInfoObject(typeWriter.preparedTypeInfo);
                    writer.WriteIntPtrValue(item);
                    if (deviatingType) FinishTypeInfoObject();
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
            if (settings.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo)
            {
                typeWriter.SetItemWriter<IntPtr?>((item, _, _) =>
                {
                    StartTypeInfoObject(typeWriter.preparedTypeInfo);
                    if (item.HasValue) writer.WriteIntPtrValue(item.Value);
                    else writer.WriteNullValue();
                    FinishTypeInfoObject();
                }, false);
            }
            else if (settings.typeInfoHandling == TypeInfoHandling.AddDeviatingTypeInfo)
            {
                typeWriter.SetItemWriter<IntPtr?>((item, deviatingType, _) =>
                {
                    if (deviatingType) StartTypeInfoObject(typeWriter.preparedTypeInfo);
                    if (item.HasValue) writer.WriteIntPtrValue(item.Value);
                    else writer.WriteNullValue();
                    if (deviatingType) FinishTypeInfoObject();
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
            if (settings.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo)
            {
                typeWriter.SetItemWriter<nuint>((item, _, _) =>
                {
                    StartTypeInfoObject(typeWriter.preparedTypeInfo);
                    writer.WriteUintPtrValue(item);
                    FinishTypeInfoObject();
                }, false);
            }
            else if (settings.typeInfoHandling == TypeInfoHandling.AddDeviatingTypeInfo)
            {
                typeWriter.SetItemWriter<nuint>((item, deviatingType, _) =>
                {
                    if (deviatingType) StartTypeInfoObject(typeWriter.preparedTypeInfo);
                    writer.WriteUintPtrValue(item);
                    if (deviatingType) FinishTypeInfoObject();
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
            if (settings.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo)
            {
                typeWriter.SetItemWriter<UIntPtr?>((item, _, _) =>
                {
                    StartTypeInfoObject(typeWriter.preparedTypeInfo);
                    if (item.HasValue) writer.WriteUintPtrValue(item.Value);
                    else writer.WriteNullValue();
                    FinishTypeInfoObject();
                }, false);
            }
            else if (settings.typeInfoHandling == TypeInfoHandling.AddDeviatingTypeInfo)
            {
                typeWriter.SetItemWriter<UIntPtr?>((item, deviatingType, _) =>
                {
                    if (deviatingType) StartTypeInfoObject(typeWriter.preparedTypeInfo);
                    if (item.HasValue) writer.WriteUintPtrValue(item.Value);
                    else writer.WriteNullValue();
                    if (deviatingType) FinishTypeInfoObject();
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
            if (settings.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo)
            {
                typeWriter.SetItemWriter<Guid>((item, _, _) =>
                {
                    StartTypeInfoObject(typeWriter.preparedTypeInfo);
                    writer.WriteGuidValue(item);
                    FinishTypeInfoObject();
                }, false);
            }
            else if (settings.typeInfoHandling == TypeInfoHandling.AddDeviatingTypeInfo)
            {
                typeWriter.SetItemWriter<Guid>((item, deviatingType, _) =>
                {
                    if (deviatingType) StartTypeInfoObject(typeWriter.preparedTypeInfo);
                    writer.WriteGuidValue(item);
                    if (deviatingType) FinishTypeInfoObject();
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
            if (settings.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo)
            {
                typeWriter.SetItemWriter<Guid?>((item, _, _) =>
                {
                    StartTypeInfoObject(typeWriter.preparedTypeInfo);
                    if (item.HasValue) writer.WriteGuidValue(item.Value);
                    else writer.WriteNullValue();
                    FinishTypeInfoObject();
                }, false);
            }
            else if (settings.typeInfoHandling == TypeInfoHandling.AddDeviatingTypeInfo)
            {
                typeWriter.SetItemWriter<Guid?>((item, deviatingType, _) =>
                {
                    if (deviatingType) StartTypeInfoObject(typeWriter.preparedTypeInfo);
                    if (item.HasValue) writer.WriteGuidValue(item.Value);
                    else writer.WriteNullValue();
                    if (deviatingType) FinishTypeInfoObject();
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
            if (settings.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo)
            {
                typeWriter.SetItemWriter<DateTime>((item, _, _) =>
                {
                    StartTypeInfoObject(typeWriter.preparedTypeInfo);
                    writer.WriteDateTimeValue(item);
                    FinishTypeInfoObject();
                }, false);
            }
            else if (settings.typeInfoHandling == TypeInfoHandling.AddDeviatingTypeInfo)
            {
                typeWriter.SetItemWriter<DateTime>((item, deviatingType, _) =>
                {
                    if (deviatingType) StartTypeInfoObject(typeWriter.preparedTypeInfo);
                    writer.WriteDateTimeValue(item);
                    if (deviatingType) FinishTypeInfoObject();
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
            if (settings.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo)
            {
                typeWriter.SetItemWriter<DateTime?>((item, _, _) =>
                {
                    StartTypeInfoObject(typeWriter.preparedTypeInfo);
                    if (item.HasValue) writer.WriteDateTimeValue(item.Value);
                    else writer.WriteNullValue();
                    FinishTypeInfoObject();
                }, false);
            }
            else if (settings.typeInfoHandling == TypeInfoHandling.AddDeviatingTypeInfo)
            {
                typeWriter.SetItemWriter<DateTime?>((item, deviatingType, _) =>
                {
                    if (deviatingType) StartTypeInfoObject(typeWriter.preparedTypeInfo);
                    if (item.HasValue) writer.WriteDateTimeValue(item.Value);
                    else writer.WriteNullValue();
                    if (deviatingType) FinishTypeInfoObject();
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
            if (settings.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo)
            {
                typeWriter.SetItemWriter<DateTimeOffset>((item, _, _) =>
                {
                    StartTypeInfoObject(typeWriter.preparedTypeInfo);
                    writer.WriteDateTimeOffsetValue(item);
                    FinishTypeInfoObject();
                }, false);
            }
            else if (settings.typeInfoHandling == TypeInfoHandling.AddDeviatingTypeInfo)
            {
                typeWriter.SetItemWriter<DateTimeOffset>((item, deviatingType, _) =>
                {
                    if (deviatingType) StartTypeInfoObject(typeWriter.preparedTypeInfo);
                    writer.WriteDateTimeOffsetValue(item);
                    if (deviatingType) FinishTypeInfoObject();
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
            if (settings.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo)
            {
                typeWriter.SetItemWriter<DateTimeOffset?>((item, _, _) =>
                {
                    StartTypeInfoObject(typeWriter.preparedTypeInfo);
                    if (item.HasValue) writer.WriteDateTimeOffsetValue(item.Value);
                    else writer.WriteNullValue();
                    FinishTypeInfoObject();
                }, false);
            }
            else if (settings.typeInfoHandling == TypeInfoHandling.AddDeviatingTypeInfo)
            {
                typeWriter.SetItemWriter<DateTimeOffset?>((item, deviatingType, _) =>
                {
                    if (deviatingType) StartTypeInfoObject(typeWriter.preparedTypeInfo);
                    if (item.HasValue) writer.WriteDateTimeOffsetValue(item.Value);
                    else writer.WriteNullValue();
                    if (deviatingType) FinishTypeInfoObject();
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
            if (settings.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo)
            {
                typeWriter.SetItemWriter<TimeSpan>((item, _, _) =>
                {
                    StartTypeInfoObject(typeWriter.preparedTypeInfo);
                    writer.WriteTimeSpanValue(item);
                    FinishTypeInfoObject();
                }, false);
            }
            else if (settings.typeInfoHandling == TypeInfoHandling.AddDeviatingTypeInfo)
            {
                typeWriter.SetItemWriter<TimeSpan>((item, deviatingType, _) =>
                {
                    if (deviatingType) StartTypeInfoObject(typeWriter.preparedTypeInfo);
                    writer.WriteTimeSpanValue(item);
                    if (deviatingType) FinishTypeInfoObject();
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
            if (settings.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo)
            {
                typeWriter.SetItemWriter<TimeSpan?>((item, _, _) =>
                {
                    StartTypeInfoObject(typeWriter.preparedTypeInfo);
                    if (item.HasValue) writer.WriteTimeSpanValue(item.Value);
                    else writer.WriteNullValue();
                    FinishTypeInfoObject();
                }, false);
            }
            else if (settings.typeInfoHandling == TypeInfoHandling.AddDeviatingTypeInfo)
            {
                typeWriter.SetItemWriter<TimeSpan?>((item, deviatingType, _) =>
                {
                    if (deviatingType) StartTypeInfoObject(typeWriter.preparedTypeInfo);
                    if (item.HasValue) writer.WriteTimeSpanValue(item.Value);
                    else writer.WriteNullValue();
                    if (deviatingType) FinishTypeInfoObject();
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

    void CreateJsonFragmentItemWriter(CachedTypeWriter typeWriter, bool nullable)
    {
        if (!nullable)
        {
            if (settings.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo)
            {
                typeWriter.SetItemWriter<JsonFragment>((item, _, _) =>
                {
                    StartTypeInfoObject(typeWriter.preparedTypeInfo);
                    writer.WriteJsonFragmentValue(item);
                    FinishTypeInfoObject();
                }, false);
            }
            else if (settings.typeInfoHandling == TypeInfoHandling.AddDeviatingTypeInfo)
            {
                typeWriter.SetItemWriter<JsonFragment>((item, deviatingType, _) =>
                {
                    if (deviatingType) StartTypeInfoObject(typeWriter.preparedTypeInfo);
                    writer.WriteJsonFragmentValue(item);
                    if (deviatingType) FinishTypeInfoObject();
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
            if (settings.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo)
            {
                typeWriter.SetItemWriter<JsonFragment?>((item, _, _) =>
                {
                    StartTypeInfoObject(typeWriter.preparedTypeInfo);
                    if (item.HasValue) writer.WriteJsonFragmentValue(item.Value);
                    else writer.WriteNullValue();
                    FinishTypeInfoObject();
                }, false);
            }
            else if (settings.typeInfoHandling == TypeInfoHandling.AddDeviatingTypeInfo)
            {
                typeWriter.SetItemWriter<JsonFragment?>((item, deviatingType, _) =>
                {
                    if (deviatingType) StartTypeInfoObject(typeWriter.preparedTypeInfo);
                    if (item.HasValue) writer.WriteJsonFragmentValue(item.Value);
                    else writer.WriteNullValue();
                    if (deviatingType) FinishTypeInfoObject();
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
            if (settings.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo)
            {
                typeWriter.SetItemWriter<TextSegment>((item, _, _) =>
                {
                    StartTypeInfoObject(typeWriter.preparedTypeInfo);
                    writer.WriteTextSegmentValue(item);
                    FinishTypeInfoObject();
                }, false);
            }
            else if (settings.typeInfoHandling == TypeInfoHandling.AddDeviatingTypeInfo)
            {
                typeWriter.SetItemWriter<TextSegment>((item, deviatingType, _) =>
                {
                    if (deviatingType) StartTypeInfoObject(typeWriter.preparedTypeInfo);
                    writer.WriteTextSegmentValue(item);
                    if (deviatingType) FinishTypeInfoObject();
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
            if (settings.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo)
            {
                typeWriter.SetItemWriter<TextSegment?>((item, _, _) =>
                {
                    StartTypeInfoObject(typeWriter.preparedTypeInfo);
                    if (item.HasValue) writer.WriteTextSegmentValue(item.Value);
                    else writer.WriteNullValue();
                    FinishTypeInfoObject();
                }, false);
            }
            else if (settings.typeInfoHandling == TypeInfoHandling.AddDeviatingTypeInfo)
            {
                typeWriter.SetItemWriter<TextSegment?>((item, deviatingType, _) =>
                {
                    if (deviatingType) StartTypeInfoObject(typeWriter.preparedTypeInfo);
                    if (item.HasValue) writer.WriteTextSegmentValue(item.Value);
                    else writer.WriteNullValue();
                    if (deviatingType) FinishTypeInfoObject();
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
        if (settings.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo)
        {
            typeWriter.SetItemWriter<Uri>((item, _, _) =>
            {
                StartTypeInfoObject(typeWriter.preparedTypeInfo);
                writer.WriteUriValue(item);
                FinishTypeInfoObject();
            }, false);
        }
        else if (settings.typeInfoHandling == TypeInfoHandling.AddDeviatingTypeInfo)
        {
            typeWriter.SetItemWriter<Uri>((item, deviatingType, _) =>
            {
                if (deviatingType) StartTypeInfoObject(typeWriter.preparedTypeInfo);
                writer.WriteUriValue(item);
                if (deviatingType) FinishTypeInfoObject();
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
}
