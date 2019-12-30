using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FeatureFlowFramework.DataFlows;
using FeatureFlowFramework.Workflows;

namespace FeatureFlowFramework.Aspects
{
    public class WorkflowInfoCollector
    {
        readonly Dictionary<long, AspectData> workflows = new Dictionary<long, AspectData>();
        readonly ProcessingEndpoint<AspectRegistry.ObjectAddedNotification> addListner;
        readonly ProcessingEndpoint<AspectRegistry.ObjectRemovedNotification> removeListner;
        readonly ProcessingEndpoint<AspectRegistry.ActivationStatusNotification> activationListner;

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
            lock(workflows)
            {
                foreach(var data in AspectRegistry.GetAllAspectData())
                {
                    if(data.TryGetAspectInterface(out Workflow wf)) workflows.Add(data.ObjectHandle, data);
                }
            }        
        }

        public IEnumerable<Workflow> AllWorkflows
        {
            get
            {
                lock(workflows)
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
