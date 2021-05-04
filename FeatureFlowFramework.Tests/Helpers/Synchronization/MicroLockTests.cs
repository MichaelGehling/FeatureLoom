using FeatureFlowFramework.Helpers.Synchronization;
using FeatureFlowFramework.Helpers.Time;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace FeatureFlowFramework.Helpers.Synchronization
{
    public class MicroLockTests
    {
        [Fact]
        public void LockAttemptBlocksWhileLockInUse()
        {
            var myLock = new MicroLock();
            bool secondLockEntered = false;
            ManualResetEventSlim waiter = new ManualResetEventSlim(false);
            Task task;
            using(myLock.Lock())
            {
                Assert.False(myLock.TryLock(out var writeLock));
                Assert.False(writeLock.IsActive);

                task = Task.Run(() =>
                {
                    waiter.Set();
                    using(myLock.Lock())
                    {
                        secondLockEntered = true;
                    }
                });
                waiter.Wait();
                Thread.Sleep(10);
                Assert.False(secondLockEntered);
            }
            task.WaitFor();
            Assert.True(secondLockEntered);
            Assert.True(myLock.TryLock(out _));
        }

        [Fact]
        public void ReadLockAttemptBlocksWhileLockInWriteUse()
        {
            var myLock = new MicroLock();
            bool secondLockEntered = false;
            ManualResetEventSlim waiter = new ManualResetEventSlim(false);
            Task task;
            using(myLock.Lock())
            {
                Assert.False(myLock.TryLockReadOnly(out _));

                task = Task.Run(() =>
                {
                    waiter.Set();
                    using(myLock.LockReadOnly())
                    {
                        secondLockEntered = true;
                    }
                });
                waiter.Wait();
                Thread.Sleep(10);
                Assert.False(secondLockEntered);
            }
            task.WaitFor();
            Assert.True(secondLockEntered);
            Assert.True(myLock.TryLockReadOnly(out _));
        }

        [Fact]
        public void ReadLockEntersWhileLockInReadUse()
        {
            var myLock = new MicroLock();
            bool secondLockEntered = false;
            ManualResetEventSlim waiter = new ManualResetEventSlim(false);
            Task task;
            using(myLock.LockReadOnly())
            {
                Assert.True(myLock.TryLockReadOnly(out var readLock));
                readLock.Exit();

                task = Task.Run(() =>
                {

                    using(myLock.LockReadOnly())
                    {
                        secondLockEntered = true;
                        waiter.Set();
                    }
                });
                waiter.Wait(1000);
                Assert.True(secondLockEntered);
            }
        }
        
        [Fact]
        public void PrioritizedAttemptSucceedsFirst()
        {
            var myLock = new MicroLock();
            int counter = 0;
            bool rightOrder = false;
            ManualResetEventSlim waiter = new ManualResetEventSlim(false);
            Thread thread1;
            Thread thread2;
            using(myLock.Lock())
            {
                thread1 = new Thread(() =>
                {
                    waiter.Set();
                    using(myLock.Lock())
                    {
                        rightOrder = 1 == counter++;
                    }
                });
                thread1.Start();
                waiter.Wait();
                thread2 = new Thread(() =>
                {
                    waiter.Set();
                    using(myLock.Lock(true))
                    {
                        rightOrder = 0 == counter++;
                    }
                });
                waiter.Reset();
                thread2.Start();
                waiter.Wait();
                Thread.Sleep(10);
            }
            Assert.True(thread1.Join(1.Seconds()));
            Assert.True(thread2.Join(1.Seconds()));
            Assert.True(rightOrder);
        }
        

        [Fact]
        public void ManyParallelLockAttemptsWillAllFinish()
        {
            var myLock = new MicroLock();
            List<Task> tasks = new List<Task>();
            TimeFrame executionTime = new TimeFrame(1.Seconds());

            AsyncManualResetEvent starter = new AsyncManualResetEvent();
            
            for(int i = 0; i < 10; i++)
            {
                tasks.Add(Task.Run(() =>
                {
                    starter.Wait();
                    while(!executionTime.Elapsed())
                    {
                        using(myLock.Lock())
                        {
                        }
                    }
                }));
            }            
            
            for(int i = 0; i < 10; i++)
            {
                tasks.Add(Task.Run(() =>
                {
                    starter.Wait();
                    while(!executionTime.Elapsed())
                    {
                        using(myLock.LockReadOnly())
                        {
                        }
                    }
                }));
            }
            
            starter.Set();
            bool allFinished = Task.WaitAll(tasks.ToArray(), executionTime.Remaining() + 100.Milliseconds());
            Assert.True(allFinished);
        }

        [Fact]
        public void ReadOnlyAccessIsAllowedSimultaneously()
        {
            var myLock = new MicroLock();

            using(myLock.LockReadOnly())
            {
                Assert.True(myLock.TryLockReadOnly(out var readLock));
                Assert.True(readLock.IsActive);
                readLock.Exit();
                Assert.False(readLock.IsActive);

                Assert.False(myLock.TryLock(out var writeLock));
                Assert.False(writeLock.IsActive);
            }
        }

    }
}
