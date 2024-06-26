using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Threading.Tasks;

namespace MagicChatboxV2.Models
{
    public interface IModule : IDisposable
    {
        string ModuleName { get; }
        string ModuleVersion { get; }
        string ModuleDescription { get; }
        bool IsActive { get; set; }

        DateTime LastUpdated { get; }
        event EventHandler DataUpdated;

        Task InitializeAsync();
        Task LoadStateAsync();
        Task SaveStateAsync();
        void StartUpdates();
        void StopUpdates();
        void UpdateData();
        string GetFormattedOutput();
        string UpdateAndGetOutput();
    }

    public interface IModule<T> : IModule where T : ISettings
    {
        T Settings { get; set; }
    }
}
