using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Union
{
    public interface IPropertyOrFieldInfo
    {
        object GetValue(object obj);

        void SetValue(object obj, object value);

        Type GetPropertyType();
    }

    public class FieldInfoAction : IPropertyOrFieldInfo
    {
        internal FieldInfo FieldInfo;

        // Нужен для присвоения типа интерфейса
        internal Type ReturnType;

        public FieldInfoAction(FieldInfo fieldInfo)
        {
            FieldInfo = fieldInfo;
            ReturnType = fieldInfo.FieldType.GetTypeInfo().IsInterface ? fieldInfo.FieldType : null;
        }

        Type IPropertyOrFieldInfo.GetPropertyType() => FieldInfo.FieldType;

        object IPropertyOrFieldInfo.GetValue(object obj)
        {
            var res = FieldInfo.GetValue(obj);
            if (res != null && ReturnType != null) res = new AutoWrap(res, ReturnType);
            return res;
        }

        void IPropertyOrFieldInfo.SetValue(object obj, object value) => FieldInfo.SetValue(obj, value);
    }

    public class PropertyInfoAction : IPropertyOrFieldInfo
    {
        internal PropertyInfo PropertyInfo;

        // Нужен для присвоения типа интерфейса
        internal Type ReturnType;

        public PropertyInfoAction(PropertyInfo propertyInfo)
        {
            this.PropertyInfo = propertyInfo;
            ReturnType = propertyInfo.PropertyType.GetTypeInfo().IsInterface ? propertyInfo.PropertyType : null;
        }

        object IPropertyOrFieldInfo.GetValue(object obj)
        {
            var res = PropertyInfo.GetValue(obj);
            if (res != null && ReturnType != null) res = new AutoWrap(res, ReturnType);
            return res;
        }

        void IPropertyOrFieldInfo.SetValue(object obj, object value)
        {
            PropertyInfo.SetValue(obj, value);
        }

        Type IPropertyOrFieldInfo.GetPropertyType() => PropertyInfo.PropertyType;
    }

    public class PropertiesAndMethodsFprType
    {
        private Type _type;
        public Dictionary<string, IPropertyOrFieldInfo> Properties = new Dictionary<string, IPropertyOrFieldInfo>();
        public Dictionary<string, AllMethodsForName> Methods = new Dictionary<string, AllMethodsForName>();
        public Dictionary<string, string> Synonyms;

        public PropertiesAndMethodsFprType(Type type)
        {
            _type = type;
        }

        internal void CheckForSynonym(ref string methodName)
        {
            if (Synonyms != null && Synonyms.TryGetValue(methodName, out var synonym)) methodName = synonym;
        }

        public void SetSynonym(string synonym, string methodName)
        {
            if (Synonyms == null) Synonyms = new Dictionary<string, string>();
            Synonyms[synonym] = methodName;
        }

        public AllMethodsForName FindAllMethodByName(string methodName)
        {
            CheckForSynonym(ref methodName);
            if (!Methods.TryGetValue(methodName, out var methods))
            {
                var methodInfos = _type.GetTypeInfo().GetMethods().Where(x => x.Name == methodName).ToArray();
                if (methodInfos.Length == 0) return null;

                methods = new AllMethodsForName(methodInfos);
                Methods[methodName] = methods;
            }

            return methods;
        }

        public RpcMethodInfo FindMethod(string methodName, bool isStatic, object[] methodParameters)
        {
            var allMethodByName = FindAllMethodByName(methodName);
            if (allMethodByName == null) return null;

            return allMethodByName.FindMethod(isStatic, methodParameters);
        }

        public RpcMethodInfo FindGenericMethod(string methodName, bool isStatic, Type[] genericParameters, Type[] methodParameters)
        {
            var allMethodByName = FindAllMethodByName(methodName);
            if (allMethodByName == null) return null;

            return allMethodByName.FindGenericMethod(isStatic, genericParameters, methodParameters);
        }

        public int MethodParametersCount(string methodName)
        {
            var allMethodByName = FindAllMethodByName(methodName);
            if (allMethodByName == null) return -1;

            return allMethodByName.MaxParamCount;
        }

        public IPropertyOrFieldInfo GetInfoMember(string propertyName)
        {
            CheckForSynonym(ref propertyName);
            var members = _type.GetTypeInfo().GetMember(propertyName);

            if (members.Length > 0)
            {
                var mi = members[0];

                FieldInfo fi = mi as FieldInfo;

                if (fi != null) return new FieldInfoAction(fi);

                PropertyInfo pi = mi as PropertyInfo;
                if (pi != null) return new PropertyInfoAction(pi);
            }

            return null;
        }

        public IPropertyOrFieldInfo FindPropertyByName(string propertyName)
        {
            CheckForSynonym(ref propertyName);
            if (!Properties.TryGetValue(propertyName, out var property))
            {
                property = GetInfoMember(propertyName);
                if (property == null) return null;
                Properties[propertyName] = property;
            }

            return property;
        }
    }

    public static class InformationOnTheTypes
    {
        public static Dictionary<Type, PropertiesAndMethodsFprType> PropertyAndMethods = new Dictionary<Type, PropertiesAndMethodsFprType>();
        public static Dictionary<Type, RpcTypeInfo> TypeInfos = new Dictionary<Type, RpcTypeInfo>();

        public static RpcTypeInfo GetTypeInformation(Type type)
        {
            if (!TypeInfos.TryGetValue(type, out var rpcTypeInfo))
            {
                rpcTypeInfo = new RpcTypeInfo(type);
                TypeInfos[type] = rpcTypeInfo;
            }
            return rpcTypeInfo;
        }

        public static PropertiesAndMethodsFprType FindPropertyAndMethodsForType(Type type)
        {
            if (!PropertyAndMethods.TryGetValue(type, out var typeMembers))
            {
                typeMembers = new PropertiesAndMethodsFprType(type);
                PropertyAndMethods[type] = typeMembers;
            }
            return typeMembers;
        }

        public static string GetMethodNameBySynonym(Type type, string methodName)
        {
            if (!PropertyAndMethods.TryGetValue(type, out var typeMember)) return methodName;
            typeMember.CheckForSynonym(ref methodName);
            return methodName;
        }

        public static void SetSynonym(Type type, string synonym, string methodName)
        {
            var typeMember = FindPropertyAndMethodsForType(type);
            typeMember.SetSynonym(synonym, methodName);
        }

        public static RpcMethodInfo FindMethod(Type type, bool isStatic, string methodName, params object[] methodParameters)
        {
            var typeMember = FindPropertyAndMethodsForType(type);
            return typeMember.FindMethod(methodName, isStatic, methodParameters);
        }

        public static RpcMethodInfo FindGenericMethodsWithGenericArguments(Type type, bool isStatic, string methodName,
            Type[] genericArguments, Type[] methodParams)
        {
            var typeMember = FindPropertyAndMethodsForType(type);
            return typeMember.FindGenericMethod(methodName, isStatic, genericArguments, methodParams);
        }

        public static int CountParametersForMethod(Type type, string methodName)
        {
            var typMember = FindPropertyAndMethodsForType(type);
            return typMember.MethodParametersCount(methodName);
        }

        public static IPropertyOrFieldInfo FindProperty(Type type, string propertyName)
        {
            var typeMember = FindPropertyAndMethodsForType(type);
            return typeMember.FindPropertyByName(propertyName);
        }
    }
}