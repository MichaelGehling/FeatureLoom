using FeatureFlowFramework.Helper;
using FeatureFlowFramework.Logging;
using System;

namespace FeatureFlowFramework.DataFlows
{
    public class ToJsonConverter : MessageConverter<object, string>
    {
        public ToJsonConverter() : base(convert)
        {
        }

        private static Func<object, string> convert = obj =>
          {
              string json = null;
              try
              {
                  json = obj.ToJson(Json.ComplexObjectsStructure_SerializerSettings);
              }
              catch (Exception e)
              {
                  Log.ERROR($"Serializing object to Json in ToJsonConverter failed.", $"Object: {obj} \n Exception: {e}");
              }
              return json;
          };
    }

    public class FromJsonConverter : MessageConverter<string, object>
    {
        public FromJsonConverter() : base(convert)
        {
        }

        private static Func<string, object> convert = json =>
          {
              object obj = null;
              try
              {
                  obj = json.FromJson<object>(Json.ComplexObjectsStructure_SerializerSettings);
              }
              catch (Exception e)
              {
                  Log.ERROR($"Deserializing object from Json in FromJsonConverter failed.", $"Json: {json} \n Exception: {e}");
              }
              return json;
          };
    }
}