using FeatureLoom.DependencyInversion;
using FeatureLoom.Extensions;
using FeatureLoom.Helpers;
using FeatureLoom.MessageFlow;
using FeatureLoom.Scheduling;
using FeatureLoom.Synchronization;
using FeatureLoom.Time;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FeatureLoom.MessageFlow;

public class Batcher<T> : IMessageSink<T>, IMessageSource, IMessageFlowConnection
{
    SourceValueHelper sourceHelper;
    List<T> buffer = new List<T>();
    MicroLock bufferLock = new MicroLock();
    int maxBatchSize;
    TimeSpan maxCollectionTime;
    TimeSpan tolerance;
    ActionSchedule timerSchedule = null;
    TimeFrame timer = TimeFrame.Invalid;
    int unusedTimerCycles = 0;
    int maxUnusedTimerCycles = 10;
    T[] singleMessageTempArray = new T[1];
    bool sendSingleMessagesAsArray;


    public Batcher(int maxBatchSize, TimeSpan maxCollectionTime, TimeSpan tolerance, bool sendSingleMessagesAsArray = true, int maxUnusedTimerCycles = 10)
    {
        this.maxBatchSize = maxBatchSize;
        this.maxCollectionTime = maxCollectionTime;
        this.tolerance = tolerance;
        this.maxUnusedTimerCycles = maxUnusedTimerCycles;
        this.sendSingleMessagesAsArray = sendSingleMessagesAsArray;
    }

    public Type ConsumedMessageType => typeof(T);

    public int CountConnectedSinks => sourceHelper.CountConnectedSinks;

    private void StartTimer()
    {
        if (maxCollectionTime <= TimeSpan.Zero) return;

        unusedTimerCycles = 0;
        timer = new TimeFrame(maxCollectionTime);

        if (timerSchedule == null)
        {
            timerSchedule = Service<SchedulerService>.Instance.ScheduleAction($"Batcher<{TypeNameHelper.GetSimplifiedTypeName(typeof(T))}>", now =>
            {
                if (timer.IsValid && timer.Elapsed(now))
                {
                    T[] batch = null;
                    using (bufferLock.Lock())
                    {
                        if (buffer.Count > 0 && 
                            timer.IsValid && 
                            timer.Elapsed(now))
                        {
                            timer = TimeFrame.Invalid;
                            if (buffer.Count == 1 && !sendSingleMessagesAsArray)
                            {
                                singleMessageTempArray[0] = buffer[0];
                                batch = singleMessageTempArray;
                            }
                            else
                            {
                                batch = buffer.ToArray();
                            }
                            buffer.Clear();
                        }
                    }
                    if (!batch.EmptyOrNull())
                    {
                        if (batch.Length == 1 && !sendSingleMessagesAsArray)
                        {
                            sourceHelper.Forward(singleMessageTempArray[0]);
                            singleMessageTempArray[0] = default;
                        }
                        else
                        {
                            sourceHelper.Forward(batch);
                        }
                    }
                }

                if (timer.IsValid)
                {
                    unusedTimerCycles = 0;
                    return new ScheduleStatus(timer.utcEndTime, timer.utcEndTime + tolerance);
                }
                else if (unusedTimerCycles++ < maxUnusedTimerCycles)
                {
                    return new ScheduleStatus(maxCollectionTime, maxCollectionTime + tolerance);
                }
                else
                {
                    using (bufferLock.Lock())
                    {
                        if (timer.IsInvalid)
                        {
                            timerSchedule = null;
                            return ScheduleStatus.Terminated;
                        }
                        else
                        {
                            return new ScheduleStatus(timer.utcEndTime, timer.utcEndTime + tolerance);
                        }
                    }
                }
            });
        }
    }

    private void StopTimer()
    {
        timer = TimeFrame.Invalid;
    }

    private void AddToBuffer(T item)
    {
        T[] batch = CreateBatchWhenReady(item);
        if (!batch.EmptyOrNull())
        {
            sourceHelper.Forward(batch);
        }
    }

    private Task AddToBufferAsync(T item)
    {
        T[] batch = CreateBatchWhenReady(item);
        if (!batch.EmptyOrNull())
        {
            return sourceHelper.ForwardAsync(batch);
        }
        
        return Task.CompletedTask;
    }

    private T[] CreateBatchWhenReady(T item)
    {
        T[] batch = null;
        using (bufferLock.Lock())
        {
            if (buffer.Count == 0) StartTimer();

            buffer.Add(item);

            if (buffer.Count == maxBatchSize)
            {
                batch = buffer.ToArray();
                buffer.Clear();
            }
        }

        return batch;
    }

    public void Post<M>(M message)
    {
        if (message is T typedMessage) AddToBuffer(typedMessage);
    }
    
    public void Post<M>(in M message)
    {
        if (message is T typedMessage) AddToBuffer(typedMessage);
    }

    public Task PostAsync<M>(M message)
    {
        if (message is T typedMessage) return AddToBufferAsync(typedMessage);
        return Task.CompletedTask;
    }

    public void ConnectTo(IMessageSink sink, bool weakReference = false)
    {
        sourceHelper.ConnectTo(sink, weakReference);
    }

    public IMessageSource ConnectTo(IMessageFlowConnection sink, bool weakReference = false)
    {
        return sourceHelper.ConnectTo(sink, weakReference);
    }

    public void DisconnectFrom(IMessageSink sink)
    {
        sourceHelper.DisconnectFrom(sink);
    }

    public void DisconnectAll()
    {
        sourceHelper.DisconnectAll();
    }

    public IMessageSink[] GetConnectedSinks()
    {
        return sourceHelper.GetConnectedSinks();
    }
}
