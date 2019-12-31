using FeatureFlowFramework.Aspects;
using FeatureFlowFramework.Aspects.AppStructure;
using FeatureFlowFramework.DataFlows;
using FeatureFlowFramework.DataFlows.RPC;
using FeatureFlowFramework.DataFlows.TCP;
using FeatureFlowFramework.DataFlows.Web;
using FeatureFlowFramework.DataStorage;
using FeatureFlowFramework.Diagnostics;
using FeatureFlowFramework.Helper;
using FeatureFlowFramework.Logging;
using FeatureFlowFramework.Web;
using FeatureFlowFramework.Workflows;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Test
{


    internal class Program
    {
        public class BM : Workflow<BM.SM>
        {
            protected override bool RemoveSynchronizationContextOnAsync => true;

            public class SM : StateMachine<BM>
            {
                protected override void Init()
                {
                    State("Loop").Build()
                        .Step("Terminate if told so")
                            .If(c => false)
                                .Finish()
                        .Step("looping")
                            .Loop();
                }
            }
        }

        public class BM2 : Workflow<BM2.SM>
        {
            protected override bool AutoLockingOnExecution => false;
            protected override bool RemoveSynchronizationContextOnAsync => false;

            public class SM : StateMachine<BM2>
            {
                protected override void Init()
                {
                    State("Loop").Build()
                        .Step("looping")
                            .Loop();
                }
            }
        }

        public class Config : Configuration
        {
            public string str = "";
            public override string Uri => "ProgramConfig";
        }

        public class TestMsg
        {
            public int myInt;
            public string myString;
        }

        static int PlusOne(int number)
        {
            //count_calleeMethod++;
            return number + 1;
        }

        static int Plus(int a, int b)
        {
            return a + b;
        }

        static int GetRandom()
        {
            return new Random().Next();
        }

        class MyProxy
        {
            private readonly RpcCaller caller;

            public Func<int, Task<int>> PlusOne;
            public Action<int> PlusOneNoResponse;
            public Func<int, int, Task<int>> Plus;
            public Func<Task<int>> GetRandom;
            public Action<string> Hello;

            public MyProxy(RpcCaller caller)
            {
                this.caller = caller;
                PlusOne = number => caller.CallAsync<int, int>("PlusOne", number);
                PlusOneNoResponse = number => caller.CallAsync<int, int>("PlusOne", number);
                Plus = (a, b) => caller.CallAsync<(int, int), int>("Plus", (a, b));
                GetRandom = () => caller.CallAsync<int>("GetRandom");
                Hello = (s) => caller.CallAsync<string>("Hello", s).Wait();
            }
        }

        /*
        volatile static int count_callerOut = 0;
        volatile static int count_tcpServerOut = 0;
        volatile static int count_calleeMethod = 0;
        volatile static int count_calleeOut = 0;
        volatile static int count_tcpClientOut = 0;
        */

        static readonly object root = new object();

        static string PrintChildren(object parent, int depth = 0)
        {
            string result = "";
            if (parent is IUpdateAppStructureAspect updatable) updatable.TryUpdateAppStructureAspects(1.Seconds());
            for (int i = 0; i < depth; i++) result += "║ ";
            result += "╠═";
            result += $"{parent.GetAspectInterface<IHasName>()?.Name ?? "Unnamed"} ({parent.GetType().Name})\r\n";
            foreach (object child in (parent.GetAspectInterface<IHasChildren>()?.Children).EmptyIfNull())
            {
                result += PrintChildren(child, depth + 1);
            }
            return result;
        }


        private static void Main(string[] args)
        {


            var wfCol = new WorkflowInfoCollector().KeepAlive();
            Log.ALWAYS(root, "Start application");

            TcpServerEndpoint tcpServer = new TcpServerEndpoint().KeepAlive();
            TcpClientEndpoint tcpClient = new TcpClientEndpoint().KeepAlive();



            while (!tcpClient.IsConnectedToServer)
            {
                Console.Write(".");
                Task.Delay(100).Wait();
            }


            RpcCaller caller = new RpcCaller(100.Seconds()).KeepAlive();
            RpcCallee callee = new RpcCallee().KeepAlive();
            Sender stringCaller = new Sender();
            var jsonConverter = new MessageConverter<object, string>(msg => msg.ToJson());
            var senderJsonConverter = new MessageConverter<object, string>(msg => msg.ToJson());
            ProcessingEndpoint<string> consoleWriter = new ProcessingEndpoint<string>(msg => Console.WriteLine(msg));

            caller.ConnectTo(tcpClient);
            tcpClient.ConnectTo(caller);

            callee.ConnectTo(tcpServer);
            tcpServer.ConnectTo(callee);                        

            //caller.ConnectTo(senderJsonConverter).ConnectTo(callee);
            //senderJsonConverter.ConnectTo(consoleWriter);
            //callee.ConnectTo(caller);
            //stringCaller.ConnectTo(callee);
            //callee.ConnectTo(jsonConverter).ConnectTo(consoleWriter);



            callee.RegisterMethod<int, int>("PlusOne", PlusOne);
            callee.RegisterMethod<int, int, int>("Plus", Plus);
            callee.RegisterMethod<int>("GetRandom", GetRandom);
            callee.RegisterMethod<string>("Hello", s => Console.WriteLine("Hello " + s));
            callee.RegisterMethod<string>("GetAppStructure", () => PrintChildren(root));
            callee.RegisterMethod<string>("Commands", () => "PlusOne X, Plus X Y, GetRandom, Hello, GetAppStructure, Commands");

            var proxy = new MyProxy(caller);
            proxy.Plus(1, 2);
            proxy.GetRandom();

            root.AddAspectAddOn(new AppStructureAddOn()).AddChild(tcpClient, "TCP-Client").AddChild(tcpServer, "TCP-Server");
            Console.WriteLine("");
            PrintChildren(root);
            Console.ReadKey();

            //var callerProbe = new DataFlowProbe<IRpcRequest, string>("callerOutPut", null, m => m.ToJson(), 100, 10.Milliseconds(), 1000).KeepAlive();
            //caller.ConnectTo(callerProbe);
            //callee.ConnectTo(new ProcessingEndpoint<object>(m => count_calleeOut++).KeepAlive());
            //tcpClient.ConnectTo(new ProcessingEndpoint<object>(m => count_tcpClientOut++).KeepAlive());
            //tcpServer.ConnectTo(new ProcessingEndpoint<object>(m => count_tcpServerOut++).KeepAlive());

            /*Task.Run(() =>
            {
                while(true)
                {
                    Log.ALWAYS($"callerOut: {count_callerOut}, tcpServerOut: {count_tcpServerOut}, calleeMethod: {count_calleeMethod}, calleeOut: {count_calleeOut}, tcpClientOut: {count_tcpClientOut}");
                    Task.Delay(1.Seconds()).Wait();
                }
            });*/



            Console.WriteLine("Go");
            
            var r1 = proxy.PlusOne(proxy.GetRandom().Result).Result;
            var r2 = proxy.Plus(r1, 50).Result;
            proxy.Hello($"World! {r1} + 50 = {r2}");
                                  

            int n =300;
            Task[] tasks = new Task[n];

            var sw = AppTime.TimeKeeper;



            sw.Restart();
            for(int i = 0; i < n; i++)
            {
                tasks[i] = proxy.Plus(i,i);
            }
            Task.WhenAll(tasks).Wait();            
            Console.WriteLine(sw.Elapsed.TotalMilliseconds);            

            sw.Restart();
            for(int i = 0; i < n; i++)
            {
                tasks[i] = proxy.PlusOne(i);
            }
            Task.WhenAll(tasks).Wait();
            Console.WriteLine(sw.Elapsed.TotalMilliseconds);

            sw.Restart();
            for(int i = 0; i < n; i++)
            {
                proxy.PlusOneNoResponse(i);
            }            
            Console.WriteLine(sw.Elapsed.TotalMilliseconds);

            sw.Restart();
            for(int i = 0; i < n; i++)
            {
                PlusOne(i);
            }            
            Console.WriteLine(sw.Elapsed.TotalMilliseconds);

            Console.ReadKey();

            Log.INFO(root, "Lets go!");
            var translator = new DefaultWebMessageTranslator(("TestMsg", typeof(TestMsg).AssemblyQualifiedName));
            HttpServerReceiver webSender = new HttpServerReceiver("/SendJson", translator);
            HttpServerFetchProvider webFetcher = new HttpServerFetchProvider("/ReceiveJson", translator, 5);
            webSender.ConnectTo(webFetcher);

            HttpServerRequestReplyForwarder webRequester = new HttpServerRequestReplyForwarder("/RequestJson", translator);
            ReplyingEndpoint<TestMsg, TestMsg> replier = new ReplyingEndpoint<TestMsg, TestMsg>(req =>
            {
                req.myInt++;
                req.myString = "Reply";
                return (req, true);
            });
            webRequester.ConnectToAndBack(replier);

            var consoleOut = new ProcessingEndpoint<object>(str =>
            {
                Console.WriteLine(str.ToJson());
                return true;
            });
            webSender.ConnectTo(consoleOut);
            webRequester.ConnectTo(consoleOut);
            replier.ConnectTo(consoleOut);

            

            Task.Delay(100).Wait();

            StorageWebAccess<string> persistenceWebAccess = new StorageWebAccess<string>(new StorageWebAccess<string>.Config()
            {
                category = "textFiles",
                allowReadUrls = true,
                allowChange = true,
                allowRead = true,
                route = "/files"
            });

            Storage.GetReader("textFiles").TrySubscribeForChangeNotifications("*", consoleOut);
            //Storage.GetWriter("textFiles")

            SharedWebServer.WebServer.Start();

            
            var wfs = wfCol.AllWorkflows;
            foreach(var wf in wfs)
            {
                Console.WriteLine(wf.GetType().ToString() + wf.GetAspectHandle());
            }

 

            /*
            TcpServerEndpoint server = new TcpServerEndpoint(new TcpServerEndpoint.Config() { receivingBufferSize = 100, x509CertificateName=null });

            List<TcpClientEndpoint> clients = new List<TcpClientEndpoint>();
            Sender<object> sender = new Sender<object>();
            for (int i = 0; i < 10; i++)
            {
                TcpClientEndpoint client = new TcpClientEndpoint(new TcpClientEndpoint.Config() { serverCertificateName=null, ignoreFailedServerAuthentication=true});
                clients.Add(client);
                sender.ConnectTo(client);
            }

            server.ConnectTo(consoleOut);

            while (server.CountConnectedClients < clients.Count)
            {
                Thread.Sleep(10);
            }
            var timeFrame = new TimeFrame(10.Minutes());
            while (!timeFrame.Expired)
            {
                Thread.Sleep(10);
                //sender.Send(AppTime.Now.ToShortDateString());
            }
            */
            Console.ReadKey();
            return;
        }
    }
}
