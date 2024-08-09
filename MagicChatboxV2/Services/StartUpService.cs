using MagicChatboxV2.Startup.Windows;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MagicChatboxV2.Services
{
    public class StartUpService
    {
        private readonly LoadingWindowViewModel _loadingWindowsViewModel;


        public StartUpService(LoadingWindowViewModel loadingWindowViewModel)
        {
            _loadingWindowsViewModel = loadingWindowViewModel;
        }

    }
}
