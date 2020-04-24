using FeatureFlowFramework.Workflows;
using System.Windows.Forms;

namespace FeatureFlowFramework.Helper
{
    public static partial class FormsExtensions
    {
        public static Control FindFocusedControl(this Control control)
        {
            var container = control as IContainerControl;
            while(container != null)
            {
                control = container.ActiveControl;
                container = control as IContainerControl;
            }
            return control;
        }

        public static ApplicationContext RunAsApplicationContext(this IWorkflowControls workflow)
        {
            return new WorkflowApplicationContext(workflow);
        }

        public static bool IsShown(this Form myForm)
        {
            foreach(Form form in Application.OpenForms)
            {
                if(myForm == form) return true;
            }
            return false;
        }
    }
}