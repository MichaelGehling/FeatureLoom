using FeatureFlowFramework.DataFlows;
using FeatureFlowFramework.Helpers.Data;
using FeatureFlowFramework.Helpers.Diagnostics;
using FeatureFlowFramework.Helpers.Time;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace FeatureFlowFramework.Helpers
{
    public class SharedDataTests
    {

        [Fact]
        public void PreventsConcurrentWriteAccess()
        {
            TestHelper.PrepareTestContext();

            SharedData<int> sharedData = new SharedData<int>(42);
            Task task;
            using(var data = sharedData.GetWriteAccess())
            {
                data.SetValue(1);

                task = Task.Run(() =>
                {
                    using(var data2 = sharedData.GetWriteAccess())
                    {
                        data2.SetValue(2);
                    }
                });
                Thread.Sleep(10.Milliseconds());
                Assert.Equal(1, data.Value);
            }
            task.Wait();
            using(var data = sharedData.GetReadAccess())
            {
                Assert.Equal(2, data.Value);
            }
        }

        [Fact]
        public void AllowsConcurrentReadAccess()
        {
            TestHelper.PrepareTestContext();

            SharedData<int> sharedData = new SharedData<int>(42);
            Task task;
            using(var data = sharedData.GetReadAccess())
            {
                int value = 0;

                task = Task.Run(() =>
                {
                    using(var data2 = sharedData.GetReadAccess())
                    {
                       value =  data2.Value;
                    }
                });
                Thread.Sleep(20.Milliseconds());
                Assert.Equal(data.Value, value);
            }
            
        }

        [Fact]
        public void NotifiesAfterWriteAccessIfNotSuppressed()
        {
            TestHelper.PrepareTestContext();

            SharedData<int> sharedData = new SharedData<int>(42);
            LatestMessageReceiver<SharedDataUpdateNotification> receiver = new LatestMessageReceiver<SharedDataUpdateNotification>();
            sharedData.UpdateNotifications.ConnectTo(receiver);

            using(var access = sharedData.GetWriteAccess(111))
            {
                access.SetValue(43);
            }
            Assert.True(receiver.TryReceive(out SharedDataUpdateNotification note));
            Assert.Equal(111, note.originatorId);
            Assert.Equal(sharedData, note.sharedData);

            using(var access = sharedData.GetWriteAccess(111))
            {
                access.SetValue(44);
                access.SuppressPublishUpdate();
            }
            Assert.False(receiver.TryReceive(out SharedDataUpdateNotification note2));
        }

    }
}
