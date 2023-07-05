using CoreOSC;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Threading;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.ViewModels;

namespace vrcosc_magicchatbox.Classes
{

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
                                var newParameter = new OSCParameter(parameterName, message.Address, message.Arguments[0].GetType().Name);
                                (ViewModel.Instance.DynamicOSCData as IDictionary<string, object>)[parameterName] = newParameter;
                            }

                            var dynamicParameter = parameterObject as OSCParameter;
                            dynamicParameter.SetValue(message.Arguments[0]);
                        }
                    }
                }
            }

            if (listener == null)
            {
                listener = new UDPListener(ViewModel.Instance.OSCPOrtIN, callback);
            }
        }

        public static void StopListening()
        {
            if (listener != null)
            {
                listener.Dispose();
                listener = null;
            }
        }

    }


}
