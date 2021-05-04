using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FeatureLoom.Helpers.Misc;
using FeatureLoom.Helpers.Synchronization;
using FeatureLoom.Helpers.Time;

namespace Playground
{

    class MessageQueueLockTester<T>
    {
        T lockObject;
        Action<T, Action, int> readLockFrame;
        Action<T, Action, int> writeLockFrame;
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

        public MessageQueueLockTester(string name, T lockObject, int numReader, int numWriter, TimeSpan duration, TimeSpan readerSlackTime, TimeSpan writerSlackTime, TimeSpan executionTime, Action<T, Action, int> readLockFrame, Action<T, Action, int> writeLockFrame)
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
                
            ManualResetEventSlim starter = new ManualResetEventSlim(false);
            List<Task> tasks = new List<Task>();
            Box<TimeFrame> timeBox = new Box<TimeFrame>();
            int max = Math.Max(numWriter, numReader);
            for(int i= 0; i < max; i++)
            {
                if (i < numWriter)
                {
                    tasks.Add(Task.Run(() =>
                    {
                        starter.Wait();
                        TimeFrame timeFrame = timeBox;
                        while (!timeFrame.Elapsed())
                        {
                            if (queue.Count > 10000) Thread.Yield();
                            writeLockFrame(lockObject, WriteToQueue, queue.Count);
                            TimeFrame slackTime = new TimeFrame(writerSlack);
                            while (!slackTime.Elapsed()) Thread.Yield();
                        }
                    }));
                }

                if (i < numReader)
                {
                    tasks.Add(Task.Run(() =>
                    {
                        starter.Wait();
                        TimeFrame timeFrame = timeBox;
                        while (!timeFrame.Elapsed())
                        {
                            if (queue.Count == 0) Thread.Yield();
                            readLockFrame(lockObject, ReadFromQueue, queue.Count);
                            TimeFrame slackTime = new TimeFrame(readerSlack);
                            while (!slackTime.Elapsed()) Thread.Yield();
                        }
                    }));
                }
            }

            timeBox.value = new TimeFrame(duration);
            starter.Set();
            Task.WaitAll(tasks.ToArray());
            queue = null;
            return new Result(name, writeCounter, readCounter, timeBox.value.TimeSinceStart());
        }

        void WriteToQueue()
        {
            TimeFrame executionTimeFrame = new TimeFrame(executionTime);
            queue.Enqueue(writeCounter++);
            while (!executionTimeFrame.Elapsed()) ;
        }

        void ReadFromQueue()
        {
            TimeFrame executionTimeFrame = new TimeFrame(executionTime);
            if (queue.TryDequeue(out _)) readCounter++;
            while (!executionTimeFrame.Elapsed()) ;
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
                return $"{name}:\tWrittenToQueue:{writeCounter},\tReadFromQueue:{readCounter}\t-> {readCounter/duration.TotalSeconds} per second / {duration.TotalMilliseconds * 1_000_000 / readCounter} ns per msg";
            }
        }
    }
}
