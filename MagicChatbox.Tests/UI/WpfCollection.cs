using Xunit;

namespace MagicChatbox.Tests.UI;

/// <summary>
/// Every test that builds real WPF controls, run one at a time.
/// </summary>
/// <remarks>
/// Two things here are process-wide and cannot be shared. <see cref="WpfHost"/> owns a single STA
/// thread, and <see cref="BindingErrorScope"/> attaches a listener to
/// <c>PresentationTraceSources.DataBindingSource</c> -- which is global, so two of these running at once
/// means one test collects the other's binding errors and fails for something it never rendered.
/// </remarks>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class WpfCollection
{
    public const string Name = "wpf";
}
