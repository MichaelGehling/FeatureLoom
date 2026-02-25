using FeatureLoom.Helpers;
using FeatureLoom.Time;
using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace FeatureLoom.MessageFlow
{
    public class StatisticsMessageProbeTests
    {
        [Fact]
        public void Post_ByValueAndByRef_WithFilterAndConverter_BuffersMessages()
        {
            using var testContext = TestHelper.PrepareTestContext();

            var probe = new StatisticsMessageProbe<int, string>(
                "probe",
                filter: value => value % 2 == 0,
                converter: value => $"v{value}",
                messageBufferSize: 10);

            probe.Post(1);
            int byRef = 2;
            probe.Post<int>(in byRef);
            probe.Post(3);
            probe.Post(4);

            Assert.Equal(2, probe.Counter);

            long pos = 0;
            var buffered = probe.GetBufferedMessages(ref pos);

            Assert.Equal(2, buffered.Length);
            Assert.Equal(new[] { "v2", "v4" }, buffered.Select(entry => entry.message).ToArray());
            Assert.All(buffered, entry => Assert.NotEqual(default, entry.timestamp));
        }

        [Fact]
        public void TimestampBufferUsed_WhenNoConverterProvided()
        {
            using var testContext = TestHelper.PrepareTestContext();

            var probe = new StatisticsMessageProbe<int, int>("probe", messageBufferSize: 5);

            probe.Post(10);
            probe.Post(20);

            long msgPos = 0;
            var messages = probe.GetBufferedMessages(ref msgPos);
            Assert.Empty(messages);
            Assert.Equal(0, msgPos);

            long tsPos = 0;
            var timestamps = probe.GetBufferedTimestamps(ref tsPos);
            Assert.Equal(2, timestamps.Length);
            Assert.All(timestamps, timestamp => Assert.NotEqual(default, timestamp));
        }

        [Fact]
        public void Post_IgnoresMismatchedType()
        {
            using var testContext = TestHelper.PrepareTestContext();

            var probe = new StatisticsMessageProbe<int, int>("probe", messageBufferSize: 5);

            probe.Post("not-an-int");
            probe.Post(7);

            Assert.Equal(1, probe.Counter);

            long tsPos = 0;
            var timestamps = probe.GetBufferedTimestamps(ref tsPos);
            Assert.Single(timestamps);
        }

        [Fact]
        public void TimeSliceCounters_Buffered_WhenSliceElapsed()
        {
            using var testContext = TestHelper.PrepareTestContext();

            var probe = new StatisticsMessageProbe<int, int>(
                "probe",
                messageBufferSize: 0,
                timeSliceSize: TimeSpan.FromMilliseconds(10),
                maxTimeSlices: 5);

            probe.Post(1);

            AppTime.Wait(TimeSpan.FromMilliseconds(20));

            probe.Post(2);

            long pos = 0;
            var slices = probe.GetBufferedTimeSlices(ref pos);

            Assert.Single(slices);
            Assert.Equal(1, slices[0].counter);
            Assert.Equal(1, probe.CurrentTimeSlize.counter);
        }

        [Fact]
        public void WaitHandle_Signals_OnPost()
        {
            using var testContext = TestHelper.PrepareTestContext();

            var probe = new StatisticsMessageProbe<int, int>("probe", messageBufferSize: 1);

            var waitHandle = probe.WaitHandle;
            Task waitingTask = waitHandle.WaitingTask;
            Assert.False(waitingTask.IsCompleted);


            probe.Post(1);

            Assert.True(waitingTask.IsCompleted);
        }
    }
}