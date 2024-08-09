using MagicChatboxV2.Models;
using MagicChatboxV2.Modules;
using MagicChatboxV2.Services;
using MagicChatboxV2.Startup.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace MagicChatboxV2.Startup
{
    public class ServiceConfigurator
    {
        public static void ConfigureServices(HostBuilderContext hostContext, IServiceCollection services)
        {
            ConfigureWindows(services);
            ConfigureDialogs(services);
            ConfigureViewModels(services);
            ConfigureDataLayer(services);
            ConfigureAppServices(services);
            ConfigureModules(services);
            ConfigureLogging(services);
        }

        private static void ConfigureWindows(IServiceCollection services)
        {
            services.AddSingleton<LoadingWindow>();
            services.AddSingleton<MainWindow>();
        }

        private static void ConfigureDialogs(IServiceCollection services)
        {
            services.AddSingleton<IDialogService, DialogService>();
        }

        private static void ConfigureViewModels(IServiceCollection services)
        {
            services.AddSingleton<LoadingWindowViewModel>();
            // Add other ViewModels
        }

        private static void ConfigureDataLayer(IServiceCollection services)
        {
            // Configure data layer services if needed
        }

        private static void ConfigureAppServices(IServiceCollection services)
        {
            services.AddSingleton<IAppOutputService, AppOutputService>();
            services.AddSingleton<ISettingsService, SettingsService>();
            // Add other services as needed
        }

        private static void ConfigureModules(IServiceCollection services)
        {
            services.AddSingleton<CurrentTimeModule>();
            services.AddSingleton<IModule>(provider => provider.GetRequiredService<CurrentTimeModule>());
            services.AddSingleton<IModule<CurrentTimeSettings>>(provider => provider.GetRequiredService<CurrentTimeModule>());
            // Register other modules similarly
        }

        private static void ConfigureLogging(IServiceCollection services)
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console()
                .WriteTo.File("logs/log.txt", rollingInterval: RollingInterval.Day)
                .CreateLogger();

            services.AddSingleton(Log.Logger);
        }
    }
}
