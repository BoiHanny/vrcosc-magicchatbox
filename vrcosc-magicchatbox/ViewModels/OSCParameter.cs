using System;
using System.Collections.Generic;
using System.Text;
using vrcosc_magicchatbox.Classes.DataAndSecurity;



public class OSCParameter
{

    public OSCParameter(string name, string address, string type, int maxHistory = 5, bool isBuiltIn = false, bool logChange = false)
    {
        Name = name;
        Address = address;
        Values = new LinkedList<object>();
        MaxHistory = maxHistory;
        IsBuiltIn = isBuiltIn;
        LogChanges = logChange;


        switch (type)
        {
            case "Int32":
                Type = typeof(int);
                break;
            case "Single":
                Type = typeof(float);
                break;
            case "Boolean":
                Type = typeof(bool);
                break;
            default:
                throw new ArgumentException($"Invalid parameter type: {type}");
        }
    }

    public object GetValue()
    {
        return Values.Last.Value;
    }

    public void LogBuilder()
    {
        var sb = new StringBuilder();
        if (LogChanges)
        {
            sb.Append(IsBuiltIn ? "BuiltIn OSCParameter -> [" : "Dynamic OSCParameter -> [")
                .Append(Name)
                .Append("] Type: (")
                .Append(Type.Name)
                .Append(") ")
                .Append("Has been set to: ")
                .Append(GetValue())
                .Append(" | History Count: ")
                .Append(Values.Count)
                .Append("/")
                .Append(MaxHistory);
            Logging.WriteInfo(sb.ToString());
        }
        
    }


    public void SetValue(object value)
    {
        if (Values.Count >= MaxHistory)
        {
            Values.RemoveFirst();
        }
        Values.AddLast(value);
        LogBuilder();
    }

    public string Address { get; set; }
    public bool IsBuiltIn { get; set; }
    public bool LogChanges { get; set; }
    public int MaxHistory { get; set; }
    public string Name { get; set; }
    public Type Type { get; set; }
    public LinkedList<object> Values { get; set; }
}

public static class OSCParameters
{
    public static OSCParameter AFK { get; private set; } = new OSCParameter("AFK", "/avatar/parameters/AFK", "Boolean", 5, true, true);
    public static OSCParameter AngularY { get; private set; } = new OSCParameter("AngularY", "/avatar/parameters/AngularY", "Single", 5, true);
    public static OSCParameter AvatarChange { get; } = new OSCParameter("AvatarChange", "/avatar/AvatarChange", "Int32", 5, true, true);
    public static OSCParameter Face { get; private set; } = new OSCParameter("Face", "/avatar/parameters/Face", "Int32", 5, true, false);
    public static OSCParameter GestureLeft { get; private set; } = new OSCParameter("GestureLeft", "/avatar/parameters/GestureLeft", "Int32", 5, true, true);
    public static OSCParameter GestureLeftWeight { get; private set; } = new OSCParameter("GestureLeftWeight", "/avatar/parameters/GestureLeftWeight", "Single", 5, true, true);
    public static OSCParameter GestureRight { get; private set; } = new OSCParameter("GestureRight", "/avatar/parameters/GestureRight", "Int32", 5, true);
    public static OSCParameter GestureRightWeight { get; private set; } = new OSCParameter("GestureRightWeight", "/avatar/parameters/GestureRightWeight", "Single", 5, true, true);
    public static OSCParameter Grounded { get; private set; } = new OSCParameter("Grounded", "/avatar/parameters/Grounded", "Boolean", 5, true, true);
    public static OSCParameter InStation { get; private set; } = new OSCParameter("InStation", "/avatar/parameters/InStation", "Boolean", 5, true, false);
    public static OSCParameter MuteSelf { get; private set; } = new OSCParameter("MuteSelf", "/avatar/parameters/MuteSelf", "Boolean", 5, true, true);
    public static OSCParameter Seated { get; private set; } = new OSCParameter("Seated", "/avatar/parameters/Seated", "Boolean", 5, true, false);
    public static OSCParameter TrackingType { get; private set; } = new OSCParameter("TrackingType", "/avatar/parameters/TrackingType", "Int32", 5, true, true);
    public static OSCParameter Upright { get; private set; } = new OSCParameter("Upright", "/avatar/parameters/Upright", "Single", 5, true, true);
    public static OSCParameter VelocityMagnitude { get; private set; } = new OSCParameter("VelocityMagnitude", "/avatar/parameters/VelocityMagnitude", "Single", 5, true);
    public static OSCParameter VelocityX { get; private set; } = new OSCParameter("VelocityX", "/avatar/parameters/VelocityX", "Single", 5, true, false);
    public static OSCParameter VelocityY { get; private set; } = new OSCParameter("VelocityY", "/avatar/parameters/VelocityY", "Single", 5, true, false);
    public static OSCParameter VelocityZ { get; private set; } = new OSCParameter("VelocityZ", "/avatar/parameters/VelocityZ", "Single", 5, true, false);
    public static OSCParameter Viseme { get; private set; } = new OSCParameter("Viseme", "/avatar/parameters/Viseme", "Int32", 5, true);
    public static OSCParameter VRMode { get; private set; } = new OSCParameter("VRMode", "/avatar/parameters/VRMode", "Int32", 5, true, true);
    public static OSCParameter Earmuffs { get; private set; } = new OSCParameter("Earmuffs", "/avatar/parameters/Earmuffs", "Boolean", 5, true, true);

}
