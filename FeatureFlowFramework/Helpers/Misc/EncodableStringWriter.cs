using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace FeatureFlowFramework.Helpers.Misc
{
    public sealed class EncodableStringWriter : StringWriter
    {
        public override Encoding Encoding { get; }

        public EncodableStringWriter(Encoding encoding = default)
        {
            if(encoding == default) encoding = Encoding.UTF8;
            Encoding = encoding;
        }
    }
}
