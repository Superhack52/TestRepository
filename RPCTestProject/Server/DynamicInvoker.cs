namespace NetObjectToNative
{
    using Microsoft.CSharp.RuntimeBinder;
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Dynamic;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Reflection;
    using System.Runtime.CompilerServices;
    using System.Text;
    using System.Threading.Tasks;

    public class DynamicInvoker
    {
        public static object InvokeMember(object target, string methodName, params object[] args)
        {
            var targetParam = CSharpArgumentInfo.Create(CSharpArgumentInfoFlags.None, null);
            CSharpArgumentInfo[] parameterFlags = new CSharpArgumentInfo[args.Length + 1];
            Expression[] parameters = new Expression[args.Length + 1];
            parameterFlags[0] = targetParam;
            parameters[0] = Expression.Constant(target);
            for (int i = 0; i < args.Length; i++)
            {
                parameterFlags[i + 1] = CSharpArgumentInfo.Create(CSharpArgumentInfoFlags.Constant | CSharpArgumentInfoFlags.UseCompileTimeType, null);
                parameters[i + 1] = Expression.Constant(args[i]);
            }
            var csb = Microsoft.CSharp.RuntimeBinder.Binder.InvokeMember(CSharpBinderFlags.None, methodName, null, typeof(DynamicInvoker), parameterFlags);
            var de = DynamicExpression.Dynamic(csb, typeof(object), parameters);
            LambdaExpression expr = System.Linq.Expressions.Expression.Lambda(de);
            return expr.Compile().DynamicInvoke();
        }

        public static object GetValue(object target, string name)
        {
            CallSite<Func<CallSite, object, object>> callSite = CallSite<Func<CallSite, object, object>>.Create(Microsoft.CSharp.RuntimeBinder.Binder.GetMember(CSharpBinderFlags.None, name, typeof(DynamicInvoker), new CSharpArgumentInfo[] { CSharpArgumentInfo.Create(CSharpArgumentInfoFlags.None, null) }));

            return callSite.Target(callSite, target);
        }

        public static void SetValue(object target, string name, object value)
        {
            CallSite<Func<CallSite, object, object, object>> callSite = CallSite<Func<CallSite, object, object, object>>.Create(Microsoft.CSharp.RuntimeBinder.Binder.SetMember(CSharpBinderFlags.None, name, typeof(DynamicInvoker), new CSharpArgumentInfo[] { CSharpArgumentInfo.Create(CSharpArgumentInfoFlags.None, null), CSharpArgumentInfo.Create(CSharpArgumentInfoFlags.UseCompileTimeType, null) }));
            callSite.Target(callSite, target, value);
        }
    }

    public class GenericExecutor : DynamicObject
    {
        private AutoWrap _wrap;
        private Type[] _arguments;

        public GenericExecutor(AutoWrap wrap, object[] arguments)
        {
            _arguments = new Type[arguments.Length];

            for (int i = 0; i < arguments.Length; i++)
                _arguments[i] = NetObjectToNative.FindTypeForCreateObject(arguments[i]);

            _wrap = wrap;
        }

        public static bool ExecuteGenericMethod(object obj, Type type, string methodName, bool isStatic, Type[] arguments, Type[] variableType, object[] parameters, out object result)
        {
            result = null;

            var res = InformationOnTheTypes.FindGenericMethodsWithGenericArguments(type, isStatic, methodName, arguments, variableType);

            if (res == null) return false;

            try
            {
                result = res.Invoke(obj, parameters);

                if (result != null && res.ReturnType.GetTypeInfo().IsInterface)
                    result = new AutoWrap(result, res.ReturnType);
            }
            catch (Exception)
            {
                return false;
            }
            return true;
        }

        public override bool TryInvokeMember(InvokeMemberBinder binder, object[] args, out object result)
        {
            var methodName = binder.Name;

            var typesParameters = AllMethodsForName.GetTypesParameters(args);
            var obj = _wrap.IsType ? null : _wrap.Object;
            var type = _wrap.Type;
            var isStatic = _wrap.IsType;

            if (ExecuteGenericMethod(obj, type, methodName, isStatic, _arguments, typesParameters, args, out result))
                return true;

            // Проверим методы расширения

            if (!isStatic)
            {
                if (ExtensionMethod.ExecuteExtensionMethodGenericType(obj, methodName, _arguments,
                    typesParameters, args, out result))
                    return true;
            }

            return false;
        }
    }

    public class TypedEnumerator : IEnumerable
    {
        private System.Collections.IEnumerable _enumerator;
        private TypeInfo _typeInfo;
        private Type _type;

        public TypedEnumerator(IEnumerable enumerator, Type type)
        {
            _enumerator = enumerator;
            _typeInfo = type.GetTypeInfo();
            _type = type;
        }

        public IEnumerator GetEnumerator()
        {
            foreach (var str in _enumerator)
            {
                object res = null;
                if (str != null && _typeInfo.IsInstanceOfType(str)) res = new AutoWrap(str, _type);

                yield return res;
            }
        }
    }

    public class AsyncRunner
    {
        private static bool GetTaskResult(Task task, out string error)
        {
            if (task.IsFaulted)
            {
                var sb = new StringBuilder();
                Exception ex = task.Exception;
                sb.AppendLine(ex.Message);
                while (ex is AggregateException && ex.InnerException != null)
                {
                    ex = ex.InnerException;
                    sb.AppendLine(ex.Message);
                }

                error = sb.ToString();
                return false;
            }

            if (task.IsCanceled)
            {
                error = "Canclled.";
                return false;
            }

            error = null;
            return true;
        }

        public static void Execute<T>(Task<T> task, Action<bool, object> callBack)
        {
            task.ContinueWith(t =>
            {
                if (GetTaskResult(t, out var error))
                {
                    var res = (object)t.Result;
                    callBack(true, res);
                }
                else callBack(false, error);
            });
        }

        public static void TaskExecute(Task task, Action<bool, object> callBack)
        {
            task.ContinueWith(t => { callBack(true, GetTaskResult(t, out var error) ? null : error); });
        }
    }

    public class ClassForEventCEF
    {
        private EventInfo _eventInfo;
        public Guid EventKey;
        public Action<Guid, object> CallBack;
        public object WrapperForEvent;

        public ClassForEventCEF(object wrapperForEvent, Guid eventKey, EventInfo eventInfo, Action<Guid, object> callBack)
        {
            EventKey = eventKey;
            _eventInfo = eventInfo;
            CallBack = callBack;
            WrapperForEvent = wrapperForEvent;
            eventInfo.AddEventHandler(wrapperForEvent, new System.Action<object>(CallEvent));
        }

        public void CallEvent(object value)
        {
            try
            {
                CallBack(EventKey, value);
            }
            catch (Exception)
            {
                // Соединение разорвано
                // Возможно клиент закончил работу
                // Отпишемся от события
                RemoveEventHandler();
            }
        }

        public void RemoveEventHandler()
        {
            _eventInfo.RemoveEventHandler(WrapperForEvent, new System.Action<object>(CallEvent));
        }
    }

    public class ExtensionMethod
    {
        private static Type[] _linqExtensionTypes = GetLinqExtensionTypes();
        private static Type _enumerableType = typeof(IEnumerable<>);

        private static bool SuitableGenericParameter(Type genericType, Type type)
        {
            var typeInfo = genericType.GetTypeInfo();
            if (!typeInfo.IsGenericParameter) return false;

            bool limitations = false;
            Type[] tpConstraints = typeInfo.GetGenericParameterConstraints();
            foreach (Type tpc in tpConstraints)
            {
                if (tpc.IsAssignableFrom(type)) return true;
                limitations = true;
            }

            if (limitations) return false;

            return true;
        }

        private static Type[] FindType(Type[] types, Type extendedType, string methodName, Func<MethodInfo, bool> filter = null)
        {
            var query = from type in types
                        let typeInfo = type.GetTypeInfo()
                        where typeInfo.IsSealed && typeInfo.IsAbstract
                        let synonym = InformationOnTheTypes.GetMethodNameBySynonym(type, methodName)
                        from method in typeInfo.GetMethods()
                        where method.IsStatic && method.IsDefined(typeof(ExtensionAttribute), false)
                                              && method.Name == synonym && (filter?.Invoke(method) ?? true)
                        let parameterType = method.GetParameters()[0].ParameterType
                        where (parameterType.IsAssignableFrom(extendedType) ||
                               GenericMethodData.IsSuit(parameterType, extendedType))
                        select type;

            return query.Distinct().ToArray();
        }

        private static Type[] GetExtensionMethods(Assembly assembly, Type extendedType, string methodName,
            Func<MethodInfo, bool> filter = null)
            => FindType(assembly.GetTypes(), extendedType, methodName, filter);

        private static Type[] GetLinqExtensionTypes()
        {
            var assembly = (typeof(Enumerable)).GetTypeInfo().Assembly;
            var query = from type in assembly.GetTypes()
                        let typeInfo = type.GetTypeInfo()
                        where typeInfo.IsSealed && typeInfo.IsAbstract
                        from method in typeInfo.GetMethods()
                        where method.IsStatic && method.IsDefined(typeof(ExtensionAttribute), false) && method.IsGenericMethod
                        let parameterType = method.GetParameters()[0].ParameterType
                        where parameterType.IsConstructedGenericType &&
                              parameterType.GetGenericTypeDefinition() == _enumerableType
                        select type;

            return query.Distinct().ToArray();
        }

        private static bool CheckExtensionMethods(Type[] types, AutoWrap autoWrap, string methodName, object[] parameters,
            out object result)
        {
            result = null;
            if (types.Length == 0) return false;

            var args = new object[parameters.Length + 1];
            Array.Copy(parameters, 0, args, 1, parameters.Length);
            args[0] = autoWrap.Object;
            foreach (var type in types)
            {
                try
                {
                    var method = InformationOnTheTypes.FindMethod(type, true, methodName, args);
                    if (method != null)
                    {
                        result = method.ExecuteMethod(null, args);
                        Array.Copy(args, 1, parameters, 0, parameters.Length);

                        return true;
                    }
                }
                catch (Exception)
                {
                    // ignored
                }
            }

            return false;
        }

        public static bool ExecuteExtensionMethod(AutoWrap autoWrap, string methodName, object[] parameters, out object result)
        {
            result = null;
            if (autoWrap.IsType)
            {
                if (methodName == "GetTypeInfo")
                {
                    result = autoWrap.Type.GetTypeInfo();
                    return true;
                }

                return false;
            }

            var assembly = autoWrap.Type.GetTypeInfo().Assembly;
            var types = GetExtensionMethods(assembly, autoWrap.Type, methodName);

            if (CheckExtensionMethods(types, autoWrap, methodName, parameters, out result))
                return true;

            if (autoWrap.Type.IsGenericTypeOf(_enumerableType.GetTypeInfo(), _enumerableType))
            {
                types = FindType(_linqExtensionTypes, autoWrap.Type, methodName, (method) => method.IsGenericMethod);
                return (CheckExtensionMethods(types, autoWrap, methodName, parameters, out result));
            }

            return false;
        }

        public static bool CheckExtensionMethodsGenericTypes(Type[] types, object obj, string methodName,
            Type[] arguments, Type[] parametersTypes, object[] parameters, out object result)
        {
            result = null;
            if (types.Length == 0) return false;

            var args = new object[parameters.Length + 1];
            Array.Copy(parameters, 0, args, 1, parameters.Length);
            args[0] = obj;

            var extensionParameterTypes = new Type[parametersTypes.Length + 1];
            Array.Copy(parametersTypes, 0, extensionParameterTypes, 1, parametersTypes.Length);
            extensionParameterTypes[0] = obj.GetType();

            foreach (var type in types)
            {
                try
                {
                    var res = GenericExecutor.ExecuteGenericMethod(obj, type, methodName, true, arguments,
                        extensionParameterTypes, args, out result);
                    if (res)
                    {
                        Array.Copy(args, 1, parameters, 0, parameters.Length);
                        return true;
                    }
                }
                catch (Exception)
                {
                    // ignored
                }
            }

            return false;
        }

        public static bool ExecuteExtensionMethodGenericType(object obj, string methodName, Type[] arguments, Type[] parametersTypes, object[] parameter, out object result)
        {
            result = null;
            var type = obj.GetType();
            var assembly = type.GetTypeInfo().Assembly;

            var types = GetExtensionMethods(assembly, type, methodName,
                (methodInfo) =>
                    methodInfo.IsGenericMethod && methodInfo.GetGenericArguments().Length == arguments.Length);

            if (CheckExtensionMethodsGenericTypes(types, obj, methodName, arguments, parametersTypes, parameter, out result))
                return true;

            if (type.IsGenericTypeOf(_enumerableType.GetTypeInfo(), _enumerableType))
            {
                types = FindType(_linqExtensionTypes, type, methodName);
                return CheckExtensionMethodsGenericTypes(types, obj, methodName, arguments, parametersTypes, parameter, out result);
            }

            return false;
        }
    }
}