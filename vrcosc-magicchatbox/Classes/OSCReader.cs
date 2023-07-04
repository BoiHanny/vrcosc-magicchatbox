using CoreOSC;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.ViewModels;

namespace vrcosc_magicchatbox.Classes
{

    public static class OSCReader
    {
        private static UDPListener listener;
        private static Dictionary<string, List<object>> oscData = new Dictionary<string, List<object>>();


        public static void StartListening()
        {
            // Define OscPacketCallback - what to do when an OSC packet is received
            void callback(OscPacket packet)
            {
                if (packet is OscMessage message && message.Arguments.Count > 0)
                {
                    // Only process certain addresses
                    if (message.Address.StartsWith("/avatar/parameters/"))
                    {
                        // Check if we already have a list for this address
                        if (!oscData.TryGetValue(message.Address, out var list))
                        {
                            // If not, create a new list and add it to the dictionary
                            list = new List<object>();
                            oscData[message.Address] = list;
                            Logging.WriteInfo($"OSC: created new list for address: {message.Address}");
                        }

                        // Add the received value to the list
                        if (message.Arguments[0] is float value)
                        {
                            list.Add(value);
                            Logging.WriteInfo($"OSC: updated new float value: {message.Address} with the value: {value}");
                        }
                        else if (message.Arguments[0] is bool boolValue)
                        {
                            list.Add(boolValue);
                            Logging.WriteInfo($"OSC: updated new bool value: {message.Address} with the value: {boolValue}");
                        }
                    }
                    else
                    {

                        Logging.WriteInfo(new Exception("Received OSC message with address: " + message.Address).ToString());
                    }
                }
            }

            // Create a new listener if it doesn't exist
            if (listener == null)
            {
                listener = new UDPListener(ViewModel.Instance.OSCPOrtIN, callback);
            }
        }

        public static void StopListening()
        {
            // Dispose of the listener if it exists
            if (listener != null)
            {
                listener.Dispose();
                listener = null;
            }
        }
    }


}
