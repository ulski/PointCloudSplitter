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
using System.IO;
namespace PointCloudSplitter
{
    public static class CreateDirectory
    {
        public static void doCreate(string fpath)
        {
            DirectoryInfo di = new DirectoryInfo(fpath);
            // Determine whether the directory exists.
            if (!di.Exists)
            {
                CreaFolder(di);
            }
        }

        private static void CreaFolder(this DirectoryInfo dirInfo)
        {
            try
            {
                if (dirInfo.Parent != null && !dirInfo.Exists)
                    CreaFolder(dirInfo.Parent);

                if (!dirInfo.Exists)
                {
                    dirInfo.Create();
                    dirInfo.Refresh();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Error:" + e.Message);
                System.Environment.Exit(1);
            }
        }
    }
}
