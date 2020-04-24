using FeatureFlowFramework.Helper;
using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace FeatureFlowFramework.Helper
{
    public static class LayoutSuspensionExtension
    {
        public static LayoutResumer LayoutSuspension(this Control control, bool recursive = false)
        {
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
                foreach(var child in children.EmptyIfNull())
                {
                    child?.ResumeLayout();
                }
            }
        }
    }
}
