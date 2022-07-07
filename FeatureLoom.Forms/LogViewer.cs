using FeatureLoom.MessageFlow;
using FeatureLoom.Logging;
using FeatureLoom.Synchronization;
using FeatureLoom.Time;
using FeatureLoom.Workflows;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using FeatureLoom.Extensions;
using System;
using System.Threading.Tasks;

namespace FeatureLoom.Forms
{
    public partial class LogViewer : Form
    {
        public bool hideOnClosing = false;

        public Sender<LogMessage> logNotificationSender = new Sender<LogMessage>();
        private QueueReceiver<LogMessage> queue = new QueueReceiver<LogMessage>(10000, default, false);
        private MessageTrigger closingTrigger = new MessageTrigger();
        private ConditionalTrigger<bool, bool> visibilityTrigger = new ConditionalTrigger<bool, bool>(m => m == true, m => m == false);
        private StringBuilder stringBuilder = new StringBuilder();

        public LogViewer(IMessageSource logMessageSource = null, bool hideOnClosing = false, bool startReading = true)
        {
            this.hideOnClosing = hideOnClosing;
            InitializeComponent();
            FormClosing += (o, e) =>
            {
                if (this.hideOnClosing)
                {
                    Hide();
                    e.Cancel = true;
                }
            };

            this.Shown += (o, e) => this.RunAsync();
            this.Disposed += (o, e) => closingTrigger.Post(true);
            this.VisibleChanged += (o, e) => visibilityTrigger.Post(Visible);
            logMessageSource.ConnectTo(queue);
        }

        private async void RunAsync()
        {
            while(true)
            {
                await Task.WhenAny(closingTrigger.WaitAsync(), Task.WhenAll(visibilityTrigger.WaitAsync(), queue.WaitAsync()));

                while (visibilityTrigger.IsTriggered() &&
                       !closingTrigger.IsTriggered() &&
                       !this.IsDisposed &&
                       queue.TryReceive(out LogMessage msg))
                {
                    if (textBox.TextLength > 1_000_000) textBox.Text = textBox.Text.Substring(100_000);
                    Color color = textBox.ForeColor;
                    switch (msg.level)
                    {
                        case Loglevel.FORCE: color = Color.Purple; break;
                        case Loglevel.ERROR: color = Color.Red; break;
                        case Loglevel.WARNING: color = Color.OrangeRed; break;
                        case Loglevel.INFO: color = Color.DarkBlue; break;
                        case Loglevel.TRACE: color = Color.Gray; break;
                    }
                    if (color != textBox.ForeColor) textBox.AppendText(msg.PrintToStringBuilder(stringBuilder).Append("\n").GetStringAndClear(), color);
                    else textBox.AppendText(msg.PrintToStringBuilder(stringBuilder).Append("\n").GetStringAndClear());

                    logNotificationSender.Send(msg);

                    Application.DoEvents();

                    if (closingTrigger.IsTriggered()) break;
                }
            }
        }

    }
}