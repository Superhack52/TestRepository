namespace NetObjectToNative
{
    using System;
    using System.Collections.Generic;
    using System.Reflection;

    public class GenericMethodData
    {
        private Type[] _genericArguments;
        private List<List<int>> _outParameters;
        private bool _canPrint;
        internal MethodInfo MethodInfo;
        private RpcTypeInfo[] _rpcParameters;

        public GenericMethodData(MethodInfo methodInfo, RpcTypeInfo[] rpcParameters)
        {
            _rpcParameters = rpcParameters;
            _genericArguments = methodInfo.GetGenericArguments();
            _canPrint = FindParameterToPrint();
            MethodInfo = methodInfo;
        }

        public static bool CanCreateInstanceUsingDefaultConstructor(TypeInfo type) =>
            type.IsValueType || (!type.IsAbstract && type.GetConstructor(Type.EmptyTypes) != null);

        private static bool ContainsAnyFlag(GenericParameterAttributes attributes, GenericParameterAttributes flags) =>
            (attributes & flags) != GenericParameterAttributes.None;

        public static bool IsHasConstraints(TypeInfo type, TypeInfo parameters)
        {
            GenericParameterAttributes constraints = type.GenericParameterAttributes &
                GenericParameterAttributes.SpecialConstraintMask;

            if (constraints == GenericParameterAttributes.None) return true;

            if (ContainsAnyFlag(constraints, GenericParameterAttributes.ReferenceTypeConstraint))
            {
                if (!parameters.IsClass && !parameters.IsInterface) return false;
            }

            if (ContainsAnyFlag(constraints, GenericParameterAttributes.NotNullableValueTypeConstraint))
            {
                if (!parameters.IsValueType) return false;

                if (parameters.IsGenericType && parameters.GetGenericTypeDefinition() != typeof(Nullable<>))
                    return false;
            }

            if (ContainsAnyFlag(constraints, GenericParameterAttributes.DefaultConstructorConstraint))
            {
                if (!CanCreateInstanceUsingDefaultConstructor(parameters)) return false;
            }

            return true;
        }

        public static bool IsSuit(Type genericType, Type type)
        {
            var typeInfo = genericType.GetTypeInfo();
            if (genericType.IsConstructedGenericType && typeInfo.ContainsGenericParameters)
                return type.IsGenericTypeOf(typeInfo, genericType);

            if (!typeInfo.IsGenericParameter) return false;

            bool hasConstraints = false;
            Type[] tpConstraints = typeInfo.GetGenericParameterConstraints();
            foreach (Type tpc in tpConstraints)
            {
                hasConstraints = true;
                var constraintType = tpc.GetTypeInfo();

                if (constraintType.ContainsGenericParameters)
                {
                    if (IsSuit(tpc, type)) return true;
                }
                else if (tpc.IsAssignableFrom(type)) return true;
            }

            if (hasConstraints) return false;

            return IsHasConstraints(typeInfo, type.GetTypeInfo());
        }

        public bool FindParameterToPrint()
        {
            bool res = true;
            _outParameters = new List<List<int>>();

            foreach (Type tParam in _genericArguments)
            {
                var matchingParam = false;
                var index = new List<int>();
                _outParameters.Add(index);
                int i = 0;
                foreach (var Param in _rpcParameters)
                {
                    if (tParam.IsSimilarType(Param.Type))
                    {
                        matchingParam = true;
                        index.Add(i);
                    }
                    i++;
                }

                if (!matchingParam) res = false;
            }
            return res;
        }

        private static Type GetRealType(Type argumentType, Type parameterType, Type realType)
        {
            if (argumentType == parameterType) return realType;

            // Handle array types
            if (parameterType.IsArray) return GetRealType(argumentType, parameterType.GetElementType(), realType.GetElementType());
            // Handle any generic arguments
            if (parameterType.GetTypeInfo().IsGenericType)
            {
                Type[] arguments = parameterType.GetTypeInfo().GetGenericArguments();
                Type[] argumentsReal = realType.GetTypeInfo().GetGenericArguments();
                for (int i = 0; i < arguments.Length; ++i)
                {
                    var res = GetRealType(argumentType, arguments[i], argumentsReal[i]);
                    if (res != null) return res;
                }
            }

            return null;
        }

        private Type[] PrintTypes(Type[] parameters)
        {
            // Сравним parameters без вывода
            for (var i = 0; i < Math.Min(parameters.Length, _rpcParameters.Length); i++)
            {
                var parameter = parameters[i];
                if (parameter == null) continue;

                var rpcParameter = _rpcParameters[i];
                var parameterType = rpcParameter.Type;
                bool isSuit = false;
                if (rpcParameter.IsGenericType)
                {
                    isSuit = parameter.GetTypeInfo().IsGenericType
                        ? parameter.IsGenericTypeOf(parameterType.GetTypeInfo(), parameterType)
                        : IsSuit(parameterType, parameter);
                }
                else isSuit = rpcParameter.IsEqual(parameter);
                if (!isSuit) return null;
            }

            var realTypesArguments = new Type[_genericArguments.Length];

            for (var i = 0; i < _genericArguments.Length; i++)
            {
                var argument = _genericArguments[i];
                var outIndexes = _outParameters[i];

                if (outIndexes.Count == 0) continue;

                foreach (var index in outIndexes)
                {
                    var realType = GetRealType(argument, _rpcParameters[index].Type, parameters[index]);

                    if (realType != null)
                    {
                        if (realTypesArguments[i] != null && realTypesArguments[i] != realType) return null;
                        realTypesArguments[i] = realType;
                    }
                }
                if (realTypesArguments[i] == null) return null;
            }

            return realTypesArguments;
        }

        public MethodInfo GetRealMethod(Type[] parameters)
        {
            if (!_canPrint) return null;

            // Сравним parameters без вывода
            for (var i = 0; i < _rpcParameters.Length; i++)
            {
                var parameter = parameters[i];
                var rpcParameter = _rpcParameters[i];
                var rpcTypeParameter = rpcParameter.Type;
                bool isSuit = false;
                if (rpcParameter.IsGenericType)
                {
                    isSuit = parameter.GetTypeInfo().IsGenericType ?
                        parameter.IsGenericTypeOf(rpcTypeParameter.GetTypeInfo(), rpcTypeParameter)
                        : IsSuit(rpcTypeParameter, parameter);
                }
                else isSuit = rpcParameter.IsEqual(parameter);
                if (!isSuit) return null;
            }

            var realTypesArguments = new Type[_genericArguments.Length];

            for (var i = 0; i < _genericArguments.Length; i++)
            {
                var argument = _genericArguments[i];
                var outIndexes = _outParameters[i];

                foreach (var index in outIndexes)
                {
                    var realType = GetRealType(argument, _rpcParameters[index].Type, parameters[index]);

                    if (realType != null)
                    {
                        if (realTypesArguments[i] != null && realTypesArguments[i] != realType) return null;
                        realTypesArguments[i] = realType;
                    }
                }
                if (realTypesArguments[i] == null) return null;
            }

            try
            {
                return MethodInfo.MakeGenericMethod(realTypesArguments); ;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private bool CompareParam(Type parameter, RpcTypeInfo rpcParameter)
        {
            var rpcParameterType = rpcParameter.Type;
            bool isSuit = false;
            if (rpcParameter.IsGenericType)
            {
                isSuit = parameter.GetTypeInfo().IsGenericType
                    ? parameter.IsGenericTypeOf(rpcParameterType.GetTypeInfo(), rpcParameterType)
                    : IsSuit(rpcParameterType, parameter);
            }
            else isSuit = rpcParameter.IsEqual(parameter);
            if (!isSuit) return false;

            return true;
        }

        public MethodInfo GetRealMethodsParams(Type[] parameters, RpcMethodInfo rpcMethodInfo)
        {
            if (!_canPrint) return null;

            if (rpcMethodInfo.HasDefaultValue)
            {
                if ((parameters.Length < rpcMethodInfo.FirstDefaultParams) || parameters.Length > rpcMethodInfo.ParametersCount)
                    return null;
            }

            int length = Math.Min(_rpcParameters.Length, parameters.Length);
            if (rpcMethodInfo.HasParams)
            {
                length = rpcMethodInfo.ParametersCount - 1;
                if (parameters.Length >= rpcMethodInfo.ParametersCount)
                {
                    if (!CompareParam(parameters[parameters.Length - 1], rpcMethodInfo.ElementType)) return null;
                }
            }

            // Сравним parameters без вывода
            for (var i = 0; i < length; i++)
            {
                var parameter = parameters[i];
                var rpcParameter = _rpcParameters[i];

                if (!CompareParam(parameter, rpcParameter)) return null;
            }

            var realTypeArguments = new Type[_genericArguments.Length];

            for (var i = 0; i < _genericArguments.Length; i++)
            {
                var argument = _genericArguments[i];
                var outIndexes = _outParameters[i];

                foreach (var index in outIndexes)
                {
                    var realType = GetRealType(argument, _rpcParameters[index].Type, parameters[index]);

                    if (realType != null)
                    {
                        if (realTypeArguments[i] != null && realTypeArguments[i] != realType) return null;
                        realTypeArguments[i] = realType;
                    }
                }
                if (realTypeArguments[i] == null) return null;
            }
            try
            {
                return MethodInfo.MakeGenericMethod(realTypeArguments); ;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public MethodInfo GetRealMethod(Type[] genericTypes, Type[] parameters)
        {
            if (genericTypes.Length != _genericArguments.Length) return null;

            var realArgumentType = PrintTypes(parameters);
            if (realArgumentType == null) return null;

            for (int i = 0; i < genericTypes.Length; i++)
            {
                var type = realArgumentType[i];
                if (type != null && type != genericTypes[i]) return null;
            }
            try
            {
                return MethodInfo.MakeGenericMethod(genericTypes); ;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }

    public static class SearchGenericTypes
    {
        public static bool IsGenericTypeOf(this Type t, TypeInfo typeInfo, Type genericDefinition) =>
            IsGenericTypeOf(t, genericDefinition, typeInfo, out var interfaceType);

        public static bool IsGenericTypeOf(this Type t, Type genericDefinition, TypeInfo typeInfo, out Type interfaceType)
        {
            interfaceType = null;
            if (t == null) return false;
            if (t.GetTypeInfo().IsGenericType && t.GetGenericTypeDefinition() == genericDefinition.GetGenericTypeDefinition()) return true;
            if (t.GetTypeInfo().BaseType != null)
            {
                var baseType = t.GetTypeInfo().BaseType;
                if (baseType != null && baseType.IsGenericTypeOf(genericDefinition, typeInfo, out interfaceType))
                {
                    if (interfaceType == null) interfaceType = baseType;
                    return true;
                }
            }

            if (typeInfo.IsInterface)
            {
                foreach (var i in t.GetTypeInfo().GetInterfaces())
                {
                    if (i.IsGenericTypeOf(genericDefinition, typeInfo, out interfaceType))
                    {
                        if (interfaceType == null) interfaceType = i;
                        return true;
                    }
                }
            }

            return false;
        }

        public static bool IsSimilarType(this Type thisType, Type type)
        {
            // Ignore any 'ref' types
            if (type.IsByRef) type = type.GetElementType();
            // Handle array types
            if (type.IsArray) return thisType.IsSimilarType(type.GetElementType());
            if (thisType == type) return true;
            // Handle any generic arguments
            if (type.GetTypeInfo().IsGenericType)
            {
                Type[] arguments = type.GetTypeInfo().GetGenericArguments();
                foreach (var argumentType in arguments)
                    if (thisType.IsSimilarType(argumentType)) return true;
            }

            return false;
        }

        public static bool FindParameterForOutput(this MethodInfo methodInfo, out List<List<int>> res)
        {
            res = new List<List<int>>();
            var genericTypes = methodInfo.GetGenericArguments();
            ParameterInfo[] parameterInfos = methodInfo.GetParameters();

            foreach (Type tParam in genericTypes)
            {
                var matchingParam = false;
                var index = new List<int>();
                res.Add(index);
                int i = 0;
                foreach (var Param in parameterInfos)
                {
                    if (tParam.IsSimilarType(Param.ParameterType))
                    {
                        matchingParam = true;
                        index.Add(i);
                    }
                    i++;
                }

                if (!matchingParam)
                {
                    res = null;
                    return false;
                }
            }
            return true;
        }
    }
}