using FeatureLoom.Extensions;
using FeatureLoom.Logging;
using FeatureLoom.RPC;
using FeatureLoom.Storages;
using FeatureLoom.Synchronization;
using FeatureLoom.TCP;
using FeatureLoom.Time;
using FeatureLoom.Workflows;
using System;
using System.Threading.Tasks;

namespace FeatureRpcClient
{
    public class FeatureRpcClient : Workflow<FeatureRpcClient.StateMachine>
    {
        public int errorCode = 0;
        private string rpcCall;
        private bool multiCall = false;
        private TcpClientEndpoint.Config tcpConfig = new TcpClientEndpoint.Config();

        public FeatureRpcClient(string rpcCall)
        {
            this.rpcCall = rpcCall;
            if (rpcCall.EmptyOrNull()) multiCall = true;
        }

        private TcpClientEndpoint tcpClient;
        private StringRpcCaller rpcCaller;
        private Task<string> rpcCallFuture;

        public class StateMachine : StateMachine<FeatureRpcClient>
        {
            protected override void Init()
            {
                var setup = State("Setup");
                var calling = State("Calling");
                var connectionFailed = State("ConnectionFailed");
                var callFailed = State("CallFailed");
                var closingConnection = State("ClosingConnection");

                setup.Build()
                    .Step("Write default config files if not existing")
                        .If(c => !Storage.GetReader(c.tcpConfig.ConfigCategory).Exists(c.tcpConfig.Uri))
                            .Do(async c => await c.tcpConfig.TryWriteToStorageAsync())
                    .Step("Create TCP client and start connecting.")
                        .Do(c => c.tcpClient = new TcpClientEndpoint())
                    .Step("Create RPC caller and wire it to TCP connection.")
                        .Do(c =>
                        {
                            c.rpcCaller = new StringRpcCaller(1.Seconds());
                            c.rpcCaller.ConnectTo(c.tcpClient);
                            c.tcpClient.ConnectTo(c.rpcCaller);
                        })
                    .Step("Wait for connection to be established.")
                        .WaitFor(c => c.tcpClient.ConnectionWaitHandle, c => 1.Seconds())
                    .Step("If connection couldn't be established goto connection failed state.")
                        .If(c => !c.tcpClient.IsConnectedToServer)
                            .Goto(connectionFailed)
                    .Step("Goto calling state")
                        .Goto(calling);

                calling.Build()
                    .Step("If multi call mode read next command")
                        .If(c => c.multiCall)
                            .Do(c => c.rpcCall = Console.ReadLine())
                    .Step("If input is empty goto closing connection state.")
                        .If(c => c.rpcCall.EmptyOrNull())
                            .Goto(closingConnection)
                    .Step("Call remote procedure.")
                        .Do(c =>
                        {
                            c.rpcCallFuture = c.rpcCaller.CallAsync(c.rpcCall);
                        })
                    .Step("Wait for RPC response.")
                        .WaitFor(c => AsyncWaitHandle.FromTask(c.rpcCallFuture))
                    .Step("If response was not received go to call failed state.")
                        .If(c => !c.rpcCallFuture.IsCompletedSuccessfully)
                            .Goto(callFailed)
                    .Step("Print result to console.")
                        .If(c => c.multiCall)
                            .Do(async c => Console.WriteLine(await c.rpcCallFuture))
                        .Else()
                            .Do(async c => Console.Write(await c.rpcCallFuture))
                    .Step("If multi call loop calling state, else go to closing connection state.")
                        .If(c => c.multiCall)
                            .Loop()
                        .Else()
                            .Goto(closingConnection);

                connectionFailed.Build()
                    .Step("Write error message to console")
                        .Do(c => Console.WriteLine("Failed establishing connection to tcp server of RPC target."))
                    .Step("Finish")
                        .Finish();

                callFailed.Build()
                    .Step("Write error message to console")
                        .Do(c => Console.WriteLine("RPC call failed. Didn't receive any response from RPC target."))
                    .Step("If multi call go back to calling state, else go to closing connection state.")
                        .If(c => c.multiCall)
                            .Goto(calling)
                        .Else()
                            .Goto(closingConnection);

                closingConnection.Build()
                    .Step("Closing tcp connection.")
                        .Do(c => c.tcpClient.DisconnectFromTcpServer())
                    .Step("Finish")
                        .Finish();
            }
        }

        public static int Main(string[] args)
        {
            Log.QueuedLogSource.DisconnectFrom(Log.defaultConsoleLogger);
            var workflow = new FeatureRpcClient(args.Length >= 1 ? args[0] : null);
            new BlockingRunner().RunAsync(workflow).WaitFor();
            WorkflowRunnerService.PauseAllWorkflowsAsync(true).Wait(1.Seconds());
            return workflow.errorCode;
        }
    }
}