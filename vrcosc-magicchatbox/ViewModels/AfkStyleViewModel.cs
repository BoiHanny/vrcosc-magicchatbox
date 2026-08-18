using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Classes.Modules.Afk;
using vrcosc_magicchatbox.Core.Osc;
using vrcosc_magicchatbox.Core.Services;
using vrcosc_magicchatbox.Core.State;

namespace vrcosc_magicchatbox.ViewModels;

public partial class AfkStyleViewModel : ObservableObject
{
    private const string PreviewElapsed = "12ᵐ 04ˢ";

    private readonly Lazy<IModuleHost> _modules;
    private readonly IUiDispatcher _dispatcher;

    private AfkModuleSettings? _observedSettings;
    private AfkStyle? _observedStyle;

    public AfkStyleViewModel(Lazy<IModuleHost> modules, IUiDispatcher dispatcher)
    {
        _modules = modules;
        _dispatcher = dispatcher;
    }

    private AfkModuleSettings? Settings
    {
        get
        {
            var settings = _modules.Value.Afk?.Settings;
            if (!ReferenceEquals(settings, _observedSettings))
            {
                if (_observedSettings != null)
                    _observedSettings.PropertyChanged -= OnSettingsChanged;

                _observedSettings = settings;

                if (_observedSettings != null)
                    _observedSettings.PropertyChanged += OnSettingsChanged;

                ObserveSelectedStyle();
            }

            return settings;
        }
    }

    public ObservableCollection<AfkStyle>? Styles => Settings?.AllStyles;

    public bool CanEditSelected => SelectedStyle is { IsBuiltIn: false };

    public string EditHint => SelectedStyle is { IsBuiltIn: true }
        ? "This one ships with MagicChatbox, so it updates with the app. Duplicate it to make it yours."
        : string.Empty;

    public AfkStyle? SelectedStyle
    {
        get
        {
            var style = Settings?.ActiveStyle;
            ObserveSelectedStyle(style);
            return style;
        }
        set
        {
            var settings = Settings;
            if (settings == null || value == null || settings.ActiveStyleId == value.Id)
                return;

            settings.ActiveStyleId = value.Id;
            settings.SaveSettings();
            RaiseStyleChanged();
        }
    }

    public string PreviewLine
    {
        get
        {
            var style = Settings?.ActiveStyle;
            if (style == null)
                return string.Empty;

            return style.Render(PreviewElapsed);
        }
    }

    public string PreviewCost
    {
        get
        {
            int cost = PreviewLine.Length;
            return $"{cost} of {OscBuildContext.MaxOscLength} characters";
        }
    }

    public bool CanDeleteSelected => SelectedStyle is { IsBuiltIn: false };

    public IReadOnlyList<AfkTextStyle> TextStyles => UnicodeTextStyler.All;

    [ObservableProperty] private AfkTextStyle _composerStyle = AfkTextStyle.Superscript;
    [ObservableProperty] private string _composerInput = string.Empty;

    public string ComposerOutput => UnicodeTextStyler.Apply(ComposerInput, ComposerStyle);

    public string ComposerCost
    {
        get
        {
            int cost = ComposerOutput.Length;
            int plain = ComposerInput?.Length ?? 0;

            if (cost == 0)
                return string.Empty;

            return cost > plain
                ? $"{cost} characters, {cost - plain} more than plain text"
                : $"{cost} characters";
        }
    }

    partial void OnComposerInputChanged(string value) => RaiseComposerChanged();
    partial void OnComposerStyleChanged(AfkTextStyle value) => RaiseComposerChanged();

    private void RaiseComposerChanged()
    {
        OnPropertyChanged(nameof(ComposerOutput));
        OnPropertyChanged(nameof(ComposerCost));
    }

    private void OnSettingsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(AfkModuleSettings.CustomStyles)
                           or nameof(AfkModuleSettings.ActiveStyleId))
            _dispatcher.BeginInvoke(RaiseStyleChanged);
    }

    private void ObserveSelectedStyle(AfkStyle? style = null)
    {
        style ??= _observedSettings?.ActiveStyle;
        if (ReferenceEquals(style, _observedStyle))
            return;

        if (_observedStyle != null)
            _observedStyle.PropertyChanged -= OnStyleEdited;

        _observedStyle = style;

        if (_observedStyle != null)
            _observedStyle.PropertyChanged += OnStyleEdited;
    }

    private void OnStyleEdited(object? sender, PropertyChangedEventArgs e)
        => _dispatcher.BeginInvoke(() =>
        {
            OnPropertyChanged(nameof(PreviewLine));
            OnPropertyChanged(nameof(PreviewCost));
        });

    private void RaiseStyleChanged()
    {
        ObserveSelectedStyle();
        OnPropertyChanged(nameof(Styles));
        OnPropertyChanged(nameof(SelectedStyle));
        OnPropertyChanged(nameof(PreviewLine));
        OnPropertyChanged(nameof(PreviewCost));
        OnPropertyChanged(nameof(CanDeleteSelected));
        OnPropertyChanged(nameof(CanEditSelected));
        OnPropertyChanged(nameof(EditHint));
        DeleteStyleCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private void AddStyle()
    {
        var settings = Settings;
        if (settings == null)
            return;

        var source = settings.ActiveStyle;
        var created = source?.Clone(NextName(settings, source.Name)) ?? new AfkStyle { Name = "New style" };

        settings.CustomStyles.Add(created);
        settings.AllStyles.Add(created);
        settings.ActiveStyleId = created.Id;
        settings.SaveSettings();
        RaiseStyleChanged();
    }

    [RelayCommand(CanExecute = nameof(CanDeleteSelected))]
    private void DeleteStyle()
    {
        var settings = Settings;
        var style = settings?.ActiveStyle;
        if (settings == null || style == null || style.IsBuiltIn)
            return;

        settings.CustomStyles.Remove(style);
        settings.AllStyles.Remove(style);

        var next = AfkStyleSeed.Resolve(settings.AllStyles, null);
        settings.ActiveStyleId = next?.Id ?? string.Empty;
        settings.SaveSettings();
        RaiseStyleChanged();
    }

    [RelayCommand]
    private void ExportStyles()
    {
        var settings = Settings;
        if (settings == null || settings.CustomStyles.Count == 0)
            return;

        try
        {
            var dialog = new SaveFileDialog
            {
                Filter = "AFK styles (*.json)|*.json",
                FileName = "AfkStyles",
                Title = "Export your AFK styles",
            };

            if (dialog.ShowDialog() != true)
                return;

            var payload = new AfkStyleExport
            {
                Styles = settings.CustomStyles.ToList(),
                SelectedStyleId = AfkStyleSeed.IsBuiltInId(settings.ActiveStyleId) ? null : settings.ActiveStyleId,
            };

            File.WriteAllText(dialog.FileName, JsonConvert.SerializeObject(payload, Formatting.Indented));
        }
        catch (Exception ex)
        {
            Logging.WriteException(ex, MSGBox: false);
        }
    }

    [RelayCommand]
    private void ImportStyles()
    {
        var settings = Settings;
        if (settings == null)
            return;

        try
        {
            var dialog = new OpenFileDialog
            {
                Filter = "AFK styles (*.json)|*.json|All files (*.*)|*.*",
                Title = "Import AFK styles",
            };

            if (dialog.ShowDialog() != true)
                return;

            var payload = JsonConvert.DeserializeObject<AfkStyleExport>(File.ReadAllText(dialog.FileName));
            if (payload?.Styles == null || payload.Styles.Count == 0)
                return;

            AfkStyle? lastImported = null;

            foreach (var incoming in payload.Styles)
            {
                var copy = incoming.Clone(UniqueName(settings, incoming.Name));
                copy.IsBuiltIn = false;

                settings.CustomStyles.Add(copy);
                settings.AllStyles.Add(copy);
                lastImported = copy;
            }

            if (lastImported != null)
                settings.ActiveStyleId = lastImported.Id;

            settings.SaveSettings();
            RaiseStyleChanged();
        }
        catch (Exception ex)
        {
            Logging.WriteException(ex, MSGBox: false);
        }
    }

    private sealed class AfkStyleExport
    {
        public List<AfkStyle> Styles { get; set; } = new();
        public string? SelectedStyleId { get; set; }
    }

    private static string UniqueName(AfkModuleSettings settings, string baseName)
    {
        string candidate = string.IsNullOrWhiteSpace(baseName) ? "Imported style" : baseName;
        int suffix = 2;

        while (settings.AllStyles.Any(s => string.Equals(s.Name, candidate, StringComparison.OrdinalIgnoreCase)))
            candidate = $"{baseName} {suffix++}";

        return candidate;
    }

    [RelayCommand]
    private void Save()
    {
        Settings?.SaveSettings();
        RaiseStyleChanged();
    }

    [RelayCommand]
    private void ApplyComposerTo(string? target)
    {
        var style = Settings?.ActiveStyle;
        if (style == null || string.IsNullOrEmpty(ComposerOutput))
            return;

        switch (target)
        {
            case "WithTime":
                style.MessageWithTime = ComposerOutput;
                break;
            case "WithoutTime":
                style.MessageWithoutTime = ComposerOutput;
                break;
            case "Prefix":
                style.Prefix = ComposerOutput;
                break;
            default:
                return;
        }

        Settings?.SaveSettings();
        OnPropertyChanged(nameof(PreviewLine));
        OnPropertyChanged(nameof(PreviewCost));
    }

    private static string NextName(AfkModuleSettings settings, string baseName)
    {
        string candidate = $"{baseName} copy";
        int suffix = 2;

        while (settings.AllStyles.Any(s => string.Equals(s.Name, candidate, StringComparison.OrdinalIgnoreCase)))
            candidate = $"{baseName} copy {suffix++}";

        return candidate;
    }
}
