namespace NetObjectToNative
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Text;

    public class WrapperForEvents<T>
    {
        public static readonly Func<Action<Guid, object>, T, object> WrapperCreator;

        public static Func<Action<Guid, object>, T, object> CreateWrapper()
        {
            Type typeTarget = typeof(T);
            string typeTargetStr = typeTarget.FullName;
            var className = "WrapperFor" + typeTargetStr.Replace(".", "_").Replace("+", "_");

            var creator = new WrapperModuleCreator();
            string classRow = creator.CreateDescription(typeTarget);

            var scr = Microsoft.CodeAnalysis.Scripting.ScriptOptions.Default;

            var assemblies = creator.AssembliesInParameters.Keys.ToArray();

            scr = scr.WithReferences(assemblies)
                .WithImports("System", "NetObjectToNative", "System.Collections.Generic", "System.Reflection");

            return (Func<Action<Guid, object>, T, object>)Microsoft.CodeAnalysis.CSharp.Scripting.CSharpScript.EvaluateAsync(classRow, scr).Result;
        }

        static WrapperForEvents()
        {
            WrapperCreator = CreateWrapper();
        }
    }

    public class WrapperModuleCreator
    {
        private StringBuilder _eventDescription = new StringBuilder();
        private StringBuilder _eventBody = new StringBuilder();
        private StringBuilder _eventsName = new StringBuilder();
        private string _className;
        private string _eventSource;
        public Dictionary<Assembly, bool> AssembliesInParameters = new Dictionary<Assembly, bool>();

        public WrapperModuleCreator()
        {
            var assemblies = "mscorlib,System.Private.CoreLib.ni,System.Runtime,System.Collections,System.Reflection".Split(',');

            foreach (var str in assemblies)
                AssembliesInParameters.Add(NetObjectToNative.GetAssembly(str, true), true);

            AssembliesInParameters.Add(typeof(WrapperModuleCreator).GetTypeInfo().Assembly, true);
        }

        public static void WriteStatic()
        {
            Console.WriteLine("Мое значение");
        }

        public List<string> GetParameters(EventInfo @event, out bool addTo)
        {
            addTo = true;
            var rez = new List<string>();
            var invokeMethod = @event.EventHandlerType.GetMethod("Invoke");

            if (invokeMethod.ReturnType != typeof(void))
            {
                addTo = false;
                return rez;
            }

            var parameters = invokeMethod.GetParameters();
            foreach (var parameter in parameters)
            {
                rez.Add(parameter.Name);
                var assembly = parameter.ParameterType.GetTypeInfo().Assembly;
                AssembliesInParameters[assembly] = true;
            }

            return rez;
        }

        public void GetEventText(Type type, List<Tuple<string, List<string>>> res)
        {
            foreach (EventInfo e in type.GetEvents())
            {
                var parameters = GetParameters(e, out var addTo);
                if (addTo) res.Add(new Tuple<string, List<string>>(e.Name, parameters));
            }
        }

        private void FillEventRealization(Tuple<string, List<string>> value)
        {
            string eventName = value.Item1;
            var parameters = value.Item2;

            _eventsName.AppendLine($"public event Action<object> {eventName};");
            if (parameters.Count == 0)
            {
                var str = $@"Target.{eventName} += () =>
                {{
                   if ({eventName}!=null)
                    {eventName}(null);
                }};";

                _eventBody.AppendLine(str);

                return;
            }
            else if (parameters.Count == 1)
            {
                string parameterName = parameters[0];
                var str = $@"Target.{eventName} += ({parameterName}) =>
                {{
                    if ({eventName}!=null){eventName}({parameterName});
                }};";

                _eventBody.AppendLine(str);
                return;
            }

            StringBuilder eventParameters = new StringBuilder();
            StringBuilder eventProperties = new StringBuilder();
            foreach (var parameter in parameters)
            {
                eventParameters.Append(parameter + ",");
                eventProperties.Append(parameter + "=" + parameter + ",");
            }

            string strClass = eventProperties.ToString(0, eventProperties.Length - 1);
            string strParam = eventParameters.ToString(0, eventParameters.Length - 1);
            string template = $@"Target.{eventName} += ({strParam}) =>
            {{
                if ({eventName}!=null)
                {{
                 var  {eventName}Object =  new {{{strClass}}};
                {eventName}({eventName}Object);
                }}
            }};
            ";

            _eventBody.AppendLine(template);
        }

        private void FillEventDescription(Tuple<string, List<string>> value)
        {
            string eventName = value.Item1;
            var parameters = value.Item2;
            if (parameters.Count == 0) return;

            string template = @"public object {0};";
            _eventDescription.AppendLine(string.Format(template, eventName));
        }

        public string CreateDescription(Type type)
        {
            string realType = type.FullName;
            _className = realType.Replace(".", "_").Replace("+", "_");
            _eventSource = @"""" + _className + @"""";

            var assembly = type.GetTypeInfo().Assembly;
            AssembliesInParameters[assembly] = true;

            var res = new List<Tuple<string, List<string>>>();
            GetEventText(type, res);
            int i = 1;
            foreach (var value in res)
            {
                i++;

                //     FillEventDescription(value);
                FillEventRealization(value);
            }

            return string.Format(ШаблонКласса.Template, _className, realType, _eventDescription, _eventBody, _eventsName); 
        }

        public static string GetWrapCode(Type type)
        {
            var creator = new WrapperModuleCreator();
            return creator.CreateDescription(type);
        }

        public static string GetCodeModuleEventWrapper(Type type) => GetWrapCode(type);
    }
}