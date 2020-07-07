using FeatureFlowFramework.Helpers.Extensions;
using System;
using System.Collections.Generic;
using System.Windows.Forms;
using FeatureFlowFramework.Services.MetaData;

namespace FeatureFlowFramework.Helpers.Forms
{
    public static class LayoutSuspensionExtension
    {
        public static LayoutResumer LayoutSuspension(this Control control, bool recursive = true)
        {
            if(control.TryGetMetaData("LayoutSuspensionActive", out bool active) && active) return new LayoutResumer(null, null);
            else control.SetMetaData("LayoutSuspensionActive", true);

            control.SuspendLayout();
            List<Control> suspendedChildren = null;
            if(recursive)
            {
                suspendedChildren = new List<Control>();
                SuspendChildren(control, suspendedChildren);
            }

            return new LayoutResumer(control, suspendedChildren);

        }

        static void SuspendChildren(Control parent, IList<Control> suspendedChildren)
        {
            foreach(var child in parent.Controls)
            {
                if(child is Control childControl)
                {

                    if(childControl.TryGetMetaData("LayoutSuspensionActive", out bool active) && active) continue;
                    else childControl.SetMetaData("LayoutSuspensionActive", true);

                    childControl.SuspendLayout();
                    suspendedChildren.Add(childControl);
                    SuspendChildren(childControl, suspendedChildren);
                }
            }
        }


        public struct LayoutResumer : IDisposable
        {
            Control control;
            IEnumerable<Control> children;

            public LayoutResumer(Control control, IEnumerable<Control> children)
            {
                this.control = control;
                this.children = children;
            }

            public void Dispose()
            {
                control?.ResumeLayout();
                control?.SetMetaData("LayoutSuspensionActive", false);

                foreach(var child in children.EmptyIfNull())
                {
                    child?.ResumeLayout();
                    child?.SetMetaData("LayoutSuspensionActive", false);
                }
            }
        }
    }
}
