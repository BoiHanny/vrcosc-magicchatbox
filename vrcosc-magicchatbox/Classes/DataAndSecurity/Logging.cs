using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using vrcosc_magicchatbox.ViewModels;
using NLog;

namespace vrcosc_magicchatbox.Classes.DataAndSecurity
{
    internal class Logging
    {
        private ViewModel _VM;

        public Logging(ViewModel vm)
        {
            _VM = vm;
        }


        //public static readonly Logger LogController = LogManager.GetCurrentClassLogger();

        //public static void WriteInfo(string info, bool makeVMDump = false, bool MSGBox = false, bool exitapp = false)
        //{
        //    LogController.Info(info);
        //    if (makeVMDump)
        //        ViewModelDump();
        //    if (MSGBox)
        //        ShowMSGBox(msgboxtext: info);
        //    if (exitapp)
        //        System.Environment.Exit(10);
        //}

        //public static void WriteException(Exception ex = null, bool makeVMDump = true, bool MSGBox = true, bool exitapp = false)
        //{
        //    LogController.Error(ex);
        //    if (makeVMDump)
        //        ViewModelDump();
        //    if (MSGBox || ex != null)
        //        ShowMSGBox(msgboxtimeout: 10000, msgboxtext: ex.Message, ex: ex);
        //    if (exitapp)
        //        System.Environment.Exit(10);
        //}

        //public static void ShowMSGBox(int msgboxtimeout = 5000, string msgboxtext = "something went wrong...", Exception ex = null)
        //{
        //    if (ex != null)
        //        msgboxtext = ex.Message;
        //    AutoClosingMessageBox.Show(msgboxtext, "Application error", msgboxtimeout);
        //}



        //public static void ViewModelDump()
        //{

        //    try
        //    {
        //        string folderPath = @"C:\Temp\PeoplePower Projects\";
        //        if (!Directory.Exists(folderPath))
        //        {
        //            Directory.CreateDirectory(folderPath);
        //        }
        //        else if ((File.GetAttributes(folderPath) & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
        //        {
        //            WriteInfo("Cannot write to directory: " + folderPath);
        //            return;
        //        }

        //        ViewModel viewModelCopy = JsonConvert.DeserializeObject<ViewModel>(JsonConvert.SerializeObject());
        //        viewModelCopy.currentCredential = null;
        //        string viewModelDump = JsonConvert.SerializeObject(viewModelCopy, Formatting.Indented);

        //        File.WriteAllText($@"{folderPath}ViewModelDump{DateTime.Now:yyyyMMddHHmmss}.json", viewModelDump);
        //        WriteInfo($@"ViewModelDump was called and created a json file in {folderPath}. ViewModel dump: ViewModelDump{DateTime.Now:yyyyMMddHHmmss}.json");
        //    }
        //    catch (Exception ex)
        //    {
        //        WriteException(ex, false);
        //    }
        //}
    }
}
