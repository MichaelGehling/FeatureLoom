using FeatureLoom.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace FeatureLoom.Forms.Forms
{
    public static class TooltipExtensions
    {
        private static LazyValue<ToolTip> sharedTooltip;
        private static Dictionary<Control, EventHandler> controlToTooltipEventHandler = new Dictionary<Control, EventHandler>();        

        public static void SetTooltip(this Control control, Func<string, string> controlTextToTooltip)
        {
            if (controlTextToTooltip != null)
            {
                if (!sharedTooltip.Exists)
                {
                    sharedTooltip.Obj.AutoPopDelay = 60_000;
                    sharedTooltip.Obj.InitialDelay = 500;
                    sharedTooltip.Obj.ReshowDelay = 200;
                    sharedTooltip.Obj.ShowAlways = true;
                }
                sharedTooltip.Obj.SetToolTip(control, controlTextToTooltip(control.Text));

                if (controlToTooltipEventHandler.TryGetValue(control, out EventHandler eventHandler))
                {
                    control.TextChanged -= eventHandler;
                }
                eventHandler = (object o, EventArgs e) => sharedTooltip.Obj.SetToolTip(control, controlTextToTooltip(control.Text));
                controlToTooltipEventHandler[control] = eventHandler;
                control.TextChanged += eventHandler;
            }
            else
            {
                if (sharedTooltip.Exists)
                {
                    if (controlToTooltipEventHandler.TryGetValue(control, out EventHandler eventHandler))
                    {
                        control.TextChanged -= eventHandler;
                    }
                    sharedTooltip.Obj.SetToolTip(control, "");
                }
            }
        }

        public static void SetTooltip(this Control control, string toolTipText)
        {
            if (toolTipText != null)
            {
                if (!sharedTooltip.Exists)
                {
                    sharedTooltip.Obj.AutoPopDelay = 60_000;
                    sharedTooltip.Obj.InitialDelay = 500;
                    sharedTooltip.Obj.ReshowDelay = 200;
                    sharedTooltip.Obj.ShowAlways = true;
                }

                if (controlToTooltipEventHandler.TryGetValue(control, out EventHandler eventHandler))
                {
                    control.TextChanged -= eventHandler;
                    controlToTooltipEventHandler.Remove(control);
                }

                sharedTooltip.Obj.SetToolTip(control, toolTipText);
            }
            else
            {
                if (sharedTooltip.Exists)
                {
                    if (controlToTooltipEventHandler.TryGetValue(control, out EventHandler eventHandler))
                    {
                        control.TextChanged -= eventHandler;
                    }
                    sharedTooltip.Obj.SetToolTip(control, "");
                }
            }
        }
    }
}
