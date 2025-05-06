using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using FeatureLoom.Forms;
using FeatureLoom.Logging;
using FeatureLoom.MessageFlow;
using FeatureLoom.Storages;
using FeatureLoom.Time;
using FeatureLoom.Workflows;

namespace FormsPlayground
{

    public class SerializerTest : Configuration
    {
        string str = "Hello";
        int i = 42;
    }

    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();

            var wf = new TestWF();
            wf.PrioritizeUiOverWorkflow();
            wf.StopWorkflowOnClosedUi();

            var strings = new String[] { "Aaaaa", "B", "Cccc" };
            this.multiPropertyControl1.GetProperty("Hello1").SetValue("B").SetValueRestrictions(strings).SetCustomFieldControl(button1, 1);
            this.multiPropertyControl1.GetProperty("Hello2").SetValueRestrictions(strings).SetValue("D").SetLabel("Hello2a").Rename("Hello2a");
            this.multiPropertyControl1.GetProperty("Hello3").MoveToPosition(0).SetValue("World3!",0).SetValue("xxx",1).SetValue("<<<", 3);
            this.multiPropertyControl1.GetProperty("Hello2a").SetReadOnly(true);
            button1.Click += (o, e) => this.multiPropertyControl1.SetReadOnly(false, "Hello2a");
            this.multiPropertyControl1.SetFieldColumnStyle(1, new ColumnStyle());
            this.multiPropertyControl1.GetProperty("Hello2a").SetVerifier(text => text == "D" || text == "B" );

            

            this.multiPropertyControl1.PropertyEventNotifier.ConnectTo(new ProcessingEndpoint<MultiPropertyControl.PropertyEventNotification>(msg =>
            {
                Log.FORCE($"Event: {msg.Event} Property:{msg.PropertyName}");
            }));

            SerializerTest serializerTest = new SerializerTest();

            button1.Click += (o, e) =>
            {

                serializerTest.TryWriteToStorage();
                serializerTest.TryUpdateFromStorage(false);

            };
            

            //var x = this.multiPropertyControl1.GetProperties();
            //int y = 0;

            _ = DelayedAction();
        }

        public async Task DelayedAction()
        {
            await Task.Delay(3.Seconds());

            using (this.LayoutSuspension())
            {
                for (int i = 0; i < 1000; i++)
                {
                    this.multiPropertyControl1.GetProperty($"extraProp{i}").SetValue($"val{i}");
                }
            }

        }
    }
}
