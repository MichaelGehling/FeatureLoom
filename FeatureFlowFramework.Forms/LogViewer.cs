using FeatureFlowFramework.DataFlows;
using FeatureFlowFramework.Helpers;
using FeatureFlowFramework.Helpers.Forms;
using FeatureFlowFramework.Helpers.Synchronization;
using FeatureFlowFramework.Helpers.Time;
using FeatureFlowFramework.Services.Logging;
using FeatureFlowFramework.Workflows;
using System.Drawing;
using System.Windows.Forms;

namespace FeatureFlowFramework.Forms
{
    public partial class LogViewer : Form
    {
        private WritingLogWorkflow workflow;
        public bool keepReading = true;
        public bool hideOnClosing = false;

        public LogViewer(IDataFlowSource logMessageSource = null)
        {
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
                                TimeFrame timeSlice = new TimeFrame(20.Milliseconds());
                                var textBox = c.logViewer.richTextBox1;
                                while(!textBox.IsDisposed &&
                                      !timeSlice.Elapsed &&
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
                                }
                            })
                            .Catch()
                        .Step("Loop")
                            .Loop();
                }
            }
        }
    }
}