/*
PointCloudSplitter splits huge laser scan point cloud files into multiple smaller manageable cubes.
Copyright (C) 2012 Ulrik Dan Christensen

This program is free software; you can redistribute it and/or
modify it under the terms of the GNU General Public License
as published by the Free Software Foundation; either version 2
of the License, or (at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program; if not, write to the Free Software
Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.
*/
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
                Console.WriteLine("Configuration file loaded: " + config.FilePath);
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