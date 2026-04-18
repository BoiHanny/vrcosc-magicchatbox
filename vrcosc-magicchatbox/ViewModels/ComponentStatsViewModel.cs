using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.ViewModels.Models;

namespace vrcosc_magicchatbox.ViewModels;

/// <summary>
/// Wraps <see cref="ComponentStatsModule"/> and exposes all component-stats
/// UI properties that were previously delegate wrappers on ViewModel.
/// </summary>
public partial class ComponentStatsViewModel : ObservableObject
{
    private readonly ComponentStatsModule _module;
    private readonly ObservableCollection<ComponentStatsItem> _statsList = new();

    public ComponentStatsViewModel(ComponentStatsModule module)
    {
        _module = module;
    }

    public ComponentStatsModule Module => _module;

    public ReadOnlyObservableCollection<ComponentStatsItem> ComponentStatsList =>
        new ReadOnlyObservableCollection<ComponentStatsItem>(_statsList);

    /// <summary>
    /// Replaces the in-progress stats list with <paramref name="newList"/> and notifies observers.
    /// </summary>
    public void UpdateComponentStatsList(ObservableCollection<ComponentStatsItem> newList)
    {
        _statsList.Clear();
        foreach (var item in newList)
            _statsList.Add(item);
    }

    /// <summary>
    /// Rebuilds the stats list from the module's current data, adding missing items
    /// and removing stale ones while preserving existing entries.
    /// </summary>
    public void SyncComponentStatsList()
    {
        _statsList.Clear();
        foreach (var stat in _module.GetAllStats())
            _statsList.Add(stat);
        RefreshAllProperties();
    }

    public void UpdateComponentStat(StatsComponentType type, string newValue)
        => _module.UpdateStatValue(type, newValue);

    public string GetComponentStatValue(StatsComponentType type)
        => _module.GetStatValue(type);

    public void SetComponentStatMaxValue(StatsComponentType type, string maxValue)
        => _module.SetStatMaxValue(type, maxValue);

    public bool ComponentStatCPUTempVisible
    {
        get => _module.GetShowCPUTemperature();
        set { _module.SetShowCPUTemperature(value); OnPropertyChanged(); }
    }

    public bool ComponentStatCPUWattageVisible
    {
        get => _module.GetShowCPUWattage();
        set { _module.SetShowCPUWattage(value); OnPropertyChanged(); }
    }

    public bool ComponentStatGPUHotSpotVisible
    {
        get => _module.GetShowGPUHotspotTemperature();
        set { _module.SetShowGPUHotspotTemperature(value); OnPropertyChanged(); }
    }

    public bool ComponentStatGPUTempVisible
    {
        get => _module.GetShowGPUTemperature();
        set { _module.SetShowGPUTemperature(value); OnPropertyChanged(); }
    }

    public bool ComponentStatGPUWattageVisible
    {
        get => _module.GetShowGPUWattage();
        set { _module.SetShowGPUWattage(value); OnPropertyChanged(); }
    }

    public string ComponentStatsError => _module.GetWhitchComponentsAreNotAvailableString();

    public bool IsThereAComponentThatIsNotAvailable => _module.IsThereAComponentThatIsNotAvailable();

    public bool IsThereAComponentThatIsNotGettingTempOrWattage => _module.IsThereAComponentThatIsNotGettingTempOrWattage();

    public bool CPU_EnableHardwareTitle
    {
        get => _module.GetHardwareTitleState(StatsComponentType.CPU);
        set { _module.SetHardwareTitle(StatsComponentType.CPU, value); OnPropertyChanged(); }
    }

    public bool CPU_NumberTrailingZeros
    {
        get => _module.GetRemoveNumberTrailing(StatsComponentType.CPU);
        set { _module.SetRemoveNumberTrailing(StatsComponentType.CPU, value); OnPropertyChanged(); }
    }

    public bool CPU_PrefixHardwareTitle
    {
        get => _module.GetShowReplaceWithHardwareName(StatsComponentType.CPU);
        set { _module.SetReplaceWithHardwareName(StatsComponentType.CPU, value); OnPropertyChanged(); }
    }

    public bool CPU_SmallName
    {
        get => _module.GetShowSmallName(StatsComponentType.CPU);
        set { _module.SetShowSmallName(StatsComponentType.CPU, value); OnPropertyChanged(); }
    }

    public string CPUCustomHardwareName
    {
        get => _module.GetCustomHardwareName(StatsComponentType.CPU);
        set { _module.SetCustomHardwareName(StatsComponentType.CPU, value); OnPropertyChanged(); }
    }

    public string CPUHardwareName => _module.GetHardwareName(StatsComponentType.CPU);

    public bool GPU_EnableHardwareTitle
    {
        get => _module.GetHardwareTitleState(StatsComponentType.GPU);
        set { _module.SetHardwareTitle(StatsComponentType.GPU, value); OnPropertyChanged(); }
    }

    public bool GPU_NumberTrailingZeros
    {
        get => _module.GetRemoveNumberTrailing(StatsComponentType.GPU);
        set { _module.SetRemoveNumberTrailing(StatsComponentType.GPU, value); OnPropertyChanged(); }
    }

    public bool GPU_PrefixHardwareTitle
    {
        get => _module.GetShowReplaceWithHardwareName(StatsComponentType.GPU);
        set { _module.SetReplaceWithHardwareName(StatsComponentType.GPU, value); OnPropertyChanged(); }
    }

    public bool GPU_SmallName
    {
        get => _module.GetShowSmallName(StatsComponentType.GPU);
        set { _module.SetShowSmallName(StatsComponentType.GPU, value); OnPropertyChanged(); }
    }

    public string GPUCustomHardwareName
    {
        get => _module.GetCustomHardwareName(StatsComponentType.GPU);
        set { _module.SetCustomHardwareName(StatsComponentType.GPU, value); OnPropertyChanged(); }
    }

    public string GPUHardwareName => _module.GetHardwareName(StatsComponentType.GPU);

    public bool isCPUAvailable
    {
        get => _module.IsStatAvailable(StatsComponentType.CPU);
        set
        {
            _module.SetStatAvailable(StatsComponentType.CPU, value);
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsThereAComponentThatIsNotAvailable));
            OnPropertyChanged(nameof(IsThereAComponentThatIsNotGettingTempOrWattage));
            OnPropertyChanged(nameof(ComponentStatsError));
        }
    }

    public bool IsCPUEnabled
    {
        get => _module.IsStatEnabled(StatsComponentType.CPU);
        set
        {
            _module.ActivateStateState(StatsComponentType.CPU, value);
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsThereAComponentThatIsNotAvailable));
            OnPropertyChanged(nameof(ComponentStatsError));
        }
    }

    public bool IsGPUAvailable
    {
        get => _module.IsStatAvailable(StatsComponentType.GPU);
        set
        {
            _module.SetStatAvailable(StatsComponentType.GPU, value);
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsThereAComponentThatIsNotAvailable));
            OnPropertyChanged(nameof(IsThereAComponentThatIsNotGettingTempOrWattage));
            OnPropertyChanged(nameof(ComponentStatsError));
        }
    }

    public bool IsGPUEnabled
    {
        get => _module.IsStatEnabled(StatsComponentType.GPU);
        set
        {
            _module.ActivateStateState(StatsComponentType.GPU, value);
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsThereAComponentThatIsNotAvailable));
            OnPropertyChanged(nameof(IsThereAComponentThatIsNotGettingTempOrWattage));
            OnPropertyChanged(nameof(ComponentStatsError));
        }
    }

    public bool IsGPUMaxValueShown
    {
        get => _module.IsStatMaxValueShown(StatsComponentType.GPU);
        set { _module.SetStatMaxValueShown(StatsComponentType.GPU, value); OnPropertyChanged(); }
    }

    public bool isRAMAvailable
    {
        get => _module.IsStatAvailable(StatsComponentType.RAM);
        set
        {
            _module.SetStatAvailable(StatsComponentType.RAM, value);
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsThereAComponentThatIsNotAvailable));
            OnPropertyChanged(nameof(IsThereAComponentThatIsNotGettingTempOrWattage));
            OnPropertyChanged(nameof(ComponentStatsError));
        }
    }

    public bool IsRAMEnabled
    {
        get => _module.IsStatEnabled(StatsComponentType.RAM);
        set
        {
            _module.ActivateStateState(StatsComponentType.RAM, value);
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsThereAComponentThatIsNotAvailable));
            OnPropertyChanged(nameof(IsThereAComponentThatIsNotGettingTempOrWattage));
            OnPropertyChanged(nameof(ComponentStatsError));
        }
    }

    public bool isRAMMaxValueShown
    {
        get => _module.IsStatMaxValueShown(StatsComponentType.RAM);
        set { _module.SetStatMaxValueShown(StatsComponentType.RAM, value); OnPropertyChanged(); }
    }

    public bool RAM_EnableHardwareTitle
    {
        get => _module.GetHardwareTitleState(StatsComponentType.RAM);
        set { _module.SetHardwareTitle(StatsComponentType.RAM, value); OnPropertyChanged(); }
    }

    public bool RAM_NumberTrailingZeros
    {
        get => _module.GetRemoveNumberTrailing(StatsComponentType.RAM);
        set { _module.SetRemoveNumberTrailing(StatsComponentType.RAM, value); OnPropertyChanged(); }
    }

    public bool RAM_PrefixHardwareTitle
    {
        get => _module.GetShowReplaceWithHardwareName(StatsComponentType.RAM);
        set { _module.SetReplaceWithHardwareName(StatsComponentType.RAM, value); OnPropertyChanged(); }
    }

    public bool RAM_ShowDDRVersion
    {
        get => _module.GetShowRamDDRVersion();
        set { _module.SetShowRamDDRVersion(value); OnPropertyChanged(); }
    }

    public bool RAM_ShowMaxValue
    {
        get => _module.GetShowMaxValue(StatsComponentType.RAM);
        set { _module.SetShowMaxValue(StatsComponentType.RAM, value); OnPropertyChanged(); }
    }

    public bool RAM_SmallName
    {
        get => _module.GetShowSmallName(StatsComponentType.RAM);
        set { _module.SetShowSmallName(StatsComponentType.RAM, value); OnPropertyChanged(); }
    }

    public string RAMCustomHardwareName
    {
        get => _module.GetCustomHardwareName(StatsComponentType.RAM);
        set { _module.SetCustomHardwareName(StatsComponentType.RAM, value); OnPropertyChanged(); }
    }

    public string RAMHardwareName => _module.GetHardwareName(StatsComponentType.RAM);

    public bool isVRAMAvailable
    {
        get => _module.IsStatAvailable(StatsComponentType.VRAM);
        set
        {
            _module.SetStatAvailable(StatsComponentType.VRAM, value);
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsThereAComponentThatIsNotAvailable));
            OnPropertyChanged(nameof(IsThereAComponentThatIsNotGettingTempOrWattage));
            OnPropertyChanged(nameof(ComponentStatsError));
        }
    }

    public bool IsVRAMEnabled
    {
        get => _module.IsStatEnabled(StatsComponentType.VRAM);
        set
        {
            _module.ActivateStateState(StatsComponentType.VRAM, value);
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsThereAComponentThatIsNotAvailable));
            OnPropertyChanged(nameof(IsThereAComponentThatIsNotGettingTempOrWattage));
            OnPropertyChanged(nameof(ComponentStatsError));
        }
    }

    public bool isVRAMMaxValueShown
    {
        get => _module.IsStatMaxValueShown(StatsComponentType.VRAM);
        set { _module.SetStatMaxValueShown(StatsComponentType.VRAM, value); OnPropertyChanged(); }
    }

    public bool VRAM_EnableHardwareTitle
    {
        get => _module.GetHardwareTitleState(StatsComponentType.VRAM);
        set { _module.SetHardwareTitle(StatsComponentType.VRAM, value); OnPropertyChanged(); }
    }

    public bool VRAM_NumberTrailingZeros
    {
        get => _module.GetRemoveNumberTrailing(StatsComponentType.VRAM);
        set { _module.SetRemoveNumberTrailing(StatsComponentType.VRAM, value); OnPropertyChanged(); }
    }

    public bool VRAM_PrefixHardwareTitle
    {
        get => _module.GetShowReplaceWithHardwareName(StatsComponentType.VRAM);
        set { _module.SetReplaceWithHardwareName(StatsComponentType.VRAM, value); OnPropertyChanged(); }
    }

    public bool VRAM_ShowMaxValue
    {
        get => _module.GetShowMaxValue(StatsComponentType.VRAM);
        set { _module.SetShowMaxValue(StatsComponentType.VRAM, value); OnPropertyChanged(); }
    }

    public bool VRAM_SmallName
    {
        get => _module.GetShowSmallName(StatsComponentType.VRAM);
        set { _module.SetShowSmallName(StatsComponentType.VRAM, value); OnPropertyChanged(); }
    }

    public string VRAMCustomHardwareName
    {
        get => _module.GetCustomHardwareName(StatsComponentType.VRAM);
        set { _module.SetCustomHardwareName(StatsComponentType.VRAM, value); OnPropertyChanged(); }
    }

    public string VRAMHardwareName => _module.GetHardwareName(StatsComponentType.VRAM);

    /// <summary>
    /// Fires <see cref="System.ComponentModel.INotifyPropertyChanged.PropertyChanged"/> for every
    /// public property, forcing all bound UI elements to re-evaluate their values.
    /// </summary>
    public void RefreshAllProperties()
    {
        OnPropertyChanged(nameof(CPUHardwareName));
        OnPropertyChanged(nameof(GPUHardwareName));
        OnPropertyChanged(nameof(RAMHardwareName));
        OnPropertyChanged(nameof(VRAMHardwareName));
        OnPropertyChanged(nameof(IsCPUEnabled));
        OnPropertyChanged(nameof(IsGPUEnabled));
        OnPropertyChanged(nameof(IsRAMEnabled));
        OnPropertyChanged(nameof(IsVRAMEnabled));
        OnPropertyChanged(nameof(isCPUAvailable));
        OnPropertyChanged(nameof(IsGPUAvailable));
        OnPropertyChanged(nameof(isRAMAvailable));
        OnPropertyChanged(nameof(isVRAMAvailable));
        OnPropertyChanged(nameof(CPUCustomHardwareName));
        OnPropertyChanged(nameof(GPUCustomHardwareName));
        OnPropertyChanged(nameof(RAMCustomHardwareName));
        OnPropertyChanged(nameof(VRAMCustomHardwareName));
        OnPropertyChanged(nameof(CPU_EnableHardwareTitle));
        OnPropertyChanged(nameof(GPU_EnableHardwareTitle));
        OnPropertyChanged(nameof(RAM_EnableHardwareTitle));
        OnPropertyChanged(nameof(VRAM_EnableHardwareTitle));
        OnPropertyChanged(nameof(CPU_PrefixHardwareTitle));
        OnPropertyChanged(nameof(GPU_PrefixHardwareTitle));
        OnPropertyChanged(nameof(RAM_PrefixHardwareTitle));
        OnPropertyChanged(nameof(VRAM_PrefixHardwareTitle));
        OnPropertyChanged(nameof(CPU_NumberTrailingZeros));
        OnPropertyChanged(nameof(GPU_NumberTrailingZeros));
        OnPropertyChanged(nameof(RAM_NumberTrailingZeros));
        OnPropertyChanged(nameof(VRAM_NumberTrailingZeros));
        OnPropertyChanged(nameof(CPU_SmallName));
        OnPropertyChanged(nameof(GPU_SmallName));
        OnPropertyChanged(nameof(RAM_SmallName));
        OnPropertyChanged(nameof(VRAM_SmallName));
        OnPropertyChanged(nameof(ComponentStatsError));
        OnPropertyChanged(nameof(IsThereAComponentThatIsNotAvailable));
        OnPropertyChanged(nameof(IsThereAComponentThatIsNotGettingTempOrWattage));
    }
}
