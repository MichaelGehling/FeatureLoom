using FeatureLoom.Collections;
using FeatureLoom.MessageFlow;
using FeatureLoom.Helpers;
using FeatureLoom.Synchronization;
using FeatureLoom.Time;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace FeatureLoom.Diagnostics
{
    public class MessageFlowProbe<T1, T2> : IMessageSink
    {
        private FeatureLock myLock = new FeatureLock();
        private readonly string name;
        private readonly Predicate<T1> filter;
        private readonly Func<T1, T2> convert;
        private readonly TimeSpan timeSliceSize;
        private TimeSliceCounter currentTimeSlice;
        private LazyValue<AsyncManualResetEvent> manualResetEvent;

        private long counter;
        private CountingRingBuffer<(DateTime timestamp, T2 message)> messageBuffer;
        private CountingRingBuffer<DateTime> timestampBuffer;
        private CountingRingBuffer<TimeSliceCounter> timeSliceCounterBuffer;

        public MessageFlowProbe(string name, Predicate<T1> filter = null, Func<T1, T2> converter = null, int messageBufferSize = 0, TimeSpan timeSliceSize = default, int maxTimeSlices = 0)
        {
            this.name = name;
            this.filter = filter;
            this.convert = converter;
            this.timeSliceSize = timeSliceSize;
            if (convert != null && messageBufferSize > 0) messageBuffer = new CountingRingBuffer<(DateTime timestamp, T2 message)>(messageBufferSize, false);
            else if (messageBufferSize > 0) timestampBuffer = new CountingRingBuffer<DateTime>(messageBufferSize, false);
            if (maxTimeSlices > 0)
            {
                timeSliceCounterBuffer = new CountingRingBuffer<TimeSliceCounter>(maxTimeSlices, false);
                currentTimeSlice = new TimeSliceCounter(timeSliceSize);
            }
        }

        public long Counter => counter;
        public TimeSliceCounter CurrentTimeSlize => currentTimeSlice;
        public IAsyncWaitHandle WaitHandle => messageBuffer?.WaitHandle ?? timestampBuffer?.WaitHandle ?? timeSliceCounterBuffer?.WaitHandle ?? manualResetEvent.Obj.AsyncWaitHandle;

        public (DateTime timestamp, T2 message)[] GetBufferedMessages(ref long bufferPosition)
        {
            if (messageBuffer != null)
            {
                using (myLock.LockReadOnly())
                {
                    var result = messageBuffer.GetAvailableSince(bufferPosition, out _);
                    bufferPosition = messageBuffer.Counter;
                    return result;
                }
            }
            else
            {
                bufferPosition = 0;
                return Array.Empty<(DateTime timestamp, T2 message)>();
            }
        }

        public DateTime[] GetBufferedTimestamps(ref long bufferPosition)
        {
            using (myLock.LockReadOnly())
            {
                if (timestampBuffer != null)
                {
                    var result = timestampBuffer.GetAvailableSince(bufferPosition, out _);
                    bufferPosition = timestampBuffer.Counter;
                    return result;
                }
                else if (messageBuffer != null)
                {
                    var result = messageBuffer.GetAvailableSince(bufferPosition, out _).Select(set => set.timestamp).ToArray();
                    bufferPosition = messageBuffer.Counter;
                    return result;
                }
                else
                {
                    bufferPosition = 0;
                    return Array.Empty<DateTime>();
                }
            }
        }

        public TimeSliceCounter[] GetBufferedTimeSlices(ref long bufferPosition)
        {
            if (timeSliceCounterBuffer != null)
            {
                using (myLock.LockReadOnly())
                {
                    var result = timeSliceCounterBuffer.GetAvailableSince(bufferPosition, out _);
                    bufferPosition = timeSliceCounterBuffer.Counter;
                    return result;
                }
            }
            else
            {
                bufferPosition = 0;
                return Array.Empty<TimeSliceCounter>();
            }
        }

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

        public Task PostAsync<M>(M message)
        {
            Post(message);
            return Task.CompletedTask;
        }

        public struct TimeSliceCounter
        {
            public TimeFrame timeFrame;
            public long counter;

            public TimeSliceCounter(TimeSpan timeSliceSize)
            {
                this.timeFrame = new TimeFrame(timeSliceSize);
                this.counter = 0;
            }
        }
    }
}