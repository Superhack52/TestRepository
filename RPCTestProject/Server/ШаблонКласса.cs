using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices.ComTypes;

namespace NetObjectToNative
{
    
    public static class ШаблонКласса
    {

        public static string Template = @"public class WrapperForEvent{0}
    {{
        public Action<Guid,object> CallBack;
        public {1} Target;
        Dictionary<Guid, ClassForEventCEF> EventStoage=new Dictionary<Guid, ClassForEventCEF>();
        {4}
       public WrapperForEvent{0}(Action<Guid,object> CallBack, {1} Target)
        {{

            this.CallBack = CallBack;
            this.Target = Target;

            {3}

        }}

       public void AddEventHandler(Guid EventKey, string EventName)
        {{
            EventInfo ei = GetType().GetEvent(EventName);
            var forEvent = new ClassForEventCEF(this,EventKey, ei,CallBack);
            EventStoage.Add(EventKey, forEvent);

        }}

      public  void RemoveEventHandler(Guid EventKey)
        {{
            ClassForEventCEF cfe = null;
           if (EventStoage.TryGetValue(EventKey,out cfe))
                {{
                EventStoage.Remove(EventKey);
                cfe.RemoveEventHandler();

            }}

        }}
 public void RemoveAllEventHandler()
        {{

           foreach( var cfe in EventStoage.Values)
                cfe.RemoveEventHandler();

            EventStoage.Clear();
        }}

{2}

public static object CreateObject(Action<Guid,object> CallBack, {1} Target)
{{

    return new WrapperForEvent{0}(CallBack, Target);
}}
    }}

return new Func<Action<Guid,object>, {1}, object>(WrapperForEvent{0}.CreateObject);


";

    }
}
