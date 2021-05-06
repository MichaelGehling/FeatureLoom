using FeatureLoom.Workflows;
using System.Windows.Forms;

namespace FeatureLoom.Helpers.Forms
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
            return control;
        }

        public static ApplicationContext RunAsApplicationContext(this Workflow workflow)
        {
            return new WorkflowApplicationContext(workflow);
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
    }
}