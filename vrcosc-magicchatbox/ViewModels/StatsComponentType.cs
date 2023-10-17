using System;

namespace vrcosc_magicchatbox.ViewModels
{
    public static class StatsComponentTypeExtensions
    {
        public static string GetSmallName(this StatsComponentType type)
        {
            switch(type)
            {
                case StatsComponentType.CPU:
                    return "ᶜᵖᵘ";
                case StatsComponentType.GPU:
                    return "ᵍᵖᵘ";
                case StatsComponentType.RAM:
                    return "ʳᵃᵐ";
                case StatsComponentType.VRAM:
                    return "ᵛʳᵃᵐ";
                case StatsComponentType.FPS:
                    return "ᶠᵖˢ";
                case StatsComponentType.Unknown:
                    return "ᵘⁿᵏⁿᵒʷⁿ";
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
        }
    }
}
