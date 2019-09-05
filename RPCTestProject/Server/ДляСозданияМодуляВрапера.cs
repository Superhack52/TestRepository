using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;

namespace NetObjectToNative
{

    
    public class WrapperForEvents<T>
    {
        public static readonly Func<Action<Guid, object>, T, object> WrapperCreator;


        public static Func<Action<Guid, object>, T, object> CreateWrapper()
        {
            Type TypeTarget = typeof(T);
            string TypeTargetStr = TypeTarget.FullName;
            var ИмяКласса = "WrapperFor" + TypeTargetStr.Replace(".", "_").Replace("+", "_");

            var wmc = new WrapperModuleCreater();
            string строкаКласса = wmc.СоздатьОписания(TypeTarget);

            var scr = Microsoft.CodeAnalysis.Scripting.ScriptOptions.Default;

            var assemblies = wmc.СборкиВПараметрах.Keys.ToArray();

            scr = scr.WithReferences(assemblies)
            .WithImports("System", "NetObjectToNative", "System.Collections.Generic", "System.Reflection");



            var res = (Func<Action<Guid, object>, T, object>)Microsoft.CodeAnalysis.CSharp.Scripting.CSharpScript.EvaluateAsync(строкаКласса, scr).Result;
            return res;
        }
        static WrapperForEvents()
        {
            WrapperCreator = CreateWrapper();


        }

    }

    public class WrapperModuleCreater
    {
        StringBuilder ДляОписанияСобытия = new StringBuilder();
        StringBuilder ДляРеализацииСобытия = new StringBuilder();
        StringBuilder EventsName = new StringBuilder();
        string ИмяКласса;
        string ИсточникСобытия;
        public Dictionary<Assembly, bool> СборкиВПараметрах = new Dictionary<Assembly, bool>();


        public WrapperModuleCreater()
        {
            var сборки = "mscorlib,System.Private.CoreLib.ni,System.Runtime,System.Collections,System.Reflection".Split(',');

            foreach (var str in сборки)
                СборкиВПараметрах.Add(NetObjectToNative.GetAssembly(str, true), true);

            СборкиВПараметрах.Add(typeof(WrapperModuleCreater).GetTypeInfo().Assembly, true);

        }

        public static void WirteStatic()
        {
            Console.WriteLine("Мое значение");
        }

        public List<string> ПолучитьПараметры(EventInfo событие, out bool добавлять)
        {
            добавлять = true;
            var rez = new List<string>();
            var Метод = событие.EventHandlerType.GetMethod("Invoke");

            if (Метод.ReturnType != typeof(void))
            {
                добавлять = false;
                return rez;
            }



            var параметры = Метод.GetParameters();
            foreach (var параметр in параметры)
            {
                rez.Add(параметр.Name);
                var Сборка = параметр.ParameterType.GetTypeInfo().Assembly;
                СборкиВПараметрах[Сборка] = true;
            }

            return rez;
        }
        public void ПолучитьТекстСобытий(Type тип, List<Tuple<string, List<string>>> res)
        {


            bool добавлять = true;
            foreach (EventInfo e in тип.GetEvents())
            {
                var параметры = ПолучитьПараметры(e, out добавлять);

                if (добавлять) res.Add(new Tuple<string, List<string>>(e.Name, параметры));

            }

        }





        void ЗаполнитьРеализацииСобытий(Tuple<string, List<string>> value)
        {
            string ИмяСобытия = value.Item1;
            var Параметры = value.Item2;


            EventsName.AppendLine($"public event Action<object> {ИмяСобытия};");
            if (Параметры.Count == 0)
            {
                var str = $@"Target.{ИмяСобытия} += () =>
                {{
                   if ({ИмяСобытия}!=null)
                    {ИмяСобытия}(null);
                }};";

                ДляРеализацииСобытия.AppendLine(str);

                return;
            }
            else if (Параметры.Count == 1)
            {
                String ИмяПараметра = Параметры[0];
                var str = $@"Target.{ИмяСобытия} += ({ИмяПараметра}) =>
                {{
               if ({ИмяСобытия}!=null)
                   {ИмяСобытия}({ИмяПараметра});


                }};";



                ДляРеализацииСобытия.AppendLine(str);
                return;
            }

            StringBuilder ПараметрыСобытия = new StringBuilder();
            StringBuilder СвойстваКласса = new StringBuilder();
            foreach (var Параметр in Параметры)
            {
                ПараметрыСобытия.Append(Параметр + ",");
                СвойстваКласса.Append(Параметр + "=" + Параметр + ",");

            }

            string strClass = СвойстваКласса.ToString(0, СвойстваКласса.Length - 1);
            string strParam = ПараметрыСобытия.ToString(0, ПараметрыСобытия.Length - 1);
            string шаблон = $@"Target.{ИмяСобытия} += ({strParam}) =>
            {{
if ({ИмяСобытия}!=null)
{{
               var  {ИмяСобытия}Object =  new {{{strClass}}};
               {ИмяСобытия}({ИмяСобытия}Object);
}}
            }};
";


            ДляРеализацииСобытия.AppendLine(шаблон);

        }
        void ЗаполнитьОписанияСобытий(Tuple<string, List<string>> value)
        {
            string ИмяСобытия = value.Item1;
            var Параметры = value.Item2;
            if (Параметры.Count == 0)
                return;

            string шаблон = @"public object {0};";
            ДляОписанияСобытия.AppendLine(string.Format(шаблон, ИмяСобытия));

        }
        public string СоздатьОписания(Type тип)
        {

            string ТипРеальногоОбъекта = тип.FullName;
            ИмяКласса = ТипРеальногоОбъекта.Replace(".", "_").Replace("+", "_");
            ИсточникСобытия = @"""" + ИмяКласса + @"""";

            var Сборка = тип.GetTypeInfo().Assembly;
            СборкиВПараметрах[Сборка] = true;

            var res = new List<Tuple<string, List<string>>>();
            ПолучитьТекстСобытий(тип, res);
            int i = 1;
            foreach (var value in res)
            {
                i++;

                //     ЗаполнитьОписанияСобытий(value);
                ЗаполнитьРеализацииСобытий(value);
            }



            var ОписанияСобытия = ДляОписанияСобытия.ToString();
            var РеализацииСобытий = ДляРеализацииСобытия.ToString();
            var ListEvents = EventsName.ToString();

            var СтрокаМодуля = string.Format(ШаблонКласса.Template, ИмяКласса, ТипРеальногоОбъекта, ОписанияСобытия, РеализацииСобытий, ListEvents);
            return СтрокаМодуля;
        }

        public static string ПолучитьКодКлассаВрапера(Type тип)
        {
            var ДСМВ = new WrapperModuleCreater();
            string строкаКласса = ДСМВ.СоздатьОписания(тип);
            return строкаКласса;
        }

        public static string GetCodeModuleEventWrapper(Type type)
        {
            return ПолучитьКодКлассаВрапера(type);
        }
    }
}
