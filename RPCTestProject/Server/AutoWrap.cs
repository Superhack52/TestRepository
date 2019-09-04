using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using System.Globalization;
using System.Runtime.InteropServices;
using System.IO;
namespace NetObjectToNative
{
    public class AutoWrap
    {


       

        // Хранилище где будем хранить объекты
        //Оптимизировано для повторного использования
        //индексов удаленных из него объектов
        internal static ObjectStorage ObjectsList;

        //Индекс в хранилище объектов
        //Который будет передаватся клиенту
        internal int IndexInStorage;

        // ссылка на объект
        protected internal object O = null;
        // Ссылка на тип. Нужна для скорости и для использования типа интерфейса
        protected internal Type T = null;

       

        // Тип нужен для создания объектов, вызва статических методов
        internal bool IsType;
        // Для перечислений нужно вызывать Enum.Parse(T, name);
        internal bool IsEnum;


        // Объекты реализующие интерфейс  System.Dynamic.IDynamicMetaObjectProvider
        // Это ExpandoObject , DinamicObject, JObject итд 

        internal bool IsDynamic;

        // Полледняя ошибка. Нужно удалить оставил для совместимости
        internal static Exception LastError = null;

        // Директории Microsoft.NETCore.App и текущая директория
        internal static string CoreClrDir, NetObjectToNativeDir;
 
       


        static string GetDirName(Type type) { 
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
            var первый = new AutoWrap(typeof(NetObjectToNative));

            CoreClrDir = GetDirName(typeof(string));
            NetObjectToNativeDir = GetDirName(typeof(AutoWrap));
        }


        public AutoWrap(object obj)
        {

            IndexInStorage = ObjectsList.Add(this);
            O = obj;
            if (O is Type)
            {
                T = O as Type;
                IsType = true;
            }
            else
            {
                T = O.GetType();
                IsType = false;
                IsDynamic = O is System.Dynamic.IDynamicMetaObjectProvider;
                IsEnum = T.GetTypeInfo().IsEnum;


            }



        }

        // Нужен для установки типа интерфейса
        public AutoWrap(object obj, Type type)
        {
            IndexInStorage = ObjectsList.Add(this);
            O = obj;
            T = type;
            IsType = false;
            //   ЭтоExpandoObject = O is System.Dynamic.ExpandoObject;

        }

        

       


        public static object GetRalObject(object obj)
        {


            if (obj is AutoWrap)
            {
                return ((AutoWrap)obj).O;
            }

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

        internal static void SetChangeInArgs(object[] ОригинальныйМассив, object[] МассивРеальныхОбъектов, List<int> ИзмененныеПараметры)
        {
            if (ОригинальныйМассив == МассивРеальныхОбъектов)
                return;

            for (int i = 0; i < ОригинальныйМассив.Length; i++)
            {

                object obj = ОригинальныйМассив[i];

                if (obj is AutoWrap)
                {
                    AutoWrap элемент = (AutoWrap)obj;
                    if (!object.Equals(элемент.O, МассивРеальныхОбъектов[i]))
                    {
                        ОригинальныйМассив[i] = МассивРеальныхОбъектов[i];
                        ИзмененныеПараметры.Add(i);
                    }
                }
                else
                {
                    if (!object.Equals(ОригинальныйМассив[i], МассивРеальныхОбъектов[i]))
                    {
                        ОригинальныйМассив[i] = МассивРеальныхОбъектов[i];
                        ИзмененныеПараметры.Add(i);
                    }
                }
            }


        }

        internal static List<int> GetChangeParams(object[] OrigParams, object[] CopyParams)
        {

            List<int> ChangeArrayParams = new List<int>();
            if (OrigParams == CopyParams)
                return ChangeArrayParams;

            for (int i = 0; i < OrigParams.Length; i++)
            {

                  if (!object.Equals(OrigParams[i], CopyParams[i]))
                    {
                        OrigParams[i] = CopyParams[i];
                        ChangeArrayParams.Add(i);
                    }
                
            }

        return ChangeArrayParams;
    }
        // Обернем объекты для посылки на сервер
        // Напрямую передаются только числа, строки, DateTime, Guid, byte[]
        public static object WrapObject(object obj)
        {


            if (obj == null)
                return obj;


           if (!ServerRPC.WorkWhithVariant.MatchTypes.ContainsKey(obj.GetType()))
                obj = new AutoWrap(obj);
           

            return obj;
        }









        public object InvokeMemberExpandoObject(string name, object[] args)
        {

            return ((Delegate)((IDictionary<string, object>)((System.Dynamic.ExpandoObject)O))[name]).DynamicInvoke(args);

        }

        public static string GetExeptionString(String TypeCall, string name, Exception e)
        {

            LastError = e;
            string Ошибка = String.Format("Ошибка в  {0} {1} {2} {3}", TypeCall, name, e.Message, e.Source);

            if (e.InnerException != null)
                Ошибка = Ошибка + "\r\n" + e.InnerException.ToString();

           
            // throw new Exception(Ошибка);
            //  СообщитьОбОшибке(Ошибка);
            return Ошибка;
        }

        bool ВыполнитьМетодИнтерфейсаКакОбъекта(string name, object[] args, out object result, ref string Error)
        {
            result = null;
            Error = null;
            if (!(IsType || T.GetTypeInfo().IsInterface)) return false;

            ИнфoрмацияОМетоде Метод;

            Метод = InformationOnTheTypes.FindMethod(O.GetType(), false, name, args);

            if (Метод == null) return false;

            try
            {
                result = Метод.ExecuteMethod(O, args);
                return true;
            }

            catch (Exception e)
            {
                Error += GetExeptionString("методе", name, e);

            }
            return false;
        }

        bool НайтиСвойствоИнтерфейсаКакОбъекта(string name, out IPropertyOrFieldInfo result)
        {
            result = null;

            if (!(IsType || T.GetTypeInfo().IsInterface)) return false;

            result = InformationOnTheTypes.НайтиСвойство(O.GetType(), name);

            if (result == null)
                return false;

            return true;
        }
        public bool TryInvokeMember(string name, object[] argsOrig, out object result, List<int> ИзмененныеПараметры, out string Error)
        {        // Unwrap any AutoWrap'd objects (they need to be raw if a paramater)
            Error = null;
            if (IndexInStorage == 0 && name == "ОчиститьСсылку")
            {
                //   NetObjectToNative.ОчиститьСсылку((string)argsOrig[0]);
                AutoWrap temp = argsOrig[0] as AutoWrap;
                if (temp != null)
                    ObjectsList.RemoveKey(temp.IndexInStorage);
                result = null;
                return true;
            }

            result = null;
            object[] args = GetArrayRealObjects(argsOrig);

            var culture = CultureInfo.InvariantCulture;



            // Invoke whatever needs be invoked!
            object obj = null;

            try
            {

                if (IsDynamic)
                {
                    obj = DynamicInvoker.InvokeMember(O, name, args);

                }
                else
                {
                    var Метод = InformationOnTheTypes.FindMethod(T, IsType, name, args);

                    if (Метод == null)
                    {
                       if (name == "_as")
                        {

                            obj = NetObjectToNative.ПолучитьИнтерфейс(O, args[0]);
                        }
                        else
                            if (!МетодыРасширения.НайтиИВыполнитьМетодРасширения(this, name, args, out obj))
                            if (!ВыполнитьМетодИнтерфейсаКакОбъекта(name, args, out obj, ref Error))
                            {
                                // СообщитьОбОшибке("Не найден метод " + name);
                                Error += " Не найден метод " + name;
                                return false;
                            }
                    }
                    else
                    {



                        if (IsType)
                            obj = Метод.ExecuteMethod(null, args);

                        else
                            obj = Метод.ExecuteMethod(O, args); ;



                    }
                }
                SetChangeInArgs(argsOrig, args, ИзмененныеПараметры);
                result = obj;

            }
            catch (Exception e)
            {
                Error = GetExeptionString("методе", name, e);

                return false;

            }

            // Так как параметры могут изменяться (OUT) и передаются по ссылке
            // нужно обратно обернуть параметры

            return true;

        }


        public bool TrySetMember(string name, object valueOrig, out string Error)
        {
            Error = null;
            object value = GetRalObject(valueOrig);
            try
            {
                if (IsDynamic)
                {
                    DynamicInvoker.SetValue(O, name, value);
                    return true;
                }


                var Свойcтво = InformationOnTheTypes.НайтиСвойство(T, name);
                if (Свойcтво == null)
                {
                    if (!НайтиСвойствоИнтерфейсаКакОбъекта(name, out Свойcтво))
                    {
                        // СообщитьОбОшибке("Не найдено Свойство " + name);
                        Error = "Не найдено Свойство " + name;
                        return false;
                    }
                }


                Свойcтво.SetValue(O, value);


                return true;

            }
            catch (Exception e)
            {
                Error = GetExeptionString("установки свойства", name, e);

            }
            return false;
        }
        // получение свойства
        public bool TryGetMember(string name, out object result, out string Error)
        {
            result = null;
            Error = null;
            try
            {
                if (IsDynamic)
                {
                    result = DynamicInvoker.GetValue(O, name);
                    return true;
                }

                if (IsEnum)
                {
                    result = Enum.Parse(T, name);
                    return true;

                }

                var Свойcтво = InformationOnTheTypes.НайтиСвойство(T, name);

                if (Свойcтво == null)
                {
                    if (!НайтиСвойствоИнтерфейсаКакОбъекта(name, out Свойcтво))
                    {
                        // СообщитьОбОшибке("Не найдено Свойство " + name);
                        Error = "Не найдено Свойство " + name;
                        return false;
                    }
                }



                result = Свойcтво.GetValue(O);


                return true;
            }
            catch (Exception e)
            {
                Error = GetExeptionString("получения свойства", name, e);
            }
            result = null;
            return false;
        }


       
       
    }
}
