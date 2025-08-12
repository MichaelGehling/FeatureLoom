using FeatureLoom.Helpers;
using System;
using System.Threading.Tasks;
using Xunit;

namespace FeatureLoom.Helpers
{
    public class TryHelperTests
    {
        [Fact]
        public void TryFunc_ReturnsTrueAndResult_OnSuccess()
        {
            bool success = TryHelper.Try(() => 42, out int result);
            Assert.True(success);
            Assert.Equal(42, result);
        }

        [Fact]
        public void TryFunc_ReturnsFalseAndDefault_OnException()
        {
            bool success = TryHelper.Try(() => { throw new InvalidOperationException(); return 0; }, out int result);
            Assert.False(success);
            Assert.Equal(default, result);
        }

        [Fact]
        public void TryFuncWithException_ReturnsTrueAndResult_OnSuccess()
        {
            bool success = TryHelper.Try(() => 42, out int result, out Exception ex);
            Assert.True(success);
            Assert.Equal(42, result);
            Assert.Null(ex);
        }

        [Fact]
        public void TryFuncWithException_ReturnsFalseAndException_OnException()
        {
            var exception = new InvalidOperationException("Test");
            bool success = TryHelper.Try(() => { throw exception; return 0; }, out int result, out Exception ex);
            Assert.False(success);
            Assert.Equal(default, result);
            Assert.Same(exception, ex);
        }

        [Fact]
        public void TryAction_ReturnsTrue_OnSuccess()
        {
            bool success = TryHelper.Try(() => { /* Do nothing */ });
            Assert.True(success);
        }

        [Fact]
        public void TryAction_ReturnsFalse_OnException()
        {
            bool success = TryHelper.Try(() => { throw new InvalidOperationException(); });
            Assert.False(success);
        }

        [Fact]
        public void TryActionWithException_ReturnsTrue_OnSuccess()
        {
            bool success = TryHelper.Try(() => { }, out Exception ex);
            Assert.True(success);
            Assert.Null(ex);
        }

        [Fact]
        public void TryActionWithException_ReturnsFalseAndException_OnException()
        {
            var exception = new InvalidOperationException("Test");
            bool success = TryHelper.Try((Action)(() => { throw exception; }), out Exception ex);
            Assert.False(success);
            Assert.Same(exception, ex);
        }

        [Fact]
        public async Task TryAsyncFunc_ReturnsTrueAndResult_OnSuccess()
        {
            var (success, result) = await TryHelper.TryAsync(() => Task.FromResult(42));
            Assert.True(success);
            Assert.Equal(42, result);
        }

        [Fact]
        public async Task TryAsyncFunc_ReturnsFalseAndDefault_OnException()
        {
            var (success, result) = await TryHelper.TryAsync<int>(() => Task.FromException<int>(new InvalidOperationException()));
            Assert.False(success);
            Assert.Equal(default, result);
        }

        [Fact]
        public async Task TryAsyncFuncWithException_ReturnsTrueAndResult_OnSuccess()
        {
            var (success, result, ex) = await TryHelper.TryAsyncWithException(() => Task.FromResult(42));
            Assert.True(success);
            Assert.Equal(42, result);
            Assert.Null(ex);
        }

        [Fact]
        public async Task TryAsyncFuncWithException_ReturnsFalseAndException_OnException()
        {
            var exception = new InvalidOperationException("Test");
            var (success, result, ex) = await TryHelper.TryAsyncWithException<int>(() => Task.FromException<int>(exception));
            Assert.False(success);
            Assert.Equal(default, result);
            Assert.Same(exception, ex);
        }

        [Fact]
        public async Task TryAsyncAction_ReturnsTrue_OnSuccess()
        {
            bool success = await TryHelper.TryAsync(() => Task.CompletedTask);
            Assert.True(success);
        }

        [Fact]
        public async Task TryAsyncAction_ReturnsFalse_OnException()
        {
            bool success = await TryHelper.TryAsync(() => Task.FromException(new InvalidOperationException()));
            Assert.False(success);
        }

        [Fact]
        public async Task TryAsyncActionWithException_ReturnsTrue_OnSuccess()
        {
            var (success, ex) = await TryHelper.TryAsyncWithException(() => Task.CompletedTask);
            Assert.True(success);
            Assert.Null(ex);
        }

        [Fact]
        public async Task TryAsyncActionWithException_ReturnsFalseAndException_OnException()
        {
            var exception = new InvalidOperationException("Test");
            var (success, ex) = await TryHelper.TryAsyncWithException(() => Task.FromException(exception));
            Assert.False(success);
            Assert.Same(exception, ex);
        }
    }
}