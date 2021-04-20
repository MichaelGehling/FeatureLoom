using FeatureFlowFramework.Helpers.Time;
using FeatureFlowFramework.Helpers.Diagnostics;
using FeatureFlowFramework.Helpers.Synchronization;
using Xunit;

namespace FeatureFlowFramework.DataFlows.RPC
{
    public class RpcTests
    {
        [Fact]
        public void CanCallMethodViaDataFlow()
        {
            TestHelper.PrepareTestContext();

            var callee = new RpcCallee();
            var caller = new RpcCaller(1.Seconds());
            caller.ConnectToAndBack(callee);

            callee.RegisterMethod("Get42", () => 42);

            int result = caller.CallAsync<int>("Get42").WaitFor();
            Assert.Equal(42, result);
        }

        [Fact]
        public void CanCallMethodWithSeveralParameters()
        {
            TestHelper.PrepareTestContext();

            var callee = new RpcCallee();
            var caller = new RpcCaller(1.Seconds());
            caller.ConnectToAndBack(callee);

            callee.RegisterMethod<string, int, string>("RepeatString", (str, num) =>
            {
                string response = "";
                for(int i = 0; i < num; i++) response += str;
                return response;
            });

            string result = caller.CallAsync<(string, int), string>("RepeatString", ("Test ", 3)).WaitFor();
            Assert.Equal("Test Test Test ", result);
        }

        [Fact]
        public void CanCallMethodAndIgnoringResponse()
        {
            TestHelper.PrepareTestContext();

            var callee = new RpcCallee();
            var caller = new RpcCaller(1.Seconds());
            caller.ConnectTo(callee);

            bool callFlag = false;
            callee.RegisterMethod("SetFlag", () => callFlag = true);

            caller.CallNoResponse("SetFlag");
            Assert.True(callFlag);
        }

        private struct TestParameters
        {
            public string str;
            public int num;

            public TestParameters(string str, int num)
            {
                this.str = str;
                this.num = num;
            }
        }

        [Fact]
        public void CanCallMethodWithSeveralParametersFromString()
        {
            TestHelper.PrepareTestContext();

            var callee = new RpcCallee();
            var caller = new StringRpcCaller(1.Seconds());
            caller.ConnectToAndBack(callee);

            callee.RegisterMethod<string, int, string>("RepeatString", (str, num) =>
            {
                string response = "";
                for(int i = 0; i < num; i++) response += str;
                return response;
            });

            string result = caller.CallAsync("RepeatString \"Test \" 3").WaitFor();
            Assert.Equal("Test Test Test ", result);

            callee.RegisterMethod<TestParameters, string>("RepeatString2", (testParams) =>
            {
                string response = "";
                for(int i = 0; i < testParams.num; i++) response += testParams.str;
                return response;
            });

            string result2 = caller.CallAsync("RepeatString2 {str:\"Abc \", num:2}").WaitFor();
            Assert.Equal("Abc Abc ", result2);
        }

        [Fact]
        public void CanCallMethodOnMultipleCalleesAndReceiveAllResults()
        {
            TestHelper.PrepareTestContext();

            var caller = new RpcCaller(1.Seconds());
            var calleeA = new RpcCallee();
            var calleeB = new RpcCallee();
            caller.ConnectToAndBack(calleeA);
            caller.ConnectToAndBack(calleeB);

            calleeA.RegisterMethod("GetValue", () => 42);
            calleeB.RegisterMethod("GetValue", () => 99);

            var resultReceiver = new QueueReceiver<int>();
            caller.CallMultiResponse<int>("GetValue", resultReceiver);
            Assert.Equal(2, resultReceiver.CountQueuedMessages);
            Assert.Contains(42, resultReceiver.PeekAll());
            Assert.Contains(99, resultReceiver.PeekAll());
        }

        [Fact]
        public void CanCallMethodOnMultipleCalleesAndReceiveAllResultsFromString()
        {
            TestHelper.PrepareTestContext();

            var caller = new StringRpcCaller(1.Seconds());
            var calleeA = new RpcCallee();
            var calleeB = new RpcCallee();
            caller.ConnectToAndBack(calleeA);
            caller.ConnectToAndBack(calleeB);

            calleeA.RegisterMethod("GetValue", () => 42);
            calleeB.RegisterMethod("GetValue", () => 99);

            var resultReceiver = new QueueReceiver<string>();
            caller.CallMultiResponse<int>("GetValue", resultReceiver);
            Assert.Equal(2, resultReceiver.CountQueuedMessages);
            Assert.Contains("42", resultReceiver.PeekAll());
            Assert.Contains("99", resultReceiver.PeekAll());
        }

        [Fact]
        public void RpcRequestsCanBeQueuedAndHandledLater()
        {
            TestHelper.PrepareTestContext();

            var callee = new QueuingRpcCallee();
            var caller = new RpcCaller(1.Seconds());
            caller.ConnectToAndBack(callee);

            callee.RegisterMethod("Get42", () => 42);

            var callTask = caller.CallAsync<int>("Get42");
            Assert.False(callTask.IsCompleted);
            Assert.Equal(1, callee.Count);

            callee.WaitHandle.Wait();
            callee.HandleQueuedRpcRequests();
            Assert.True(callTask.IsCompleted);
            Assert.Equal(42, callTask.WaitFor());
            Assert.Equal(0, callee.Count);
        }
    }
}