using System;
using System.Collections.Generic;
using System.Text;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.ViewModels;


public class OSCParameter
{
    private readonly object lockObject = new object();
    private readonly Stack<object> values;

    public OSCParameter(
        string name,
        string address,
        OSCParameterType type,
        int maxHistory = 5,
        bool isBuiltIn = false,
        bool logChange = false,
        bool execute = true)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Address = address ?? throw new ArgumentNullException(nameof(address));
        MaxHistory = maxHistory > 0 ? maxHistory : throw new ArgumentOutOfRangeException(nameof(maxHistory));
        IsBuiltIn = isBuiltIn;
        LogChanges = logChange;
        values = new Stack<object>(maxHistory);
        Execute = execute;

        switch(type)
        {
            case OSCParameterType.Int32:
                Type = typeof(int);
                break;
            case OSCParameterType.Single:
                Type = typeof(float);
                break;
            case OSCParameterType.Boolean:
                Type = typeof(bool);
                break;
            case OSCParameterType.String:
                Type = typeof(string);
                break;
            default:
                throw new ArgumentException($"Invalid parameter type: {type}");
        }
    }

    public object GetLatestValue()
    {
        lock(lockObject)
        {
            return values.Peek();
        }
    }

    public void LogBuilder()
    {
        var sb = new StringBuilder();
        if(LogChanges)
        {
            object x = GetLatestValue().ToString();
            sb.Append(IsBuiltIn ? "BuiltIn OSCParameter -> [" : "Dynamic OSCParameter -> [")
                .Append(Name)
                .Append("] Type: (")
                .Append(Type.Name)
                .Append(") ")
                .Append("Has been set to: ")
                .Append(GetLatestValue())
                .Append(" | History Count: ")
                .Append(values.Count)
                .Append("/")
                .Append(MaxHistory);
            Logging.WriteInfo(sb.ToString());
        }
    }


    public void SetValue(object value)
    {
        if(value == null || value.GetType() != Type)
        {
            throw new ArgumentException("Invalid parameter value type");
        }

        lock(lockObject)
        {
            if(values.Count >= MaxHistory)
            {
                values.Pop();
            }
            values.Push(value);
        }
    }

    public string Address { get; set; }

    public bool Execute { get; set; }

    public bool IsBuiltIn { get; set; }

    public bool LogChanges { get; set; }

    public int MaxHistory { get; set; }

    public string Name { get; set; }

    public Type Type { get; set; }

    public LinkedList<object> Values { get; set; }
}

public static class OSCParameters
{
    private static readonly Dictionary<string, OSCParameter> parameters = new Dictionary<string, OSCParameter>();

    static OSCParameters()
    {
        parameters["IsLocal"] = new OSCParameter(
            "IsLocal",
            "/avatar/parameters/IsLocal",
            OSCParameterType.Boolean,
            5,
            true,
            true);
        parameters["Viseme"] = new OSCParameter(
            "Viseme",
            "/avatar/parameters/Viseme",
            OSCParameterType.Int32,
            5,
            true,
            false,
            false);
        parameters["Voice"] = new OSCParameter(
            "Voice",
            "/avatar/parameters/Voice",
            OSCParameterType.Single,
            5,
            true,
            true);
        parameters["GestureLeft"] = new OSCParameter(
            "GestureLeft",
            "/avatar/parameters/GestureLeft",
            OSCParameterType.Int32,
            5,
            true,
            true);
        parameters["GestureRight"] = new OSCParameter(
            "GestureRight",
            "/avatar/parameters/GestureRight",
            OSCParameterType.Int32,
            5,
            true,
            true);
        parameters["GestureLeftWeight"] = new OSCParameter(
            "GestureLeftWeight",
            "/avatar/parameters/GestureLeftWeight",
            OSCParameterType.Single,
            5,
            true,
            false,
            false);
        parameters["GestureRightWeight"] = new OSCParameter(
            "GestureRightWeight",
            "/avatar/parameters/GestureRightWeight",
            OSCParameterType.Single,
            5,
            true,
            false,
            false);
        parameters["AngularY"] = new OSCParameter(
            "AngularY",
            "/avatar/parameters/AngularY",
            OSCParameterType.Single,
            5,
            true,
            false,
            false);
        parameters["VelocityX"] = new OSCParameter(
            "VelocityX",
            "/avatar/parameters/VelocityX",
            OSCParameterType.Single,
            5,
            true,
            false,
            false);
        parameters["VelocityY"] = new OSCParameter(
            "VelocityY",
            "/avatar/parameters/VelocityY",
            OSCParameterType.Single,
            5,
            true,
            false,
            false);
        parameters["VelocityZ"] = new OSCParameter(
            "VelocityZ",
            "/avatar/parameters/VelocityZ",
            OSCParameterType.Single,
            5,
            true,
            false,
            false);
        parameters["VelocityMagnitude"] = new OSCParameter(
            "VelocityMagnitude",
            "/avatar/parameters/VelocityMagnitude",
            OSCParameterType.Single,
            5,
            true,
            false,
            false);
        parameters["Upright"] = new OSCParameter(
            "Upright",
            "/avatar/parameters/Upright",
            OSCParameterType.Single,
            5,
            true,
            false,
            false);
        parameters["Grounded"] = new OSCParameter(
            "Grounded",
            "/avatar/parameters/Grounded",
            OSCParameterType.Boolean,
            5,
            true,
            false,
            false);
        parameters["Seated"] = new OSCParameter(
            "Seated",
            "/avatar/parameters/Seated",
            OSCParameterType.Boolean,
            5,
            true,
            false,
            false);
        parameters["AFK"] = new OSCParameter("AFK", "/avatar/parameters/AFK", OSCParameterType.Boolean, 5, true, true);
        parameters["AvatarChange"] = new OSCParameter(
            "AvatarChange",
            "/avatar/Change",
            OSCParameterType.String,
            15,
            true,
            true);

        for(int i = 1; i <= 16; i++)
        {
            parameters["Expression" + i.ToString()] = new OSCParameter(
                "Expression" + i.ToString(),
                "/avatar/parameters/Expression" + i.ToString(),
                OSCParameterType.Int32,
                5,
                true,
                true);
        }

        parameters["TrackingType"] = new OSCParameter(
            "TrackingType",
            "/avatar/parameters/TrackingType",
            OSCParameterType.Int32,
            5,
            true,
            true);
        parameters["VRMode"] = new OSCParameter(
            "VRMode",
            "/avatar/parameters/VRMode",
            OSCParameterType.Int32,
            5,
            true,
            true);
        parameters["MuteSelf"] = new OSCParameter(
            "MuteSelf",
            "/avatar/parameters/MuteSelf",
            OSCParameterType.Boolean,
            5,
            true,
            true);
        parameters["InStation"] = new OSCParameter(
            "InStation",
            "/avatar/parameters/InStation",
            OSCParameterType.Boolean,
            5,
            true,
            true);
        parameters["Earmuffs"] = new OSCParameter(
            "Earmuffs",
            "/avatar/parameters/Earmuffs",
            OSCParameterType.Boolean,
            5,
            true,
            true);

        // Avatar Scaling Parameters
        parameters["ScaleModified"] = new OSCParameter(
            "ScaleModified",
            "/avatar/parameters/ScaleModified",
            OSCParameterType.Boolean,
            5,
            true,
            true);
        parameters["ScaleFactor"] = new OSCParameter(
            "ScaleFactor",
            "/avatar/parameters/ScaleFactor",
            OSCParameterType.Single,
            5,
            true,
            true);
        parameters["ScaleFactorInverse"] = new OSCParameter(
            "ScaleFactorInverse",
            "/avatar/parameters/ScaleFactorInverse",
            OSCParameterType.Single,
            5,
            true,
            true);
        parameters["EyeHeightAsMeters"] = new OSCParameter(
            "EyeHeightAsMeters",
            "/avatar/parameters/EyeHeightAsMeters",
            OSCParameterType.Single,
            5,
            true,
            true);
        parameters["EyeHeightAsPercent"] = new OSCParameter(
            "EyeHeightAsPercent",
            "/avatar/parameters/EyeHeightAsPercent",
            OSCParameterType.Single,
            5,
            true,
            true);
    }

    public static OSCParameter GetParameter(string name)
    {
        if(name == null)
        {
            throw new ArgumentNullException(nameof(name));
        }

        if(!parameters.TryGetValue(name, out OSCParameter parameter))
        {
            throw new ArgumentException($"No such parameter: {name}");
        }
        return parameter;
    }
}
