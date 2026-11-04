using System;
using System.Globalization;

namespace FeatureLoom.LogStorage;

/// <summary>
/// Naming of segment (.log) and index (.idx) files: <c>segment.&lt;seq:D10&gt;.&lt;yyyyMMddHHmmss&gt;.(log|idx)</c>.
/// Sequence and start time in the name keep files of different times unique when archives are combined.
/// </summary>
public static class LogSegmentFiles
{
    /// <summary>Common prefix of segment and index files.</summary>
    public const string Prefix = "segment";

    /// <summary>Extension of segment files.</summary>
    public const string SegmentExtension = ".log";

    /// <summary>Extension of index files.</summary>
    public const string IndexExtension = ".idx";

    /// <summary>Search pattern matching all index files of a log folder.</summary>
    public const string IndexSearchPattern = Prefix + ".*" + IndexExtension;

    /// <summary>Returns the file name without extension for a segment.</summary>
    public static string GetBaseName(uint segmentSequence, DateTime startTime) =>
        Prefix + "." + segmentSequence.ToString("D10", CultureInfo.InvariantCulture) + "." +
        startTime.ToUniversalTime().ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);

    /// <summary>Search pattern matching the index file of one segment.</summary>
    public static string GetIndexSearchPattern(uint segmentSequence) =>
        Prefix + "." + segmentSequence.ToString("D10", CultureInfo.InvariantCulture) + ".*" + IndexExtension;

    /// <summary>Parses the segment creation time (UTC, second precision) from a segment or index file name.</summary>
    public static bool TryParseStartTime(string fileName, out DateTime startTime)
    {
        startTime = default;
        if (fileName == null) return false;
        var parts = fileName.Split('.');
        return parts.Length == 4 && parts[0] == Prefix &&
               DateTime.TryParseExact(parts[2], "yyyyMMddHHmmss", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out startTime);
    }

    /// <summary>Parses the segment sequence from a segment or index file name.</summary>
    public static bool TryParseSequence(string fileName, out uint segmentSequence)
    {
        segmentSequence = 0;
        if (fileName == null) return false;
        var parts = fileName.Split('.');
        return parts.Length == 4 && parts[0] == Prefix &&
               uint.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out segmentSequence);
    }
}
