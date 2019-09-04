using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using Newtonsoft.Json;
using Formatting = Newtonsoft.Json.Formatting;

namespace NetObjectToNative
{
    public partial class NetObjectToNative
    {
      static public object JsonToObject(object typeOrig, string data)
        {

            Type type = TypeForCreateObject(typeOrig);

           return AutoWrap.WrapObject(JsonConvert.DeserializeObject(data, type));
            
        }

        static public object JsonToArray(object typeOrig, string data, int rank=0)
        {

            Type type = TypeForCreateObject(typeOrig);
            Type typeArray = null;
            if (rank > 0)
                typeArray = type.MakeArrayType(rank);
            else
                typeArray = type.MakeArrayType();

            return AutoWrap.WrapObject(JsonConvert.DeserializeObject(data, typeArray));

        }


        static public string ObjectToJson(object value,bool formattingIndented = false)
        {
            if (!formattingIndented)
                return JsonConvert.SerializeObject(value);

            return JsonConvert.SerializeObject(value, Formatting.Indented);
        }

        static public string ArrayToJson(object value)
        {

            return JsonConvert.SerializeObject(value);
        }

        static public Type GetJsonConvert()
        {

            return typeof(JsonConvert);
        }

    }
}
