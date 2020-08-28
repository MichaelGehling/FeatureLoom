using FeatureFlowFramework.Helpers.Synchronization;
using FeatureFlowFramework.Helpers.Time;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace FeatureFlowFramework.Tests.Helper.Synchronization
{
    [CollectionDefinition("SerializedCollection", DisableParallelization = true)]
    public class SerializedCollection { }

    [Collection("SerializedCollection")]
    public class FeatureLockTests
    {
        [Fact]
        public void LockAttemptBlocksWhileLockInUse()
        {
            var myLock = new FeatureLock();
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
            task.Wait();
            Assert.True(secondLockEntered);
            Assert.True(myLock.TryLock(out _));
        }

        [Fact]
        public async void LockAttemptBlocksWhileLockInUseAsync()
        {
            var myLock = new FeatureLock();
            bool secondLockEntered = false;
            ManualResetEventSlim waiter = new ManualResetEventSlim(false);
            Task task;
            using(await myLock.LockAsync())
            {
                task = Task.Run(async () =>
                 {
                     waiter.Set();
                     using(await myLock.LockAsync())
                     {
                         secondLockEntered = true;
                     }
                 });
                waiter.Wait();
                Thread.Sleep(10);
                Assert.False(secondLockEntered);
            }
            task.Wait();
            Assert.True(secondLockEntered);
        }

        [Fact]
        public void ReadLockAttemptBlocksWhileLockInWriteUse()
        {
            var myLock = new FeatureLock();
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
            task.Wait();
            Assert.True(secondLockEntered);
            Assert.True(myLock.TryLockReadOnly(out _));
        }

        [Fact]
        public async void ReadLockAttemptBlocksWhileLockInWriteUseAsync()
        {
            var myLock = new FeatureLock();
            bool secondLockEntered = false;
            ManualResetEventSlim waiter = new ManualResetEventSlim(false);
            Task task;
            using(await myLock.LockAsync())
            {
                task = Task.Run(async () =>
                {
                    waiter.Set();
                    using(await myLock.LockReadOnlyAsync())
                    {
                        secondLockEntered = true;
                    }
                });
                waiter.Wait();
                Thread.Sleep(10);
                Assert.False(secondLockEntered);
            }
            task.Wait();
            Assert.True(secondLockEntered);
        }

        [Fact]
        public void ReadLockEntersWhileLockInReadUse()
        {
            var myLock = new FeatureLock();
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
        public async void ReadLockEntersWhileLockInReadUseAsync()
        {
            var myLock = new FeatureLock();
            bool secondLockEntered = false;
            ManualResetEventSlim waiter = new ManualResetEventSlim(false);
            Task task;
            using(await myLock.LockReadOnlyAsync())
            {
                task = Task.Run(async () =>
                {
                    waiter.Set();
                    using(await myLock.LockReadOnlyAsync())
                    {
                        secondLockEntered = true;
                    }
                });
                waiter.Wait();
                Thread.Sleep(10);
                Assert.True(secondLockEntered);
            }
        }

        [Fact]
        public void PriotizedAttemptSucceedsFirst()
        {
            var myLock = new FeatureLock();
            int counter = 0;
            ManualResetEventSlim waiter = new ManualResetEventSlim(false);
            Task task1;
            Task task2;
            bool task1Started = false;
            bool task2Started = false;
            using(myLock.Lock())
            {
                task1 = Task.Run(() =>
                {
                    task1Started = true;
                    waiter.Set();
                    using(myLock.Lock())
                    {
                        Assert.Equal(1, counter++);
                    }
                });
                waiter.Wait();
                task2 = Task.Run(() =>
                {
                    task2Started = true;
                    waiter.Set();
                    using(myLock.Lock(FeatureLock.MAX_PRIORITY))
                    {
                        Assert.Equal(0, counter++);
                    }
                });
                while(!task1Started || !task2Started) waiter.Wait();
                Thread.Sleep(10);
            }
            Task.WaitAll(task1, task2);
        }

        [Fact]
        public void FirstAttemptSucceedsFirst()
        {
            var myLock = new FeatureLock();
            int counter = 0;
            ManualResetEventSlim waiter = new ManualResetEventSlim(false);
            Task task1;
            Task task2;
            bool task1Started = false;
            bool task2Started = false;
            using(myLock.Lock())
            {
                task1 = Task.Run(() =>
                {
                    task1Started = true;
                    waiter.Set();
                    using(myLock.Lock())
                    {
                        Assert.Equal(0, counter++);
                    }
                });
                waiter.Wait();
                Thread.Sleep(10);
                task2 = Task.Run(() =>
                {
                    task2Started = true;
                    waiter.Set();
                    using(myLock.Lock())
                    {
                        Assert.Equal(1, counter++);
                    }
                });
                while(!task1Started || !task2Started) waiter.Wait();
                Thread.Sleep(10);
            }
            Task.WaitAll(task1, task2);
        }

        [Fact]
        public void FirstAttemptSucceedsFirstAsync()
        {
            var myLock = new FeatureLock();
            int counter = 0;
            ManualResetEventSlim waiter = new ManualResetEventSlim(false);
            Task task1;
            Task task2;
            bool task1Started = false;
            bool task2Started = false;
            using(myLock.Lock())
            {
                task1 = Task.Run(async () =>
                {
                    task1Started = true;
                    waiter.Set();
                    using(await myLock.LockAsync())
                    {
                        Assert.Equal(0, counter++);
                    }
                });
                waiter.Wait();
                Thread.Sleep(10);
                task2 = Task.Run(async () =>
                {
                    task2Started = true;
                    waiter.Set();
                    using(await myLock.LockAsync())
                    {
                        Assert.Equal(1, counter++);
                    }
                });
                while(!task1Started || !task2Started) waiter.Wait();
                Thread.Sleep(10);
            }
            Task.WaitAll(task1, task2);
        }

        [Fact]
        public void FirstAttemptSucceedsFirstMixed()
        {
            var myLock = new FeatureLock();
            int counter = 0;
            ManualResetEventSlim waiter = new ManualResetEventSlim(false);
            Task task1;
            Task task2;
            bool task1Started = false;
            bool task2Started = false;
            using(myLock.Lock())
            {
                task1 = Task.Run(async () =>
                {
                    task1Started = true;
                    waiter.Set();
                    using(await myLock.LockAsync())
                    {
                        Assert.Equal(0, counter++);
                    }
                });
                waiter.Wait();
                Thread.Sleep(10);
                task2 = Task.Run(() =>
                {
                    task2Started = true;
                    waiter.Set();
                    using(myLock.Lock())
                    {
                        Assert.Equal(1, counter++);
                    }
                });
                while(!task1Started || !task2Started) waiter.Wait();
                Thread.Sleep(10);
            }
            Task.WaitAll(task1, task2);
        }

        [Fact]
        public void ManyParallelLockAttemptsWillAllFinish()
        {
            var myLock = new FeatureLock();
            List<Task> tasks = new List<Task>();
            TimeFrame executionTime = new TimeFrame(2.Seconds());
            for(int i = 0; i < 2; i++)
            {
                tasks.Add(Task.Run(() =>
                {
                    while(!executionTime.Elapsed)
                    {
                        using(myLock.Lock())
                        {
                        }
                    }
                }));
            }
            for(int i = 0; i < 2; i++)
            {
                tasks.Add(Task.Run(async () =>
                {
                    while(!executionTime.Elapsed)
                    {
                        using(await myLock.LockAsync())
                        {
                        }
                    }
                }));
            }

            for(int i = 0; i < 2; i++)
            {
                tasks.Add(Task.Run(() =>
                {
                    while(!executionTime.Elapsed)
                    {
                        using(myLock.LockReadOnly())
                        {
                        }
                    }
                }));
            }


            for(int i = 0; i < 2; i++)
            {
                tasks.Add(Task.Run(async () =>
                {
                    while(!executionTime.Elapsed)
                    {
                        using(await myLock.LockReadOnlyAsync())
                        {
                        }
                    }
                }));
            }


            bool allFinished = Task.WaitAll(tasks.ToArray(), executionTime.Remaining + 100.Milliseconds());
            Assert.True(allFinished);
        }

        [Fact]
        public void ReadOnlyAccessIsAllowedSimultaneously()
        {
            FeatureLock myLock = new FeatureLock();

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

        [Fact]
        public void AllowsForWriteLockReentrance()
        {
            FeatureLock myLock = new FeatureLock(true);

            using(myLock.Lock())
            {
                Assert.True(myLock.TryLock(out var writeLock));
                Assert.True(writeLock.IsActive);
                writeLock.Exit();

                Assert.True(myLock.IsLocked);
            }
            Assert.False(myLock.IsLocked);
        }

        [Fact]
        public void AllowsForReadLockReentrance()
        {
            FeatureLock myLock = new FeatureLock(true);

            using(myLock.LockReadOnly())
            {
                Assert.True(myLock.TryLockReadOnly(out var readLock));
                Assert.Equal(1, myLock.CountParallelReadLocks);
                Assert.True(readLock.IsActive);
                readLock.Exit();
                Assert.Equal(1, myLock.CountParallelReadLocks);
                Assert.True(myLock.IsLocked);
            }
            Assert.False(myLock.IsLocked);
        }

        [Fact]
        public void AllowsForUpgradeSingleReadLockToWriteLockViaReentrance()
        {
            FeatureLock myLock = new FeatureLock(true);

            using(myLock.LockReadOnly())
            {
                Assert.Equal(1, myLock.CountParallelReadLocks);
                Assert.False(myLock.IsWriteLocked);

                Assert.True(myLock.TryLock(out var writeLock));
                Assert.Equal(0, myLock.CountParallelReadLocks);
                Assert.True(myLock.IsWriteLocked);
                Assert.True(writeLock.IsActive);

                writeLock.Exit();

                Assert.Equal(1, myLock.CountParallelReadLocks);
                Assert.False(myLock.IsWriteLocked);
                Assert.True(myLock.IsLocked);
            }
            Assert.False(myLock.IsLocked);
        }

        [Fact]
        public void MultipleReadLocksCannotBeUpgradedToWriteLockViaReentrance()
        {
            FeatureLock myLock = new FeatureLock(true);
            TimeFrame timer = new TimeFrame(1.Seconds());
            AsyncManualResetEvent signal = new AsyncManualResetEvent(false);
            Task task = Task.Run(() =>
            {
                using(myLock.LockReadOnly())
                {
                    while(myLock.CountParallelReadLocks < 2 && !timer.Elapsed) ;
                    Assert.False(timer.Elapsed);

                    signal.Wait(timer.Remaining);
                    Assert.False(timer.Elapsed);
                    Thread.Sleep(10);
                }
            });

            using(myLock.LockReadOnly())
            {
                while(myLock.CountParallelReadLocks < 2 && !timer.Elapsed) ;
                Assert.False(timer.Elapsed);
                Assert.Equal(2, myLock.CountParallelReadLocks);

                Assert.False(myLock.TryLock(out _));
                Assert.Equal(2, myLock.CountParallelReadLocks);

                signal.Set();
                // The other readlock will be exited in 10 ms. So long TryLock will wait and then perform the upgrade.
                Assert.True(myLock.TryLock(out var writeLock, timer.Remaining));
                Assert.False(timer.Elapsed);
                Assert.Equal(0, myLock.CountParallelReadLocks);
                Assert.True(myLock.IsWriteLocked);
                Assert.True(writeLock.IsActive);

                writeLock.Exit();

                Assert.Equal(1, myLock.CountParallelReadLocks);
                Assert.False(myLock.IsWriteLocked);
                Assert.True(myLock.IsLocked);
            }
            Assert.False(myLock.IsLocked);
        }

        [Fact]
        public async Task DeferredTasksMayNotUseReentrancy()
        {
            // Check first for sync call
            TimeFrame timer = new TimeFrame(1.Seconds());
            FeatureLock myLock = new FeatureLock(true);
            AsyncManualResetEvent signal1 = new AsyncManualResetEvent(false);
            bool exited = false;
            Task task;
            using(myLock.Lock())
            {
                task = myLock.RunDeferredTask(() =>
                {
                    Assert.False(myLock.TryLock(out _));
                    Assert.False(exited);

                    signal1.Set();
                    Assert.True(myLock.TryLock(out var deferredLock, timer.Remaining));
                    Assert.True(exited);
                    deferredLock.Exit();
                });

                signal1.Wait(timer.Remaining);
                Assert.False(timer.Elapsed);
                exited = true;
            }
            await task.WaitAsync(timer.Remaining);
            Assert.False(timer.Elapsed);

            // Check again for async call
            signal1.Reset();
            exited = false;
            task = null;
            using(myLock.Lock())
            {
                task = myLock.RunDeferredAsync(async () =>
                {
                    await Task.Yield(); // ensure not to run synchronously

                    Assert.False(myLock.TryLock(out _)); // TODO TryLockAsync needed
                    Assert.False(exited);
                    
                    signal1.Set();
                    Assert.True(myLock.TryLock(out var deferredLock, timer.Remaining));  // TODO TryLockAsync needed
                    Assert.True(exited);
                    deferredLock.Exit();
                });

                signal1.Wait(timer.Remaining);
                Assert.False(timer.Elapsed);
                exited = true;
            }
        }


    }
}
