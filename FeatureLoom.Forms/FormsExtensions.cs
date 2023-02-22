using FeatureLoom.MessageFlow;
using System;
using System.Windows.Forms;

namespace FeatureLoom.Forms
{
    public static partial class FormsExtensions
    {
        public static Control FindFocusedControl(this Control control)
        {
            var container = control as IContainerControl;
            while (container != null)
            {
                control = container.ActiveControl;
                container = control as IContainerControl;
            }
            
            return control.Focused ? control : null;
        }



        public static bool IsShown(this Form myForm)
        {
            foreach (Form form in Application.OpenForms)
            {
                if (myForm == form) return true;
            }
            return false;
        }

        public static T FindParent<T>(this Control control, string name = null) where T : Control
        {
            Control parent = control.Parent;
            while (parent != null && !(parent is T) && (name == null || parent.Name != name)) parent = parent.Parent;
            return parent as T;
        }

        public static ControMaskOut GetMaskOut(this Control control) => new ControMaskOut(control);

        public static void HandleUiOnMessage(this IMessageSource source)
        {
            source.ConnectTo(new ProcessingEndpoint<object>(_ => Application.DoEvents()));
        }

    }    

    public struct ControMaskOut : IDisposable
    {
        Control control;

        public ControMaskOut(Control control)
        {
            this.control = control;            
            control.Hide();
        }

        public void Dispose()
        {
            control.Show();
        }
    }
}