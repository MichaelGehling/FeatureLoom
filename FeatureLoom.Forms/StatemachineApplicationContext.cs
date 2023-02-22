using FeatureLoom.MessageFlow;
using System;
using System.Windows.Forms;
using FeatureLoom.Statemachines;
using System.Threading;

namespace FeatureLoom.Forms
{
    public class StatemachineApplicationContext<T> : ApplicationContext where T : class
    {
        private Statemachine<T> _statemachine;
        private Statemachine<T>.Job _job;
        private CancellationTokenSource _cts = new CancellationTokenSource();

        public StatemachineApplicationContext(Statemachine<T> statemachine, T context)
        {
            _statemachine = statemachine;
            _job = _statemachine.CreateJob(context);            
            _job.UpdateSource.ProcessMessage<IStatemachineJob>(j =>
            {
                Application.DoEvents();
                if (Application.OpenForms.Count == 0) _cts.Cancel();
                if (j.IsCompleted) Application.ExitThread();
            });

            Application.Idle += StartJob;
        }

        public void StartJob(object sender, EventArgs e)
        {
            Application.Idle -= StartJob;
            _statemachine.StartJob(_job, _cts.Token);
        }
    }
}