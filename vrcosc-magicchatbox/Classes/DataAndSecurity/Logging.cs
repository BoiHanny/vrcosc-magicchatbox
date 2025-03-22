using NLog;
using System;
using System.Windows.Forms;
using vrcosc_magicchatbox.UI.Dialogs;

namespace vrcosc_magicchatbox.Classes.DataAndSecurity;

internal static class Logging
{
    // Logger instance for the application
    public static readonly Logger LogController = LogManager.GetCurrentClassLogger();

    // Centralized method to handle errors during logging
    private static void HandleLoggingError(string context, Exception e)
    {
        try
        {
            // Attempt to log the error to NLog
            LogController.Error($"{context}\n{e.Message}\n{e.StackTrace}");
        }
        catch
        {
            // If NLog fails, fallback to console logging
            Console.Error.WriteLine($"{context}\n{e.Message}\n{e.StackTrace}");
        }
    }



    // Display a message box with error information
    public static void ShowMSGBox(
        int msgboxtimeout = 10000,
        bool autoClose = true,
        string msgboxtext = "something went wrong...",
        Exception? ex = null)
    {
        try
        {
            if (ex != null)
                msgboxtext = ex.Message;
            new ApplicationError(ex, autoClose, msgboxtimeout).ShowDialog();
        }
        catch (Exception e)
        {
            HandleLoggingError("Error in ShowMSGBox", e);
            Environment.Exit(10);
        }
    }

    // Log a debug message and optionally show a message box and/or exit the application
    public static void WriteDebug(string debug, bool MSGBox = false, bool autoclose = false, bool exitapp = false)
    {
        try
        {
            LogController.Debug(debug);
            if (MSGBox)
                ShowMSGBox(msgboxtext: debug, autoClose: autoclose);
            if (exitapp)
                Environment.Exit(10);
        }
        catch (Exception e)
        {
            HandleLoggingError("Error in WriteDebug", e);
            if (exitapp)
                Environment.Exit(10);
        }
    }

    // Log an exception and optionally show a message box and/or exit the application
    public static void WriteException(
        Exception? ex = null,
        bool MSGBox = true,
        bool autoclose = false,
        bool exitapp = false,
        bool log = true)
    {
        try
        {
            if (log && ex != null)
                LogController.Error(ex.ToString());

            // Show message box if requested and exception is not null
            if (MSGBox && ex != null)
                ShowMSGBox(msgboxtimeout: 10000, autoClose: autoclose, msgboxtext: ex.Message, ex: ex);

            if (exitapp)
                Environment.Exit(10);
        }
        catch (Exception e)
        {
            HandleLoggingError("Error in WriteException", e);
            if (exitapp)
                Environment.Exit(10);
        }
    }

    // Log an informational message and optionally show a message box and/or exit the application
    public static void WriteInfo(string info, bool MSGBox = false, bool autoclose = false, bool exitapp = false)
    {
        try
        {
            LogController.Info(info);
            if (MSGBox)
                ShowMSGBox(msgboxtext: info, autoClose: autoclose);
            if (exitapp)
                Environment.Exit(10);
        }
        catch (Exception e)
        {
            HandleLoggingError("Error in WriteInfo", e);
            if (exitapp)
                Environment.Exit(10);
        }
    }
}
