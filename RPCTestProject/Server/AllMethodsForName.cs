namespace NetObjectToNative
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;

    public class CompareMethods : IComparer<RpcMethodInfo>
    {
        public int Compare(RpcMethodInfo x, RpcMethodInfo y)
        {
            int res = 0;
            for (int i = 0; i < x.Parameters.Count(); i++)
            {
                res = x.Parameters[i].CompareTo(y.Parameters[i]);

                if (res != 0) return res;
            }

            res = x.HasParams.CompareTo(y.HasParams);

            if (res != 0) return res;

            res = -x.ParamsCount.CompareTo(y.ParamsCount);
            if (res != 0) return res;

            return string.Compare(x.Method.ToString(), y.Method.ToString(), StringComparison.Ordinal);
        }
    }

    public class CompareMethodsParams : IComparer<RpcMethodInfo>
    {
        public int Compare(RpcMethodInfo x, RpcMethodInfo y)
        {
            int res = 0;
            res = -x.HasDefaultValue.CompareTo(y.HasDefaultValue);
            if (res != 0) return res;

            res = -x.ParametersCount.CompareTo(y.ParametersCount);
            if (res != 0) return res;

            for (int i = 0; i < x.Parameters.Count() - 1; i++)
            {
                res = x.Parameters[i].CompareTo(y.Parameters[i]);
                if (res != 0) return res;
            }

            res = x.HasParams.CompareTo(y.HasParams);
            if (res != 0) return res;

            return string.Compare(x.Method.ToString(), y.Method.ToString(), StringComparison.Ordinal);
        }
    }

    public class AllMethodsForName
    {
        public int MaxParamCount { get; private set; }

        private Dictionary<int, List<RpcMethodInfo>> _commonMethods = new Dictionary<int, List<RpcMethodInfo>>();

        private List<RpcMethodInfo> _methodsParams = new List<RpcMethodInfo>();

        private void AddMethod(RpcMethodInfo methodInfo, int parametersCount)
        {
            if (!_commonMethods.TryGetValue(parametersCount, out var methodList))
            {
                methodList = new List<RpcMethodInfo>();
                _commonMethods[parametersCount] = methodList;
            }

            methodList.Add(methodInfo);
        }

        private void AddParamsToList(KeyValuePair<int, List<RpcMethodInfo>>[] valueArray, RpcMethodInfo methodInfo)
        {
            int minCount = methodInfo.HasDefaultValue ? methodInfo.FirstDefaultParams : methodInfo.ParametersCount - 1;

            foreach (var keyValuePair in valueArray)
            {
                if (keyValuePair.Key < minCount) continue;

                if (!(methodInfo.HasDefaultValue && keyValuePair.Key >= methodInfo.ParametersCount))
                {
                    keyValuePair.Value.Add(new RpcMethodInfo(methodInfo, keyValuePair.Key));
                }
            }
        }

        private void AddParamsMethodToCommon()
        {
            var keyValuePairs = _commonMethods.OrderBy(x => x.Key).ToArray();
            foreach (var methodInfo in _methodsParams)
            {
                AddParamsToList(keyValuePairs, methodInfo);
            }
            foreach (var kv in keyValuePairs)
            {
                kv.Value.Sort(new CompareMethods());
            }
        }

        public AllMethodsForName(IEnumerable<MethodInfo> methods)
        {
            foreach (var method in methods)
            {
                var methodInfo = new RpcMethodInfo(method);

                if (methodInfo.HasParams || methodInfo.HasDefaultValue)
                {
                    _methodsParams.Add(methodInfo);
                    var commonMethod = new RpcMethodInfo(methodInfo);
                    AddMethod(commonMethod, commonMethod.ParametersCount);
                }
                else
                {
                    if (MaxParamCount < methodInfo.ParametersCount) MaxParamCount = methodInfo.ParametersCount;

                    AddMethod(methodInfo, methodInfo.ParametersCount);
                }
            }

            AddParamsMethodToCommon();

            if (_methodsParams.Any())
            {
                _methodsParams.Sort(new CompareMethodsParams());
                if (MaxParamCount < 16) MaxParamCount = 16;
            }
        }

        public RpcMethodInfo FindGenericMethod(bool isStatic, List<RpcMethodInfo> methodsList, Type[] parameters)
        {
            foreach (var method in methodsList)
            {
                if (method.IsGeneric && isStatic == method.Method.IsStatic)
                {
                    var methodInfo = method.GenericMethod.GetRealMethod(parameters);

                    if (methodInfo != null)
                    {
                        var res = new RpcMethodInfo(methodInfo);
                        if (res.Compare(parameters)) return res;
                    }
                }
            }

            return null;
        }

        public static Type[] GetTypesParameters(object[] parametersObjects)
        {
            Type[] parameters = new Type[parametersObjects.Length];

            for (var i = 0; i < parametersObjects.Length; i++)
            {
                if (parametersObjects[i] == null) parameters[i] = null;
                else parameters[i] = parametersObjects[i].GetType();
            }

            return parameters;
        }

        public RpcMethodInfo FindMethod(bool isStatic, object[] parametersObjects)
        {
            var parameters = GetTypesParameters(parametersObjects);

            if (_commonMethods.TryGetValue(parameters.Length, out var methodsList))
            {
                if (parameters.Length == 0)
                {
                    var method = methodsList[0];
                    if (!method.IsGeneric && isStatic == method.Method.IsStatic) return method;

                    return null;
                }

                foreach (var method in methodsList)
                {
                    if (!method.IsGeneric && isStatic == method.Method.IsStatic && method.Compare(parameters))
                        return method;
                }
            }

            foreach (var method in _methodsParams)
            {
                if (!method.IsGeneric && isStatic == method.Method.IsStatic && method.CompareParams(parameters))
                    return method;

                if (method.IsGeneric && isStatic == method.Method.IsStatic)
                {
                    var methodInfo = method.GenericMethod.GetRealMethodsParams(parameters, method);
                    var res = new RpcMethodInfo(methodInfo);

                    if (res.CompareParams(parameters)) return res;
                }
            }

            if (methodsList != null)
            {
                var res = FindGenericMethod(isStatic, methodsList, parameters);
                if (res != null) return res;
            }

            // AutoWrap.СообщитьОбОшибке("Метод существует но не подходят parametersObjects");
            return null;
        }

        public RpcMethodInfo FindGenericMethod(bool isStatic, Type[] genericParameters, Type[] methodParameters)
        {
            if (_commonMethods.TryGetValue(methodParameters.Length, out var methodList))
            {
                foreach (var method in methodList)
                {
                    if (method.IsGeneric && isStatic == method.Method.IsStatic)
                    {
                        // var MethodInfo = метод.GenericMethod.GetRealMethod(genericParameters, methodParameters);
                        var methodInfo = method.GenericMethod.MethodInfo.MakeGenericMethod(genericParameters);
                        if (methodInfo != null)
                        {
                            var res = new RpcMethodInfo(methodInfo);
                            if (res.Compare(methodParameters)) return res;
                        }
                    }
                }
            }

            foreach (var method in _methodsParams)
            {
                if (method.IsGeneric && isStatic == method.Method.IsStatic)
                {
                    var methodInfo = method.GenericMethod.MethodInfo.MakeGenericMethod(genericParameters);// метод.GenericMethod.GetRealMethod(genericParameters, methodParameters);
                    if (methodInfo != null)
                    {
                        var res = new RpcMethodInfo(methodInfo);
                        if (res.CompareParams(methodParameters)) return res;
                    }
                }
            }

            return null;
        }
    }
}