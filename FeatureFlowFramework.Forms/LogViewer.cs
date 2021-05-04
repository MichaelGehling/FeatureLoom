using FeatureLoom.DataFlows;
using FeatureLoom.Helpers;
using FeatureLoom.Helpers.Forms;
using FeatureLoom.Helpers.Synchronization;
using FeatureLoom.Helpers.Time;
using FeatureLoom.Services;
using FeatureLoom.Services.Logging;
using FeatureLoom.Workflows;
using System.Drawing;
using System.Windows.Forms;

namespace FeatureLoom.Forms
{
    public partial class LogViewer : Form
    {
        private WritingLogWorkflow workflow;
        public bool keepReading = true;
        public bool hideOnClosing = false;

        public Sender<LogMessage> logNotificationSender = new Sender<LogMessage>();

        public LogViewer(IDataFlowSource logMessageSource = null, bool hideOnClosing = false, bool startReading = true)
        {
            this.hideOnClosing = hideOnClosing;
            this.keepReading = startReading;
            InitializeComponent();
            FormClosing += (o, e) =>
            {
                if(hideOnClosing)
                {
                    Hide();
                    keepReading = true;
                    e.Cancel = true;
                }
            };

            this.richTextBox1.DoubleClick += (a, b) => keepReading = !keepReading;            
            this.workflow = new WritingLogWorkflow(this, logMessageSource ?? Log.LogForwarder);
            Log.logRunner.Run(workflow);
        }

        public class WritingLogWorkflow : Workflow<WritingLogWorkflow.SM>
        {
            private LogViewer logViewer;
            private QueueReceiver<LogMessage> queue = new QueueReceiver<LogMessage>(10000, default, false);
            private MessageTrigger closingTrigger = new MessageTrigger();
            private WaitHandleCollection waitHandles;

            public WritingLogWorkflow(LogViewer logViewer, IDataFlowSource logMessageSource)
            {
                this.logViewer = logViewer;

                logViewer.FormClosing += (a, b) => closingTrigger.Post(true);
                logMessageSource.ConnectTo(queue);
            }

            public class SM : StateMachine<WritingLogWorkflow>
            {
                protected override void Init()
                {
                    State("Running").Build()
                        .Step("Wait for logMessage or that the window was closed")
                            .WaitForAny(c => c.waitHandles.All ?? c.waitHandles.Init(c.queue, c.closingTrigger))
                        .Step("If the window was closed disconnect and finish the workflow")
                            .If(c => c.closingTrigger.IsTriggered(false) && !c.logViewer.hideOnClosing)
                                .Do(c => Log.LogForwarder.DisconnectFrom(c.queue))
                                .Finish()
                        .Step("Read logmessages from queue and write them to the textbox for a short time")
                            .Do(c =>
                            {
                                TimeFrame timeSlice = new TimeFrame(50.Milliseconds());
                                var textBox = c.logViewer.richTextBox1;
                                while(!textBox.IsDisposed &&
                                      !timeSlice.Elapsed(AppTime.CoarseNow) &&
                                      c.logViewer.keepReading &&
                                      c.queue.TryReceive(out LogMessage msg))
                                {
                                    if(textBox.TextLength > 1_000_000) textBox.Text = textBox.Text.Substring(100_000);
                                    Color color = textBox.ForeColor;
                                    switch(msg.level)
                                    {
                                        case Loglevel.ALWAYS: color = Color.Purple; break;
                                        case Loglevel.ERROR: color = Color.Red; break;
                                        case Loglevel.WARNING: color = Color.OrangeRed; break;
                                        case Loglevel.INFO: color = Color.DarkBlue; break;
                                        case Loglevel.TRACE: color = Color.Gray; break;
                                    }
                                    if(color != textBox.ForeColor) textBox.AppendText(msg.Print() + "\n", color);
                                    else textBox.AppendText(msg.Print() + "\n");

                                    c.logViewer.logNotificationSender.Send(msg);
                                }
                            })
                            .Catch()
                        .Step("Loop")
                            .Wait(20.Milliseconds())
                            .Loop();
                }
            }
        }
    }
}