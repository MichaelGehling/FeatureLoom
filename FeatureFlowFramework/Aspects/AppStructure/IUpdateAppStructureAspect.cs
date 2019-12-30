using System;
using System.Collections.Generic;
using System.Text;

namespace FeatureFlowFramework.Aspects.AppStructure
{
    public interface IUpdateAppStructureAspect
    {
        bool TryUpdateAppStructureAspects(TimeSpan timeout);
    }
}
