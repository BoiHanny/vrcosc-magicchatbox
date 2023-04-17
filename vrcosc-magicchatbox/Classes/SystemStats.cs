using System;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.ViewModels;

namespace vrcosc_magicchatbox.Classes
{
    public static class SystemStats
    {
        public static string GetTime()
        {
            try
            {
                if (ViewModel.Instance.Time24H == true)
                {
                    return string.Format("{0:HH:mm}", DateTime.Now).ToUpper();
                }
                else
                {
                    return string.Format("{0:hh:mm tt}", DateTime.Now).ToUpper();
                }

            }
            catch (Exception ex)
            {
                Logging.WriteException(ex, makeVMDump: false, MSGBox: false);
                return "00:00 XX";
            }

        }


    }
}
