using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;

namespace Union
{
    public partial class NetObjectToNative
    {
        public static void CloseResource(Object obj)
        {
            if (obj is IDisposable d) d.Dispose();
        }

        public static object LastError => AutoWrap.LastError;

        public static string ToString(object valueOrig) => valueOrig != null ? valueOrig.ToString() : "неопределено";

        public static Type GetType(string type) => Type.GetType(type, false);

        public static Type FindTypeForCreateObject(object TypeOrig)
        {
            Type type = TypeOrig as Type;

            if (type != null)
            {
            }
            else if (TypeOrig is string asString) type = GetType(asString);

            if (type == null)
            {
                string typeStr = TypeOrig.ToString();
                string error = " неверный type " + typeStr;
                throw new Exception(error);
            }

            return type;
        }

        public static Type TypeForCreateObject(object typeOrig) => FindTypeForCreateObject(typeOrig);

        public static object CreateObject(object type) =>
            AutoWrap.WrapObject(System.Activator.CreateInstance(FindTypeForCreateObject(type)));

        public static object New(object type, params object[] argOrig) =>
            System.Activator.CreateInstance(FindTypeForCreateObject(type), argOrig);

        public static object CreateArray(object type, int length) =>
            Array.CreateInstance(FindTypeForCreateObject(type), length);

        // Возвращает типизированный Дженерик type
        // примеры использования
        // Int32=Net.GetType("System.Int32");
        // List=Net.GetGenericType("System.Collections.Generic.List`1",Int32);
        // List=Net.GetGenericType(("System.Collections.Generic.List`1","System.Int32"));
        //
        //Generic_List=Net.GetType("System.Collections.Generic.List`1");
        //List=Net.GetGenericType(Generic_List,Int32);

        public static object GetGenericType(object type, params object[] argOrig)
        {
            var res = FindTypeForCreateObject(type);

            if (res == null)
            {
                string ошибка = " неверный type " + type.ToString();

                throw new Exception(ошибка);
            }

            Type[] types = new Type[argOrig.Length];

            for (int i = 0; i < argOrig.Length; i++)
                types[i] = FindTypeForCreateObject(argOrig[i]);

            return res.MakeGenericType(types);
        }

        // Получения TypeInfo
        // В текущей версии поиск раширений для типа не ищется
        //static TypeInfo GetTypeInfo(this Type type);
        public static object GetTypeInfo(object type) => new AutoWrap(FindTypeForCreateObject(type).GetTypeInfo());

        // Получение информации о типе
        public static string TypeInformation(object type)
        {
            Type тип;
            if (type is Type asType)
                тип = asType;
            else
                тип = type.GetType();

            var sb = new StringBuilder();
            sb.AppendFormat("AssemblyQualifiedName {0}", тип.AssemblyQualifiedName).AppendLine();
            sb.AppendFormat("AssemblyQualifiedName {0}", тип.GetTypeInfo().AssemblyQualifiedName).AppendLine();
            sb.AppendFormat("Assembly FullName {0}", тип.GetTypeInfo().Assembly.FullName).AppendLine();

            foreach (var inter in тип.GetTypeInfo().GetInterfaces())
            {
                sb.AppendFormat("Поддерживаемый интерфейс {0}", inter.Name).AppendLine();
                sb.AppendFormat("Полное имя интерфейс {0}", inter.FullName).AppendLine();
            }
            return sb.ToString();
        }

        // Получает энумератор для обхода коллекции через MoveNext()
        // И получения результата через Current
        // Пример использования
        //Список=ъ(Врап.ПолучитьЭнумератор(лист.ПолучитьСсылку()));
        // Пока Список.MoveNext() Цикл
        //     Зазача = ъ(Список.Current);
        // Сообщить(Зазача.Result);
        //КонецЦикла

        public static object GetIterator(object obj) =>
            new AutoWrap(((System.Collections.IEnumerable)obj).GetEnumerator(),
                typeof(System.Collections.IEnumerator));

        // Получить типизированный энумератор для получения элемента
        // с приведением типа
        // Пример использования
        //
        //      Ячейки=Net.GetTypeIterator(Ячейки,INode);
        //      Пока Ячейки.MoveNext() Цикл
        //          Ячейка =Ячейки.Current;
        //          (Ячейка.TextContent);
        //	    КонецЦикла

        public static object GetTypeIterator(object obj, Type type) => new AutoWrap(
            (new TypedEnumerator((System.Collections.IEnumerable)obj, type)).GetEnumerator(),
            typeof(System.Collections.IEnumerator));

        // Получаем массив объектов используя эффект получения массива параметров при использовании
        // ключевого слова params
        // Пример использования
        //МассивПараметров=ъ(Врап.Массив(doc.ПолучитьСсылку(), "a[title='The Big Bang Theory']"));

        public static object GetNetArray(params object[] argOrig) => argOrig;

        // Получаем масив элементов определенного типа
        // type выводится по первому элементу с индексом 0
        // Пример использования
        // ТипыПараметров=ъ(Врап.ТипМассив(IParentNode.ПолучитьСсылку(),String.ПолучитьСсылку()));

        public static object GetTypeArray(params object[] argOrig)
        {
            if (!(argOrig != null && argOrig.Length > 0)) return new object[0];
            var type = argOrig[0].GetType();
            var typeInfo = type.GetTypeInfo();
            var typeRes = typeof(System.Collections.Generic.List<>).MakeGenericType(type);

            var res = Activator.CreateInstance(typeRes);
            var addMethod = typeRes.GetTypeInfo().GetMethod("Add");
            var toArrayMethod = typeRes.GetTypeInfo().GetMethod("ToArray");
            foreach (var str in argOrig)
            {
                if (str != null && typeInfo.IsInstanceOfType(str)) addMethod.Invoke(res, new object[] { str });
            }

            return toArrayMethod.Invoke(res, new object[0]); ;
        }

        // Ищет Дженерик метод по дженерик аргументам и типам параметров
        // Пример использования
        //ТипыПараметров=ъ(Врап.ТипМассив(IParentNode.ПолучитьСсылку(),String.ПолучитьСсылку()));
        // ТипыАргументов=ъ(Врап.ТипМассив(IHtmlAnchorElement.ПолучитьСсылку()));
        ////public static TElement QuerySelector<TElement>(this IParentNode parent, string selectors) where TElement : class, IElement;
        //стр=Врап.FindGenericMethod(ApiExtensions.ПолучитьСсылку(),true,"QuerySelector",ТипыАргументов.ПолучитьСсылку(),ТипыПараметров.ПолучитьСсылку());
        //QuerySelector_AnchorElement = ъ(стр);

        public static RpcMethodInfo FindGenericMethod(Type type, bool isStatic, string methodName, Type[] genericParameters, Type[] methodParameters)
        {
            var res = InformationOnTheTypes.FindGenericMethodsWithGenericArguments(type, isStatic, methodName, genericParameters, methodParameters);

            if (res == null)
            {
                //  AutoWrap.СообщитьОбОшибке("Не найден метод "+ methodName);
                throw new Exception("Не найден метод " + methodName);
            }
            return res;
        }

        public static Assembly FindAssembly(string directory, string fileName)
        {
            string path = Path.Combine(directory, fileName);

            if (File.Exists(path))
            {
                Assembly assembly;
                try
                {
                    assembly = Assembly.Load(System.Runtime.Loader.AssemblyLoadContext.GetAssemblyName(path));
                }
                catch (Exception)
                {
                    assembly = System.Runtime.Loader.AssemblyLoadContext.Default.LoadFromAssemblyPath(path);
                }

                return assembly;
            }

            return null;
        }

        private static bool FindTypeInFile(string type, string directory, string fileName, out Type result)
        {
            result = null;

            var assembly = FindAssembly(directory, fileName);
            if (assembly != null)
            {
                if (assembly.GetType(type, false) != null) return true;
            }

            return false;
        }

        // Ищет type по имени полному имени в файле
        // ГлобальнаяСборка означает что сборка находится рядом с coreclr.dll
        // Пример использования
        // List=ъ(Врап.type("System.Collections.Generic.List`1[System.String]"));
        // Тестовый=ъ(Врап.type("TestDllForCoreClr.Тестовый","TestDllForCoreClr"));
        // HttpClient=ъ(Врап.type("System.Net.Http.HttpClient, System.Net.Http,true));

        public static Type GetType(string type, string fileName) => GetType(type, fileName, false);

        public static Type GetType(string type, string fileName = "", bool isGlabalAssembly = false)
        {
            Type result = null;

            if (string.IsNullOrWhiteSpace(fileName))
            {
                result = GetType(type);
                if (result != null) return result;
            }
            else
            {
                CheckFileName(ref fileName);
                if (!(!isGlabalAssembly && FindTypeInFile(type, AutoWrap.NetObjectToNativeDir, fileName, out result)))
                    FindTypeInFile(type, AutoWrap.CoreClrDir, fileName, out result);

                if (result != null) return result;
            }
            throw new Exception(" неверный type " + type + " в сборке " + fileName);
        }

        public static void CheckFileName(ref string fileName)
        {
            string AssemblyExtension = "DLL";
            var res = fileName.LastIndexOf('.');

            if (res < 0)
            {
                fileName += "." + AssemblyExtension;
                return;
            }

            if (res == fileName.Length - 1)
            {
                fileName += AssemblyExtension;
                return;
            }
            var расширение = fileName.Substring(res + 1);
            if (!AssemblyExtension.Equals(расширение, StringComparison.OrdinalIgnoreCase))
                fileName += "." + AssemblyExtension;
        }

        // Ищем сборку по путям переданным при создании компоненты
        //ПодключитьВнешнююКомпоненту(fileName, "NetObjectToNative",ТипВнешнейКомпоненты.Native);
        // Врап = Новый("AddIn.NetObjectToNative.LoaderCLR");
        // Врап.СоздатьОбертку(CoreClrDir,ДиректорияNetObjectToNative,"");
        // Где
        // CoreClrDir Это директория где лежат основные библиотеки .Net и в частности coreclr
        // ДиректорияNetObjectToNative директория где лежит эта сборка
        // на данный момент все пользовательские сборки нужно сохранять рядом с ней
        //Пример использования
        //СборкаHttpClient=ъ(Врап.Сборка("System.Net.Http",истина));
        //HttpClient=ъ(СборкаHttpClient.GetType("System.Net.Http.HttpClient"));
        public static Assembly GetAssembly(string fileName, bool isGlobalAssembly = false)
        {
            CheckFileName(ref fileName);

            Assembly assembly = null;
            if (!isGlobalAssembly) assembly = FindAssembly(AutoWrap.NetObjectToNativeDir, fileName);

            if (assembly != null) return assembly;

            assembly = FindAssembly(AutoWrap.CoreClrDir, fileName);

            if (assembly != null) return assembly;

            throw new Exception(" Не найдена сборка " + fileName);
        }

        // Добавляет синоним дле метода
        //Assembly=ъ(СборкаHttpClient.GetType());
        // Врап.AddSynonym(Assembly.ПолучитьСсылку(),"type","GetType");
        // Теперь вмето GetType можно использовать type
        //HttpClient=ъ(СборкаHttpClient.type("System.Net.Http.HttpClient"));

        public static void AddSynonym(object type, string synonym, string methodName) =>
            InformationOnTheTypes.SetSynonym(FindTypeForCreateObject(type), synonym, methodName);

        // Нужен для получения типа неподдерживаемого 1С, беззнаковые,Decimal итд
        public static Object ChangeType(object type, object valueOrig) =>
            new AutoWrap(
                Convert.ChangeType(AutoWrap.GetRalObject(valueOrig),
                    FindTypeForCreateObject(type),
                    CultureInfo.InvariantCulture));

        // Русскоязычный аналог ChangeType
        public static Object PrintType(object type, object valueOrig) => ChangeType(type, valueOrig);

        public static Object ToDecimal(object valueOrig) =>
            new AutoWrap(Convert.ChangeType(
                AutoWrap.GetRalObject(valueOrig),
                typeof(Decimal),
                CultureInfo.InvariantCulture));

        public static Object ToInt(object valueOrig) => new AutoWrap(Convert.ChangeType(
            AutoWrap.GetRalObject(valueOrig),
            typeof(Int32),
            CultureInfo.InvariantCulture));

        // Аналог C# операции as
        // Создаем обертку с типом интерфейса
        // И можно вызывать методы и свойства интерфеса, так как в объекте они могут быть закрытыми
        public static object GetInterface(object obj, object @interface)
        {
            if (@interface is Type asTpe) return new AutoWrap(obj, asTpe);

            object realObject = obj;

            if (@interface is string interfaceName)
            {
                foreach (var inter in realObject.GetType().GetTypeInfo().GetInterfaces())
                {
                    if (inter.Name == interfaceName)
                        return new AutoWrap(realObject, inter);
                }
            }
            return null;
        }

        // Битовая операция OR
        // Часто используемая для Enum
        // Пример использования
        //handler.AutomaticDecompression=Врап.OR(ссылкаGZip,ссылкаDeflate);
        //
        public static object OR(params object[] parameters)
        {
            if (parameters.Length == 0) return null;

            var parameter = parameters[0];
            var type = parameter.GetType();

            long res = (long)Convert.ChangeType(parameter, typeof(long));

            for (int i = 1; i < parameters.Length; i++)
                res |= (long)Convert.ChangeType(parameters[i], typeof(long));

            if (type.GetTypeInfo().IsEnum)
            {
                var valueType = Enum.GetUnderlyingType(type);
                var number = Convert.ChangeType(res, valueType);
                return Enum.ToObject(type, number);
            }

            return Convert.ChangeType(res, type);
        }

        // Возвращает делегат для вызова внешнего события в 1С
        // Пример использования
        //Делегат=Ъ(Врап.ПолучитьДелегатВнешнегоСобытия1C());
        // У объекта Тест есть поле
        //  public Action<string, string, string> ВнешнееСобытие1С;
        // Которое мы установим
        //Тест.ВнешнееСобытие1С=Делегат.ПолучитьСсылку();
        // И ктоторое может быть вызвано при возникновении события
        // ВнешнееСобытие1С?.DynamicInvoke("Тестовый", "ТестовоеСообщение", значение);

        private static IPropertyOrFieldInfo FindProperty(object obj, string delegateName)
        {
            var T = obj.GetType();
            var property = InformationOnTheTypes.FindProperty(T, delegateName);
            if (property == null)
            {
                throw new Exception("Не найдено Делегат  " + delegateName);
            }

            return property;
        }

        public static string GetUniqueString() => Convert.ToBase64String(Guid.NewGuid().ToByteArray());

        public static object ReturnParam(object obj) => obj;

        public static int StorageElementCount() => AutoWrap.ObjectsList.RealObjectCount();

        public static int CountItemsInStore() => AutoWrap.ObjectsList.RealObjectCount();

        public static int FirstDeleted() => AutoWrap.ObjectsList.FirstDeleted;
    }
}