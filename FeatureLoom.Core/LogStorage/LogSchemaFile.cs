using System;
using System.IO;
using FeatureLoom.Helpers;

namespace FeatureLoom.LogStorage;

/// <summary>
/// Persists the current schema of a log in its folder, so a reopened log continues with the (possibly evolved) schema.
/// Each segment's index header still carries the schema it was written with.
/// </summary>
/// <remarks>
/// Layout (little-endian): magic "FLSC" | u16 version | 16 B log id | schema (see <see cref="LogIndexSchemaCodec"/>) | u32 CRC.
/// </remarks>
public static class LogSchemaFile
{
    /// <summary>File name inside the log folder.</summary>
    public const string FileName = "schema.def";

    /// <summary>File magic "FLSC".</summary>
    public const uint Magic = 0x43534C46;

    /// <summary>Current format version.</summary>
    public const ushort CurrentVersion = 1;

    private const int FixedSize = 4 + 2 + 16;

    /// <summary>Serializes <paramref name="schema"/> together with the owning <paramref name="logId"/>.</summary>
    public static byte[] ToBytes(LogIndexSchema schema, Guid logId)
    {
        var buffer = new byte[FixedSize + LogIndexSchemaCodec.GetSize(schema) + 4];
        int pos = 0;
        LittleEndian.WriteUInt32(buffer, pos, Magic); pos += 4;
        LittleEndian.WriteUInt16(buffer, pos, CurrentVersion); pos += 2;
        Array.Copy(logId.ToByteArray(), 0, buffer, pos, 16); pos += 16;
        pos = LogIndexSchemaCodec.Write(schema, buffer, pos);
        LittleEndian.WriteUInt32(buffer, pos, Crc32.Compute(buffer, 0, pos));
        return buffer;
    }

    /// <summary>Parses serialized schema file content.</summary>
    /// <exception cref="InvalidDataException">The data is truncated, corrupt or of an unsupported version.</exception>
    public static LogIndexSchema Parse(byte[] data, out Guid logId)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        if (data.Length < FixedSize + 4) throw new InvalidDataException("Schema file is truncated.");
        if (LittleEndian.ReadUInt32(data, 0) != Magic) throw new InvalidDataException("Not a log schema file (magic mismatch).");
        ushort version = LittleEndian.ReadUInt16(data, 4);
        if (version != CurrentVersion) throw new InvalidDataException($"Unsupported schema file version {version}.");
        int crcPos = data.Length - 4;
        if (Crc32.Compute(data, 0, crcPos) != LittleEndian.ReadUInt32(data, crcPos)) throw new InvalidDataException("Schema file checksum mismatch.");

        var idBytes = new byte[16];
        Array.Copy(data, 6, idBytes, 0, 16);
        logId = new Guid(idBytes);
        int pos = FixedSize;
        var schema = LogIndexSchemaCodec.Read(data, ref pos, crcPos);
        if (pos != crcPos) throw new InvalidDataException("Schema file size mismatch.");
        return schema;
    }

    /// <summary>Loads the schema from <paramref name="directory"/>; returns false if the file does not exist.</summary>
    /// <exception cref="InvalidDataException">The file is corrupt.</exception>
    public static bool TryLoad(string directory, out LogIndexSchema schema, out Guid logId)
    {
        string path = Path.Combine(directory, FileName);
        if (!File.Exists(path))
        {
            schema = null;
            logId = default;
            return false;
        }
        schema = Parse(File.ReadAllBytes(path), out logId);
        return true;
    }

    /// <summary>
    /// Writes the schema to <paramref name="directory"/> via a temp file and replace, so a crash never leaves a torn file.
    /// Must happen before the first entry of a segment using a new schema is written.
    /// </summary>
    public static void Write(string directory, LogIndexSchema schema, Guid logId)
    {
        string path = Path.Combine(directory, FileName);
        string tempPath = path + ".tmp";
        File.WriteAllBytes(tempPath, ToBytes(schema, logId));
        if (File.Exists(path)) File.Replace(tempPath, path, null);
        else File.Move(tempPath, path);
    }
}
