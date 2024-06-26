using CommunityToolkit.Mvvm.ComponentModel;
using System;

namespace MagicChatboxV2.Models
{
    public interface ISettings : IDisposable
    {
        bool Enabled { get; set; }
        bool EnabledVR { get; set; }
        bool EnabledDesktop { get; set; }
        string SettingVersion { get; set; }
        int ModulePosition { get; set; }
        int ModuleMemberGroupNumbers { get; set; }
    }
}
