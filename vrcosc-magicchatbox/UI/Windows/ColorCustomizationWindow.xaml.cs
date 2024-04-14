using System;
using System.Threading.Tasks;
using System.Windows;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.Classes;
using vrcosc_magicchatbox.ViewModels;
using Newtonsoft.Json.Linq;
using vrcosc_magicchatbox.Classes.Modules;

namespace vrcosc_magicchatbox.UI.Windows
{
    /// <summary>
    /// Interaction logic for ManualPulsoidAuth.xaml
    /// </summary>
    public partial class ColorCustomizationWindow : Window
    {
        ColorCustomizationViewModel ColorCustomizationViewModel;
        public ColorCustomizationWindow()
        {
            ColorCustomizationViewModel = ViewModel.Instance.ColorCustomizationViewModel;
            DataContext = ColorCustomizationViewModel;
            InitializeComponent();
        }

        private void Button_close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
       
    }
}
