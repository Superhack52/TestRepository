//namespace Union
//{
//    using Newtonsoft.Json;
//    using System;
//    using Formatting = System.Xml.Formatting;

//    public partial class NetObjectToNative
//    {
//        public static object JsonToObject(object typeOrig, string data) =>
//            AutoWrap.WrapObject(JsonConvert.DeserializeObject(data, TypeForCreateObject(typeOrig)));

//        public static object JsonToArray(object typeOrig, string data, int rank = 0)
//        {
//            Type type = TypeForCreateObject(typeOrig);
//            Type typeArray = null;
//            typeArray = rank > 0 ? type.MakeArrayType(rank) : type.MakeArrayType();

//            return AutoWrap.WrapObject(JsonConvert.DeserializeObject(data, typeArray));
//        }

//        public static string ObjectToJson(object value, bool formattingIndented = false)
//        {
//            if (!formattingIndented) return JsonConvert.SerializeObject(value);

//            return JsonConvert.SerializeObject(value, (Newtonsoft.Json.Formatting)Formatting.Indented);
//        }

//        public static string ArrayToJson(object value) => JsonConvert.SerializeObject(value);

//        public static Type GetJsonConvert() => typeof(JsonConvert);
//    }
//}