using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using FeatureLoom.Helpers;

namespace FeatureLoom.LogStorage;

/// <summary>
/// Persisted name-to-id map of the payload types of a log (file <see cref="FileName"/> in the log folder).
/// Ids are assigned once per name and never reused, so stored entries keep their meaning even if
/// registration order changes or types are no longer registered.
/// <para>Binary layout: magic "FLTM" | u16 version | u16 count | per entry: u16 id, u16 name length, UTF-8 name | u32 CRC32.</para>
/// </summary>
public sealed class LogPayloadTypeMap
{
    /// <summary>File name of the map within the log folder.</summary>
    public const string FileName = "types.map";

    /// <summary>Current format version.</summary>
    public const ushort CurrentVersion = 1;

    private static readonly byte[] Magic = { (byte)'F', (byte)'L', (byte)'T', (byte)'M' };

    private readonly Dictionary<string, ushort> idsByName = new Dictionary<string, ushort>(StringComparer.Ordinal);
    private readonly Dictionary<ushort, string> namesById = new Dictionary<ushort, string>();
    private int nextId;

    /// <summary>True if entries were added since creation, load or <see cref="ToBytes"/>/<see cref="Write"/>.</summary>
    public bool IsDirty { get; private set; }

    /// <summary>Number of entries.</summary>
    public int Count => idsByName.Count;

    /// <summary>All entries (name, id).</summary>
    public IEnumerable<KeyValuePair<string, ushort>> Entries => idsByName;

    /// <summary>Gets the id assigned to <paramref name="name"/>.</summary>
    public bool TryGetId(string name, out ushort id) => idsByName.TryGetValue(name, out id);

    /// <summary>Gets the name assigned to <paramref name="id"/>.</summary>
    public bool TryGetName(ushort id, out string name) => namesById.TryGetValue(id, out name);

    /// <summary>Assigns the next free id to a new <paramref name="name"/>. Returns false if no id up to <paramref name="maxId"/> is left.</summary>
    /// <exception cref="ArgumentException">The name is empty or already mapped.</exception>
    public bool TryAdd(string name, int maxId, out ushort id)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Name must not be empty.", nameof(name));
        if (idsByName.ContainsKey(name)) throw new ArgumentException($"Name '{name}' is already mapped.", nameof(name));
        if (nextId > maxId || nextId > ushort.MaxValue)
        {
            id = 0;
            return false;
        }
        id = (ushort)nextId++;
        Add(name, id);
        IsDirty = true;
        return true;
    }

    private void Add(string name, ushort id)
    {
        idsByName.Add(name, id);
        namesById.Add(id, name);
        if (id >= nextId) nextId = id + 1;
    }

    /// <summary>Serializes the map and resets <see cref="IsDirty"/>.</summary>
    public byte[] ToBytes()
    {
        var utf8 = Encoding.UTF8;
        int size = Magic.Length + 2 + 2 + 4;
        foreach (var pair in idsByName) size += 4 + utf8.GetByteCount(pair.Key);

        var buffer = new byte[size];
        Array.Copy(Magic, buffer, Magic.Length);
        int pos = Magic.Length;
        LittleEndian.WriteUInt16(buffer, pos, CurrentVersion); pos += 2;
        LittleEndian.WriteUInt16(buffer, pos, (ushort)idsByName.Count); pos += 2;
        foreach (var pair in idsByName)
        {
            int length = utf8.GetBytes(pair.Key, 0, pair.Key.Length, buffer, pos + 4);
            if (length > ushort.MaxValue) throw new InvalidOperationException($"Payload type name '{pair.Key}' is too long.");
            LittleEndian.WriteUInt16(buffer, pos, pair.Value);
            LittleEndian.WriteUInt16(buffer, pos + 2, (ushort)length);
            pos += 4 + length;
        }
        LittleEndian.WriteUInt32(buffer, pos, Crc32.Compute(buffer, 0, pos));
        IsDirty = false;
        return buffer;
    }

    /// <summary>Parses a serialized map.</summary>
    /// <exception cref="InvalidDataException">The data is corrupt, truncated or of an unsupported version.</exception>
    public static LogPayloadTypeMap Parse(byte[] data)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        int headerSize = Magic.Length + 4;
        if (data.Length < headerSize + 4) throw new InvalidDataException("Type map is truncated.");
        for (int i = 0; i < Magic.Length; i++)
        {
            if (data[i] != Magic[i]) throw new InvalidDataException("Not a type map.");
        }
        int crcPos = data.Length - 4;
        if (Crc32.Compute(data, 0, crcPos) != LittleEndian.ReadUInt32(data, crcPos)) throw new InvalidDataException("Type map checksum mismatch.");
        int pos = Magic.Length;
        ushort version = LittleEndian.ReadUInt16(data, pos); pos += 2;
        if (version != CurrentVersion) throw new InvalidDataException($"Unsupported type map version {version}.");
        int count = LittleEndian.ReadUInt16(data, pos); pos += 2;

        var map = new LogPayloadTypeMap();
        for (int i = 0; i < count; i++)
        {
            if (pos + 4 > crcPos) throw new InvalidDataException("Type map is truncated.");
            ushort id = LittleEndian.ReadUInt16(data, pos);
            int length = LittleEndian.ReadUInt16(data, pos + 2);
            pos += 4;
            if (pos + length > crcPos) throw new InvalidDataException("Type map is truncated.");
            string name = Encoding.UTF8.GetString(data, pos, length);
            pos += length;
            if (map.idsByName.ContainsKey(name) || map.namesById.ContainsKey(id)) throw new InvalidDataException("Type map contains duplicate entries.");
            map.Add(name, id);
        }
        if (pos != crcPos) throw new InvalidDataException("Type map size mismatch.");
        return map;
    }

    /// <summary>Loads the map from <paramref name="directory"/>; returns an empty map if the file does not exist.</summary>
    public static LogPayloadTypeMap Load(string directory)
    {
        string path = Path.Combine(directory, FileName);
        return File.Exists(path) ? Parse(File.ReadAllBytes(path)) : new LogPayloadTypeMap();
    }

    /// <summary>
    /// Writes the map to <paramref name="directory"/> via a temp file and replace, so a crash never leaves a torn map.
    /// Must happen before the first entry with a newly assigned id is written.
    /// </summary>
    public void Write(string directory)
    {
        string path = Path.Combine(directory, FileName);
        string tempPath = path + ".tmp";
        File.WriteAllBytes(tempPath, ToBytes());
        if (File.Exists(path)) File.Replace(tempPath, path, null);
        else File.Move(tempPath, path);
    }
}
