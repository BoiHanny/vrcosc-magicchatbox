using CoreOSC;
using System;
using System.Collections.Generic;
using System.Text;
using vrcosc_magicchatbox.ViewModels;

namespace vrcosc_magicchatbox.Classes.DataAndSecurity
{
    public class OSCReader
    {
        private static UDPListener listener;

        public static void StartListening()
        {
            if (listener != null)
            {
                throw new InvalidOperationException("Listener is already running.");
            }

            void callback(OscPacket packet)
            {
                if (packet is OscMessage message && message.Arguments.Count > 0)
                {
                    var sb = new StringBuilder();
                    string parameterName;
                    var parameterPrefix = "/avatar/parameters/";

                    if (message.Address.StartsWith(parameterPrefix))
                    {
                        // This is a parameter
                        parameterName = message.Address.Substring(parameterPrefix.Length);
                    }
                    else if (message.Address == "/avatar/change")
                    {
                        // This is the avatar change built-in parameter
                        parameterName = "AvatarChange";
                    }
                    else
                    {
                        // This is a different built-in parameter
                        parameterName = message.Address;
                    }

                    try
                    {
                        // This is a built-in parameter
                        var parameter = OSCParameters.GetParameter(parameterName);
                        parameter.SetValue(message.Arguments[0]);
                        ViewModel.Instance.BuiltInOSCData[parameterName] = parameter;
                        parameter.LogBuilder();
                        if (ViewModel.Instance.AvatarSyncExecute)
                        {

                            sb.Append("BuiltIn OSCParameter !! [")
                            .Append(parameter.Name)
                            .Append("] Type: (")
                            .Append(parameter.Type.Name)
                            .Append(") ")
                            .Append("RUN >>> ");
                            Logging.WriteInfo(sb.ToString());
                            AvatarSyncController.RunSync(OSCParameters.GetParameter(parameterName));
                        }




                    }
                    catch (ArgumentException)
                    {
                        // This is a dynamic parameter
                        bool isNewParameter = false;

                        if (!(ViewModel.Instance.DynamicOSCData as IDictionary<string, object>).TryGetValue(
                            parameterName,
                            out var parameterObject))
                        {
                            // This is a new parameter
                            var newParameter = new OSCParameter(
                                parameterName,
                                message.Address,
                                GetOSCParameterType(message.Arguments[0].GetType()),
                                5,
                                false,
                                true);
                            (ViewModel.Instance.DynamicOSCData as IDictionary<string, object>)[parameterName] = newParameter;
                            parameterObject = newParameter;
                            isNewParameter = true;
                        }

                        OSCParameter dynamicParameter = parameterObject as OSCParameter;
                        dynamicParameter.SetValue(message.Arguments[0]);



                        if (isNewParameter)
                        {
                            sb.Append("Dynamic OSCParameter ++ [")
                            .Append(dynamicParameter.Name)
                            .Append("] Type: (")
                            .Append(dynamicParameter.Type.Name)
                            .Append(") ")
                            .Append("Added and has been set to: ")
                            .Append(dynamicParameter.GetLatestValue())
                            .Append(" | History allowed: ")
                            .Append(dynamicParameter.MaxHistory);
                            Logging.WriteInfo(sb.ToString());
                        }

                        dynamicParameter.LogBuilder();

                        sb.Clear();

                        if (ViewModel.Instance.AvatarSyncExecute)
                        {
                            sb.Append("Dynamic OSCParameter !! [")
                            .Append(dynamicParameter.Name)
                            .Append("] Type: (")
                            .Append(dynamicParameter.Type.Name)
                            .Append(") ")
                            .Append("RUN >>> ");
                            Logging.WriteInfo(sb.ToString());
                            AvatarSyncController.RunSync(dynamicParameter);
                        }

                    }
                }
            }

            try
            {
                listener = new UDPListener(ViewModel.Instance.OSCPOrtIN, callback);
                Logging.WriteInfo("OSCParameter listener started successfully on port: " + ViewModel.Instance.OSCPOrtIN);
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex);
                listener?.Dispose();
                listener = null;
            }
        }

        private static OSCParameterType GetOSCParameterType(Type type)
        {
            switch (type)
            {
                case Type t when t == typeof(int):
                    return OSCParameterType.Int32;
                case Type t when t == typeof(float):
                    return OSCParameterType.Single;
                case Type t when t == typeof(bool):
                    return OSCParameterType.Boolean;
                case Type t when t == typeof(string):
                    return OSCParameterType.String;  // if you have a String type in OSCParameterType
                default:
                    throw new ArgumentException($"Invalid parameter type: {type}");
            }
        }

        public static void StopListening()
        {
            try
            {
                listener?.Dispose();
                listener = null;
                Logging.WriteInfo("Listener stopped successfully.");
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex);
            }
        }
    }
}

