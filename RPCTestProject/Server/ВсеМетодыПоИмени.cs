using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;

namespace NetObjectToNative
{

    public class СравнительМетодов : IComparer<ИнфoрмацияОМетоде>
    {


        public int Compare(ИнфoрмацияОМетоде A, ИнфoрмацияОМетоде B)
        {
            int res = 0;
            for (int i = 0; i < A.Параметры.Count(); i++)
            {

                res = A.Параметры[i].CompareTo(B.Параметры[i]);

                if (res != 0) return res;
            }

            res = A.hasParams.CompareTo(B.hasParams);

            if (res != 0) return res;


            res = -A.КоличествоПараметровПарамс.CompareTo(B.КоличествоПараметровПарамс);
            if (res != 0) return res;

            return A.Method.ToString().CompareTo(B.Method.ToString());
        }



    }

    public class СравнительМетодовСпарамс : IComparer<ИнфoрмацияОМетоде> 
    {


        public int Compare(ИнфoрмацияОМетоде A, ИнфoрмацияОМетоде B)
        {


            int res = 0;
            res=-A.HasDefaultValue.CompareTo(B.HasDefaultValue);
            if (res != 0) return res;

            res = -A.КоличествоПараметров.CompareTo(B.КоличествоПараметров);
            if (res != 0) return res;

            for (int i = 0; i < A.Параметры.Count() - 1; i++)
            {

                res = A.Параметры[i].CompareTo(B.Параметры[i]);

                if (res != 0) return res;
            }

            res = A.hasParams.CompareTo(B.hasParams);

            if (res != 0) return res;




            return A.Method.ToString().CompareTo(B.Method.ToString());
        }
    }
    public class AllMethodsForName
    {

        public int MaxParamCount { get; private set;}

        Dictionary<int, List<ИнфoрмацияОМетоде>> ОбычныеМетоды = new Dictionary<int, List<ИнфoрмацияОМетоде>>();

        List<ИнфoрмацияОМетоде> МетодыСParams = new List<ИнфoрмацияОМетоде>();

        void ДобавитьВСловарь(ИнфoрмацияОМетоде им, int КоличествоПараметров)
        {
            List<ИнфoрмацияОМетоде> СписокМетодов = null;

            if (!ОбычныеМетоды.TryGetValue(КоличествоПараметров, out СписокМетодов))
            {
                СписокМетодов = new List<ИнфoрмацияОМетоде>();
                ОбычныеМетоды[КоличествоПараметров] = СписокМетодов;


            }


            СписокМетодов.Add(им);
        }

        void ДобавитПарамсВСписок(KeyValuePair<int, List<ИнфoрмацияОМетоде>>[] Массив, ИнфoрмацияОМетоде Им)
        {
            int минКолПарам =Им.HasDefaultValue? Им.FirstDefaultParams: Им.КоличествоПараметров - 1;


            foreach (var кв in Массив)
            {

                if (кв.Key < минКолПарам) continue;

                if (!(Им.HasDefaultValue && кв.Key >= Им.КоличествоПараметров))
                {
                    var им = new ИнфoрмацияОМетоде(Им, кв.Key);
                    кв.Value.Add(им);
                }
            }

        }
        void ДобавитьМетодыСпарамсВОбычныеМетоды()
        {

            var KV = ОбычныеМетоды.OrderBy(x => x.Key).ToArray();
            foreach (var им in МетодыСParams)
            {

                ДобавитПарамсВСписок(KV, им);


            }

            foreach (var kv in KV)
            {
                kv.Value.Sort(new СравнительМетодов());

            }
        }
        public AllMethodsForName(IEnumerable<MethodInfo> методы)
        {
            
            foreach (var метод in методы)
            {

                var им = new ИнфoрмацияОМетоде(метод);

                if (им.hasParams || им.HasDefaultValue)
                {
                    МетодыСParams.Add(им);
                    var ОбычныйМетод = new ИнфoрмацияОМетоде(им);
                    ДобавитьВСловарь(ОбычныйМетод, ОбычныйМетод.КоличествоПараметров);
                }
                else
                {
                    if (MaxParamCount < им.КоличествоПараметров) MaxParamCount = им.КоличествоПараметров;

                    ДобавитьВСловарь(им, им.КоличествоПараметров);
                }

            }


            ДобавитьМетодыСпарамсВОбычныеМетоды();

            if (МетодыСParams.Count()>0)
            {
                
                МетодыСParams.Sort(new СравнительМетодовСпарамс());

                if (MaxParamCount < 16) MaxParamCount = 16;

            }


        }



        public ИнфoрмацияОМетоде НайтиДженерикМетод(bool IsStatic, List<ИнфoрмацияОМетоде> СписокМетодов, Type[] параметры)
        {

        

                foreach (var метод in СписокМетодов)
                {
                    if (метод.IsGeneric && IsStatic == метод.Method.IsStatic)
                {
                    var MI = метод.ДженерикМетод.ПолучитьРеальныйМетод(параметры);

                    if (MI!=null)
                    {
                        var res = new ИнфoрмацияОМетоде(MI);

                        if (res.Сравнить(параметры))
                            return res;

                    }

                }
            }

            return null;
         

        }

        public static Type[] GetTypesParameters(object[] параметрыОбъекты)
        {
            Type[] параметры = new Type[параметрыОбъекты.Length];

            for (var i = 0; i < параметрыОбъекты.Length; i++)
            {
                if (параметрыОбъекты[i] == null)
                    параметры[i] = null;
                else
                    параметры[i] = параметрыОбъекты[i].GetType();


            }

            return параметры;

        }
        public ИнфoрмацияОМетоде НайтиМетод(bool IsStatic, object[] параметрыОбъекты)
        {

            
            List<ИнфoрмацияОМетоде> СписокМетодов;

           var параметры= GetTypesParameters(параметрыОбъекты);

            if (ОбычныеМетоды.TryGetValue(параметры.Length, out СписокМетодов))
            {
                if (параметры.Length == 0)
                {
                    var метод = СписокМетодов[0];

                    if (!метод.IsGeneric && IsStatic == метод.Method.IsStatic)
                        return метод;
                    else
                        return null;
                }

                foreach (var метод in СписокМетодов)
                {
                    if (!метод.IsGeneric && IsStatic == метод.Method.IsStatic && метод.Сравнить(параметры))
                        return метод;

                }


            }


            foreach (var метод in МетодыСParams)
            {
                if (!метод.IsGeneric && IsStatic == метод.Method.IsStatic && метод.СравнитьПарамс(параметры))
                    return метод;

                if (метод.IsGeneric && IsStatic == метод.Method.IsStatic)
                {
                    // var MI = метод.ДженерикМетод.ПолучитьРеальныйМетод(параметры);
                    var MI = метод.ДженерикМетод.ПолучитьРеальныйМетодСПарамс(параметры, метод);
                    var res = new ИнфoрмацияОМетоде(MI);

                    if (res.СравнитьПарамс(параметры))
                        return res;

                }
            }

            if (СписокМетодов != null)
            {
                var res = НайтиДженерикМетод(IsStatic, СписокМетодов, параметры);
                if (res != null) return res;
            }

          // AutoWrap.СообщитьОбОшибке("Метод существует но не подходят параметры");
            return null;
        }

        public ИнфoрмацияОМетоде НайтиДженерикМетод2(bool IsStatic, Type[] ДженерикПараметры, Type[] ПараметрыМетода)
        {
            List<ИнфoрмацияОМетоде> СписокМетодов;
            if (ОбычныеМетоды.TryGetValue(ПараметрыМетода.Length, out СписокМетодов))
            {

                foreach (var метод in СписокМетодов)
                {
                    if (метод.IsGeneric && IsStatic == метод.Method.IsStatic)
                    {
                        // var MI = метод.ДженерикМетод.ПолучитьРеальныйМетод2(ДженерикПараметры, ПараметрыМетода);
                        var MI = метод.ДженерикМетод.MI.MakeGenericMethod(ДженерикПараметры);
                        if (MI != null)
                        {
                            var res = new ИнфoрмацияОМетоде(MI);

                            if (res.Сравнить(ПараметрыМетода))
                                return res;

                        }

                    }
                }
            }


            foreach (var метод in МетодыСParams)
            {
                if (метод.IsGeneric && IsStatic == метод.Method.IsStatic)
                { 
                    var MI = метод.ДженерикМетод.MI.MakeGenericMethod(ДженерикПараметры);// метод.ДженерикМетод.ПолучитьРеальныйМетод2(ДженерикПараметры, ПараметрыМетода);
                    if (MI != null)
                    {
                        var res = new ИнфoрмацияОМетоде(MI);

                        if (res.СравнитьПарамс(ПараметрыМетода))
                            return res;

                    }

                }

            }

            return null;
        }
    }


   
}
