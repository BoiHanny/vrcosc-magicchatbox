using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.Generic;
using vrcosc_magicchatbox.Classes.Modules;

namespace vrcosc_magicchatbox.ViewModels;

public partial class ComponentStatsViewModel : ObservableObject
{
    private readonly ComponentStatsModule _module;
    private readonly Dictionary<string, object?> _lastRefreshedValues = new();

    public ComponentStatsViewModel(ComponentStatsModule module)
    {
        _module = module;
    }

    public ComponentStatsModule Module => _module;

    public void SyncComponentStatsList()
        => RefreshAllProperties();

    public void UpdateComponentStat(StatsComponentType type, string newValue)
        => _module.UpdateStatValue(type, newValue);

    public string? GetComponentStatValue(StatsComponentType type)
        => _module.GetStatValue(type);

    public void SetComponentStatMaxValue(StatsComponentType type, string maxValue)
        => _module.SetStatMaxValue(type, maxValue);

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

    public string? CPUCustomHardwareName
    {
        get => _module.GetCustomHardwareName(StatsComponentType.CPU);
        set { _module.SetCustomHardwareName(StatsComponentType.CPU, value); OnPropertyChanged(); }
    }

    public string? CPUHardwareName => _module.GetHardwareName(StatsComponentType.CPU);

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

    public string? GPUCustomHardwareName
    {
        get => _module.GetCustomHardwareName(StatsComponentType.GPU);
        set { _module.SetCustomHardwareName(StatsComponentType.GPU, value); OnPropertyChanged(); }
    }

    public string? GPUHardwareName => _module.GetHardwareName(StatsComponentType.GPU);

    public bool isCPUAvailable
    {
        get => _module.IsStatAvailable(StatsComponentType.CPU);
        set
        {
            if (_module.IsStatAvailable(StatsComponentType.CPU) == value)
                return;
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
            if (_module.IsStatAvailable(StatsComponentType.GPU) == value)
                return;
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
            if (_module.IsStatAvailable(StatsComponentType.RAM) == value)
                return;
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

    public string? RAMCustomHardwareName
    {
        get => _module.GetCustomHardwareName(StatsComponentType.RAM);
        set { _module.SetCustomHardwareName(StatsComponentType.RAM, value); OnPropertyChanged(); }
    }

    public string? RAMHardwareName => _module.GetHardwareName(StatsComponentType.RAM);

    public bool isVRAMAvailable
    {
        get => _module.IsStatAvailable(StatsComponentType.VRAM);
        set
        {
            if (_module.IsStatAvailable(StatsComponentType.VRAM) == value)
                return;
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

    public string? VRAMCustomHardwareName
    {
        get => _module.GetCustomHardwareName(StatsComponentType.VRAM);
        set { _module.SetCustomHardwareName(StatsComponentType.VRAM, value); OnPropertyChanged(); }
    }

    public string? VRAMHardwareName => _module.GetHardwareName(StatsComponentType.VRAM);

    public void RefreshAllProperties()
    {
        RaiseIfChanged(nameof(CPUHardwareName), CPUHardwareName);
        RaiseIfChanged(nameof(GPUHardwareName), GPUHardwareName);
        RaiseIfChanged(nameof(RAMHardwareName), RAMHardwareName);
        RaiseIfChanged(nameof(VRAMHardwareName), VRAMHardwareName);
        RaiseIfChanged(nameof(IsCPUEnabled), IsCPUEnabled);
        RaiseIfChanged(nameof(IsGPUEnabled), IsGPUEnabled);
        RaiseIfChanged(nameof(IsRAMEnabled), IsRAMEnabled);
        RaiseIfChanged(nameof(IsVRAMEnabled), IsVRAMEnabled);
        RaiseIfChanged(nameof(isCPUAvailable), isCPUAvailable);
        RaiseIfChanged(nameof(IsGPUAvailable), IsGPUAvailable);
        RaiseIfChanged(nameof(isRAMAvailable), isRAMAvailable);
        RaiseIfChanged(nameof(isVRAMAvailable), isVRAMAvailable);
        RaiseIfChanged(nameof(CPUCustomHardwareName), CPUCustomHardwareName);
        RaiseIfChanged(nameof(GPUCustomHardwareName), GPUCustomHardwareName);
        RaiseIfChanged(nameof(RAMCustomHardwareName), RAMCustomHardwareName);
        RaiseIfChanged(nameof(VRAMCustomHardwareName), VRAMCustomHardwareName);
        RaiseIfChanged(nameof(CPU_EnableHardwareTitle), CPU_EnableHardwareTitle);
        RaiseIfChanged(nameof(GPU_EnableHardwareTitle), GPU_EnableHardwareTitle);
        RaiseIfChanged(nameof(RAM_EnableHardwareTitle), RAM_EnableHardwareTitle);
        RaiseIfChanged(nameof(VRAM_EnableHardwareTitle), VRAM_EnableHardwareTitle);
        RaiseIfChanged(nameof(CPU_PrefixHardwareTitle), CPU_PrefixHardwareTitle);
        RaiseIfChanged(nameof(GPU_PrefixHardwareTitle), GPU_PrefixHardwareTitle);
        RaiseIfChanged(nameof(RAM_PrefixHardwareTitle), RAM_PrefixHardwareTitle);
        RaiseIfChanged(nameof(VRAM_PrefixHardwareTitle), VRAM_PrefixHardwareTitle);
        RaiseIfChanged(nameof(CPU_NumberTrailingZeros), CPU_NumberTrailingZeros);
        RaiseIfChanged(nameof(GPU_NumberTrailingZeros), GPU_NumberTrailingZeros);
        RaiseIfChanged(nameof(RAM_NumberTrailingZeros), RAM_NumberTrailingZeros);
        RaiseIfChanged(nameof(VRAM_NumberTrailingZeros), VRAM_NumberTrailingZeros);
        RaiseIfChanged(nameof(CPU_SmallName), CPU_SmallName);
        RaiseIfChanged(nameof(GPU_SmallName), GPU_SmallName);
        RaiseIfChanged(nameof(RAM_SmallName), RAM_SmallName);
        RaiseIfChanged(nameof(VRAM_SmallName), VRAM_SmallName);
        RaiseIfChanged(nameof(ComponentStatsError), ComponentStatsError);
        RaiseIfChanged(nameof(IsThereAComponentThatIsNotAvailable), IsThereAComponentThatIsNotAvailable);
        RaiseIfChanged(nameof(IsThereAComponentThatIsNotGettingTempOrWattage), IsThereAComponentThatIsNotGettingTempOrWattage);
    }

    private void RaiseIfChanged(string propertyName, object? value)
    {
        if (_lastRefreshedValues.TryGetValue(propertyName, out var existing) && Equals(existing, value))
            return;

        _lastRefreshedValues[propertyName] = value;
        OnPropertyChanged(propertyName);
    }
}
