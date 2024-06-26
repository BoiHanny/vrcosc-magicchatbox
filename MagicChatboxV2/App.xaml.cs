using MagicChatboxV2.Services;
using MagicChatboxV2.Startup.Windows;
using MagicChatboxV2.Startup;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using MagicChatboxV2.Models;
using MagicChatboxV2.Modules;

namespace MagicChatboxV2
{
    public partial class App : Application
    {
        private IAppOutputService _appOutputService = null;

        public static IHost? AppHost { get; private set; }

        public App()
        {
            AppHost = Host.CreateDefaultBuilder()
                .ConfigureServices(ServiceConfigurator.ConfigureServices)
                .Build();
        }

        protected override async void OnStartup(StartupEventArgs e)
        {
            await AppHost!.StartAsync();

            _appOutputService = AppHost.Services.GetRequiredService<IAppOutputService>();
            AppDomain.CurrentDomain.UnhandledException += _appOutputService.HandleUnhandledDomainException;
            DispatcherUnhandledException += _appOutputService.HandleUnhandledException;

            LoadingWindow loadingWindow = AppHost.Services.GetRequiredService<LoadingWindow>();
            loadingWindow.Show();

            var modules = AppHost.Services.GetServices<IModule>().ToList();
            var initializationTasks = modules.Select(module => module.InitializeAsync());
            await Task.WhenAll(initializationTasks);
        }

        protected override async void OnExit(ExitEventArgs e)
        {
            var modules = AppHost.Services.GetServices<IModule>().ToList();
            var saveTasks = modules.Select(module => module.SaveStateAsync());
            await Task.WhenAll(saveTasks);

            await AppHost!.StopAsync();
            base.OnExit(e);
        }
    }
}
