using NLog;
using System;
using System.Net.Http;
using vrcosc_magicchatbox.Core.State;
using vrcosc_magicchatbox.Services;
using vrcosc_magicchatbox.UI.Dialogs;
using vrcosc_magicchatbox.ViewModels.State;

namespace vrcosc_magicchatbox.Classes.DataAndSecurity;

internal static class Logging
{
    private static AppUpdateState? _appUpdateState;
    private static IEnvironmentService? _env;
    private static IHttpClientFactory? _httpClientFactory;
    private static IUiDispatcher? _dispatcher;
    private static Core.Services.IVersionService? _versionService;
    private static INavigationService? _nav;

    /// <summary>
    /// Called once from App.OnStartup after DI container is built.
    /// Eliminates all service-locator calls from this class.
    /// </summary>
    public static void Initialize(
        AppUpdateState appUpdateState,
        IEnvironmentService env,
        IHttpClientFactory httpClientFactory,
        IUiDispatcher dispatcher,
        Core.Services.IVersionService versionService,
        INavigationService nav)
    {
        _appUpdateState = appUpdateState;
        _env = env;
        _httpClientFactory = httpClientFactory;
        _dispatcher = dispatcher;
        _versionService = versionService;
        _nav = nav;
    }

    public static readonly Logger LogController = LogManager.GetCurrentClassLogger();

    private static void HandleLoggingError(string context, Exception e)
    {
        try
        {
            LogController.Error($"{context}\n{e.Message}\n{e.StackTrace}");
        }
        catch
        {
            // NLog itself failed; last-resort fallback to stderr
            Console.Error.WriteLine($"{context}\n{e.Message}\n{e.StackTrace}");
        }
    }



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
            new ApplicationError(
                ex, autoClose, msgboxtimeout, _appUpdateState!,
                _env!, _httpClientFactory!, _dispatcher!, _versionService!, _nav!).ShowDialog();
        }
        catch (Exception e)
        {
            HandleLoggingError("Error in ShowMSGBox", e);
            Environment.Exit(10);
        }
    }

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
