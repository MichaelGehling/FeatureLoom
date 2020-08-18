using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FeatureFlowFramework.Helpers.Misc;
using FeatureFlowFramework.Helpers.Synchronization;
using FeatureFlowFramework.Helpers.Time;

namespace Playground
{
    class MessageQueueAsyncLockTester<T>
    {
        T lockObject;
        Func<T, Action, Task> readLockFrame;
        Func<T, Action, Task> writeLockFrame;
        int numReader;
        int numWriter;
        TimeSpan duration;
        TimeSpan executionTime;
        TimeSpan readerSlack;
        TimeSpan writerSlack;
        string name;

        Queue<long> queue;
        long writeCounter = 0;
        long readCounter = 0;

        public MessageQueueAsyncLockTester(string name, T lockObject, int numReader, int numWriter, TimeSpan duration, TimeSpan readerSlackTime, TimeSpan writerSlackTime, TimeSpan executionTime, Func<T, Action, Task> readLockFrame, Func<T, Action, Task> writeLockFrame)
        {
            this.name = name;
            this.lockObject = lockObject;
            this.readLockFrame = readLockFrame;
            this.writeLockFrame = writeLockFrame;
            this.numReader = numReader;
            this.numWriter = numWriter;
            this.duration = duration;
            this.readerSlack = readerSlackTime;
            this.writerSlack = writerSlackTime;
            this.executionTime = executionTime;
        }

        public Result Run()
        {
            queue = new Queue<long>();
            writeCounter = 0;
            readCounter = 0;

            AsyncManualResetEvent starter = new AsyncManualResetEvent(false);
            List<Task> tasks = new List<Task>();
            Box<TimeFrame> timeBox = new Box<TimeFrame>();
            for(int i = 0; i < numWriter; i++)
            {
                tasks.Add(new Func<Task>(async () =>
                {
                    await starter.WaitAsync();
                    TimeFrame timeFrame = timeBox;
                    await Task.Yield();
                    while(!timeFrame.Elapsed)
                    {
                        await writeLockFrame(lockObject, WriteToQueue);
                        await Task.Yield();
                        TimeFrame slackTime = new TimeFrame(writerSlack);
                        while(!slackTime.Elapsed) await Task.Yield();
                    }
                }).Invoke());
            }

            for(int i = 0; i < numReader; i++)
            {
                tasks.Add(new Func<Task>(async () =>
                {
                    await starter.WaitAsync();
                    TimeFrame timeFrame = timeBox;
                    await Task.Yield();
                    while(!timeFrame.Elapsed)
                    {
                        await readLockFrame(lockObject, ReadFromQueue);
                        await Task.Yield();
                        TimeFrame slackTime = new TimeFrame(readerSlack);
                        while(!slackTime.Elapsed) await Task.Yield();
                    }
                }).Invoke());
            }

            timeBox.value = new TimeFrame(duration);
            starter.Set();
            Task.WaitAll(tasks.ToArray());
            return new Result(name, writeCounter, readCounter, duration);
        }

        void WriteToQueue()
        {
            TimeFrame executionTimeFrame = new TimeFrame(executionTime);
            queue.Enqueue(writeCounter++);
            while(!executionTimeFrame.Elapsed) ;
        }

        void ReadFromQueue()
        {
            TimeFrame executionTimeFrame = new TimeFrame(executionTime);
            if(queue.TryDequeue(out _)) readCounter++;
            while(!executionTimeFrame.Elapsed) ;
        }

        public readonly struct Result
        {
            readonly string name;
            readonly long writeCounter;
            readonly long readCounter;
            readonly TimeSpan duration;

            public Result(string name, long writeCounter, long readCounter, TimeSpan duration)
            {
                this.name = name;
                this.writeCounter = writeCounter;
                this.readCounter = readCounter;
                this.duration = duration;
            }

            public override string ToString()
            {
                return $"{name}:\tWrittenToQueue:{writeCounter},\tReadFromQueue:{readCounter}\t-> {readCounter / duration.TotalSeconds} per second / {duration.TotalMilliseconds * 1_000_000 / readCounter} ns per msg";
            }
        }
    }
}
