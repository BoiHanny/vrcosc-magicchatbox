using System;
using System.Globalization;
using vrcosc_magicchatbox.ViewModels;

namespace vrcosc_magicchatbox.Classes


{
    public class SystemStats
    {
        private ViewModel _VM;
        public SystemStats(ViewModel vm)
        {
            _VM = vm;
        }

        public string GetTime()
        {
            try
            {
                return string.Format("{0:hh:mm tt}", DateTime.Now).ToUpper();
            }
            catch (Exception)
            {

                return "00:00 XX";
            }

        }
    }
}
    