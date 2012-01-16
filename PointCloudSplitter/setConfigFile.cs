using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Configuration;
using System.Collections;
using System.Collections.Specialized;
using System.Diagnostics;
namespace PointCloudSplitter
{

    public static class UsingConfigurationManager
    {
        public static KeyValueConfigurationCollection GetAppConfiguration(string configfile)
        {
            AppSettingsSection appSettings = null;
            try
            {
                ExeConfigurationFileMap fileMap = new ExeConfigurationFileMap();
                fileMap.ExeConfigFilename = configfile;
                Configuration config = ConfigurationManager.OpenMappedExeConfiguration(fileMap, ConfigurationUserLevel.None);
                Console.WriteLine("Config loaded. Config filepath: " + config.FilePath);
                appSettings = config.AppSettings;
                string sectionName = "appSettings";
                // Refreshes the named section so the next time that it is retrieved it will be re-read from disk.
                ConfigurationManager.RefreshSection(sectionName);
                // Get the AppSettings section.
                AppSettingsSection appSettingSection = (AppSettingsSection)config.GetSection(sectionName);
                //Console.WriteLine(appSettingSection.SectionInformation.GetRawXml());
            }
            catch (ConfigurationErrorsException e)
            {
                Console.WriteLine("Error in config file");
                Console.WriteLine("[Error exception: {0}]", e.ToString());
                System.Environment.Exit(1);
            }
            return appSettings.Settings;
        }
    }
}