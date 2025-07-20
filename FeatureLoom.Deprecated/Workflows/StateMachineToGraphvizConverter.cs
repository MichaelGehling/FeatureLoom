using FeatureLoom.Extensions;
using FeatureLoom.Workflows;

namespace FeatureLoom.Workflows
{
    public static class StateMachineToGraphvizConverter
    {
        public static string Convert(IStateMachineInfo stateMachine)
        {
            string result = $"digraph sm {{\n";
            result += $"label = \"{stateMachine.Name}\"\n";
            result += "labelloc = t\n";
            result += "splines = ortho\n";
            result += "ranksep=1\n";
            result += "node[shape = box]\n";
            var states = stateMachine.StateInfos;
            result += $"start [style = invis, height=0]\n";
            bool hasEnd = false;
            foreach (var state in states)
            {
                result += $"subgraph cluster_state{state.StateIndex.ToString()} {{\n";
                result += $"label=\"{state.Name}\"\n";
                var steps = state.StepInfos;
                result += "{ rank = \"same\"\n";
                result += $"state{state.StateIndex.ToString()}_anchor [label=\"\", style=invis, width=0]\n";
                foreach (var step in steps)
                {
                    string description = step.Description.TextWrap(20, "\\l") + "\\l";
                    result += $"state{state.StateIndex.ToString()}_step{step.StepIndex.ToString()} [label=\"{description}\"]\n";
                }
                result += "}\n";
                result += $"state{state.StateIndex.ToString()}_anchor->state{state.StateIndex.ToString()}_step0[style = invis]\n";
                foreach (var step in steps)
                {
                    result += $"state{state.StateIndex.ToString()}_step{step.StepIndex.ToString()}";
                    if (step.StepIndex + 1 < steps.Length) result += " -> ";
                }
                result += "\n";
                result += "}\n";
                foreach (var step in steps)
                {
                    var targets = step.TargetStates;
                    foreach (var target in targets)
                    {
                        result += $"state{state.StateIndex.ToString()}_step{step.StepIndex.ToString()} -> state{target.StateIndex.ToString()}_step0 [constraint=false]\n";
                    }
                    if (step.MayTerminate)
                    {
                        result += $"state{state.StateIndex.ToString()}_step{step.StepIndex.ToString()} -> end\n";
                        hasEnd = true;
                    }
                }
            }
            result += $"start -> state{stateMachine.StartStateInfo.StateIndex.ToString()}_step0\n";
            if (hasEnd) result += "end [style = invis, height=0]\n";

            foreach (var state in states)
            {
                result += $"state{state.StateIndex.ToString()}_anchor";
                if (state.StateIndex + 1 < states.Length) result += " -> ";
            }
            result += "[style = invis]\n";

            result += "}";
            return result;
        }
    }
}