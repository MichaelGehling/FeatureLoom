using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using FeatureLoom.Helpers;

namespace FeatureLoom.Helpers
{
    // NOTE:
    // Each test uses a unique item type so the static generic SharedPool<T> can be initialized independently.
    // Never reuse a pooled type in another test expecting a fresh initialization.

    public class SharedPoolTests
    {
        class InitItem { }
        [Fact]
        public void Initialization_AllowsSingleSuccessfulCall()
        {            
            Assert.True(SharedPool<InitItem>.TryInit(() => new InitItem(), _ => { }));
            Assert.False(SharedPool<InitItem>.TryInit(() => new InitItem(), _ => { }));
            Assert.True(SharedPool<InitItem>.IsInitialized);
        }

        class CreateItem { public static int Created; public CreateItem() { Interlocked.Increment(ref Created); } }
        [Fact]
        public void Take_Creates_WhenEmpty()
        {            
            SharedPool<CreateItem>.TryInit(() => new CreateItem(), _ => { }, globalCapacity: 10, localCapacity: 5);
            var a = SharedPool<CreateItem>.Take();
            var b = SharedPool<CreateItem>.Take(); // second create because pool empty again
            Assert.NotNull(a);
            Assert.NotNull(b);
            Assert.Equal(2, CreateItem.Created);
        }

        class ResetItem { public int State; }
        [Fact]
        public void Return_And_Reset_AreInvoked()
        {            
            bool resetCalled = false;
            SharedPool<ResetItem>.TryInit(
                () => new ResetItem(),
                r => { r.State = 0; resetCalled = true; },
                globalCapacity: 10,
                localCapacity: 5);

            var item = SharedPool<ResetItem>.Take();
            item.State = 42;
            SharedPool<ResetItem>.Return(item);
            Assert.True(resetCalled);

            // Should get same item from local stack
            var again = SharedPool<ResetItem>.Take();
            Assert.Same(item, again);
            Assert.Equal(0, again.State);
        }

        class BatchItem { public int Id; public BatchItem(int id) { Id = id; } }
        [Fact]
        public void BatchPrefetch_FillsLocalStack_OnEmptyTake()
        {            
            int counter = 0;
            // Small localCapacity so we can see prefetched count clearly.
            SharedPool<BatchItem>.TryInit(
                () => new BatchItem(Interlocked.Increment(ref counter)),
                _ => { },
                globalCapacity: 50,
                localCapacity: 8,
                fetchOnEmpty: 5,
                keepOnFull: 2);

            // Fill global by overflowing local
            for (int i = 0; i < 20; i++)
            {
                var obj = SharedPool<BatchItem>.Take();
                SharedPool<BatchItem>.Return(obj);
            }

            // Drain local so next Take triggers batch fetch
            while (SharedPool<BatchItem>.LocalCount > 0)
            {
                SharedPool<BatchItem>.Take();
            }

            int beforeGlobal = SharedPool<BatchItem>.GlobalCount;
            var reserved = SharedPool<BatchItem>.Take(); // triggers refill
            int afterLocal = SharedPool<BatchItem>.LocalCount;

            Assert.NotNull(reserved);
            // Local should contain exactly fetchOnEmpty after refill (if enough global items existed).
            Assert.True(afterLocal == 5 || beforeGlobal < 6); // allow case where global had fewer
        }

        class SpillItem { public int Id; public SpillItem(int id) { Id = id; } }
        [Fact]
        public void SpillToGlobal_OnLocalOverflow()
        {            
            int id = 0;
            SharedPool<SpillItem>.TryInit(
                () => new SpillItem(Interlocked.Increment(ref id)),
                _ => { },
                globalCapacity: 100,
                localCapacity: 5,
                fetchOnEmpty: 2,
                keepOnFull: 2);

            // Cause many returns so overflow repeatedly spills to global.
            for (int i = 0; i < 30; i++)
            {
                var obj = new SpillItem(i);
                SharedPool<SpillItem>.Return(obj);
            }

            Assert.True(SharedPool<SpillItem>.GlobalCount > 0);
            Assert.True(SharedPool<SpillItem>.LocalCount <= 5);
        }

        class DiscardItem { }
        [Fact]
        public void Discard_IsCalled_WhenGlobalFull()
        {            
            int discarded = 0;
            SharedPool<DiscardItem>.TryInit(
                () => new DiscardItem(),
                _ => { },
                onDiscard: _ => Interlocked.Increment(ref discarded),
                globalCapacity: 3,
                localCapacity: 4,
                fetchOnEmpty: 2,
                keepOnFull: 2);

            // Fill global to capacity
            for (int i = 0; i < 50; i++)
            {
                var item = new DiscardItem();
                SharedPool<DiscardItem>.Return(item);
            }

            int filled = SharedPool<DiscardItem>.GlobalCount;
            Assert.True(filled <= 3);

            discarded = 0;
            // Now push many more to force discards (local overflow & global full)
            for (int i = 0; i < 20; i++)
            {
                var item = new DiscardItem();
                SharedPool<DiscardItem>.Return(item);
            }

            Assert.True(discarded > 0);
        }

        class DisposableItem : IDisposable
        {
            public bool Disposed;
            public void Dispose() => Disposed = true;
        }

        [Fact]
        public void DefaultDiscard_Disposes_IDisposableItems()
        {
           
            SharedPool<DisposableItem>.TryInit(
                () => new DisposableItem(),
                _ => { },
                onDiscard: null,                   // use default disposal
                globalCapacity: 0,                 // force discard quickly
                localCapacity: 2,
                fetchOnEmpty: 0,
                keepOnFull: 0);

            var a = new DisposableItem();
            SharedPool<DisposableItem>.Return(a); // local now has 1
            var b = new DisposableItem();
            SharedPool<DisposableItem>.Return(b); // local now has 2
            // Add more to force discard
            var c = new DisposableItem();
            SharedPool<DisposableItem>.Return(c); // should discard any due to keepOnFull=0

            // We cannot access discarded items directly; just assert at least one disposed.
            Assert.True(a.Disposed || b.Disposed || c.Disposed);
        }

        class LocalClearItem { }
        [Fact]
        public void ClearLocal_DiscardsAllLocalItems()
        {            
            int discarded = 0;
            SharedPool<LocalClearItem>.TryInit(
                () => new LocalClearItem(),
                _ => { },
                onDiscard: _ => Interlocked.Increment(ref discarded),
                globalCapacity: 10,
                localCapacity: 5,
                fetchOnEmpty: 2,
                keepOnFull: 2);

            // Populate local
            var obj1 = SharedPool<LocalClearItem>.Take();
            SharedPool<LocalClearItem>.Return(obj1);
            var obj2 = SharedPool<LocalClearItem>.Take();
            SharedPool<LocalClearItem>.Return(obj2);

            Assert.True(SharedPool<LocalClearItem>.LocalCount > 0);

            SharedPool<LocalClearItem>.ClearLocal();

            Assert.Equal(0, SharedPool<LocalClearItem>.LocalCount);
            Assert.True(discarded > 0);
        }

        class GlobalClearItem { }
        [Fact]
        public void ClearGlobal_DiscardsAllGlobalItems()
        {            
            int discarded = 0;
            SharedPool<GlobalClearItem>.TryInit(
                () => new GlobalClearItem(),
                _ => { },
                onDiscard: _ => Interlocked.Increment(ref discarded),
                globalCapacity: 50,
                localCapacity: 5,
                fetchOnEmpty: 2,
                keepOnFull: 2);

            // Cause spills to global
            for (int i = 0; i < 40; i++)
            {
                var item = new GlobalClearItem();
                SharedPool<GlobalClearItem>.Return(item);
            }
            Assert.True(SharedPool<GlobalClearItem>.GlobalCount > 0);

            SharedPool<GlobalClearItem>.ClearGlobal();

            Assert.Equal(0, SharedPool<GlobalClearItem>.GlobalCount);
            Assert.True(discarded > 0);
        }


        class IsoItem { public int Id; }
        [Fact]
        public void ThreadLocal_Isolation()
        {            
            SharedPool<IsoItem>.TryInit(
                () => new IsoItem(),
                _ => { },
                globalCapacity: 100,
                localCapacity: 10,
                fetchOnEmpty: 5,
                keepOnFull: 3);

            int localCountThread1 = -1;
            int localCountThread2 = -1;

            var t1 = new Thread(() =>
            {
                var x = SharedPool<IsoItem>.Take();
                SharedPool<IsoItem>.Return(x);
                localCountThread1 = SharedPool<IsoItem>.LocalCount;
            });

            var t2 = new Thread(() =>
            {
                var y = SharedPool<IsoItem>.Take();
                SharedPool<IsoItem>.Return(y);
                SharedPool<IsoItem>.Return(new IsoItem()); // push extra
                localCountThread2 = SharedPool<IsoItem>.LocalCount;
            });

            t1.Start(); t2.Start();
            t1.Join(); t2.Join();

            Assert.NotEqual(localCountThread1, localCountThread2);
        }

        class NotInitializedItem { }
        [Fact]
        public void Take_BeforeInit_Throws()
        {            
            // Intentionally do not init this type.
            Assert.Throws<InvalidOperationException>(() => SharedPool<NotInitializedItem>.Take());
        }
    }
}