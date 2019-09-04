using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Globalization;
using System.Reflection;
using System.Text;
using Microsoft.CodeAnalysis.CSharp;
namespace NetObjectToNative
{
    public partial class NetObjectToNative
    {
        public static string ТестВыделенияСтроки(string str)
        {

            return str;
        }
        public static void ЗакрытьРесурс(Object Oбъект)
        {

            IDisposable d = Oбъект as IDisposable;

            if (d != null) d.Dispose();

        }

      
        public static object ПоследняяОшибка
        {
            get
            {
                if (AutoWrap.LastError == null) return null;

                return AutoWrap.LastError;
            }
        }

       
        public static string ВСтроку(object valueOrig)
        {
            if (valueOrig == null)
                return "неопределено";

            return valueOrig.ToString();


        }

        public static string toString(object valueOrig)
        {
            return ВСтроку(valueOrig);

        }
        public static Type GetType(string type)
        {

            return Type.GetType(type, false);

        }

        public static Type FindTypeForCreateObject(object TypeOrig)
        {



            Type type = TypeOrig as Type;

            if (type != null)
            {

            }
            else if (TypeOrig.GetType() == typeof(String))
            {

                type = GetType((string)TypeOrig);


            }

            if (type == null)
            {
                string TypeStr = TypeOrig.ToString();
                string ошибка = " неверный тип " + TypeStr;
                // MessageBox.Show(ошибка);
                throw new Exception(ошибка);
            }

            return type;
        }

        public static Type TypeForCreateObject(object TypeOrig)
        {

            return FindTypeForCreateObject(TypeOrig);
        }
        public static object CreateObject(object type)
        {

            var res = FindTypeForCreateObject(type);
            return AutoWrap.WrapObject(System.Activator.CreateInstance(res));

        }







        public static object New(object Тип, params object[] argOrig)
        {
            //   MessageBox.Show(Тип.ToString() + " параметров=" + args.Length.ToString());

            var res = FindTypeForCreateObject(Тип);

            object[] args = argOrig;
            return System.Activator.CreateInstance(res, args);

        }

       
            


        public static object CreateArray(object type, int length)
        {
            return Array.CreateInstance(FindTypeForCreateObject(type), length);
        }

       

       

        // Возвращает типизированный Дженерик тип 
        // примеры использования 
        // Int32=Net.GetType("System.Int32");
        // List=Net.GetGenericType("System.Collections.Generic.List`1",Int32);
        // List=Net.GetGenericType(("System.Collections.Generic.List`1","System.Int32"));
        //
        //Generic_List=Net.GetType("System.Collections.Generic.List`1");
        //List=Net.GetGenericType(Generic_List,Int32);

        public static object GetGenericType(object type, params object[] argOrig)
        {
            return ПолучитьДженерикТип(type, argOrig);

        }
        public static object ПолучитьДженерикТип(object type, params object[] argOrig)
        {

            var res = FindTypeForCreateObject(type);

            if (res == null)
            {
                string ошибка = " неверный тип " + type.ToString();

                throw new Exception(ошибка);
            }

            Type[] Типы = new Type[argOrig.Length];

            for (int i = 0; i < argOrig.Length; i++)
                Типы[i] = FindTypeForCreateObject(argOrig[i]);

            return res.MakeGenericType(Типы);

        }


        // Оставлен для севместимости.
        public static object ТипКакОбъект(Type Тип)
        {


            return new AutoWrap(Тип, Тип.GetType());
        }


        // Получения TypeInfo
        // В текущей версии поиск раширений для типа не ищется
        //static TypeInfo GetTypeInfo(this Type type);
        public static object GetTypeInfo(object Тип)
        {

            var тип = FindTypeForCreateObject(Тип);

            return new AutoWrap(тип.GetTypeInfo());



        }


        // Получение информации о типе
        public static string ИнформацияОТипе(object Тип)
        {
            Type тип;
            if (Тип is Type)
                тип = (Type)Тип;
            else
                тип = Тип.GetType();
            //   var тип = typeof(AutoWrap);
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

        public static object GetIterator(object obj)
        {

            return ПолучитьЭнумератор(obj);
        }
        public static object ПолучитьЭнумератор(object obj)
        {


            System.Collections.IEnumerable enumer = (System.Collections.IEnumerable)obj;


            System.Collections.IEnumerator e = enumer.GetEnumerator();

            return new AutoWrap(e, typeof(System.Collections.IEnumerator));


        }


        // Получить типизированный энумератор для получения элемента
        // с приведением типа
        // Пример использования
        //	
        //      Ячейки=Net.GetTypeIterator(Ячейки,INode);
        //      Пока Ячейки.MoveNext() Цикл
        //          Ячейка =Ячейки.Current;
        //          (Ячейка.TextContent);
        //	    КонецЦикла

        public static object GetTypeIterator(object obj, Type тип)
        {

            return ПолучитьТипЭнумератор(obj, тип);
        }
        public static object ПолучитьТипЭнумератор(object obj, Type тип)
        {


            System.Collections.IEnumerable enumer = (System.Collections.IEnumerable)obj;

            enumer = new ТипизированныйЭнумератор(enumer, тип);
            System.Collections.IEnumerator e = enumer.GetEnumerator();

            return new AutoWrap(e, typeof(System.Collections.IEnumerator));


        }

        // Получаем массив объектов используя эффект получения массива параметров при использовании 
        // ключевого слова params
        // Пример использования 
        //МассивПараметров=ъ(Врап.Массив(doc.ПолучитьСсылку(), "a[title='The Big Bang Theory']"));

            public static object GetNetArray(params object[] argOrig)
        {

            return argOrig;

        }
        public static object Массив(params object[] argOrig)
        {

            return argOrig;
        }

        // Получаем масив элементов определенного типа
        // Тип выводится по первому элементу с индексом 0
        // Пример использования
        // ТипыПараметров=ъ(Врап.ТипМассив(IParentNode.ПолучитьСсылку(),String.ПолучитьСсылку()));

        public static object GetTypeArray(params object[] argOrig)
        {

            return ТипМассив(argOrig);

        }
        public static object ТипМассив(params object[] argOrig)
        {
            if (!(argOrig != null && argOrig.Length > 0))
                return new object[0];

            var Тип = argOrig[0].GetType();
            var TI = Тип.GetTypeInfo();
            var TypeRes = typeof(System.Collections.Generic.List<>).MakeGenericType(Тип);

            var res = Activator.CreateInstance(TypeRes);
            var MI = TypeRes.GetTypeInfo().GetMethod("Add");
            var MI2 = TypeRes.GetTypeInfo().GetMethod("ToArray");
            foreach (var str in argOrig)
            {
                if (str != null && TI.IsInstanceOfType(str))
                    MI.Invoke(res, new object[] { str });

            }

            return MI2.Invoke(res, new object[0]); ;
        }


        // Ищет Дженерик метод по дженерик аргументам и типам параметров
        // Пример использования
        //ТипыПараметров=ъ(Врап.ТипМассив(IParentNode.ПолучитьСсылку(),String.ПолучитьСсылку()));
        // ТипыАргументов=ъ(Врап.ТипМассив(IHtmlAnchorElement.ПолучитьСсылку()));
        ////public static TElement QuerySelector<TElement>(this IParentNode parent, string selectors) where TElement : class, IElement;
        //стр=Врап.НайтиДженерикМетод(ApiExtensions.ПолучитьСсылку(),true,"QuerySelector",ТипыАргументов.ПолучитьСсылку(),ТипыПараметров.ПолучитьСсылку());
        //QuerySelector_AnchorElement = ъ(стр);

        public static ИнфoрмацияОМетоде НайтиДженерикМетод(Type Тип, bool IsStatic, string ИмяМетода, Type[] ДженерикПараметры, Type[] ПараметрыМетода)
        {

            var res = InformationOnTheTypes.FindGenericMethodsWithGenericArguments(Тип, IsStatic, ИмяМетода, ДженерикПараметры, ПараметрыМетода);

            if (res == null)
            {

                //  AutoWrap.СообщитьОбОшибке("Не найден метод "+ ИмяМетода);
                var ошибка = "Не найден метод " + ИмяМетода;
                throw new Exception(ошибка);
            }
            return res;
        }

        // Асинхронное выполнение задачи
        // Пример использования
        //Задача=ъ(Клиент.GetStringAsync(стр));
        // объект=ПолучитьДанныеДляЗадачи(стр, сч);
        //public static void ВыполнитьЗадачу(System.Threading.Tasks.Task Задача, String ИмяМетода, Object ДанныеДляЗадача)
        //Врап.ВыполнитьЗадачу(Задача.ПолучитьСсылку(),"ПолученаСтраница",объект.ПолучитьСсылку());
        // Результат получается в процедуре
        //Процедура ВнешнееСобытие(Источник, Событие, Данные)
        //  Если Источник = "АсинхронныйВыполнитель" Тогда
        //      Данные = ъ(Данные);
        //      Выполнить(Событие+"(Данные)");
        //  КонецЕсли; 
        //КонецПроцедуры
        // ИмяМетода передается в параметре Событие
        public static void ВыполнитьЗадачу(System.Threading.Tasks.Task Задача, String ИмяМетода, Object ДанныеДляЗадача)
        {
         //   new АсинхронныйВыполнитель(Задача, ИмяМетода, ДанныеДляЗадача);

        }


        public static Assembly FindAssembly(string Каталог, string ИмяФайла)
        {
            string path = Path.Combine(Каталог, ИмяФайла);

            if (File.Exists(path))
            {
                Assembly assembly;
                try { 
                var asm = System.Runtime.Loader.AssemblyLoadContext.GetAssemblyName(path);
                 assembly = Assembly.Load(asm);
                }
                catch (Exception) { 
                   assembly = System.Runtime.Loader.AssemblyLoadContext.Default.LoadFromAssemblyPath(path);
                }


                return assembly;

            }

            return null;
        }
        static bool FindTypeInFile(string type, string Каталог, string ИмяФайла, out Type result)
        {
            result = null;

            var assembly = FindAssembly(Каталог, ИмяФайла);
            if (assembly != null)
            {

                result = assembly.GetType(type, false);
                if (result != null) return true;

            }

            return false;
        }


        // Ищет тип по имени полному имени в файле
        // ГлобальнаяСборка означает что сборка находится рядом с coreclr.dll
        // Пример использования 
        // List=ъ(Врап.Тип("System.Collections.Generic.List`1[System.String]"));
        // Тестовый=ъ(Врап.Тип("TestDllForCoreClr.Тестовый","TestDllForCoreClr"));
        // HttpClient=ъ(Врап.Тип("System.Net.Http.HttpClient, System.Net.Http,true));

        public static Type GetType(string type, string fileName)
        {
            return GetType(type, fileName, false);
        }
           public static Type GetType(string type, string FileName = "", bool IsGlabalAssembly = false)
        {
            Type result = null;

            if (string.IsNullOrWhiteSpace(FileName))
            {
                result = GetType(type);
                if (result != null)
                    return result;

            }
            else
            {
                CheckFileName(ref FileName);
                if (!(!IsGlabalAssembly && FindTypeInFile(type, AutoWrap.NetObjectToNativeDir, FileName, out result)))
                    FindTypeInFile(type, AutoWrap.CoreClrDir, FileName, out result);

                if (result != null)
                {

                    return result;
                }
            }
            string ошибка = " неверный тип " + type + " в сборке " + FileName;
            throw new Exception(ошибка);

        }

        
        public static void CheckFileName(ref string FileName)
        {
            string AssemblyExtension = "DLL";
            var res = FileName.LastIndexOf('.');

            if (res < 0)
            {
                FileName += "." + AssemblyExtension;
                return;
            }

            if (res == FileName.Length - 1)
            {
                FileName += AssemblyExtension;
                return;
            }
            var расширение = FileName.Substring(res + 1);
            if (!AssemblyExtension.Equals(расширение, StringComparison.OrdinalIgnoreCase))
                FileName += "." + AssemblyExtension;


        }


        // Ищем сборку по путям переданным при создании компоненты
        //ПодключитьВнешнююКомпоненту(ИмяФайла, "NetObjectToNative",ТипВнешнейКомпоненты.Native); 
        // Врап = Новый("AddIn.NetObjectToNative.LoaderCLR");
        // Врап.СоздатьОбертку(CoreClrDir,ДиректорияNetObjectToNative,"");
        // Где 
        // CoreClrDir Это директория где лежат основные библиотеки .Net и в частности coreclr
        // ДиректорияNetObjectToNative директория где лежит эта сборка
        // на данный момент все пользовательские сборки нужно сохранять рядом с ней
        //Пример использования 
        //СборкаHttpClient=ъ(Врап.Сборка("System.Net.Http",истина));
        //HttpClient=ъ(СборкаHttpClient.GetType("System.Net.Http.HttpClient"));
        public static Assembly GetAssembly(string FileName, bool IsGlabalAssembly = false)
        {
            CheckFileName(ref FileName);

            Assembly assembly = null;
            if (!IsGlabalAssembly)
                assembly = FindAssembly(AutoWrap.NetObjectToNativeDir, FileName);

            if (assembly != null) return assembly;

            assembly = FindAssembly(AutoWrap.CoreClrDir, FileName);

            if (assembly != null) return assembly;

            string ошибка = " Не найдена сборка " + FileName;
            throw new Exception(ошибка);

        }


        // Добавляет синоним дле метода
        //Assembly=ъ(СборкаHttpClient.GetType());
        // Врап.ДобавитьСиноним(Assembly.ПолучитьСсылку(),"Тип","GetType");
        // Теперь вмето GetType можно использовать Тип
        //HttpClient=ъ(СборкаHttpClient.Тип("System.Net.Http.HttpClient"));

        public static void ДобавитьСиноним(object type, string Синоним, string ИмяМетода)
        {
            var тип = FindTypeForCreateObject(type);
            InformationOnTheTypes.УстановитьСиноним(тип, Синоним, ИмяМетода);
        }

        // Нужен для получения типа неподдерживаемого 1С, беззнаковые,Decimal итд
        public static Object ChangeType(object type, object valueOrig)
        {

            object value = AutoWrap.GetRalObject(valueOrig);

            Type result = FindTypeForCreateObject(type);

            return new AutoWrap(Convert.ChangeType(value, result, CultureInfo.InvariantCulture));

        }


        // Русскоязычный аналог ChangeType 
        public static Object ВывестиТип(object type, object valueOrig)
        {

            return ChangeType(type, valueOrig);
        }



        public static Object ToDecimal(object valueOrig)
        {

            object value = AutoWrap.GetRalObject(valueOrig);


            return new AutoWrap(Convert.ChangeType(value, typeof(Decimal), CultureInfo.InvariantCulture));

        }

        public static Object ToInt(object valueOrig)
        {

            object value = AutoWrap.GetRalObject(valueOrig);


            return new AutoWrap(Convert.ChangeType(value, typeof(Int32), CultureInfo.InvariantCulture));

        }


        // Аналог C# операции as
        // Создаем обертку с типом интерфейса
        // И можно вызывать методы и свойства интерфеса, так как в объекте они могут быть закрытыми
        public static object ПолучитьИнтерфейс(object obj, object Interfase)
        {

            if (Interfase is Type)
                return new AutoWrap(obj, (Type)Interfase);

            object РеальныйОбъект = obj;

            if (Interfase is string)
            {
                string InterfaseName = (string)Interfase;

                foreach (var inter in РеальныйОбъект.GetType().GetTypeInfo().GetInterfaces())
                {
                    if (inter.Name == InterfaseName)
                        return new AutoWrap(РеальныйОбъект, inter);

                }
            }
            return null;


        }


        // Битовая операция OR
        // Часто используемая для Enum
        // Пример использования
        //handler.AutomaticDecompression=Врап.OR(ссылкаGZip,ссылкаDeflate);
        //
        public static object OR(params object[] параметры)
        {
            if (параметры.Length == 0)
                return null;


            var парам = параметры[0];
            var тип = парам.GetType();

            long res = (long)Convert.ChangeType(парам, typeof(long));

            for (int i = 1; i < параметры.Length; i++)
                res |= (long)Convert.ChangeType(параметры[i], typeof(long));


            if (тип.GetTypeInfo().IsEnum)
            {
                var ТипЗначений = Enum.GetUnderlyingType(тип);
                var number = Convert.ChangeType(res, ТипЗначений);
                return Enum.ToObject(тип, number);
            }

            return Convert.ChangeType(res, тип);
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


        

        static IPropertyOrFieldInfo FindProperty(object объект, string ИмяДелегата)
        {

            var T = объект.GetType();
            var Свойcтво = InformationOnTheTypes.НайтиСвойство(T, ИмяДелегата);
            if (Свойcтво == null)
            {

                throw new Exception("Не найдено Делегат  " + ИмяДелегата);


            }

            return Свойcтво;
        }

    
      
       

        public static string GetUniqueString()
        {

            return Convert.ToBase64String(Guid.NewGuid().ToByteArray());

        }

        public static object ReturnParam(object obj)
        {
            return obj;

        }

        public static int КоличествоЭлементовВХранилище()
        {

            return AutoWrap.ObjectsList.RealObjectCount();
        }

        public static int CountItemsInStore()
        {
            return AutoWrap.ObjectsList.RealObjectCount();

        }
        public static int FirstDeleted()
        {

            return AutoWrap.ObjectsList.FirstDeleted;
        }
    }


}
