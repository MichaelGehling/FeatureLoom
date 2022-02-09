using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace FeatureLoom.Web
{
    public interface IWebRequestInterceptor
    {        
        Task<bool> InterceptRequestAsync(IWebRequest request, IWebResponse response);
    }
}
