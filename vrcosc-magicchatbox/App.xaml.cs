using System.Windows;
using vrcosc_magicchatbox.Classes.DataAndSecurity;

namespace vrcosc_magicchatbox
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            if (e.Args != null && e.Args.Length > 0)
            {
                if (e.Args[0] == "-update")
                {
                    UpdateApp updater = new UpdateApp();
                    updater.UpdateApplication();
                    Shutdown();
                    return;
                }
                if (e.Args[0] == "-updateadmin")
                {
                    UpdateApp updater = new UpdateApp();
                    updater.UpdateApplication(true);
                    Shutdown();
                    return;
                }
            }
        }


    }
}
