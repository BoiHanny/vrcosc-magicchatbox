using System;

namespace vrcosc_magicchatbox.ViewModels
{
    public enum StatsComponentType
    {
        GPU,
        CPU,
        RAM,
        VRAM,
        FPS
    }



    public static class StatsComponentTypeExtensions
    {
        public static string GetSmallName(this StatsComponentType type)
        {
            switch (type)
            {
                case StatsComponentType.CPU: return "ᶜᵖᵘ";
                case StatsComponentType.GPU: return "ᵍᵖᵘ";
                case StatsComponentType.RAM: return "ʳᵃᵐ";
                case StatsComponentType.VRAM: return "ᵛʳᵃᵐ";
                case StatsComponentType.FPS: return "ᶠᵖˢ";
                default: throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
        }

    }

}
