using FeatureLoom.Extensions;
using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace FeatureLoom.Forms
{
    public static class LayoutSuspensionExtension
    {
        public static LayoutResumer LayoutSuspension(this Control control, bool recursive = true)
        {
            if (control.TryGetMetaData("LayoutSuspensionActive", out bool active) && active) return new LayoutResumer(null, null);
            else control.SetMetaData("LayoutSuspensionActive", true);

            control.SuspendLayout();
            List<Control> suspendedChildren = null;
            if (control is TreeView tree) tree.BeginUpdate();
            if (recursive)
            {
                suspendedChildren = new List<Control>();
                SuspendChildren(control, suspendedChildren);
            }

            return new LayoutResumer(control, suspendedChildren);
        }

        private static void SuspendChildren(Control parent, IList<Control> suspendedChildren)
        {
            foreach (var child in parent.Controls)
            {
                if (child is Control childControl)
                {
                    if (childControl.TryGetMetaData("LayoutSuspensionActive", out bool active) && active) continue;
                    else childControl.SetMetaData("LayoutSuspensionActive", true);

                    childControl.SuspendLayout();
                    if (childControl is TreeView tree) tree.BeginUpdate();
                    suspendedChildren.Add(childControl);
                    SuspendChildren(childControl, suspendedChildren);
                }
            }
        }

        public struct LayoutResumer : IDisposable
        {
            private Control control;
            private IEnumerable<Control> children;

            public bool IsValid => control != null;            

            public LayoutResumer(Control control, IEnumerable<Control> children)
            {
                this.control = control;
                this.children = children;
            }

            public void Dispose()
            {
                if (!IsValid) return;

                if (control is TreeView tree) tree.EndUpdate();
                control?.ResumeLayout();
                control?.SetMetaData("LayoutSuspensionActive", false);

                foreach (var child in children.EmptyIfNull())
                {
                    if (child is TreeView treeChild) treeChild.EndUpdate();
                    child?.ResumeLayout();
                    child?.SetMetaData("LayoutSuspensionActive", false);
                }

                control = null;
                children = null;
            }
        }
    }
}