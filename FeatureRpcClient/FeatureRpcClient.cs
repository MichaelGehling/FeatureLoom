using FeatureLoom.Extensions;
using FeatureLoom.Logging;
using FeatureLoom.RPC;
using FeatureLoom.DependencyInversion;
using FeatureLoom.Storages;
using FeatureLoom.Synchronization;
using FeatureLoom.TCP;
using FeatureLoom.Time;
using FeatureLoom.Workflows;
using System;
using System.Threading.Tasks;
using FeatureLoom.Statemachines;
using System.Threading;

namespace FeatureRpcClient
{
    public class FeatureRpcClient
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

        public static async Task<int> Main(string[] args)
        {
            Log.QueuedLogSource.DisconnectFrom(Log.DefaultConsoleLogger);

            Statemachine<FeatureRpcClient> statemachine = new Statemachine<FeatureRpcClient>(
                new Statemachine<FeatureRpcClient>.State(nameof(Setup), Setup),
                new Statemachine<FeatureRpcClient>.State(nameof(Calling), Calling),
                new Statemachine<FeatureRpcClient>.State(nameof(ConnectionFailed), ConnectionFailed),
                new Statemachine<FeatureRpcClient>.State(nameof(CallFailed), CallFailed),
                new Statemachine<FeatureRpcClient>.State(nameof(ClosingConnection), ClosingConnection));

            var context = new FeatureRpcClient(args.Length >= 1 ? args[0] : null);
            await statemachine.CreateAndStartJob(context);
            return context.errorCode;
        }

        private static async Task<string> ConnectionFailed()
        {
            Console.WriteLine("Failed establishing connection to tcp server of RPC target.");
            return "";
        }

        private static async Task<string> CallFailed(FeatureRpcClient c)
        {
            Console.WriteLine("RPC call failed. Didn't receive any response from RPC target.");
            if (c.multiCall) return nameof(Calling);
            else return nameof(ClosingConnection);
        }

        private static async Task<string> ClosingConnection(FeatureRpcClient c)
        {
            c.tcpClient.DisconnectFromTcpServer();
            return "";
        }

        private static async Task<string> Calling(FeatureRpcClient c, CancellationToken token)
        {
            while (c.multiCall)
            {
                if (c.multiCall) c.rpcCall = Console.ReadLine();
                if (c.rpcCall.EmptyOrNull()) return nameof(ClosingConnection);
                c.rpcCallFuture = c.rpcCaller.CallAsync(c.rpcCall);
                await c.rpcCallFuture.WaitAsync(token);
                if (!c.rpcCallFuture.IsCompletedSuccessfully) return nameof(CallFailed);
                if (c.multiCall) Console.WriteLine(await c.rpcCallFuture.WaitAsync(token));
                else Console.Write(await c.rpcCallFuture);
            }
            return nameof(ClosingConnection);
        }

        private static async Task<string> Setup(FeatureRpcClient c, CancellationToken token)
        {
            if (!Storage.GetReader(c.tcpConfig.ConfigCategory).Exists(c.tcpConfig.Uri)) await c.tcpConfig.TryWriteToStorageAsync();
            c.tcpClient = new TcpClientEndpoint();
            c.rpcCaller = new StringRpcCaller(1.Seconds());
            c.rpcCaller.ConnectTo(c.tcpClient);
            c.tcpClient.ConnectTo(c.rpcCaller);
            await c.tcpClient.ConnectionWaitHandle.WaitAsync(1.Seconds(), token);
            if (!c.tcpClient.IsConnectedToServer) return nameof(ConnectionFailed);
            return nameof(Calling);
        }
    }
}