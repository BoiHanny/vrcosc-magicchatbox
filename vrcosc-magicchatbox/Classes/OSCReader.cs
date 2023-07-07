using CoreOSC;
using System;
using System.Collections.Generic;
using System.Threading;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.ViewModels;

public static class OSCReader
{
    private static UDPListener listener;
    private static CancellationTokenSource cancellation = new CancellationTokenSource();


    public static void StartListening()
    {
        void callback(OscPacket packet)
        {

            if (packet is OscMessage message && message.Arguments.Count > 0)
            {

                if (message.Address.StartsWith("/avatar/parameters/"))
                {

                    var parameterName = message.Address.Substring("/avatar/parameters/".Length);

                    var parameterProperty = typeof(OSCParameters).GetProperty(parameterName);

                    if (parameterProperty != null)
                    {
                        // This is a built-in parameter
                        var parameter = (OSCParameter)parameterProperty.GetValue(null);
                        parameter.SetValue(message.Arguments[0]);
                        ViewModel.Instance.BuiltInOSCData[parameterName] = parameter;

                    }
                    else
                    {
                        // This is a dynamic parameter

                        if (!(ViewModel.Instance.DynamicOSCData as IDictionary<string, object>).TryGetValue(parameterName, out var parameterObject))
                        {
                            // This is a new parameter
                            Logging.WriteInfo("This is a new parameter");

                            var newParameter = new OSCParameter(parameterName, message.Address, message.Arguments[0].GetType().Name);
                            (ViewModel.Instance.DynamicOSCData as IDictionary<string, object>)[parameterName] = newParameter;
                            parameterObject = newParameter;

                            Logging.WriteInfo($"New dynamic parameter {parameterName} created with value: {message.Arguments[0]}");
                        }

                        var dynamicParameter = parameterObject as OSCParameter;
                        dynamicParameter.SetValue(message.Arguments[0]);

                        Logging.WriteInfo($"Dynamic parameter {parameterName} set with value: {message.Arguments[0]}");
                    }
                }
                else
                {

                }
            }
        }

        try
        {
            if (listener == null)
            {
                listener = new UDPListener(ViewModel.Instance.OSCPOrtIN, callback);
                Logging.WriteInfo("Listener started successfully on port: " + ViewModel.Instance.OSCPOrtIN);
            }
        }
        catch (Exception ex)
        {
            Logging.WriteException(ex);
        }
    }

    public static void StopListening()
    {
        try
        {
            if (listener != null)
            {
                listener.Dispose();
                listener = null;
                Logging.WriteInfo("Listener stopped successfully.");
            }
        }
        catch (Exception ex)
        {
            Logging.WriteException(ex);
        }
    }

}
