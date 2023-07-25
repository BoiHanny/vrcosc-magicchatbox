using System.Collections.Generic;
using System;



    public class OSCParameter
    {
        public string Name { get; set; }
        public string Address { get; set; }
        public Type Type { get; set; }
        public LinkedList<object> Values { get; set; }
        public int MaxHistory { get; set; }

        public OSCParameter(string name, string address, string type, int maxHistory = 5)
        {
            Name = name;
            Address = address;
            Values = new LinkedList<object>();
            MaxHistory = maxHistory;

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

        public void SetValue(object value)
        {
            if (Values.Count >= MaxHistory)
            {
                Values.RemoveFirst();
            }
            Values.AddLast(value);
        }

        public object GetValue()
        {
            return Values.Last.Value;
        }
    }

public static class OSCParameters
{
    public static OSCParameter Face { get; private set; } = new OSCParameter("Face", "/avatar/parameters/Face", "Int32", 5);
    public static OSCParameter VelocityZ { get; private set; } = new OSCParameter("VelocityZ", "/avatar/parameters/VelocityZ", "Single", 5);
    public static OSCParameter VelocityY { get; private set; } = new OSCParameter("VelocityY", "/avatar/parameters/VelocityY", "Single", 5);
    public static OSCParameter VelocityX { get; private set; } = new OSCParameter("VelocityX", "/avatar/parameters/VelocityX", "Single", 5);
    public static OSCParameter InStation { get; private set; } = new OSCParameter("InStation", "/avatar/parameters/InStation", "Boolean", 5);
    public static OSCParameter Seated { get; private set; } = new OSCParameter("Seated", "/avatar/parameters/Seated", "Boolean", 5);
    public static OSCParameter AFK { get; private set; } = new OSCParameter("AFK", "/avatar/parameters/AFK", "Boolean", 5);
    public static OSCParameter Upright { get; private set; } = new OSCParameter("Upright", "/avatar/parameters/Upright", "Single", 5);
    public static OSCParameter VelocityMagnitude { get; private set; } = new OSCParameter("VelocityMagnitude", "/avatar/parameters/VelocityMagnitude", "Single", 5);
    public static OSCParameter AngularY { get; private set; } = new OSCParameter("AngularY", "/avatar/parameters/AngularY", "Single", 5);
    public static OSCParameter Grounded { get; private set; } = new OSCParameter("Grounded", "/avatar/parameters/Grounded", "Boolean", 5);
    public static OSCParameter MuteSelf { get; private set; } = new OSCParameter("MuteSelf", "/avatar/parameters/MuteSelf", "Boolean", 5);
    public static OSCParameter VRMode { get; private set; } = new OSCParameter("VRMode", "/avatar/parameters/VRMode", "Int32", 5);
    public static OSCParameter TrackingType { get; private set; } = new OSCParameter("TrackingType", "/avatar/parameters/TrackingType", "Int32", 5);
    public static OSCParameter GestureRightWeight { get; private set; } = new OSCParameter("GestureRightWeight", "/avatar/parameters/GestureRightWeight", "Single", 5);
    public static OSCParameter GestureRight { get; private set; } = new OSCParameter("GestureRight", "/avatar/parameters/GestureRight", "Int32", 5);
    public static OSCParameter GestureLeftWeight { get; private set; } = new OSCParameter("GestureLeftWeight", "/avatar/parameters/GestureLeftWeight", "Single", 5);
    public static OSCParameter GestureLeft { get; private set; } = new OSCParameter("GestureLeft", "/avatar/parameters/GestureLeft", "Int32", 5);
    public static OSCParameter Viseme { get; private set; } = new OSCParameter("Viseme", "/avatar/parameters/Viseme", "Int32", 5);

}
