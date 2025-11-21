using FeatureLoom.Diagnostics;
using FeatureLoom.Helpers;
using System.Threading.Tasks;
using Xunit;

namespace FeatureLoom.MessageFlow
{
    public class MessageConverterTests
    {
        [Fact]
        public void CanConvertMessage()
        {
            using var testContext = TestHelper.PrepareTestContext();

            var sender = new Sender<string>();
            var converter = new MessageConverter<string, int>(str => int.Parse(str));
            var sink = new LatestMessageReceiver<int>();
            sender.ConnectTo(converter).ConnectTo(sink);

            sender.Send("42");

            Assert.True(sink.HasMessage);
            Assert.Equal(42, sink.LatestMessageOrDefault);
        }

        [Fact]
        public void ForwardsOtherMessagesWhenEnabled()
        {
            using var testContext = TestHelper.PrepareTestContext();

            var sender = new Sender();
            var converter = new MessageConverter<string, int>(str => int.Parse(str), forwardOtherMessages: true);
            var intSink = new LatestMessageReceiver<int>();
            var doubleSink = new LatestMessageReceiver<double>();
            sender.ConnectTo(converter);
            converter.ConnectTo(intSink);
            converter.ConnectTo(doubleSink);

            // Send convertible string -> should reach int sink
            sender.Send("7");
            Assert.True(intSink.HasMessage);
            Assert.Equal(7, intSink.LatestMessageOrDefault);

            // Send non-matching double -> should be forwarded unchanged to double sink only
            sender.Send(3.5);
            Assert.True(doubleSink.HasMessage);
            Assert.Equal(3.5, doubleSink.LatestMessageOrDefault);
            // Ensure int sink didn't receive a new value from the double message
            Assert.Equal(7, intSink.LatestMessageOrDefault);
        }

        [Fact]
        public void DoesNotForwardOtherMessagesWhenDisabled()
        {
            using var testContext = TestHelper.PrepareTestContext();

            var sender = new Sender();
            var converter = new MessageConverter<string, int>(str => int.Parse(str), forwardOtherMessages: false);
            var intSink = new LatestMessageReceiver<int>();
            var doubleSink = new LatestMessageReceiver<double>();
            sender.ConnectTo(converter);
            converter.ConnectTo(intSink);
            converter.ConnectTo(doubleSink);

            // Convertible
            sender.Send("10");
            Assert.True(intSink.HasMessage);
            Assert.Equal(10, intSink.LatestMessageOrDefault);

            // Non-matching -> must not be forwarded
            sender.Send(4.2);
            Assert.False(doubleSink.HasMessage);
            // Int sink remains unchanged
            Assert.Equal(10, intSink.LatestMessageOrDefault);
        }

        [Fact]
        public async Task AsyncConversionWorks()
        {
            using var testContext = TestHelper.PrepareTestContext();

            var sender = new Sender<string>();
            var converter = new MessageConverter<string, int>(str => int.Parse(str));
            var sink = new LatestMessageReceiver<int>();
            sender.ConnectTo(converter).ConnectTo(sink);

            await sender.SendAsync("123");

            Assert.True(sink.HasMessage);
            Assert.Equal(123, sink.LatestMessageOrDefault);
        }

        [Fact]
        public void InParameterOverloadIsHandled()
        {
            using var testContext = TestHelper.PrepareTestContext();

            var sender = new Sender();
            var converter = new MessageConverter<string, int>(str => int.Parse(str));
            var intSink = new LatestMessageReceiver<int>();
            sender.ConnectTo(converter).ConnectTo(intSink);

            // Use the in-parameter path on Sender<T> to hit converter.Post(in M)
            var input = "99";
            sender.Send(in input);

            Assert.True(intSink.HasMessage);
            Assert.Equal(99, intSink.LatestMessageOrDefault);
        }

        [Fact]
        public void NullableNumericOutputConverts()
        {
            using var testContext = TestHelper.PrepareTestContext();

            var sender = new Sender<string>();
            var converter = new MessageConverter<string, int?>(str => int.Parse(str));
            var sink = new LatestMessageReceiver<int?>();
            sender.ConnectTo(converter).ConnectTo(sink);

            sender.Send("0");

            Assert.True(sink.HasMessage);
            Assert.Equal(0, sink.LatestMessageOrDefault);
        }

        private enum MyEnum : byte { A = 1, B = 2 }

        [Fact]
        public void EnumOutputConverts()
        {
            using var testContext = TestHelper.PrepareTestContext();

            var sender = new Sender<int>();
            var converter = new MessageConverter<int, MyEnum>(i => i == 1 ? MyEnum.A : MyEnum.B);
            var sink = new LatestMessageReceiver<MyEnum>();
            sender.ConnectTo(converter).ConnectTo(sink);

            sender.Send(2);

            Assert.True(sink.HasMessage);
            Assert.Equal(MyEnum.B, sink.LatestMessageOrDefault);
        }

        private struct BigStruct
        {
            public long A;
            public long B;
            public long C;
            public BigStruct(long a, long b, long c) { A = a; B = b; C = c; }
        }

        [Fact]
        public void NonNumericStructOutputFlows()
        {
            using var testContext = TestHelper.PrepareTestContext();

            var sender = new Sender<string>();
            var converter = new MessageConverter<string, BigStruct>(s => new BigStruct(1, 2, 3));
            var sink = new LatestMessageReceiver<BigStruct>();
            sender.ConnectTo(converter).ConnectTo(sink);

            sender.Send("ignored");

            Assert.True(sink.HasMessage);
            var val = sink.LatestMessageOrDefault;
            Assert.Equal(1, val.A);
            Assert.Equal(2, val.B);
            Assert.Equal(3, val.C);
        }

        [Fact]
        public void NullStringIsNotConverted()
        {
            using var testContext = TestHelper.PrepareTestContext();

            var sender = new Sender<string>();
            var converter = new MessageConverter<string, int>(str => int.Parse(str), forwardOtherMessages: true);
            var intSink = new LatestMessageReceiver<int>();
            sender.ConnectTo(converter).ConnectTo(intSink);

            // Sending null won't match "is I" and thus won't be converted.
            string msg = null;
            sender.Send(msg);

            Assert.False(intSink.HasMessage);
        }
    }
}