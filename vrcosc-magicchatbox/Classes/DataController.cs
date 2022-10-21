using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using vrcosc_magicchatbox.ViewModels;


namespace vrcosc_magicchatbox.Classes
{
    internal class DataController
    {
        private ViewModel _VM;
        public DataController(ViewModel vm)
        {
            _VM = vm;
        }


        public void SaveSettingsToXML()
        {
            if(CreateIfMissing(_VM.DataPath) == true)
            {
                try
                {
                    XmlDocument xmlDoc = new XmlDocument();
                    XmlNode rootNode = xmlDoc.CreateElement("Settings");
                    xmlDoc.AppendChild(rootNode);

                    XmlNode userNode = xmlDoc.CreateElement("IntgrScanWindowActivity");
                    userNode.InnerText = _VM.IntgrScanWindowActivity.ToString();
                    rootNode.AppendChild(userNode);

                    userNode = xmlDoc.CreateElement("IntgrScanSpotify");
                    userNode.InnerText = _VM.IntgrScanSpotify.ToString();
                    rootNode.AppendChild(userNode);

                    userNode = xmlDoc.CreateElement("IntgrScanWindowTime");
                    userNode.InnerText = _VM.IntgrScanWindowTime.ToString();
                    rootNode.AppendChild(userNode);

                    userNode = xmlDoc.CreateElement("ScanInterval");
                    userNode.InnerText = _VM.ScanInterval.ToString();
                    rootNode.AppendChild(userNode);

                    userNode = xmlDoc.CreateElement("OSCIP");
                    userNode.InnerText = _VM.OSCIP.ToString();
                    rootNode.AppendChild(userNode);

                    userNode = xmlDoc.CreateElement("OSCPort");
                    userNode.InnerText = _VM.OSCPort.ToString();
                    rootNode.AppendChild(userNode);

                    xmlDoc.Save(Path.Combine(_VM.DataPath, "settings.xml"));
                }
                catch (Exception)
                {


                }
            }
  
             

        }

        public void LoadSettingsFromXML()
        {
            try
            {
                XmlDocument doc = new XmlDocument();
                doc.Load(Path.Combine(_VM.DataPath, "settings.xml"));

                _VM.IntgrScanSpotify = bool.Parse(doc.GetElementsByTagName("IntgrScanSpotify")[0].InnerText);
                _VM.IntgrScanWindowActivity = bool.Parse(doc.GetElementsByTagName("IntgrScanWindowActivity")[0].InnerText);
                _VM.IntgrScanSpotify = bool.Parse(doc.GetElementsByTagName("IntgrScanSpotify")[0].InnerText);
                _VM.IntgrScanWindowTime = bool.Parse(doc.GetElementsByTagName("IntgrScanWindowTime")[0].InnerText);
                _VM.ScanInterval = int.Parse(doc.GetElementsByTagName("ScanInterval")[0].InnerText);
                _VM.OSCIP = doc.GetElementsByTagName("OSCIP")[0].InnerText;
                _VM.OSCPort = int.Parse(doc.GetElementsByTagName("OSCPort")[0].InnerText);


            }
            catch (Exception)
            {

            }
        }

        public bool CreateIfMissing(string path)
        {
            try
            {
                if (!Directory.Exists(path))
                {
                    DirectoryInfo di = Directory.CreateDirectory(path);
                    return true;
                }
                return true;
            }
            catch (IOException ex)
            {
                return false;
            }

        }

    }
}
