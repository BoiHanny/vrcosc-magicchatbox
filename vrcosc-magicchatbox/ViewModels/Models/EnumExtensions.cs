using System;
using System.ComponentModel;
using System.Reflection;

namespace vrcosc_magicchatbox.ViewModels.Models
{
    public static class EnumExtensions
    {
        public static string GetDescription(this Enum value)
        {
            FieldInfo field = value.GetType().GetField(value.ToString());

            DescriptionAttribute attribute = field.GetCustomAttribute<DescriptionAttribute>(false);

            return attribute != null ? attribute.Description : value.ToString();
        }
    }
}
