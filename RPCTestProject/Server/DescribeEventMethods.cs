using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;


namespace NetObjectToNative
{

    public class DescribeEventMethods
    {
        StringBuilder МетодыСобытий = new StringBuilder();
        StringBuilder ПодпискиНаСобытия = new StringBuilder();
        public List<Tuple<string, Type>> ПолучитьПараметрыСТипами(EventInfo событие)
        {
            var rez = new List<Tuple<string, Type>>();
            var параметры = событие.
 EventHandlerType.
 GetMethod("Invoke").
 GetParameters();
            foreach (var параметр in параметры)
            {
                rez.Add(new Tuple<string, Type>(параметр.Name, параметр.ParameterType));
            }

            return rez;
        }


        void ПолучитьОписаниеПараметров(List<Tuple<string, Type>> value)
        {

            if (value.Count == 0)
                return;

            var str = "";
            if (value.Count == 0)
            {
                str = @"//  параметр value:=null для универсального вызова";
                МетодыСобытий.AppendLine(str);

            }
            else if (value.Count == 1)
            {
                str = @"//  параметр value:{0}";
                МетодыСобытий.AppendLine(string.Format(str, value[0].Item2.FullName));
            }
            else
            {
                str = @"//  параметр value:Анонимный Тип
                       // Свойства параметра";
                МетодыСобытий.AppendLine(str);
                foreach (var свойство in value)
                {
                    str = "// {0}:{1}";
                    МетодыСобытий.AppendLine(string.Format(str, свойство.Item1, свойство.Item2.FullName));
                }

            }
        }
        public void ПолучитьТекстСобытийСтипомПараметров(Type тип, List<Tuple<string, List<Tuple<string, Type>>>> res)
        {


            foreach (EventInfo e in тип.GetEvents())
            {
                var Метод = e.EventHandlerType.GetMethod("Invoke");

                if (Метод.ReturnType != typeof(void))
                {
                    continue;
                }
                res.Add(new Tuple<string, List<Tuple<string, Type>>>(e.Name, ПолучитьПараметрыСТипами(e)));

            }

        }


        void ЗаполнитьМетодыСобытий(Tuple<string, List<Tuple<string, Type>>> value)
        {

            string событие = value.Item1;

            string Данные = "dynamic value";//value.Item2.Count > 0 ? "value" : "";

            //Console.WriteLine("EventWithTwoParameter" + wrap.toString(value));
            string ТелоПроцедуры = value.Item2.Count > 0 ? @"Console.WriteLine(""" + событие + @" ""+wrap.toString(value));
             //value(ClientRPC.AutoWrapClient.FlagDeleteObject);" : @"Console.WriteLine(""" + событие + @""");";
            string стр = @"
            public void {0}({1})
                {{            
                {2}
                }}
";

            var StrAddEventHandler = @"wrapForEvents.AddEventHandler(""{0}"",new Action<dynamic>({0}));";
            ПодпискиНаСобытия.AppendLine(string.Format(StrAddEventHandler, событие));
            ПолучитьОписаниеПараметров(value.Item2);
            МетодыСобытий.AppendLine(string.Format(стр, событие, Данные, ТелоПроцедуры));



        }


        public string СоздатьОписаниеМодуля(Type тип)
        {



            StringBuilder ФункцияСозданияВрапера = new StringBuilder();


            var str = @"
                   void CreateWrapperForEvents(ClientRPC.TCPClientConnector connector,dynamic obj)
                    {{
                        var  wrapForEvents=connector.CreateWrapperForEvents(obj);
                      
                        {0}
                       // установить переменную wrapForEvents переменной класса
                      this.WrapperFor{1}=wrapForEvents;
                   }}
";

            //    ФункцияСозданияВрапера.AppendLine(стр);

            var res = new List<Tuple<string, List<Tuple<string, Type>>>>();
            ПолучитьТекстСобытийСтипомПараметров(тип, res);

            foreach (var value in res)
            {

                //ЗаполнитьФункцияСозданияВрапера(value);
                ЗаполнитьМетодыСобытий(value);

            }

            //       ФункцияСозданияВрапера.AppendLine("КонецПроцедуры");
            ФункцияСозданияВрапера.AppendLine("");
            ФункцияСозданияВрапера.AppendLine(МетодыСобытий.ToString());

            string ТипРеальногоОбъекта = тип.FullName;
            var ИмяКласса = ТипРеальногоОбъекта.Replace(".", "_").Replace("+", "_");

            ФункцияСозданияВрапера.AppendLine("");

            var CreateWrapperForEvents = string.Format(str, ПодпискиНаСобытия.ToString(), ИмяКласса);
            ФункцияСозданияВрапера.AppendLine(CreateWrapperForEvents);

            //  ФункцияСозданияВрапера.AppendLine(string.Format(Заключение, ИмяКласса));
            return ФункцияСозданияВрапера.ToString();
        }

        public static string GetCodeModuleForEvents(Type тип)
        {

            return (new DescribeEventMethods()).СоздатьОписаниеМодуля(тип);

        }

    }
}