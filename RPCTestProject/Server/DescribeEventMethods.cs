namespace NetObjectToNative
{
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using System.Text;

    public class DescribeEventMethods
    {
        private StringBuilder _eventMethod = new StringBuilder();
        private StringBuilder _eventSubscriptions = new StringBuilder();

        public List<Tuple<string, Type>> GetParametersWithType(EventInfo @event)
        {
            var parametersWithType = new List<Tuple<string, Type>>();
            var parameters = @event.EventHandlerType.GetMethod("Invoke").GetParameters();

            foreach (var parameterInfo in parameters)
            {
                parametersWithType.Add(new Tuple<string, Type>(parameterInfo.Name, parameterInfo.ParameterType));
            }

            return parametersWithType;
        }

        private void GetParametersDescription(List<Tuple<string, Type>> value)
        {
            if (value.Count == 0) return;

            if (value.Count == 0)
            {
                _eventMethod.AppendLine(@"//  параметр value:=null для универсального вызова");
            }
            else if (value.Count == 1)
            {
                _eventMethod.AppendLine(string.Format(@"//  параметр value:{0}", value[0].Item2.FullName));
            }
            else
            {
                string str = @"//  параметр value:Анонимный Тип
                       // Свойства параметра";
                _eventMethod.AppendLine(str);
                foreach (var свойство in value)
                {
                    str = "// {0}:{1}";
                    _eventMethod.AppendLine(string.Format(str, свойство.Item1, свойство.Item2.FullName));
                }
            }
        }

        public void GetEventTextWithParametersType(Type type, List<Tuple<string, List<Tuple<string, Type>>>> events)
        {
            foreach (EventInfo e in type.GetEvents())
            {
                var method = e.EventHandlerType.GetMethod("Invoke");

                if (method != null && method.ReturnType != typeof(void)) continue;
                events.Add(new Tuple<string, List<Tuple<string, Type>>>(e.Name, GetParametersWithType(e)));
            }
        }

        private void FillEventMethod(Tuple<string, List<Tuple<string, Type>>> value)
        {
            string @event = value.Item1;

            string data = "dynamic value";//value.Item2.Count > 0 ? "value" : "";

            string body = value.Item2.Count > 0 ? @"Console.WriteLine(""" + @event + @" ""+wrap.toString(value));
             //value(ClientRPC.AutoWrapClient.FlagDeleteObject);" : @"Console.WriteLine(""" + @event + @""");";
            string text = @"
            public void {0}({1})
                {{
                {2}
                }}";

            var StrAddEventHandler = @"wrapForEvents.AddEventHandler(""{0}"",new Action<dynamic>({0}));";
            _eventSubscriptions.AppendLine(string.Format(StrAddEventHandler, @event));
            GetParametersDescription(value.Item2);
            _eventMethod.AppendLine(string.Format(text, @event, data, body));
        }

        public string CreateModuleDescription(Type type)
        {
            var wrapFuncText = new StringBuilder();

            var str = @"
                   void CreateWrapperForEvents(ClientRPC.TCPClientConnector connector,dynamic obj)
                    {{
                        var  wrapForEvents=connector.CreateWrapperForEvents(obj);

                        {0}
                       // установить переменную wrapForEvents переменной класса
                      this.WrapperFor{1}=wrapForEvents;
                   }}";

            var res = new List<Tuple<string, List<Tuple<string, Type>>>>();
            GetEventTextWithParametersType(type, res);

            foreach (var value in res)
            {
                //ЗаполнитьФункцияСозданияВрапера(value);
                FillEventMethod(value);
            }

            //       ФункцияСозданияВрапера.AppendLine("КонецПроцедуры");
            wrapFuncText.AppendLine("");
            wrapFuncText.AppendLine(_eventMethod.ToString());

            var className = type.FullName?.Replace(".", "_").Replace("+", "_");

            wrapFuncText.AppendLine("");

            var createWrapperForEvents = string.Format(str, _eventSubscriptions, className);
            wrapFuncText.AppendLine(createWrapperForEvents);

            //  ФункцияСозданияВрапера.AppendLine(string.Format(Заключение, ИмяКласса));
            return wrapFuncText.ToString();
        }

        public static string GetCodeModuleForEvents(Type тип)
        {
            return (new DescribeEventMethods()).CreateModuleDescription(тип);
        }
    }
}