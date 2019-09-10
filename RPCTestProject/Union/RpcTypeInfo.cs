using System;
using System.Reflection;

namespace Union
{
    public class RpcTypeInfo : IComparable<RpcTypeInfo>
    {
        public Type Type;
        private bool _isByRef;
        private bool _isValue;
        private int _hierarchyLevel;
        private bool _isNullable;
        public bool IsGenericType;

        public RpcTypeInfo(Type type)
        {
            var typeInfo = type.GetTypeInfo();
            _isByRef = typeInfo.IsByRef;

            IsGenericType = (typeInfo.IsGenericType && typeInfo.IsGenericTypeDefinition) || typeInfo.ContainsGenericParameters;

            if (_isByRef)
            {
                Type = type.GetElementType();
                typeInfo = Type.GetTypeInfo();
            }
            else
                Type = type;

            _isValue = typeInfo.IsValueType;

            if (_isValue)
            {
                _hierarchyLevel = 0;
                if (typeInfo.IsGenericType && typeInfo.GetGenericTypeDefinition() == typeof(Nullable<>))
                {
                    _isNullable = true;
                    Type = typeInfo.GenericTypeArguments[0];
                }
            }
            else _hierarchyLevel = FindLvl(0, Type);
        }

        private static int FindLvl(int lvl, Type type)
        {
            if (type == null) return -1;// всякие char*
            if (type == typeof(object)) return lvl;

            return FindLvl(lvl + 1, type.GetTypeInfo().BaseType);
        }

        public int CompareTo(RpcTypeInfo elem)
        {
            int res = -_isByRef.CompareTo(elem._isByRef);

            if (res != 0) return res;

            if (Type == elem.Type) return 0;

            res = -_isValue.CompareTo(elem._isValue);

            if (res != 0) return res;

            if (_isValue && elem._isValue)
            {
                res = _isNullable.CompareTo(elem._isNullable);
                if (res != 0) return res;
            }

            res = -_hierarchyLevel.CompareTo(elem._hierarchyLevel);

            if (res != 0) return res;

            return string.Compare(Type.ToString(), elem.Type.ToString(), StringComparison.Ordinal);
        }

        public bool IsEqual(Type type)
        {
            if (type == null) return !_isValue || _isNullable;
            // или использовать IsInstanceOfType
            if (_isValue) return Type == type;

            return Type.GetTypeInfo().IsAssignableFrom(type);
        }
    }

    public class RpcMethodInfo
    {
        public MethodInfo Method;
        public RpcTypeInfo[] Parameters;
        public int ParametersCount;
        public bool HasParams;
        public bool HasDefaultValue;
        public int FirstDefaultParams;
        public Type TypeParams;
        public int ParamsCount;
        public RpcTypeInfo ElementType;
        public bool IsGeneric;
        public GenericMethodData GenericMethod;
        public Type ReturnType;

        public RpcMethodInfo(MethodInfo methodInfo)
        {
            Method = methodInfo;

            ParameterInfo[] parameters = Method.GetParameters();
            HasParams = false;
            ParametersCount = parameters.Length;
            ParamsCount = 0;
            if (ParametersCount > 0)
            {
                HasParams = parameters[parameters.Length - 1].GetCustomAttributes(typeof(ParamArrayAttribute), false).GetEnumerator().MoveNext();
            }

            if (HasParams)
            {
                TypeParams = parameters[parameters.Length - 1].ParameterType.GetElementType();
                ElementType = InformationOnTheTypes.GetTypeInformation(TypeParams);
            }

            Parameters = new RpcTypeInfo[ParametersCount];

            for (int i = 0; i < parameters.Length; i++)
            {
                var param = parameters[i];
                Parameters[i] = InformationOnTheTypes.GetTypeInformation(param.ParameterType);

                if (!HasDefaultValue && param.HasDefaultValue)
                {
                    HasDefaultValue = true;

                    FirstDefaultParams = i;
                }
            }

            IsGeneric = methodInfo.IsGenericMethod && methodInfo.IsGenericMethodDefinition;

            if (IsGeneric) GenericMethod = new GenericMethodData(Method, Parameters);
            ReturnType = methodInfo.ReturnType.GetTypeInfo().IsInterface ? methodInfo.ReturnType : null;
        }

        public RpcMethodInfo(RpcMethodInfo methodInfo, int parametersCount)
        {
            Method = methodInfo.Method;
            ParametersCount = parametersCount;
            ParamsCount = methodInfo.ParametersCount;
            ReturnType = methodInfo.ReturnType;

            Parameters = new RpcTypeInfo[parametersCount];

            var count = methodInfo.HasDefaultValue ? parametersCount : ParamsCount - 1;
            for (int i = 0; i < count; i++)
            {
                Parameters[i] = methodInfo.Parameters[i];
            }

            if (methodInfo.HasDefaultValue)
            {
                HasDefaultValue = true;
                FirstDefaultParams = methodInfo.FirstDefaultParams;
                return;
            }

            HasParams = true;
            TypeParams = methodInfo.TypeParams;
            ElementType = methodInfo.ElementType;

            var rpcTypeInfo = InformationOnTheTypes.GetTypeInformation(methodInfo.TypeParams);

            for (int i = ParamsCount - 1; i < parametersCount; i++)
            {
                Parameters[i] = rpcTypeInfo;
            }
        }

        // Добавить парамс как обычный метод
        public RpcMethodInfo(RpcMethodInfo methodInfo)
        {
            Method = methodInfo.Method;
            ParametersCount = methodInfo.ParametersCount;
            ParamsCount = 0;
            HasParams = false;
            HasDefaultValue = false;
            ReturnType = methodInfo.ReturnType;
            Parameters = methodInfo.Parameters;
        }

        public bool Compare(Type[] parameters)
        {
            for (int i = 0; i < parameters.Length; i++)
            {
                if (!Parameters[i].IsEqual(parameters[i])) return false;
            }

            return true;
        }

        public bool CompareDefault(Type[] parameters)
        {
            if ((parameters.Length < FirstDefaultParams) || parameters.Length > ParametersCount) return false;
            return Compare(parameters);
        }

        public bool CompareParams(Type[] parameters)
        {
            if (HasDefaultValue) return CompareDefault(parameters);

            var lastParameterIndex = ParametersCount - 1;

            if (parameters.Length < lastParameterIndex) return false;

            for (int i = 0; i < lastParameterIndex; i++)
            {
                if (!Parameters[i].IsEqual(parameters[i])) return false;
            }

            if (parameters.Length == ParametersCount && parameters[lastParameterIndex] == Parameters[ParametersCount - 1].Type)
                return true;

            for (int i = lastParameterIndex; i < parameters.Length; i++)
            {
                if (!ElementType.IsEqual(parameters[i])) return false;
            }

            return true;
        }

        public object Invoke(object target, object[] input) => Method.Invoke(target, input);

        public object InvokeWithDefaultParameters(object target, object[] input, int parametersCount)
        {
            if (input.Length == parametersCount) return Invoke(target, input);
            object[] parametersValue = new object[parametersCount];
            ParameterInfo[] parameters = Method.GetParameters();
            Array.Copy(input, parametersValue, input.Length);

            for (int i = input.Length; i < parameters.Length; i++)
            {
                parametersValue[i] = parameters[i].RawDefaultValue;
            }

            var res = Invoke(target, parametersValue);

            Array.Copy(parametersValue, input, input.Length);

            if (res != null && ReturnType != null) res = new AutoWrap(res, ReturnType);
            return res;
        }

        public object ExecuteMethod(object Target, object[] input)
        {
            if (!(HasParams || HasDefaultValue))
                return Invoke(Target, input);

            int parametersCount = (ParamsCount > 0) ? ParamsCount : ParametersCount;

            if (HasDefaultValue) return InvokeWithDefaultParameters(Target, input, parametersCount);

            int lastParameterIndex = parametersCount - 1;

            object[] realParams = new object[parametersCount];
            for (int i = 0; i < lastParameterIndex; i++) realParams[i] = input[i];

            Array parameters = Array.CreateInstance(TypeParams, input.Length - lastParameterIndex);
            for (int i = 0; i < parameters.Length; i++) parameters.SetValue(input[i + lastParameterIndex], i);

            realParams[lastParameterIndex] = parameters;

            var res = Invoke(Target, realParams);
            parameters = (Array)realParams[lastParameterIndex];
            for (int i = 0; i < parameters.Length; i++) input[i + lastParameterIndex] = parameters.GetValue(i);
            if (res != null && ReturnType != null) res = new AutoWrap(res, ReturnType);
            return res;
        }
    }
}