using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using NLog;
using System;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using vrcosc_magicchatbox.DataAndSecurity;
using vrcosc_magicchatbox.UI.Dialogs;
using vrcosc_magicchatbox.ViewModels;

namespace vrcosc_magicchatbox.Classes.DataAndSecurity
{
    internal static class Logging
    {
        public static readonly Logger LogController = LogManager.GetCurrentClassLogger();

        public static void ShowMSGBox(
            int msgboxtimeout = 10000,
            bool autoClose = true,
            string msgboxtext = "something went wrong...",
            Exception? ex = null)
        {
            if(ex != null)
                msgboxtext = ex.Message;
            new ApplicationError(ex, autoClose, msgboxtimeout).ShowDialog();
        }

        public static void WriteException(
            Exception? ex = null,
            bool MSGBox = true,
            bool autoclose = false,
            bool exitapp = false,
            bool log = true
            )
        {
            if(log && ex != null)
                LogController.Error(ex.ToString());

            // Check if MSGBox is true AND ex is not null
            if(MSGBox && ex != null)
                ShowMSGBox(msgboxtimeout: 10000, autoClose: autoclose, msgboxtext: ex.Message, ex: ex);

            if(exitapp)
                System.Environment.Exit(10);
        }

        public static void WriteInfo(string info, bool MSGBox = false, bool autoclose = false, bool exitapp = false)
        {
            LogController.Info(info);
            if(MSGBox)
                ShowMSGBox(msgboxtext: info, autoClose: autoclose);
            if(exitapp)
                System.Environment.Exit(10);
        }

        public static void WriteDebug(string debug, bool MSGBox = false, bool autoclose = false, bool exitapp = false)
        {
            LogController.Debug(debug);
            if(MSGBox)
                ShowMSGBox(msgboxtext: debug, autoClose: autoclose);
            if(exitapp)
                ShowMSGBox(msgboxtext: "debug did throw application exit", autoClose: autoclose);
        }
    }
}
