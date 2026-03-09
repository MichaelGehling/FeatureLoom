using FeatureLoom.Collections;
using FeatureLoom.MessageFlow;
using FeatureLoom.Helpers;
using FeatureLoom.Synchronization;
using FeatureLoom.Time;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace FeatureLoom.MessageFlow;

/// <summary>
/// Provides message probing with optional filtering, conversion, buffering, and time-slice statistics for an <see cref="IMessageSink"/>.
/// </summary>
/// <typeparam name="T1">Input message type to probe.</typeparam>
/// <typeparam name="T2">Converted message type stored in the buffer.</typeparam>
public sealed class StatisticsMessageProbe<T1, T2> : IMessageSink
{
    private FeatureLock myLock = new FeatureLock();
    private readonly string name;
    private readonly Predicate<T1> filter;
    private readonly Func<T1, T2> convert;
    private readonly TimeSpan timeSliceSize;
    private TimeSliceCounter currentTimeSlice;
    private LazyValue<AsyncManualResetEvent> manualResetEvent;

    private long counter;
    private CircularLogBuffer<(DateTime timestamp, T2 message)> messageBuffer;
    private CircularLogBuffer<DateTime> timestampBuffer;
    private CircularLogBuffer<TimeSliceCounter> timeSliceCounterBuffer;

    /// <summary>
    /// Initializes a new instance of <see cref="StatisticsMessageProbe{T1, T2}"/> with optional filtering, conversion, buffering, and time-slice tracking.
    /// </summary>
    /// <param name="name">Name of the probe (not used internally but retained for identification).</param>
    /// <param name="filter">Optional predicate to decide whether a message is counted.</param>
    /// <param name="converter">Optional converter applied to accepted messages before buffering.</param>
    /// <param name="messageBufferSize">Size of the circular message or timestamp buffer; 0 disables buffering.</param>
    /// <param name="timeSliceSize">Duration of each time slice for counting.</param>
    /// <param name="maxTimeSlices">Maximum number of time-slice counters to keep; 0 disables time-slice buffering.</param>
    public StatisticsMessageProbe(string name, Predicate<T1> filter = null, Func<T1, T2> converter = null, int messageBufferSize = 0, TimeSpan timeSliceSize = default, int maxTimeSlices = 0)
    {
        this.name = name;
        this.filter = filter;
        this.convert = converter;
        this.timeSliceSize = timeSliceSize;
        if (convert != null && messageBufferSize > 0) messageBuffer = new CircularLogBuffer<(DateTime timestamp, T2 message)>(messageBufferSize, false);
        else if (messageBufferSize > 0) timestampBuffer = new CircularLogBuffer<DateTime>(messageBufferSize, false);
        if (maxTimeSlices > 0)
        {
            timeSliceCounterBuffer = new CircularLogBuffer<TimeSliceCounter>(maxTimeSlices, false);
            currentTimeSlice = new TimeSliceCounter(timeSliceSize);
        }
    }

    /// <summary>
    /// Gets the total number of accepted messages since creation.
    /// </summary>
    public long Counter => counter;

    /// <summary>
    /// Gets the currently active time-slice counter.
    /// </summary>
    public TimeSliceCounter CurrentTimeSlize => currentTimeSlice;

    /// <summary>
    /// Gets a wait handle that is signaled when new data is added to any buffer.
    /// </summary>
    public IAsyncWaitHandle WaitHandle => messageBuffer?.WaitHandle ?? timestampBuffer?.WaitHandle ?? timeSliceCounterBuffer?.WaitHandle ?? manualResetEvent.Obj;

    /// <summary>
    /// Returns buffered converted messages with timestamps starting from a buffer position.
    /// </summary>
    /// <param name="bufferPosition">Current read position; updated to the new position after retrieval.</param>
    /// <returns>All available buffered message entries starting from the given position.</returns>
    public (DateTime timestamp, T2 message)[] GetBufferedMessages(ref long bufferPosition)
    {
        if (messageBuffer != null)
        {
            using (myLock.LockReadOnly())
            {
                var result = messageBuffer.GetAllAvailable(bufferPosition, out _, out bufferPosition);
                return result;
            }
        }
        else
        {
            bufferPosition = 0;
            return Array.Empty<(DateTime timestamp, T2 message)>();
        }
    }

    /// <summary>
    /// Returns buffered timestamps (from timestamp or message buffers) starting from a buffer position.
    /// </summary>
    /// <param name="bufferPosition">Current read position; updated to the new position after retrieval.</param>
    /// <returns>All available timestamps starting from the given position.</returns>
    public DateTime[] GetBufferedTimestamps(ref long bufferPosition)
    {
        using (myLock.LockReadOnly())
        {
            if (timestampBuffer != null)
            {
                var result = timestampBuffer.GetAllAvailable(bufferPosition, out _, out bufferPosition);
                return result;
            }
            else if (messageBuffer != null)
            {
                var result = messageBuffer.GetAllAvailable(bufferPosition, out _, out bufferPosition).Select(set => set.timestamp).ToArray();
                return result;
            }
            else
            {
                bufferPosition = 0;
                return Array.Empty<DateTime>();
            }
        }
    }

    /// <summary>
    /// Returns buffered time-slice counters starting from a buffer position.
    /// </summary>
    /// <param name="bufferPosition">Current read position; updated to the new position after retrieval.</param>
    /// <returns>All available time-slice counters starting from the given position.</returns>
    public TimeSliceCounter[] GetBufferedTimeSlices(ref long bufferPosition)
    {
        if (timeSliceCounterBuffer != null)
        {
            using (myLock.LockReadOnly())
            {
                var result = timeSliceCounterBuffer.GetAllAvailable(bufferPosition, out _, out bufferPosition);
                return result;
            }
        }
        else
        {
            bufferPosition = 0;
            return Array.Empty<TimeSliceCounter>();
        }
    }

    /// <summary>
    /// Posts a message by reference, applying optional filter and converter, and updates buffers and counters.
    /// </summary>
    /// <typeparam name="M">Actual message type provided to the probe.</typeparam>
    /// <param name="message">Message to process.</param>
    public void Post<M>(in M message)
    {
        if (!(message is T1 msgT1)) return;
        if (!(filter?.Invoke(msgT1) ?? true)) return;
        T2 msgT2 = convert == null ? default : convert(msgT1);
        using (myLock.Lock())
        {
            counter++;
            messageBuffer?.Add((AppTime.Now, msgT2));
            timestampBuffer?.Add(AppTime.Now);
            if (timeSliceCounterBuffer != null)
            {
                if (currentTimeSlice.timeFrame.Elapsed())
                {
                    timeSliceCounterBuffer.Add(currentTimeSlice);
                    currentTimeSlice = new TimeSliceCounter(timeSliceSize);
                }
                currentTimeSlice.counter++;
            }
        }
        manualResetEvent.ObjIfExists?.Set();
        manualResetEvent.ObjIfExists?.Reset();
    }

    /// <summary>
    /// Posts a message, applying optional filter and converter, and updates buffers and counters.
    /// </summary>
    /// <typeparam name="M">Actual message type provided to the probe.</typeparam>
    /// <param name="message">Message to process.</param>
    public void Post<M>(M message)
    {
        if (!(message is T1 msgT1)) return;
        if (!(filter?.Invoke(msgT1) ?? true)) return;
        T2 msgT2 = convert == null ? default : convert(msgT1);
        using (myLock.Lock())
        {
            counter++;
            messageBuffer?.Add((AppTime.Now, msgT2));
            timestampBuffer?.Add(AppTime.Now);
            if (timeSliceCounterBuffer != null)
            {
                if (currentTimeSlice.timeFrame.Elapsed())
                {
                    timeSliceCounterBuffer.Add(currentTimeSlice);
                    currentTimeSlice = new TimeSliceCounter(timeSliceSize);
                }
                currentTimeSlice.counter++;
            }
        }
        manualResetEvent.ObjIfExists?.Set();
        manualResetEvent.ObjIfExists?.Reset();
    }

    /// <summary>
    /// Asynchronously posts a message and completes immediately.
    /// </summary>
    /// <typeparam name="M">Actual message type provided to the probe.</typeparam>
    /// <param name="message">Message to process.</param>
    /// <returns>A completed task.</returns>
    public Task PostAsync<M>(M message)
    {
        Post(message);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Represents a counter within a fixed time slice.
    /// </summary>
    public struct TimeSliceCounter
    {
        /// <summary>
        /// Time frame represented by the counter.
        /// </summary>
        public TimeFrame timeFrame;

        /// <summary>
        /// Number of messages observed in the time frame.
        /// </summary>
        public long counter;

        /// <summary>
        /// Initializes a new <see cref="TimeSliceCounter"/> for the given time slice duration.
        /// </summary>
        /// <param name="timeSliceSize">Duration of the time slice.</param>
        public TimeSliceCounter(TimeSpan timeSliceSize)
        {
            this.timeFrame = new TimeFrame(timeSliceSize);
            this.counter = 0;
        }
    }
}