using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using MagicChatboxV2.Services;
using MagicChatboxV2.Helpers;
using MagicChatboxV2.Extensions;
using System.Reflection;
using Serilog;
using System.Windows.Threading;
using MagicChatboxV2.UIVM.Windows;

namespace MagicChatboxV2
{
    public partial class App : Application
    {
        private IServiceProvider serviceProvider;
        public delegate PrimaryInterface PrimaryInterfaceFactory();


        public App()
        {
            // Configure the logger
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console()
                .WriteTo.File("logs/application.log", rollingInterval: RollingInterval.Day)
                .CreateLogger();

            // Set the shutdown mode to explicit
            Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            // Handle unhandled exceptions in the Dispatcher
            DispatcherUnhandledException += App_DispatcherUnhandledException;

            // Handle unhandled exceptions in the AppDomain
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        }

        // Handler for unhandled exceptions in the Dispatcher
        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            // Log the exception
            Log.Error(e.Exception, "An unhandled Dispatcher exception occurred");

            // Show a custom error dialog with the exception details
            var errorDialog = new CustomErrorDialog(e.Exception.Message, e.Exception.StackTrace);
            errorDialog.ShowDialog();

            // Mark the exception as handled
            e.Handled = true;
        }

        // Handler for unhandled exceptions in the AppDomain
        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            // Log the exception
            Log.Error((Exception)e.ExceptionObject, "An unhandled Domain exception occurred");

            // Show a custom error dialog with the exception details
            var errorDialog = new CustomErrorDialog(((Exception)e.ExceptionObject).Message, ((Exception)e.ExceptionObject).StackTrace);
            errorDialog.ShowDialog();
        }

        // Configure and build the service provider
        private IServiceProvider ConfigureServices()
        {
            var services = new ServiceCollection();

            // Register services and modules as before
            services.AddSingleton<VRChatMonitorService>();
            services.AddSingleton<SystemTrayService>();
            services.AddSingleton<StartupHelper>();
            services.AddModules(Assembly.GetExecutingAssembly());



            // Register ModuleManagerService
            services.AddSingleton<ModuleManagerService>();

            services.AddSingleton<PrimaryInterface>();
            services.AddSingleton<PrimaryInterfaceFactory>(serviceProvider => () => serviceProvider.GetRequiredService<PrimaryInterface>());


            return services.BuildServiceProvider();
        }


        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            serviceProvider = ConfigureServices();

            // Get the StartupHelper service from the service provider
            var startupHelper = serviceProvider.GetService<StartupHelper>();

            // Start the application logic using the StartupHelper
            startupHelper?.Start();
        }

    }
}
