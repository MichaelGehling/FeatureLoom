using System;

namespace FeatureFlowFramework.Aspects.AppStructure
{
    public interface IUpdateAppStructureAspect
    {
        bool TryUpdateAppStructureAspects(TimeSpan timeout);
    }
}