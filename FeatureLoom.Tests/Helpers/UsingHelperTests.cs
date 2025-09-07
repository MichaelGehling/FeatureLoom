using System;
using Xunit;
using FeatureLoom.Helpers;

namespace FeatureLoom.Helpers
{
    public class UsingHelperTests
    {
        [Fact]
        public void Constructor_Executes_Before_Immediately()
        {
            bool beforeRan = false;
            bool afterRan = false;

            var helper = new UsingHelper(
                before: () => beforeRan = true,
                after: () => afterRan = true);

            Assert.True(beforeRan);
            Assert.False(afterRan);

            helper.Dispose();
            Assert.True(afterRan);
        }

        [Fact]
        public void Using_Block_Executes_After_On_Scope_Exit()
        {
            bool beforeRan = false;
            bool afterRan = false;

            using (UsingHelper.Do(
                before: () => beforeRan = true,
                after: () => afterRan = true))
            {
                Assert.True(beforeRan);
                Assert.False(afterRan);
            }

            Assert.True(afterRan);
        }

        [Fact]
        public void Factory_Overload_With_Only_After_Works()
        {
            bool afterRan = false;

            using (UsingHelper.Do(() => afterRan = true))
            {
                Assert.False(afterRan);
            }

            Assert.True(afterRan);
        }

        [Fact]
        public void Null_Actions_Are_Allowed()
        {
            var helper1 = new UsingHelper(null, null);
            helper1.Dispose();

            using (UsingHelper.Do(null, null)) { }

            using (UsingHelper.Do((Action)null)) { }

            // No assertions needed; test passes if no exception is thrown.
        }

        [Fact]
        public void Explicit_Dispose_Triggers_After()
        {
            bool afterRan = false;
            var helper = UsingHelper.Do(() => afterRan = true);
            Assert.False(afterRan);

            helper.Dispose();
            Assert.True(afterRan);
        }

        [Fact]
        public void Multiple_Dispose_Calls_Invoke_After_Each_Time()
        {
            int count = 0;
            var helper = UsingHelper.Do(() => count++);
            helper.Dispose();
            helper.Dispose(); // Not idempotent by design (documented)
            Assert.Equal(2, count);
        }

        [Fact]
        public void Copying_Struct_Results_In_Multiple_After_Invocations()
        {
            int count = 0;
            var original = UsingHelper.Do(() => count++);
            var copy = original; // Struct copy — both hold the same delegate reference value-wise

            original.Dispose();
            copy.Dispose(); // Calls again

            Assert.Equal(2, count);
        }

        [Fact]
        public void Passing_By_Value_Causes_Copy_And_Double_Dispose()
        {
            int count = 0;

            void Use(UsingHelper h)
            {
                h.Dispose(); // Disposes the copy
            }

            var helper = UsingHelper.Do(() => count++);
            Use(helper);      // after called once
            helper.Dispose(); // after called again

            Assert.Equal(2, count);
        }

        [Fact]
        public void No_After_Execution_Before_Dispose()
        {
            bool afterRan = false;
            var helper = UsingHelper.Do(() => afterRan = true);
            Assert.False(afterRan);
            GC.KeepAlive(helper); // Ensure not optimized away
            Assert.False(afterRan);
            helper.Dispose();
            Assert.True(afterRan);
        }
    }
}