using Newtonsoft.Json;
using NLog;
using System;
using System.IO;
using System.Windows.Forms;
using vrcosc_magicchatbox.DataAndSecurity;
using vrcosc_magicchatbox.ViewModels;

namespace vrcosc_magicchatbox.Classes.DataAndSecurity
{
    internal static class Logging
    {



        public static readonly Logger LogController = LogManager.GetCurrentClassLogger();

        public static void WriteInfo(string info, bool makeVMDump = false, bool MSGBox = false, bool exitapp = false)
        {
            LogController.Info(info);
            if (makeVMDump)
                ViewModelDump();
            if (MSGBox)
                ShowMSGBox(msgboxtext: info);
            if (exitapp)
                System.Environment.Exit(10);
        }

        public static void WriteException(Exception ex = null, bool makeVMDump = true, bool MSGBox = true, bool exitapp = false)
        {
            LogController.Error(ex);
            if (makeVMDump)
                ViewModelDump();

            // Check if MSGBox is true AND ex is not null
            if (MSGBox && ex != null)
                ShowMSGBox(msgboxtimeout: 10000, msgboxtext: ex.Message, ex: ex);

            if (exitapp)
                System.Environment.Exit(10);
        }


        public static void ShowMSGBox(int msgboxtimeout = 5000, string msgboxtext = "something went wrong...", Exception ex = null)
        {
            if (ex != null)
                msgboxtext = ex.Message;
            AutoClosingMessageBox.Show(msgboxtext, "Application error", msgboxtimeout);
        }



        public static void ViewModelDump()
        {
            try
            {
                if (DataController.CreateIfMissing(ViewModel.Instance.LogPath))
                {
                    string folderPath = ViewModel.Instance.LogPath;
                    if (!Directory.Exists(folderPath))
                    {
                        Directory.CreateDirectory(folderPath);
                    }
                    else if ((File.GetAttributes(folderPath) & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                    {
                        WriteInfo("Cannot write to directory: " + folderPath);
                        return;
                    }

                    ViewModel viewModelCopy = JsonConvert.DeserializeObject<ViewModel>(JsonConvert.SerializeObject(ViewModel.Instance));
                    viewModelCopy.aesKey = null;
                    viewModelCopy.ApiStream = null;
                    string viewModelDump = JsonConvert.SerializeObject(viewModelCopy, Formatting.Indented);

                    string timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
                    File.WriteAllText($@"{folderPath}\ViewModelDump{timestamp}.json", viewModelDump);
                    WriteInfo($@"ViewModelDump was called and created a json file in {folderPath}. ViewModel dump: ViewModelDump{timestamp}.json");

                    // Call the method to delete old view model dumps
                    DeleteOldViewModelDumps(folderPath);
                }
                else
                {
                    ShowMSGBox(msgboxtext: @"Couldn't create ViewModelDump in 'C:\Temp\Vrcosc-MagicChatbox'");
                }
            }
            catch (Exception ex)
            {
                WriteException(ex, makeVMDump: false, MSGBox: false);
            }
        }

        private static void DeleteOldViewModelDumps(string folderPath)
        {
            try
            {
                DirectoryInfo directoryInfo = new DirectoryInfo(folderPath);
                FileInfo[] files = directoryInfo.GetFiles("*.json");

                // Sort files by creation time
                Array.Sort(files, (x, y) => x.CreationTime.CompareTo(y.CreationTime));

                // Delete files if there are more than 10
                if (files.Length > 10)
                {
                    for (int i = 0; i < files.Length - 10; i++)
                    {
                        files[i].Delete();
                    }
                }
            }
            catch (Exception ex)
            {
                WriteException(ex, makeVMDump: false, MSGBox: false);
            }
        }
    }
}
