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
