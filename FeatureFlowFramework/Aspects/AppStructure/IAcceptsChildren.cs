using System;
using System.Collections.Generic;
using System.Text;

namespace FeatureFlowFramework.Aspects.AppStructure
{

    public interface IAcceptsChildren : IHasChildren
    {
        IAcceptsChildren AddChild(object child, string childName = null);
        IAcceptsChildren RemoveChild(object child);
        IAcceptsChildren RemoveAllChildren();
    }

}
