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

            this.multiPropertyControl1.SetProperty("Hello", "World!", button1);
            this.multiPropertyControl1.SetProperty("Hello2", "World2!");
        }
    }
}
