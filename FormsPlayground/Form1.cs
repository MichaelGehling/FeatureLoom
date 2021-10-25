using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace FormsPlayground
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();

            this.multiPropertyControl1.GetProperty("Hello").SetValue("B").SetValueRestrictions(new String[] { "Aaaaa", "B", "Cccc" }).SetCustomFieldControl(button1, 1);
            this.multiPropertyControl1.GetProperty("Hello2").SetValueRestrictions(new String[] { "A", "B", "C" }).SetValue("C");
            this.multiPropertyControl1.GetProperty("Hello3").SetValue("World3!");            
            this.multiPropertyControl1.SetReadOnly(true, "Hello2");
            button1.Click += (o, e) => this.multiPropertyControl1.SetReadOnly(false, "Hello2");
            this.multiPropertyControl1.SetFieldColumnStyle(1, new ColumnStyle());
            this.multiPropertyControl1.GetProperty("Hello2").SetVerifier(text => text == "A" || text == "B" );
        }
    }
}
