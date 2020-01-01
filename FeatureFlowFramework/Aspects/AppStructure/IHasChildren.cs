using System.Collections.Generic;

namespace FeatureFlowFramework.Aspects.AppStructure
{
    public interface IHasChildren
    {
        IEnumerable<object> Children { get; }
    }
}