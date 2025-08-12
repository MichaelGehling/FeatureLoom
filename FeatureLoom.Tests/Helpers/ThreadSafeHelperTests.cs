using FeatureLoom.Helpers;
using System.Threading.Tasks;
using Xunit;

namespace FeatureLoom.Helpers
{
    public class ThreadSafeHelperTests
    {
        private class TestObject
        {
            public int value;
        }

        [Fact]
        public void ReplaceObject_ReplacesObjectCorrectly_WhenNoContention()
        {
            // Arrange
            var initialObject = new TestObject { value = 1 };
            var originalRef = initialObject;

            // Act
            var result = ThreadSafeHelper.ReplaceObject(ref initialObject, (old) =>
            {
                Assert.Same(originalRef, old);
                return new TestObject { value = old.value + 1 };
            });

            // Assert
            Assert.Equal(2, result.value);
            Assert.Equal(2, initialObject.value);
            Assert.NotSame(originalRef, initialObject);
        }

        [Fact]
        public async Task ReplaceObject_HandlesContentionCorrectly()
        {
            // Arrange
            var obj = new TestObject { value = 0 };
            int numThreads = 10;
            int numIncrementsPerThread = 1000;

            // Act
            Task[] tasks = new Task[numThreads];
            for (int i = 0; i < numThreads; i++)
            {
                tasks[i] = Task.Run(() =>
                {
                    for (int j = 0; j < numIncrementsPerThread; j++)
                    {
                        ThreadSafeHelper.ReplaceObject(ref obj, (current) => new TestObject { value = current.value + 1 });
                    }
                });
            }
            await Task.WhenAll(tasks);

            // Assert
            Assert.Equal(numThreads * numIncrementsPerThread, obj.value);
        }
    }
}