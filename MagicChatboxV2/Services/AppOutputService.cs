using Serilog;
using Serilog.Events;
using System;
using System.Windows.Threading;

namespace MagicChatboxV2.Services
{
    public interface IAppOutputService
    {
        void HandleUnhandledDomainException(object sender, UnhandledExceptionEventArgs e);
        void HandleUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e);
        void LogExceptionWithDialog(Exception ex, string message = "An error occurred", LogEventLevel level = LogEventLevel.Error, bool exitApp = false, bool allowContinue = false, int timeout = 5000, bool autoclose = false);
        bool ShowErrorDialog(Exception ex, string message = "Something went wrong...", int timeout = 5000, bool autoclose = false, bool allowContinue = false);
        void ShowInfoDialog(string message, int timeout = 5000, bool autoclose = false);
        void LogException(Exception ex, string message = "An error occurred", LogEventLevel level = LogEventLevel.Error, bool exitApp = false, int timeout = 5000, bool autoclose = false);
        void LogFatal(string message);
        void LogError(string message, bool exitApp = false, int timeout = 5000, bool autoclose = false);
        void LogWarning(string message);
        void LogInformation(string message);
    }

    public class AppOutputService : IAppOutputService
    {
        private readonly IDialogService _dialogService;
        private readonly ILogger _logger;

        public AppOutputService(IDialogService dialogService, ILogger logger)
        {
            _dialogService = dialogService;
            _logger = logger;
        }

        public void LogExceptionWithDialog(Exception ex, string message, LogEventLevel level, bool exitApp, bool allowContinue, int timeout, bool autoclose)
        {
            _logger.Write(level, ex, message);
            bool userWantsToExit = ShowErrorDialog(ex, message, timeout, autoclose, allowContinue);
            if (exitApp && userWantsToExit)
                Environment.Exit(1);
        }

        public bool ShowErrorDialog(Exception ex, string message, int timeout, bool autoclose, bool allowContinue)
        {
            return _dialogService.ShowErrorDialog(ex, message, timeout, autoclose, allowContinue);
        }

        public void ShowInfoDialog(string message, int timeout, bool autoclose)
        {
            _dialogService.ShowInfoDialog(message, timeout, autoclose);
        }

        public void LogException(Exception ex, string message, LogEventLevel level, bool exitApp, int timeout, bool autoclose)
        {
            _logger.Write(level, ex, message);
            if (exitApp)
            {
                bool userWantsToExit = ShowErrorDialog(ex, message, timeout, autoclose, false);
                if (userWantsToExit)
                    Environment.Exit(1);
            }
        }

        public void LogFatal(string message)
        {
            _logger.Fatal(message);
            Environment.Exit(1);
        }

        public void LogError(string message, bool exitApp = false, int timeout = 5000, bool autoclose = false)
        {
            _logger.Error(message);
            if (exitApp)
            {
                bool userWantsToExit = ShowErrorDialog(new Exception(message), message, timeout, autoclose, false);
                if (userWantsToExit)
                    Environment.Exit(1);
            }
        }

        public void LogWarning(string message)
        {
            _logger.Warning(message);
        }

        public void LogInformation(string message)
        {
            _logger.Information(message);
        }

        public void HandleUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            LogExceptionWithDialog(e.Exception, "An unhandled exception occurred", LogEventLevel.Error, true, true, 5000, false);
        }

        public void HandleUnhandledDomainException(object sender, UnhandledExceptionEventArgs e)
        {
            LogExceptionWithDialog(e.ExceptionObject as Exception, "An unhandled domain exception occurred", LogEventLevel.Error, true, true, 5000, false);
        }
    }
}
