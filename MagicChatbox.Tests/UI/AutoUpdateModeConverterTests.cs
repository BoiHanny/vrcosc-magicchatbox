using System.Globalization;
using vrcosc_magicchatbox.Classes;
using vrcosc_magicchatbox.Core.Updates;
using Xunit;

namespace MagicChatbox.Tests.UI;

/// <summary>
/// The update card's tick is a two-state view of a three-state channel. What matters is where unticking
/// lands: on Notify, so someone turning off automatic installing still hears about new versions. The
/// obvious reuse - EnumEqualsConverter - returns Binding.DoNothing when unticked, which is right for a
/// radio group and would leave this checkbox stuck on.
/// </summary>
public class AutoUpdateModeConverterTests
{
    private static readonly AutoUpdateModeConverter Converter = new();

    [Theory]
    [InlineData(UpdateChannelMode.Auto, true)]
    [InlineData(UpdateChannelMode.Notify, false)]
    [InlineData(UpdateChannelMode.Off, false)]
    public void Only_Auto_shows_as_ticked(UpdateChannelMode mode, bool expected)
        => Assert.Equal(expected, Converter.Convert(mode, typeof(bool), null!, CultureInfo.InvariantCulture));

    [Fact]
    public void Ticking_it_asks_for_automatic_installs()
        => Assert.Equal(
            UpdateChannelMode.Auto,
            Converter.ConvertBack(true, typeof(UpdateChannelMode), null!, CultureInfo.InvariantCulture));

    [Fact]
    public void Unticking_it_falls_back_to_being_told_not_to_silence()
        => Assert.Equal(
            UpdateChannelMode.Notify,
            Converter.ConvertBack(false, typeof(UpdateChannelMode), null!, CultureInfo.InvariantCulture));

    [Fact]
    public void A_null_tick_is_treated_as_off_rather_than_throwing()
        => Assert.Equal(
            UpdateChannelMode.Notify,
            Converter.ConvertBack(null!, typeof(UpdateChannelMode), null!, CultureInfo.InvariantCulture));
}
