using System;
using System.Net;
using Rug.Osc;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.ViewModels;

public static class OscReceiver
{
    //public static void CheckOSCConnection()
    //{
    //    using (var receiver = new Rug.Osc.OscReceiver(IPAddress.Parse(ViewModel.Instance.OSCIP),ViewModel.Instance.OSCPortIn))
    //    {
    //        try
    //        {
    //            receiver.Connect();
    //            DateTime startTime = DateTime.Now;
    //            string addressToCheck = "/avatar/parameters/AFK";

    //            while ((DateTime.Now - startTime).TotalMilliseconds < 1000)
    //            {
    //                if (receiver.State == OscSocketState.Connected)
    //                {
    //                    OscPacket packet;
    //                    if (receiver.TryReceive(out packet))
    //                    {
    //                        if (packet.Error == OscPacketError.None && packet is OscMessage message)
    //                        {
    //                            if (message.Address == addressToCheck)
    //                            {
    //                                if (message.Count == 1 && message[0] is bool afk)
    //                                {
    //                                    ViewModel.Instance.VrcConnected = true;
    //                                    ViewModel.Instance.AfkStatus = afk;
    //                                    double responseTimeMs = (DateTime.Now - startTime).TotalMilliseconds;
    //                                    ViewModel.Instance.OscResponseTimeMs = responseTimeMs;
    //                                    receiver.Close();
    //                                    return;
    //                                }
    //                            }
    //                        }
    //                    }
    //                }
    //                System.Threading.Thread.Sleep(100);
    //            }
    //            ViewModel.Instance.VrcConnected = false;
    //            receiver.Close();
    //        }
    //        catch (Exception ex)
    //        {
    //            Logging.WriteException(ex, makeVMDump: false, MSGBox: false);
    //            ViewModel.Instance.VrcConnected = false;
    //            receiver.Close();
    //        }
    //    }
    //}
}