using System.Threading.Tasks;

namespace vrcosc_magicchatbox.Services;

public sealed record OptionsSectionResetResult(string DisplayName, int ResetCount, bool RestartRequired = false, string? Note = null);

public interface IOptionsSectionResetService
{
    Task<OptionsSectionResetResult> ResetSectionAsync(string sectionKey);
}
