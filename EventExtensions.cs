using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

static public class cEventHelper
{
    static Dictionary<Type, List<FieldInfo>> dicEventFieldInfos = new Dictionary<Type, List<FieldInfo>>();

    static BindingFlags AllBindings
    {
        get { return BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static; }
    }

    //--------------------------------------------------------------------------------
    static List<FieldInfo> GetTypeEventFields(Type t)
    {
        if (dicEventFieldInfos.ContainsKey(t))
            return dicEventFieldInfos[t];

        List<FieldInfo> lst = new List<FieldInfo>();
        BuildEventFields(t, lst);
        dicEventFieldInfos.Add(t, lst);
        return lst;
    }

    //--------------------------------------------------------------------------------
    static void BuildEventFields(Type t, List<FieldInfo> lst)
    {
        // Type.GetEvent(s) gets all Events for the type AND it's ancestors
        // Type.GetField(s) gets only Fields for the exact type.
        //  (BindingFlags.FlattenHierarchy only works on PROTECTED & PUBLIC
        //   doesn't work because Fieds are PRIVATE)

        // NEW version of this routine uses .GetEvents and then uses .DeclaringType
        // to get the correct ancestor type so that we can get the FieldInfo.
        foreach (EventInfo ei in t.GetEvents(AllBindings))
        {
            Type dt = ei.DeclaringType;
            FieldInfo fi = dt.GetField(ei.Name, AllBindings);
            if (fi != null)
                lst.Add(fi);
        }

        // OLD version of the code - called itself recursively to get all fields
        // for 't' and ancestors and then tested each one to see if it's an EVENT
        // Much less efficient than the new code
        /*
              foreach (FieldInfo fi in t.GetFields(AllBindings))
              {
                EventInfo ei = t.GetEvent(fi.Name, AllBindings);
                if (ei != null)
                {
                  lst.Add(fi);
                  Console.WriteLine(ei.Name);
                }
              }
              if (t.BaseType != null)
                BuildEventFields(t.BaseType, lst);*/
    }

    //--------------------------------------------------------------------------------
    static EventHandlerList GetStaticEventHandlerList(Type t, object obj)
    {
        MethodInfo mi = t.GetMethod("get_Events", AllBindings);
        return (EventHandlerList)mi.Invoke(obj, new object[] { });
    }

    //--------------------------------------------------------------------------------
    public static void RemoveAllEventHandlers(object obj) { RemoveEventHandler(obj, ""); }

    //--------------------------------------------------------------------------------
    public static void RemoveEventHandler(object obj, string EventName)
    {
        if (obj == null)
            return;

        Type t = typeof(Control); //obj.GetType();
        List<FieldInfo> event_fields = GetTypeEventFields(t);
        EventHandlerList static_event_handlers = null;

        foreach (FieldInfo fi in event_fields)
        {
            if (EventName != "" && string.Compare(EventName, fi.Name, true) != 0)
                continue;

            // After hours and hours of research and trial and error, it turns out that
            // STATIC Events have to be treated differently from INSTANCE Events...
            if (fi.IsStatic)
            {
                // STATIC EVENT
                if (static_event_handlers == null)
                    static_event_handlers = GetStaticEventHandlerList(t, obj);

                object idx = fi.GetValue(obj);
                Delegate eh = static_event_handlers[idx];
                if (eh == null)
                    continue;

                Delegate[] dels = eh.GetInvocationList();
                if (dels == null)
                    continue;

                EventInfo ei = t.GetEvent(fi.Name, AllBindings);
                foreach (Delegate del in dels)
                    ei.RemoveEventHandler(obj, del);
            }
            else
            {
                // INSTANCE EVENT
                EventInfo ei = t.GetEvent(fi.Name, AllBindings);
                if (ei != null)
                {
                    object val = fi.GetValue(obj);
                    Delegate mdel = (val as Delegate);
                    if (mdel != null)
                    {
                        foreach (Delegate del in mdel.GetInvocationList())
                            ei.RemoveEventHandler(obj, del);
                    }
                }
            }
        }
    }

    //--------------------------------------------------------------------------------
}




public static class EventExtensions
{






    public static void ClearEventHandlers(this object obj, string eventName)
    {
        if (obj == null)
        {
            return;
        }

        var objType =  obj.GetType();
        var eventInfo = objType.GetEvent(eventName);
        if (eventInfo == null)
        {
            return;
        }

        var isEventProperty = false;
        var type = objType;
        FieldInfo eventFieldInfo = null;
        while (type != null)
        {
            /* Find events defined as field */
            eventFieldInfo = type.GetField(eventName, BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy);
            if (eventFieldInfo != null && (eventFieldInfo.FieldType == typeof(MulticastDelegate) || eventFieldInfo.FieldType.IsSubclassOf(typeof(MulticastDelegate))))
            {
                break;
            }

            /* Find events defined as property { add; remove; } */
            eventFieldInfo = type.GetField("EVENT_" + eventName.ToUpper(), BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy);
            if (eventFieldInfo != null)
            {
                isEventProperty = true;
                break;
            }

            type = type.BaseType;
        }

        if (eventFieldInfo == null)
        {
            return;
        }

        if (isEventProperty)
        {
            // Default Events Collection Type
            RemoveHandler<EventHandlerList>(obj, eventFieldInfo);
            // Infragistics Events Collection Type
            //RemoveHandler<EventHandlerDictionary>(obj, eventFieldInfo);
            return;
        }

        if (!(eventFieldInfo.GetValue(obj) is Delegate eventDelegate))
        {
            return;
        }

        // Remove Field based event handlers
        foreach (var d in eventDelegate.GetInvocationList())
        {
            eventInfo.RemoveEventHandler(obj, d);
        }
    }

    private static void RemoveHandler<T>(object obj, FieldInfo eventFieldInfo)
    {
        var objType = obj.GetType();
        var eventPropertyValue = eventFieldInfo.GetValue(obj);

        if (eventPropertyValue == null)
        {
            return;
        }

        var propertyInfo = objType.GetProperties(BindingFlags.NonPublic | BindingFlags.Instance)
                                  .FirstOrDefault(p => p.Name == "Events" && p.PropertyType == typeof(T));
        if (propertyInfo == null)
        {
            return;
        }

        var eventList = propertyInfo?.GetValue(obj, null);
        switch (eventList)
        {
            case null:
                return;
            //case EventHandlerDictionary typedEventList:
            //    typedEventList.RemoveHandler(eventPropertyValue, typedEventList[eventPropertyValue]);
            //    break;
        }
    }
}