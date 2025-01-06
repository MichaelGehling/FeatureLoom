using FeatureLoom.Synchronization;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace FeatureLoom.PerformanceTests.FeatureLockPerformance.QueueTest
{
    public class QueuePerformanceTest
    {
        public int numProducers = 1;
        public int numConsumers = 1;
        public int numOverallMessages = 1_000_000;

        public void Run(Action<Action, bool> producerLock, Action<Action, bool> consumerLock = null)
        {
            if (consumerLock == null) consumerLock = producerLock;
            Queue<int> queue = new Queue<int>();
            AsyncManualResetEvent starter = new AsyncManualResetEvent(false);
            bool producersDone = false;
            int messagesPerProducer = numOverallMessages / numProducers;
            List<Thread> producerThreads = new List<Thread>();
            List<Thread> consumerThreads = new List<Thread>();

            for (int i = 0; i < numConsumers; i++)
            {
                var thread = new Thread(() =>
                {
                    starter.Wait();
                    bool empty = false;
                    while (!producersDone || !empty)
                    {
                        empty = false;
                        consumerLock(() =>
                        {
                            if (!queue.TryDequeue(out _))
                            {
                                empty = true;
                            }
                        }, false);
                    }
                });
                thread.Start();
                consumerThreads.Add(thread);
            }

            for (int i = 0; i < numProducers; i++)
            {
                var thread = new Thread(() =>
                {
                    starter.Wait();
                    int count = 0;
                    while (count < messagesPerProducer)
                    {
                        producerLock(() =>
                        {
                            queue.Enqueue(count++);
                        }, false);
                    }
                });
                thread.Start();
                producerThreads.Add(thread);
            }

            starter.Set();
            foreach (var t in producerThreads) t.Join();
            //if (!Task.WhenAll(producerThreads.ToArray()).Wait(10000)) Console.Write("! TIMEOUT !");
            producersDone = true;
            foreach (var t in consumerThreads) t.Join();
            //if(!Task.WhenAll(consumerThreads.ToArray()).Wait(10000)) Console.Write("! TIMEOUT !");
        }

        public void AsyncRun(Func<Func<Task>, bool, Task> producerLock, Func<Func<Task>, bool, Task> consumerLock = null)
        {
            if (consumerLock == null) consumerLock = producerLock;
            Queue<int> queue = new Queue<int>();
            AsyncManualResetEvent starter = new AsyncManualResetEvent();
            bool producersDone = false;
            int messagesPerProducer = numOverallMessages / numProducers;
            List<Task> producerTasks = new List<Task>();
            List<Task> consumerTasks = new List<Task>();

            for (int i = 0; i < numConsumers; i++)
            {
                consumerTasks.Add(new Func<Task>(async () =>
                {
                    await starter.WaitAsync();
                    bool empty = false;
                    while (!empty || !producersDone)
                    {
                        empty = false;
                        await consumerLock(() =>
                        {
                            if (!queue.TryDequeue(out _))
                            {
                                empty = true;
                            }
                            return Task.CompletedTask;
                        }, false);
                    }
                }).Invoke());
            }

            for (int i = 0; i < numProducers; i++)
            {
                producerTasks.Add(new Func<Task>(async () =>
                {
                    await starter.WaitAsync();
                    int count = 0;
                    while (count < messagesPerProducer)
                    {
                        await producerLock(() =>
                        {
                            queue.Enqueue(count++);
                            return Task.CompletedTask;
                        }, false);
                    }
                }).Invoke());
            }

            starter.Set();
            if (!Task.WhenAll(producerTasks.ToArray()).Wait(10000)) Console.Write("! TIMEOUT !");
            producersDone = true;
            if (!Task.WhenAll(consumerTasks.ToArray()).Wait(10000)) Console.Write("! TIMEOUT !");
        }
    }
}