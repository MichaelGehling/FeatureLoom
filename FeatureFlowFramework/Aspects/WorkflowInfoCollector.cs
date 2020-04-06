using FeatureFlowFramework.DataFlows;
using FeatureFlowFramework.Helper;
using FeatureFlowFramework.Workflows;
using System.Collections.Generic;
using System.Linq;

namespace FeatureFlowFramework.Aspects
{
    public class WorkflowInfoCollector
    {
        private readonly Dictionary<long, AspectData> workflows = new Dictionary<long, AspectData>();
        private readonly ProcessingEndpoint<AspectRegistry.ObjectAddedNotification> addListner;
        private readonly ProcessingEndpoint<AspectRegistry.ObjectRemovedNotification> removeListner;
        private readonly ProcessingEndpoint<AspectRegistry.ActivationStatusNotification> activationListner;

        FeatureLock workflowsLock = new FeatureLock();

        public WorkflowInfoCollector()
        {
            addListner = new ProcessingEndpoint<AspectRegistry.ObjectAddedNotification>(msg => workflows.Add(msg.objectHandle, msg.aspectData), workflows);
            removeListner = new ProcessingEndpoint<AspectRegistry.ObjectRemovedNotification>(msg => workflows.Remove(msg.objectHandle), workflows);
            activationListner = new ProcessingEndpoint<AspectRegistry.ActivationStatusNotification>(msg =>
            {
                if(!msg.isActive) workflows.Clear();
            }, workflows);

            AspectRegistry.NotificationSource.ConnectTo(addListner);
            AspectRegistry.NotificationSource.ConnectTo(removeListner);
            AspectRegistry.NotificationSource.ConnectTo(activationListner);
            using(workflowsLock.ForWriting())
            {
                foreach (var data in AspectRegistry.GetAllAspectData())
                {
                    if (data.TryGetAspectInterface(out Workflow wf)) workflows.Add(data.ObjectHandle, data);
                }
            }
        }

        public IEnumerable<Workflow> AllWorkflows
        {
            get
            {
                using (workflowsLock.ForReading())
                {
                    return workflows.Values.Select(data =>
                    {
                        data.TryGetAspectInterface(out Workflow wf);
                        return wf;
                    }).Where(wf => wf != default);
                }
            }
        }

        private void AddStateInfo(AspectData aspectData)
        {
            //aspectData.TryGetAspectInterface(out IWorkflowInfo)
        }
    }
}