using FeatureLoom.Helpers;
using FeatureLoom.Synchronization;
using FeatureLoom.Time;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Playground
{
    internal class MessageQueueAsyncLockTester<T>
    {
        private T lockObject;
        private Func<T, Action, int, Task> readLockFrame;
        private Func<T, Action, int, Task> writeLockFrame;
        private int numReader;
        private int numWriter;
        private TimeSpan duration;
        private TimeSpan executionTime;
        private TimeSpan readerSlack;
        private TimeSpan writerSlack;
        private string name;

        private Queue<long> queue;
        private long writeCounter = 0;
        private long readCounter = 0;

        public MessageQueueAsyncLockTester(string name, T lockObject, int numReader, int numWriter, TimeSpan duration, TimeSpan readerSlackTime, TimeSpan writerSlackTime, TimeSpan executionTime, Func<T, Action, int, Task> readLockFrame, Func<T, Action, int, Task> writeLockFrame)
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
            GC.Collect();
            GC.WaitForPendingFinalizers();
            queue = new Queue<long>(10_000_000);
            writeCounter = 0;
            readCounter = 0;

            AsyncManualResetEvent starter = new AsyncManualResetEvent(false);
            List<Task> tasks = new List<Task>();
            Box<TimeFrame> timeBox = new Box<TimeFrame>();
            for (int i = 0; i < numWriter; i++)
            {
                tasks.Add(new Func<Task>(async () =>
                {
                    await Task.Yield();
                    await starter.WaitAsync();
                    TimeFrame timeFrame = timeBox;
                    while (!timeFrame.Elapsed())
                    {
                        if (queue.Count > 10000) await Task.Yield();
                        await writeLockFrame(lockObject, WriteToQueue, queue.Count);
                        TimeFrame slackTime = new TimeFrame(writerSlack);
                        while (!slackTime.Elapsed()) Thread.Yield();
                    }
                }).Invoke());
            }

            for (int i = 0; i < numReader; i++)
            {
                tasks.Add(new Func<Task>(async () =>
                {
                    await Task.Yield();
                    await starter.WaitAsync();
                    TimeFrame timeFrame = timeBox;
                    while (!timeFrame.Elapsed())
                    {
                        if (queue.Count == 0) await Task.Yield();
                        await readLockFrame(lockObject, ReadFromQueue, queue.Count);
                        TimeFrame slackTime = new TimeFrame(readerSlack);
                        while (!slackTime.Elapsed()) Thread.Yield();
                    }
                }).Invoke());
            }

            timeBox.value = new TimeFrame(duration);
            starter.Set();
            Task.WaitAll(tasks.ToArray());
            queue = null;
            return new Result(name, writeCounter, readCounter, duration);
        }

        private void WriteToQueue()
        {
            TimeFrame executionTimeFrame = new TimeFrame(executionTime);
            queue.Enqueue(writeCounter++);
            while (!executionTimeFrame.Elapsed()) ;
        }

        private void ReadFromQueue()
        {
            TimeFrame executionTimeFrame = new TimeFrame(executionTime);
            if (queue.TryDequeue(out _)) readCounter++;
            while (!executionTimeFrame.Elapsed()) ;
        }

        public readonly struct Result
        {
            private readonly string name;
            private readonly long writeCounter;
            private readonly long readCounter;
            private readonly TimeSpan duration;

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