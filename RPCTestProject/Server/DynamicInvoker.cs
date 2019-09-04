using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Linq.Expressions;
using Microsoft.CSharp.RuntimeBinder;
using System.Dynamic;
using System.Runtime.CompilerServices;
using System.Collections;
using System.Reflection;
using System.Net;


namespace NetObjectToNative
{
    public class DynamicInvoker
    {
        public static object InvokeMember(object target, string methodName, params object[] args)
        {
            var targetParam = CSharpArgumentInfo.Create(CSharpArgumentInfoFlags.None, null);
            CSharpArgumentInfo[] parameterFlags = new CSharpArgumentInfo[args.Length + 1];
            System.Linq.Expressions.Expression[] parameters = new System.Linq.Expressions.Expression[args.Length + 1];
            parameterFlags[0] = targetParam;
            parameters[0] = System.Linq.Expressions.Expression.Constant(target);
            for (int i = 0; i < args.Length; i++)
            {
                parameterFlags[i + 1] = CSharpArgumentInfo.Create(CSharpArgumentInfoFlags.Constant | CSharpArgumentInfoFlags.UseCompileTimeType, null);
                parameters[i + 1] = System.Linq.Expressions.Expression.Constant(args[i]);
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


   public class ДженерикВыполнитель : DynamicObject
    {

        AutoWrap объект;
        Type[] аргументы;

        public ДженерикВыполнитель(AutoWrap объект,object[] Аргументы )
        {
            аргументы = new Type[Аргументы.Length];

            for (int i = 0; i < Аргументы.Length; i++)
                аргументы[i] = NetObjectToNative.FindTypeForCreateObject(Аргументы[i]);

            this.объект = объект;


        }
        // вызов метода

        public static bool НайтиИВыполнитьДженерикМетод(object obj, Type Тип, string ИмяМетода,bool IsStatic, Type[] Аргументы,Type[] ТипыПараметров, object[] параметры, out object result)
        {
            result = null;

            var res = InformationOnTheTypes.FindGenericMethodsWithGenericArguments(Тип, IsStatic, ИмяМетода, Аргументы, ТипыПараметров);

            if (res==null)  return false;

         

           
            try
            {
                result = res.Invoke(obj, параметры);

                if (result != null && res.ReturnType.GetTypeInfo().IsInterface)
                    result = new AutoWrap(result, res.ReturnType);

            }
            catch (Exception)
            {
               

                return false;

            }
            return true;
        }
        //public override bool TryInvokeMember(InvokeMemberBinder binder, object[] args, out object result)
        //{
        //    result = null;
        //    var ИмяМетода = binder.Name;

        //    var ТипыПараметров = ВсеМетодыПоИмени.ПолучитьТипыПараметров(args);

        //    var res = ИнформацияПоТипам.НайтиДженерикМетод(объект.T, объект.ЭтоТип, ИмяМетода, аргументы, ТипыПараметров);


        //    if (res == null)
        //    {
        //        AutoWrap.СообщитьОбОшибке("Не найден дженерик метод " + ИмяМетода);
        //        return false;

        //    }

        //    var obj = объект.ЭтоТип ? null : объект.O;

        //    try
        //    {
        //        result = res.Invoke(obj, args);
        //    }
        //    catch (Exception e)
        //    {
        //        AutoWrap.СообщитьОбИсключении("методе", ИмяМетода, e);

        //        return false;

        //    }

        //    return true;


        //}

        public override bool TryInvokeMember(InvokeMemberBinder binder, object[] args, out object result)
        {
            result = null;
          //  Error = null;
            var ИмяМетода = binder.Name;

            var ТипыПараметров = AllMethodsForName.GetTypesParameters(args);
            var obj = объект.IsType ? null : объект.O;
            var Тип = объект.T;
            var IsStatic = объект.IsType;

            if (НайтиИВыполнитьДженерикМетод(obj, Тип, ИмяМетода, IsStatic, аргументы, ТипыПараметров, args, out result))
                return true;

            // Проверим методы расширения

            if (!IsStatic)
                {
                if (МетодыРасширения.НайтиИВыполнитьМетодРасширенияДляДженерикТипа(obj, ИмяМетода, аргументы, ТипыПараметров, args, out result))
                   return true;
                }

          //  Error=AutoWrap.СообщитьОбОшибке("не найден дженерик тип "+ИмяМетода);
            return false;


        }
    }
    public class ТипизированныйЭнумератор : System.Collections.IEnumerable
    {
        System.Collections.IEnumerable Энумератор;
        TypeInfo TI;
        Type Тип;
        public ТипизированныйЭнумератор(System.Collections.IEnumerable Энумератор, Type Тип)
        {
            this.Энумератор = Энумератор;
            this.TI = Тип.GetTypeInfo();
            this.Тип = Тип;

        }

        public IEnumerator GetEnumerator()
        {

            foreach (var str in Энумератор)
            {
                object res = null;
                if (str != null && TI.IsInstanceOfType(str))
                {
                    res = new AutoWrap(str, Тип);
                }

                yield return res;
            }

        }
    }

    public class AsyncRuner
    {
      static  bool  ПолучитьДанныеОВыполненииЗадачи(Task t, out string Error)
        {
            if (t.IsFaulted)
            {
                var sb = new StringBuilder();
                Exception ex = t.Exception;
                sb.AppendLine(ex.Message);
                while (ex is AggregateException && ex.InnerException != null)
                {
                    ex = ex.InnerException;
                    sb.AppendLine(ex.Message);

                }
                Error = sb.ToString();
                return false;

            }
            else if (t.IsCanceled)
            {
                Error="Canclled.";
                return false;
            }

            Error = null;
            return true;

        }
        public static void Execute<T>(Task<T> Задача, Action<bool, object> callBack)
        {

            Задача.ContinueWith(t =>
            {

              
                string Error;
               if (ПолучитьДанныеОВыполненииЗадачи(t, out Error))
                {
                   
                    var res = (object)t.Result;
                   
                    callBack(true, res);
                }
               else
                {

                   callBack(false, Error);
                }
                           



            });
            }

        public static void TaskExecute(Task Задача, Action<bool, object> callBack)
        {

            Задача.ContinueWith(t =>
            {

                string Error;
                if (ПолучитьДанныеОВыполненииЗадачи(t, out Error))
                {


                    callBack(true, null);
                }
                else
                {
                   
                    callBack(true, Error);

                }



            });
        }


    }


    public class ClassForEventCEF
    {


        EventInfo EI;
        public Guid EventKey;
        public Action<Guid, object> CallBack;
        public object WrapperForEvent;
        public ClassForEventCEF(object WrapperForEvent, Guid EventKey, EventInfo EI, Action<Guid,object> CallBack)
        {
            this.EventKey = EventKey;
            this.EI = EI;
            this.CallBack = CallBack;
            this.WrapperForEvent = WrapperForEvent;
            EI.AddEventHandler(WrapperForEvent, new System.Action<object>(CallEvent));
        }

        public void CallEvent(object value)
        {
            try { 
               CallBack(EventKey,value);
            }
            catch(Exception)
            {
                // Соединение разорвано
                // Возможно клиент закончил работу
                // Отпишемся от события
                RemoveEventHandler();

            }

        }

        public void RemoveEventHandler()
        {
            EI.RemoveEventHandler(WrapperForEvent, new System.Action<object>(CallEvent));

        }

    }


    public class ДляВыполненияЗадачи<TResult>
{

    static public void Выполнить(System.Threading.Tasks.Task<TResult> Задача, AsyncRuner выполнитель)
    {
        

    }
}

    public class МетодыРасширения
    {
        static Type[] ТипыРасширенияLinq = ПолучитьТипыРасширенийLinq();
        static bool ПодходитДженерикПараметр(Type ДженерикТип, Type тип)
        {
            var TI = ДженерикТип.GetTypeInfo();
            if (!TI.IsGenericParameter) return false;

            bool ЕстьОграничения = false;
            Type[] tpConstraints = TI.GetGenericParameterConstraints();
            foreach (Type tpc in tpConstraints)
            {
                if (tpc.IsAssignableFrom(тип)) return true;
                ЕстьОграничения = true;
            }

            if (ЕстьОграничения) return false;



            return true;
        }

        static Type[] НайтиМетодыВТипах(Type[] types, Type extendedType, string MethodName, Func<MethodInfo, bool> filter = null)
        {

            var query = from type in types
                        let TI = type.GetTypeInfo()
                        where TI.IsSealed && TI.IsAbstract
                        let ИмяМетода = InformationOnTheTypes.ИмяМетодаПоСинониму(type, MethodName)
                        from method in TI.GetMethods()
                        where method.IsStatic && method.IsDefined(typeof(System.Runtime.CompilerServices.ExtensionAttribute), false)
                         && method.Name == ИмяМетода && (filter == null ? true : filter(method))
                        let ParameterType = method.GetParameters()[0].ParameterType
                        where (ParameterType.IsAssignableFrom(extendedType) || ДанныеОДженерикМетоде.ПодходитДженерикПараметр(ParameterType, extendedType))

                        select type;


            return query.Distinct().ToArray();
        }
        static Type[] GetExtensionMethods(Assembly assembly, Type extendedType, string MethodName, Func<MethodInfo,bool> filter=null)
        {


            var types = assembly.GetTypes();

            return НайтиМетодыВТипах(types, extendedType, MethodName, filter);


        }


        static Type[] ПолучитьТипыРасширенийLinq()
        {
            var ТипПеречислителя = typeof(IEnumerable<>);
            var assembly = (typeof(System.Linq.Enumerable)).GetTypeInfo().Assembly;
            var query = from type in assembly.GetTypes()
                        let TI = type.GetTypeInfo()
                        where TI.IsSealed && TI.IsAbstract
                        from method in TI.GetMethods()
                        where method.IsStatic && method.IsDefined(typeof(System.Runtime.CompilerServices.ExtensionAttribute), false) && method.IsGenericMethod
                        let ParameterType = method.GetParameters()[0].ParameterType
                        where ParameterType.IsConstructedGenericType && ParameterType.GetGenericTypeDefinition() == ТипПеречислителя

                        select type;


            return query.Distinct().ToArray();
        }


       
       static bool ПроверитьМетодыРасширений(Type[] типы, AutoWrap AW, string MethodName, object[] параметры, out object result)
        {
            result = null;
            if (типы.Length == 0) return false;

            var args = new object[параметры.Length + 1];
            Array.Copy(параметры, 0, args, 1, параметры.Length);
            args[0] = AW.O;
            foreach (var type in типы)
            {
                try
                {
                    var Метод = InformationOnTheTypes.FindMethod(type, true, MethodName, args);
                    if (Метод != null)
                    {
                        result = Метод.ExecuteMethod(null, args);
                        Array.Copy(args, 1, параметры, 0, параметры.Length);

                        return true;

                    }
                }
                catch (Exception)
                {

                }

            }

            return false;

        }

        

        public static bool НайтиИВыполнитьМетодРасширения(AutoWrap AW, string MethodName,object[] параметры, out object result)
        {
            result = null;
            if (AW.IsType)
            {
                if (MethodName== "GetTypeInfo")
                {
                    result = System.Reflection.IntrospectionExtensions.GetTypeInfo(AW.T);
                    return true;

                }
                return false;
            }
            var тип = AW.T;
            var assembly = тип.GetTypeInfo().Assembly;
            var типы = GetExtensionMethods(assembly, тип, MethodName);

            if (ПроверитьМетодыРасширений(типы, AW, MethodName, параметры, out result))
                return true;


            var ТипПеречислителя = typeof(IEnumerable<>);
            if (тип.IsGenericTypeOf(ТипПеречислителя.GetTypeInfo(), ТипПеречислителя))
            {
                типы = НайтиМетодыВТипах(ТипыРасширенияLinq, тип, MethodName, (Метод) => Метод.IsGenericMethod);
                return (ПроверитьМетодыРасширений(типы, AW, MethodName, параметры, out result)) ;
            }

            return false;
        }

        //public static bool НайтиИВыполнитьДженерикМетод(object obj, Type Тип, string ИмяМетода,bool IsStatic, Type[] Аргументы,Type[] ТипыПараметров, object[] параметры, out object result)

         public static bool ПроверитьМетодыРасширенийДженерикТипов(Type[] типы, object obj, string ИмяМетода, Type[] Аргументы, Type[] ТипыПараметров, object[] параметры, out object result)
        {
            result = null;
            if (типы.Length == 0) return false;

            var тип = obj.GetType();
            var args = new object[параметры.Length + 1];
            Array.Copy(параметры, 0, args, 1, параметры.Length);
            args[0] = obj;

            var ТипыПараметровРасширения = new Type[ТипыПараметров.Length + 1];
            Array.Copy(ТипыПараметров, 0, ТипыПараметровРасширения, 1, ТипыПараметров.Length);
            ТипыПараметровРасширения[0] = тип;

            foreach (var type in типы)
            {
                try
                {
                    var res = ДженерикВыполнитель.НайтиИВыполнитьДженерикМетод(obj, type, ИмяМетода, true, Аргументы, ТипыПараметровРасширения, args, out result);
                    if (res)
                    {

                        Array.Copy(args, 1, параметры, 0, параметры.Length);

                        return true;

                    }
                }
                catch (Exception)
                {

                }

            }

            return false;

        }
        public static bool НайтиИВыполнитьМетодРасширенияДляДженерикТипа(object obj, string ИмяМетода,  Type[] Аргументы, Type[] ТипыПараметров, object[] параметры, out object result)
        {

            result = null;
             var тип = obj.GetType();
            var assembly = тип.GetTypeInfo().Assembly;

            var типы = GetExtensionMethods(assembly, тип, ИмяМетода, (Метод)=> Метод.IsGenericMethod && Метод.GetGenericArguments().Length== Аргументы.Length);

            if (ПроверитьМетодыРасширенийДженерикТипов(типы, obj, ИмяМетода, Аргументы, ТипыПараметров, параметры, out result))
                return true;

            var ТипПеречислителя = typeof(IEnumerable<>);
            if (тип.IsGenericTypeOf(ТипПеречислителя.GetTypeInfo(), ТипПеречислителя))
            {

                типы = НайтиМетодыВТипах(ТипыРасширенияLinq, тип, ИмяМетода);
                return ПроверитьМетодыРасширенийДженерикТипов(типы, obj, ИмяМетода, Аргументы, ТипыПараметров, параметры, out result);
            }

          return false;

        }
    }
}
