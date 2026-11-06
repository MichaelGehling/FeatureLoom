using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FeatureLoom.Synchronization;

namespace FeatureLoom.LogStorage;

/// <summary>Merges multiple logs by timestamp at query time.</summary>
/// <remarks>
/// Each log is queried with the same <see cref="LogQuery"/> (including <see cref="LogQuery.Limit"/>, which also bounds the merged result).
/// Field filters address field indices, so they should only be used if all readers share the same field layout.
/// Entries with equal timestamps keep the order of the readers.
/// </remarks>
public sealed class MultiLogReader
{
	private readonly ILogReader[] readers;

	/// <summary>Creates a merging reader over <paramref name="readers"/>.</summary>
	public MultiLogReader(IEnumerable<ILogReader> readers)
	{
		if (readers == null) throw new ArgumentNullException(nameof(readers));
		this.readers = new List<ILogReader>(readers).ToArray();
		foreach (var reader in this.readers)
		{
			if (reader == null) throw new ArgumentException("Readers must not be null.", nameof(readers));
		}
	}

	/// <summary>Queries all logs for entries of payload type <typeparamref name="T"/> and merges results by timestamp.</summary>
	public async Task<IReadOnlyList<LogEntry<T>>> QueryAsync<T>(LogQuery query, CancellationToken cancellationToken = default)
	{
#if NETSTANDARD2_1_OR_GREATER || NET
		// Streaming merge: with a limit, each log is only read as far as needed instead of returning up to Limit entries each.
		var merged = new List<LogEntry<T>>();
		await foreach (var entry in QueryStreamAsync<T>(query, cancellationToken).ConfigureAwait(false)) merged.Add(entry);
		return merged;
#else
		var results = new IReadOnlyList<LogEntry<T>>[readers.Length];
		for (int i = 0; i < readers.Length; i++)
		{
			results[i] = await readers[i].QueryAsync<T>(query, cancellationToken).ConfiguredAwait();
		}
		return Merge(results, query?.Reverse ?? false, query?.Limit);
#endif
	}

#if NETSTANDARD2_1_OR_GREATER || NET
	/// <summary>Streams entries of payload type <typeparamref name="T"/> from all logs, merged by timestamp.</summary>
	/// <remarks>Each log is enumerated lazily; only one pending entry per log is held at a time.</remarks>
	public async IAsyncEnumerable<LogEntry<T>> QueryStreamAsync<T>(LogQuery query, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		bool reverse = query?.Reverse ?? false;
		long remaining = query?.Limit ?? long.MaxValue;
		if (remaining <= 0) yield break;

		var enumerators = new IAsyncEnumerator<LogEntry<T>>[readers.Length];
		var hasCurrent = new bool[readers.Length];
		try
		{
			for (int i = 0; i < readers.Length; i++)
			{
				enumerators[i] = readers[i].QueryStreamAsync<T>(query, cancellationToken).GetAsyncEnumerator(cancellationToken);
				hasCurrent[i] = await enumerators[i].MoveNextAsync().ConfiguredAwait();
			}

			while (true)
			{
				// Linear selection: the number of logs is small, so a heap would not pay off.
				int best = -1;
				for (int i = 0; i < enumerators.Length; i++)
				{
					if (!hasCurrent[i]) continue;
					if (best < 0) { best = i; continue; }
					var candidate = enumerators[i].Current.Info.Timestamp;
					var current = enumerators[best].Current.Info.Timestamp;
					if (reverse ? candidate > current : candidate < current) best = i;
				}
				if (best < 0) yield break;

				yield return enumerators[best].Current;
				if (--remaining == 0) yield break;
				hasCurrent[best] = await enumerators[best].MoveNextAsync().ConfiguredAwait();
			}
		}
		finally
		{
			foreach (var enumerator in enumerators)
			{
				if (enumerator != null) await enumerator.DisposeAsync().ConfiguredAwait();
			}
		}
	}
#else
	// Per-log results are already ordered by time (index timestamps are monotonic), so a k-way merge suffices.
	private static List<LogEntry<T>> Merge<T>(IReadOnlyList<LogEntry<T>>[] results, bool reverse, int? limit)
	{
		int total = 0;
		foreach (var r in results) total += r.Count;
		if (limit.HasValue && limit.Value < total) total = Math.Max(limit.Value, 0);

		var merged = new List<LogEntry<T>>(total);
		var positions = new int[results.Length];
		while (merged.Count < total)
		{
			int best = -1;
			for (int i = 0; i < results.Length; i++)
			{
				if (positions[i] >= results[i].Count) continue;
				if (best < 0) { best = i; continue; }
				var candidate = results[i][positions[i]].Info.Timestamp;
				var current = results[best][positions[best]].Info.Timestamp;
				if (reverse ? candidate > current : candidate < current) best = i;
			}
			if (best < 0) break;
			merged.Add(results[best][positions[best]++]);
		}
		return merged;
	}
#endif
}
