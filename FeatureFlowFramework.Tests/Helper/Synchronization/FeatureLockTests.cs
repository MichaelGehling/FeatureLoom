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
        public async void CanTryLockAsync()
        {
            FeatureLock myLock = new FeatureLock();

            using(myLock.Lock())
            {
                Assert.False((await myLock.TryLockAsync(10.Milliseconds())).Succeeded(out var notAcquiredLock));
                Assert.False(notAcquiredLock.IsActive);
            }

            Assert.True((await myLock.TryLockAsync(10.Milliseconds())).Succeeded(out var acquiredLock));
            Assert.True(acquiredLock.IsActive);
            acquiredLock.Exit();
        }

        [Fact]
        public async void CanTryLockReentrantAsync()
        {
            FeatureLock myLock = new FeatureLock();

            if((await myLock.TryLockReentrantAsync(TimeSpan.Zero)).Succeeded(out var outerLock))
                using(outerLock)
                {
                    Assert.True(outerLock.IsActive);
                    Assert.True((await myLock.TryLockReentrantAsync(TimeSpan.Zero)).Succeeded(out var innerLock));
                    Assert.True(innerLock.IsActive);
                    Assert.True(myLock.IsWriteLocked);
                    innerLock.Exit();
                    Assert.True(myLock.IsWriteLocked);
                }
            Assert.False(myLock.IsLocked);
            Assert.False(myLock.HasValidReentrancyContext);
        }

        [Fact]
        public async void CanTryLockReadOnlyReentrantAsync()
        {
            FeatureLock myLock = new FeatureLock();

            if((await myLock.TryLockReentrantReadOnlyAsync(TimeSpan.Zero)).Succeeded(out var outerLock))
                using(outerLock)
                {
                    Assert.True(outerLock.IsActive);
                    Assert.True((await myLock.TryLockReentrantReadOnlyAsync(TimeSpan.Zero)).Succeeded(out var innerLock));
                    Assert.True(innerLock.IsActive);
                    Assert.True(myLock.IsReadOnlyLocked);
                    innerLock.Exit();
                    Assert.True(myLock.IsReadOnlyLocked);

                    Assert.True((await myLock.TryLockReentrantAsync(TimeSpan.Zero)).Succeeded(out var upgradedLock));
                    Assert.True(upgradedLock.IsActive);
                    Assert.True(myLock.IsWriteLocked);
                    upgradedLock.Exit();
                    Assert.False(myLock.IsWriteLocked);
                }
            Assert.False(myLock.IsLocked);
            Assert.False(myLock.HasValidReentrancyContext);
        }

        [Fact]
        public async void CanTryLockReadOnlyAsync()
        {
            FeatureLock myLock = new FeatureLock();

            using(myLock.Lock())
            {
                Assert.False((await myLock.TryLockReadOnlyAsync(10.Milliseconds())).Succeeded(out var notAcquiredLock));
                Assert.False(notAcquiredLock.IsActive);
            }

            Assert.True((await myLock.TryLockReadOnlyAsync(10.Milliseconds())).Succeeded(out var acquiredLock));
            Assert.True(acquiredLock.IsActive);
            acquiredLock.Exit();
        }

        
        [Fact]
        public void PriotizedAttemptSucceedsFirst()
        {
            var myLock = new FeatureLock();
            int counter = 0;
            ManualResetEventSlim waiter = new ManualResetEventSlim(false);
            Thread thread1;
            Thread thread2;
            bool task1Started = false;
            bool task2Started = false;
            using(myLock.Lock())
            {
                thread1 = new Thread(() =>
                {
                    task1Started = true;
                    waiter.Set();
                    using(myLock.Lock())
                    {
                        Assert.Equal(1, counter++);
                    }
                });
                thread1.Start();
                waiter.Wait();
                thread2 = new Thread(() =>
                {
                    task2Started = true;
                    waiter.Set();
                    using(myLock.LockPrioritized())
                    {
                        Assert.Equal(0, counter++);
                    }
                });
                thread2.Start();
                while(!task1Started || !task2Started) waiter.Wait();
                Thread.Sleep(10);
            }
            thread1.Join(1.Seconds());
            thread2.Join(1.Seconds());
        }
        

        [Fact]
        public void FirstAttemptSucceedsFirst()
        {
            var myLock = new FeatureLock();
            int counter = 0;
            ManualResetEventSlim waiter = new ManualResetEventSlim(false);
            Thread thread1;
            Thread thread2;
            bool task1Started = false;
            bool task2Started = false;
            using(myLock.Lock())
            {
                thread1 = new Thread(() =>
                {
                    task1Started = true;
                    waiter.Set();
                    using(myLock.Lock())
                    {
                        Assert.Equal(0, counter++);
                    }
                });
                thread1.Start();
                waiter.Wait();
                Thread.Sleep(10);
                thread2 = new Thread(() =>
                {
                    task2Started = true;
                    waiter.Set();
                    using(myLock.Lock())
                    {
                        Assert.Equal(1, counter++);
                    }
                });
                thread2.Start();
                while(!task1Started || !task2Started) waiter.Wait();
                Thread.Sleep(10);
            }
            thread1.Join(1.Seconds());
            thread2.Join(1.Seconds());
        }

        [Fact]
        public void FirstAttemptSucceedsFirstAsync()
        {
            var myLock = new FeatureLock();
            int counter = 0;
            ManualResetEventSlim waiter = new ManualResetEventSlim(false);
            Thread thread1;
            Thread thread2;
            bool task1Started = false;
            bool task2Started = false;
            using(myLock.Lock())
            {
                thread1 = new Thread(async () =>
                {
                    task1Started = true;
                    waiter.Set();
                    using(await myLock.LockAsync())
                    {
                        Assert.Equal(0, counter++);
                    }
                });
                thread1.Start();
                waiter.Wait();
                Thread.Sleep(10);
                thread2 = new Thread(async () =>
                {
                    task2Started = true;
                    waiter.Set();
                    using(await myLock.LockAsync())
                    {
                        Assert.Equal(1, counter++);
                    }
                });
                thread2.Start();
                while(!task1Started || !task2Started) waiter.Wait();
                Thread.Sleep(10);
            }
            thread1.Join(1.Seconds());
            thread2.Join(1.Seconds());
        }

        [Fact]
        public void FirstAttemptSucceedsFirstMixed()
        {
            var myLock = new FeatureLock();
            int counter = 0;
            ManualResetEventSlim waiter = new ManualResetEventSlim(false);
            Thread thread1;
            Thread thread2;
            bool task1Started = false;
            bool task2Started = false;
            using(myLock.Lock())
            {
                thread1 = new Thread(async () =>
                {
                    task1Started = true;
                    waiter.Set();
                    using(await myLock.LockAsync())
                    {
                        Assert.Equal(0, counter++);
                    }
                });
                thread1.Start();
                waiter.Wait();
                Thread.Sleep(10);
                thread2 = new Thread(() =>
                {
                    task2Started = true;
                    waiter.Set();
                    using(myLock.Lock())
                    {
                        Assert.Equal(1, counter++);
                    }
                });
                thread2.Start();
                while(!task1Started || !task2Started) waiter.Wait();
                Thread.Sleep(10);
            }
            thread1.Join(1.Seconds());
            thread2.Join(1.Seconds());
        }

        [Fact]
        public void ManyParallelLockAttemptsWillAllFinish()
        {
            var myLock = new FeatureLock();
            List<Task> tasks = new List<Task>();
            TimeFrame executionTime = new TimeFrame(1.Seconds());

            AsyncManualResetEvent starter = new AsyncManualResetEvent();
            
            for(int i = 0; i < 2; i++)
            {
                tasks.Add(Task.Run(() =>
                {
                    starter.Wait();
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
                    starter.Wait();
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
                    starter.Wait();
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
                    starter.Wait();
                    while(!executionTime.Elapsed)
                    {
                        using(await myLock.LockReadOnlyAsync())
                        {
                        }
                    }
                }));
            }
            
            starter.Set();
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
            FeatureLock myLock = new FeatureLock();

            using(myLock.LockReentrant())
            {
                Assert.True(myLock.TryLockReentrant(out var writeLock));
                Assert.True(writeLock.IsActive);
                writeLock.Exit();

                Assert.True(myLock.HasValidReentrancyContext);
                Assert.True(myLock.IsLocked);                
            }
            Assert.False(myLock.IsLocked);
            Assert.False(myLock.HasValidReentrancyContext);
        }

        [Fact]
        public void AllowsForReadLockReentrance()
        {
            FeatureLock myLock = new FeatureLock();

            using(myLock.LockReentrantReadOnly())
            {
                Assert.True(myLock.TryLockReentrantReadOnly(out var readLock));
                Assert.Equal(1, myLock.CountParallelReadLocks);
                Assert.True(readLock.IsActive);
                readLock.Exit();
                Assert.Equal(1, myLock.CountParallelReadLocks);

                Assert.True(myLock.HasValidReentrancyContext);
                Assert.True(myLock.IsLocked);                
            }
            Assert.False(myLock.IsLocked);
            Assert.False(myLock.HasValidReentrancyContext);
        }

        [Fact]
        public void AllowsForUpgradeSingleReadLockToWriteLockViaReentrance()
        {
            FeatureLock myLock = new FeatureLock();

            using(myLock.LockReentrantReadOnly())
            {
                Assert.Equal(1, myLock.CountParallelReadLocks);
                Assert.False(myLock.IsWriteLocked);

                Assert.True(myLock.TryLockReentrant(out var writeLock));
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
            FeatureLock myLock = new FeatureLock();
            TimeFrame timer = new TimeFrame(1.Seconds());
            AsyncManualResetEvent signal = new AsyncManualResetEvent(false);
            Task task = Task.Run(() =>
            {
                using(myLock.LockReentrantReadOnly())
                {
                    while(myLock.CountParallelReadLocks < 2 && !timer.Elapsed) ;
                    Assert.False(timer.Elapsed);

                    signal.Wait(timer.Remaining);
                    Assert.False(timer.Elapsed);
                    Thread.Sleep(10);
                }
            });

            using(myLock.LockReentrantReadOnly())
            {
                while(myLock.CountParallelReadLocks < 2 && !timer.Elapsed) ;
                Assert.False(timer.Elapsed);
                Assert.Equal(2, myLock.CountParallelReadLocks);

                Assert.False(myLock.TryLockReentrant(out _));
                Assert.Equal(2, myLock.CountParallelReadLocks);

                signal.Set();
                // The other readlock will be exited in 10 ms. So long TryLock will wait and then perform the upgrade.
                Assert.True(myLock.TryLockReentrant(timer.Remaining, out var writeLock));
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
            FeatureLock myLock = new FeatureLock();
            AsyncManualResetEvent signal1 = new AsyncManualResetEvent(false);
            bool exited = false;
            Task task;
            using(myLock.LockReentrant())
            {
                task = myLock.RunDeferredTask(() =>
                {
                    Assert.False(myLock.TryLockReentrant(out _));
                    Assert.False(exited);

                    signal1.Set();
                    Assert.True(myLock.TryLockReentrant(timer.Remaining, out var deferredLock));
                    Assert.True(exited);
                    deferredLock.Exit();
                });

                Assert.True(myLock.TryLockReentrant(out var innerLock));
                innerLock.Exit();

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
            using(myLock.LockReentrant())
            {
                task = myLock.RunDeferredAsync(async () =>
                {
                    await Task.Yield(); // ensure not to run synchronously

                    Assert.False(myLock.TryLockReentrant(out _)); // TODO TryLockAsync needed
                    Assert.False(exited);
                    
                    signal1.Set();
                    Assert.True(myLock.TryLockReentrant(timer.Remaining, out var deferredLock));  // TODO TryLockAsync needed
                    Assert.True(exited);
                    deferredLock.Exit();
                });

                Assert.True(myLock.TryLockReentrant(out var innerLock));
                innerLock.Exit();

                signal1.Wait(timer.Remaining);
                Assert.False(timer.Elapsed);
                exited = true;
            }
            await task.WaitAsync(timer.Remaining);
            Assert.False(timer.Elapsed);

            // Check again for removing context from within the task
            signal1.Reset();
            exited = false;
            task = null;
            using (myLock.LockReentrant())
            {
                task = Task.Run(() =>
                {
                    myLock.RemoveReentrancyContext();

                    Assert.False(myLock.TryLockReentrant(out _));
                    Assert.False(exited);

                    signal1.Set();
                    Assert.True(myLock.TryLockReentrant(timer.Remaining, out var deferredLock));
                    Assert.True(exited);
                    deferredLock.Exit();
                });

                Assert.True(myLock.TryLockReentrant(out var innerLock));
                innerLock.Exit();

                signal1.Wait(timer.Remaining);
                Assert.False(timer.Elapsed);
                exited = true;
            }
            await task.WaitAsync(timer.Remaining);
            Assert.False(timer.Elapsed);
        }

        [Fact]
        public void CanUpgradeAndDowngradeLocks()
        {
            FeatureLock myLock = new FeatureLock();

            using (var acquiredLock = myLock.LockReadOnly())
            {
                Assert.False(myLock.IsWriteLocked);
                Assert.True(acquiredLock.TryUpgradeToWriteMode());
                Assert.True(myLock.IsWriteLocked);
                Assert.True(acquiredLock.TryDowngradeToReadOnlyMode());
                Assert.False(myLock.IsWriteLocked);

                Assert.False(acquiredLock.TryDowngradeToReadOnlyMode());
            }

            using (var acquiredLock = myLock.Lock())
            {
                Assert.True(myLock.IsWriteLocked);
                Assert.True(acquiredLock.TryDowngradeToReadOnlyMode());
                Assert.False(myLock.IsWriteLocked);
                Assert.True(acquiredLock.TryUpgradeToWriteMode());
                Assert.True(myLock.IsWriteLocked);

                Assert.False(acquiredLock.TryUpgradeToWriteMode());
            }
            Assert.False(myLock.IsLocked);

            using (var outerLock = myLock.LockReentrantReadOnly())
            {
                Assert.False(myLock.IsWriteLocked);
                using (var innerLock = myLock.LockReentrant())
                {
                    Assert.True(myLock.IsWriteLocked);
                    Assert.False(outerLock.TryDowngradeToReadOnlyMode());
                    Assert.True(innerLock.TryDowngradeToReadOnlyMode());
                    Assert.False(myLock.IsWriteLocked);
                }
                Assert.False(myLock.IsWriteLocked);
            }
            Assert.False(myLock.IsLocked);
        }


    }
}
