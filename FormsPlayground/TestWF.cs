using FeatureLoom.Workflows;

namespace FormsPlayground
{
    public class TestWF : Workflow<TestWF.SM>
    {
        long currentIteration = -1;

        public class SM : StateMachine<TestWF>
        {
            protected override void Init()
            {
                var run = State("Run");

                run.Build()
                    .Step()
                        .Do(c => { var x = c.currentIteration; })
                    .Step()
                        .Do(c => c.currentIteration++)
                    .Step()
                        .Loop();
            }
        }
    }
}
