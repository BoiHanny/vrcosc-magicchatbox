using MagicChatboxV2.UIVM.Models;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace MagicChatboxV2.Services;

public class ModuleManagerService : IDisposable
{
    private readonly IServiceProvider serviceProvider;
    private readonly List<IModule> modules = new List<IModule>();

    public ModuleManagerService(IServiceProvider serviceProvider)
    {
        this.serviceProvider = serviceProvider;
        InitializeModules();
    }

    public void InitializeModules()
    {
        // Directly get the instances of all modules implementing IModule
        var moduleInstances = serviceProvider.GetServices<IModule>();

        foreach (var module in moduleInstances)
        {
            try
            {
                module.Initialize();
                module.LoadState();
                modules.Add(module);
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Failed to initialize module {module.ModuleName}");
                // Optionally, show a user-friendly message or take corrective action
            }
        }
    }


    // Dispose of the modules when the service is disposed
    public void Dispose()
    {
        DisposeModules();
    }

    // Start or stop updates for all modules based on their enabled state
    public void StartModuleUpdates()
        {
            foreach (var module in modules.Where(m => m.IsEnabled))
            {
                module.StartUpdates();
            }
        }

    // Start or stop updates for all modules based on their enabled state
        public void StopModuleUpdates()
        {
            foreach (var module in modules)
            {
                module.StopUpdates();
            }
        }

    // Update data for all active modules
        public void UpdateModuleData()
        {
            foreach (var module in modules.Where(m => m.IsActive))
            {
                module.UpdateData();
            }
        }


        public string GetFormattedOutputs()
        {
            // Concatenate or cycle through outputs based on your strategy
            return string.Join("\n", modules.Where(m => m.IsActive).Select(m => m.GetFormattedOutput()));
        }

        public void DisposeModules()
        {
            foreach (var module in modules)
            {
                module.Dispose();
            }
            modules.Clear();
        }
    }
