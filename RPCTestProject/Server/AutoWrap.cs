namespace NetObjectToNative
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Reflection;

    public class AutoWrap
    {
        // Хранилище где будем хранить объекты
        // Оптимизировано для повторного использования
        // индексов удаленных из него объектов
        internal static ObjectStorage ObjectsList;

        // Индекс в хранилище объектов
        // Который будет передаватся клиенту
        internal int IndexInStorage;

        // ссылка на объект
        protected internal object Object;

        // Ссылка на тип. Нужна для скорости и для использования типа интерфейса
        protected internal Type Type;

        // Type нужен для создания объектов, вызва статических методов
        internal bool IsType;

        // Для перечислений нужно вызывать Enum.Parse(Type, name);
        internal bool IsEnum;

        // Объекты реализующие интерфейс  System.Dynamic.IDynamicMetaObjectProvider
        // Это ExpandoObject , DinamicObject, JObject итд

        internal bool IsDynamic;

        // Полледняя ошибка. Нужно удалить оставил для совместимости
        internal static Exception LastError;

        // Директории Microsoft.NETCore.App и текущая директория
        internal static string CoreClrDir, NetObjectToNativeDir;

        private static string GetDirName(Type type)
        {
            string codeBase = type.GetTypeInfo().Assembly.Location;
            UriBuilder uri = new UriBuilder(codeBase);
            string path = Uri.UnescapeDataString(uri.Path);
            return Path.GetDirectoryName(path);
        }

        static AutoWrap()
        {
            // В начале установим ссылку на вспомогательный класс
            //Для создания объектов, получения типов итд
            //который будет идти в списке под индексом 0
            ObjectsList = new ObjectStorage();
            //first to initial
            new AutoWrap(typeof(NetObjectToNative));
            CoreClrDir = GetDirName(typeof(string));
            NetObjectToNativeDir = GetDirName(typeof(AutoWrap));
        }

        public AutoWrap(object obj)
        {
            IndexInStorage = ObjectsList.Add(this);
            Object = obj;
            if (Object is Type type)
            {
                Type = type;
                IsType = true;
            }
            else
            {
                Type = Object.GetType();
                IsType = false;
                IsDynamic = Object is System.Dynamic.IDynamicMetaObjectProvider;
                IsEnum = Type.GetTypeInfo().IsEnum;
            }
        }

        // Нужен для установки типа интерфейса
        public AutoWrap(object obj, Type type)
        {
            IndexInStorage = ObjectsList.Add(this);
            Object = obj;
            Type = type;
            IsType = false;
            //   ЭтоExpandoObject = Object is System.Dynamic.ExpandoObject;
        }

        public static object GetRalObject(object obj)
        {
            if (obj is AutoWrap wrap) return wrap.Object;
            return obj;
        }

        internal static object[] GetArrayRealObjects(object[] args)
        {
            if (args.Length == 0)
                return args;

            object[] res = new object[args.Length];

            for (int i = 0; i < args.Length; i++)
            {
                res[i] = GetRalObject(args[i]);
            }

            return res;
        }

        internal static void SetChangeInArgs(object[] originalArray, object[] realObjectsArray, List<int> changedParameters)
        {
            if (originalArray == realObjectsArray) return;

            for (int i = 0; i < originalArray.Length; i++)
            {
                object obj = originalArray[i];

                if (obj is AutoWrap wrap)
                {
                    if (!Equals(wrap.Object, realObjectsArray[i]))
                    {
                        originalArray[i] = realObjectsArray[i];
                        changedParameters.Add(i);
                    }
                }
                else
                {
                    if (!Equals(originalArray[i], realObjectsArray[i]))
                    {
                        originalArray[i] = realObjectsArray[i];
                        changedParameters.Add(i);
                    }
                }
            }
        }

        internal static List<int> GetChangeParams(object[] origParams, object[] copyParams)
        {
            var changeArrayParams = new List<int>();
            if (origParams == copyParams)
                return changeArrayParams;

            for (int i = 0; i < origParams.Length; i++)
            {
                if (!Equals(origParams[i], copyParams[i]))
                {
                    origParams[i] = copyParams[i];
                    changeArrayParams.Add(i);
                }
            }

            return changeArrayParams;
        }

        // Обернем объекты для посылки на сервер
        // Напрямую передаются только числа, строки, DateTime, Guid, byte[]
        public static object WrapObject(object obj)
        {
            if (obj == null) return null;
            if (!ServerRPC.WorkWithVariant.MatchTypes.ContainsKey(obj.GetType())) obj = new AutoWrap(obj);

            return obj;
        }

        public object InvokeMemberExpandoObject(string name, object[] args)
        {
            return ((Delegate)((IDictionary<string, object>)((System.Dynamic.ExpandoObject)Object))[name]).DynamicInvoke(args);
        }

        public static string GetExceptionString(String TypeCall, string name, Exception e)
        {
            LastError = e;
            string error = $"Ошибка в  {TypeCall} {name} {e.Message} {e.Source}";
            if (e.InnerException != null) error = error + "\r\n" + e.InnerException;

            return error;
        }

        private bool ExecuteInterfaceMethodAsObject(string name, object[] args, out object result, ref string Error)
        {
            result = null;
            Error = null;
            if (!(IsType || Type.GetTypeInfo().IsInterface)) return false;

            RpcMethodInfo method = InformationOnTheTypes.FindMethod(Object.GetType(), false, name, args);

            if (method == null) return false;

            try
            {
                result = method.ExecuteMethod(Object, args);
                return true;
            }
            catch (Exception e)
            {
                Error += GetExceptionString("методе", name, e);
            }
            return false;
        }

        private bool FindInterfacePropertyAsObject(string name, out IPropertyOrFieldInfo result)
        {
            result = null;

            if (!(IsType || Type.GetTypeInfo().IsInterface)) return false;

            result = InformationOnTheTypes.FindProperty(Object.GetType(), name);

            if (result == null)
                return false;

            return true;
        }

        public bool TryInvokeMember(string name, object[] argsOrig, out object result, List<int> changedParameters, out string error)
        {
            error = null;
            if (IndexInStorage == 0 && name == "ОчиститьСсылку")
            {
                if (argsOrig[0] is AutoWrap temp) ObjectsList.RemoveKey(temp.IndexInStorage);
                result = null;
                return true;
            }

            result = null;
            object[] args = GetArrayRealObjects(argsOrig);

            try
            {
                object obj;
                if (IsDynamic) obj = DynamicInvoker.InvokeMember(Object, name, args);
                else
                {
                    var method = InformationOnTheTypes.FindMethod(Type, IsType, name, args);

                    if (method == null)
                    {
                        if (name == "_as") obj = NetObjectToNative.GetInterface(Object, args[0]);
                        else if (!ExtensionMethod.ExecuteExtensionMethod(this, name, args, out obj)
                                 && !ExecuteInterfaceMethodAsObject(name, args, out obj, ref error))
                        {
                            error += " Не найден метод " + name;
                            return false;
                        }
                    }
                    else obj = method.ExecuteMethod(IsType ? null : Object, args);
                }
                SetChangeInArgs(argsOrig, args, changedParameters);
                result = obj;
            }
            catch (Exception e)
            {
                error = GetExceptionString("методе", name, e);

                return false;
            }

            // Так как параметры могут изменяться (OUT) и передаются по ссылке
            // нужно обратно обернуть параметры

            return true;
        }

        public bool TrySetMember(string name, object valueOrig, out string error)
        {
            error = null;
            object value = GetRalObject(valueOrig);
            try
            {
                if (IsDynamic)
                {
                    DynamicInvoker.SetValue(Object, name, value);
                    return true;
                }

                var property = InformationOnTheTypes.FindProperty(Type, name);
                if (property == null)
                {
                    if (!FindInterfacePropertyAsObject(name, out property))
                    {
                        error = "Не найдено Свойство " + name;
                        return false;
                    }
                }

                property.SetValue(Object, value);

                return true;
            }
            catch (Exception e)
            {
                error = GetExceptionString("установки свойства", name, e);
            }
            return false;
        }

        // получение свойства
        public bool TryGetMember(string name, out object result, out string error)
        {
            result = null;
            error = null;
            try
            {
                if (IsDynamic)
                {
                    result = DynamicInvoker.GetValue(Object, name);
                    return true;
                }

                if (IsEnum)
                {
                    result = Enum.Parse(Type, name);
                    return true;
                }

                var property = InformationOnTheTypes.FindProperty(Type, name);
                if (property == null)
                {
                    if (!FindInterfacePropertyAsObject(name, out property))
                    {
                        error = "Не найдено Свойство " + name;
                        return false;
                    }
                }

                result = property.GetValue(Object);

                return true;
            }
            catch (Exception e)
            {
                error = GetExceptionString("получения свойства", name, e);
            }
            result = null;
            return false;
        }
    }
}