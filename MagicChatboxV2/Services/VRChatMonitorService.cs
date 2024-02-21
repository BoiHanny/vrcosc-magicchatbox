using System.Diagnostics;
using System.Timers;
using Timer = System.Timers.Timer;

namespace MagicChatboxV2.Services
{
    public class VRChatMonitorService
    {
        private readonly Timer timer;
        private bool vrChatIsRunningPreviously = false;
        public bool IsVRChatRunning => !vrChatIsRunningPreviously;
        public event Action OnVRChatStarted;

        public VRChatMonitorService()
        {
            timer = new Timer(10000); // Check every 10 seconds
            timer.Elapsed += CheckVRChatProcess;
        }

        public void StartMonitoring()
        {
            timer.Start();
        }

        private void CheckVRChatProcess(object sender, ElapsedEventArgs e)
        {
            var vrChatRunning = Process.GetProcessesByName("VRChat").Any();

            if (vrChatRunning && !vrChatIsRunningPreviously)
            {
                vrChatIsRunningPreviously = vrChatRunning;
                OnVRChatStarted?.Invoke(); // Notify about VRChat start
            }
            else if (!vrChatRunning)
            {
                vrChatIsRunningPreviously = false;
                // Handle VRChat closure if needed
            }
        }

    }
}
