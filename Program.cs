using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Test.CommandLineParsing;
using System.Windows.Forms;
using System.Diagnostics;
using System.ComponentModel;
using System.IO;
using System.Configuration;
using System.Globalization;
namespace PointCloudSplitter
{
    class CommandLineArguments
    {

        [Description("Print this help")]
        public bool? Help { get; set; }

        [Description("Full path to the configuration file")]
        public string ConfigFile { get; set; }

        [Description("Facility code")]
        public string FacilityCode { get; set; }

        [Description("Source folder")]
        public string SourceFolder { get; set; }

        [Description("Destination folder")]
        public string DestinationFolder { get; set; }

        [Description("Optional - Scan date YYYY-MM-DD if omitted timestamp from source pts file will be used")]
        public string ScanDate { get; set; }
    }


    class Program
    {
        public static int DictionaryBufferSize = 4000000;
        //30spaces
        public static string EmptyHeaderStr = "                              ";
        /*
         * testing pts file 3,66 GB (3 937 108 560 bytes)
         * Time elapsed: 00:37:20.2635846 buffer size 1000
         * output only 2.37gb? in first run
         * 
         * second run with buffer size 2000000
         * also changed disk write from string to stringbuilder
         * and also added a stream flush command
         * Time elapsed: 00:08:35.2906564
         * 3.42gb now written to disk
         * 
         * third run
         * changed stingbuilder obj to one stream writeline per point
         * 3.66gb written to disk
         * uses less memory and approx. same speed
         * Time elapsed: 00:08:39.4959986
         * 
         * 4th test
         * buffer 4000000, peak working set 1005076K
         * Time elapsed: 00:08:20.8302115
         * 3,66 GB (3 937 108 540 bytes) written (20bytes diff. could be due to missing header
         * from orgfile (temp pts files are headerless) 
         * 
         * 5th test
         * buffer 4000000, peak working set 987352K
         * Time elapsed: 00:07:56.9039535
         * written :3,66 GB (3 937 108 540 bytes)
         */
        public static List<string> GetFiles(string path, List<string> searchPatterns, SearchOption searchOption)
        {
            var result = searchPatterns.SelectMany(x => Directory.GetFiles(path, x, searchOption));
            return result.ToList();
        }

        static void Main(string[] args)
        {
            Stopwatch stopwatch = new Stopwatch();
            // Begin application timing
            stopwatch.Start();
            Console.WriteLine("---------------------------------------------------------------------");
            Console.WriteLine("{0} Version ({1})", System.Windows.Forms.Application.ProductName, System.Windows.Forms.Application.ProductVersion);
            Console.WriteLine("---------------------------------------------------------------------");
            Console.WriteLine();
            #region readarguments
            CommandLineArguments a = new CommandLineArguments();
            CommandLineParser.ParseArguments(a, args);
            if (a.ConfigFile == null || a.Help.GetValueOrDefault(false))
            {
                CommandLineParser.PrintUsage(a);
                Console.WriteLine(@"Example - " + System.Windows.Forms.Application.ProductName + @".exe /ConfigFile=""D:\PointCloudSplitter\Standard.config"" /FacilityCode=""ZZZ"" /SourceFolder=""D:\ScanData"" /DestinationFolder=""D:\ScanDataOutput"" /ScanDate=""2012-01-15""");
                return;
            }

            if (!File.Exists(a.ConfigFile))
            {
                Console.WriteLine("Error, configfile not found. Please check network connection");
                CommandLineParser.PrintUsage(a);
                Console.WriteLine(@"Example - " + System.Windows.Forms.Application.ProductName + @".exe /ConfigFile=""D:\PointCloudSplitter\Standard.config"" /FacilityCode=""ZZZ"" /SourceFolder=""D:\ScanData"" /DestinationFolder=""D:\ScanDataOutput"" /ScanDate=""2012-01-15""");

                return;
            }

            //get FacilityCode
            string FacilityCode = Default.Get(a.FacilityCode, "Plant");
            //get SourceFolder
            string SourceFolder = a.SourceFolder;
            if (!Directory.Exists(SourceFolder))
            {
                Console.WriteLine("Error, The folder {0} does not exist", SourceFolder);
                return;
            }
            //get DestinationFolder
            string DestinationFolder = a.DestinationFolder;
            //doCreate automatically checks if the folder exist before creating it
            CreateDirectory.doCreate(DestinationFolder);
            //get ScanDate
            DateTime ScanDate;
            bool ScanDateBool = false;
            if (a.ScanDate != null)
            {
                ScanDateBool = DateTime.TryParse(a.ScanDate, out ScanDate);
                if (ScanDateBool == false)
                {
                    Console.WriteLine("Error, invalid scan date:" + a.ScanDate);
                    return;
                }
            }

            //load the config file
            KeyValueConfigurationCollection LoadedAppSettings = UsingConfigurationManager.GetAppConfiguration(a.ConfigFile);

            if (LoadedAppSettings.Count == 0)
            {
                Console.WriteLine("Error in configuration");
                return;
            }
            char[] SeparatorCharArray;
            if (LoadedAppSettings["SeparatorChar"] != null)
            {
                SeparatorCharArray = LoadedAppSettings["SeparatorChar"].Value.ToCharArray();
            }
            else
            {
                /* From msdn about using whitespace as separatorchar:
                 * --------------------------------------------------
                 * If the separator parameter is Nothing or contains no characters,
                 * white-space characters are assumed to be the delimiters.
                 * White-space characters are defined by the Unicode standard and
                 * return true if they are passed to the Char.IsWhiteSpace method.
                 * However, if the separator parameter in the call to this method overload
                 * is Nothing, compiler overload resolution fails. To unambiguously identify
                 * the called method, your code must indicate the type of the null.
                 * 
                 * We use default(Char[]) to separate on whitespace
                 * (note whitespace matches more than just space)
                 */
                SeparatorCharArray = default(Char[]);
            }
            // Note:
            // try to find out how to use validators on config file value
            // a la LoadedAppSettings["key"].ElementInformation.Validator.Validate;

            #region readclippingsettings
            bool xlowerbool = false;
            int xlowerclip = 0;
            if (LoadedAppSettings["xlowerclip"] != null)
            {
                xlowerbool = int.TryParse(LoadedAppSettings["xlowerclip"].Value, out xlowerclip);
            }

            bool xupperbool = false;
            int xupperclip = 0;
            if (LoadedAppSettings["xupperclip"] != null)
            {
                xupperbool = int.TryParse(LoadedAppSettings["xupperclip"].Value, out xupperclip);
            }

            bool ylowerbool = false;
            int ylowerclip = 0;
            if (LoadedAppSettings["ylowerclip"] != null)
            {
                ylowerbool = int.TryParse(LoadedAppSettings["ylowerclip"].Value, out ylowerclip);
            }

            bool yupperbool = false;
            int yupperclip = 0;
            if (LoadedAppSettings["yupperclip"] != null)
            {
                yupperbool = int.TryParse(LoadedAppSettings["yupperclip"].Value, out yupperclip);
            }

            bool zlowerbool = false;
            int zlowerclip = 0;
            if (LoadedAppSettings["zlowerclip"] != null)
            {
                zlowerbool = int.TryParse(LoadedAppSettings["zlowerclip"].Value, out zlowerclip);
            }

            bool zupperbool = false;
            int zupperclip = 0;
            if (LoadedAppSettings["zupperclip"] != null)
            {
                zupperbool = int.TryParse(LoadedAppSettings["zupperclip"].Value, out zupperclip);
            }
            bool ClipSanityCheckResult = ClipSanityCheck(xlowerbool, xlowerclip, xupperbool, xupperclip, ylowerbool, ylowerclip, yupperbool, yupperclip, zlowerbool, zlowerclip, zupperbool, zupperclip);
            if (ClipSanityCheckResult == false)
            {
                Console.WriteLine("Error, invalid clipping value");
                return;
            }
            #endregion
            #region readcubelength
            /* in order to parse number with dot as decimal point
             * on machines where default language uses comma
             * as decimal point
             * we use en-US because pts files normally uses dot not comma
             */
            NumberStyles style;
            CultureInfo culture;
            culture = CultureInfo.CreateSpecificCulture("en-US");
            style = NumberStyles.AllowDecimalPoint;

            decimal CubeLengthX;
            decimal.TryParse(LoadedAppSettings["cubelengthx"].Value, style, culture, out CubeLengthX);
            decimal CubeLengthY;
            decimal.TryParse(LoadedAppSettings["cubelengthy"].Value, style, culture, out CubeLengthY);
            decimal CubeLengthZ;
            decimal.TryParse(LoadedAppSettings["cubelengthz"].Value, style, culture, out CubeLengthZ);
            //tryparse returns false and int=0 for invalid strings
            //check for 0 cube length and negative length
            if (CubeLengthX <= 0 ||
                CubeLengthY <= 0 ||
                CubeLengthZ <= 0
                )
            {
                // one or more strings are not a number.
                Console.WriteLine("Error in cube length configuration");
                return;
            }
            #endregion
            #region readptssyntax
            List<string> ptssyntax = null;
            if (LoadedAppSettings["ptssyntax"] != null)
            {
                if (LoadedAppSettings["ptssyntax"].Value.Length > 2)
                {
                    ptssyntax = new List<string>(LoadedAppSettings["ptssyntax"].Value.Split(new char[] { ';' }));
                }
            }
            else
            {
                //set default syntax
                Console.WriteLine(@"Error in ptssyntax configuration, using default value ""p x y z i r g b""");
                ptssyntax = new List<string>();
                ptssyntax.Add("p");
                ptssyntax.Add("x");
                ptssyntax.Add("y");
                ptssyntax.Add("z");
                ptssyntax.Add("i");
                ptssyntax.Add("r");
                ptssyntax.Add("g");
                ptssyntax.Add("b");
            }
            int pcolumn = ptssyntax.IndexOf("p");
            int xcolumn = ptssyntax.IndexOf("x");
            int ycolumn = ptssyntax.IndexOf("y");
            int zcolumn = ptssyntax.IndexOf("z");
            int icolumn = ptssyntax.IndexOf("i");
            int rcolumn = ptssyntax.IndexOf("r");
            int gcolumn = ptssyntax.IndexOf("g");
            int bcolumn = ptssyntax.IndexOf("b");
            #endregion
            List<string> cubefilenamesyntax = null;
            if (LoadedAppSettings["cubefilenamesyntax"] != null)
            {
                if (LoadedAppSettings["cubefilenamesyntax"].Value.Length > 14)
                {
                    cubefilenamesyntax = new List<string>(LoadedAppSettings["cubefilenamesyntax"].Value.Split(new char[] { ';' }));
                }
            }
            else
            {
                //set default syntax
                Console.WriteLine(@"Error in cubefilenamesyntax configuration, using default value ""FacilityCode;¤ [¤;cubex;¤ ¤;cubey;¤ ¤;cubez;¤] ¤;YYYY;¤-¤;MM;¤-¤;DD;¤.pts¤""");
                cubefilenamesyntax = new List<string>();
                cubefilenamesyntax.Add("FacilityCode");
                cubefilenamesyntax.Add("¤ [¤");
                cubefilenamesyntax.Add("cubex");
                cubefilenamesyntax.Add("¤ ¤");
                cubefilenamesyntax.Add("cubey");
                cubefilenamesyntax.Add("¤ ¤");
                cubefilenamesyntax.Add("cubez");
                cubefilenamesyntax.Add("¤] ¤");
                cubefilenamesyntax.Add("YYYY");
                cubefilenamesyntax.Add("¤-¤");
                cubefilenamesyntax.Add("MM");
                cubefilenamesyntax.Add("¤-¤");
                cubefilenamesyntax.Add("DD");
                cubefilenamesyntax.Add("¤.pts¤");
            }

            #endregion



            #region findptsfiles
            // search all subfolders, perhaps this should be a setting
            SearchOption mysearchOption = SearchOption.AllDirectories;

            List<string> mysearchPatterns = new List<string>();
            mysearchPatterns.Add("*.pts");

            List<string> ptsfiles = GetFiles(SourceFolder, mysearchPatterns, mysearchOption);
            decimal NoOfInputFiles = ptsfiles.Count;
            if (NoOfInputFiles == 0)
            {
                Console.WriteLine("no pts files found");
                return;
            }
            #endregion

            int CubeNoX;
            int CubeNoY;
            int CubeNoZ;
            decimal CoordX;
            decimal CoordY;
            decimal CoordZ;
            bool resx;
            bool resy;
            bool resz;
            /* dictionary holding counts per cube.
             * each cube needs a point count in the header*/
            Dictionary<string, ulong> CubeDictionary = new Dictionary<string, ulong>();
            Dictionary<string, List<string>> CubeBufferDictionary = new Dictionary<string, List<string>>();
            int PointsInMemory = 0;
            int TotalPointCounter = 0;
            string MyCubeFullPath = string.Empty;
            string MyDictionaryKey = string.Empty;
            foreach (string FileName in ptsfiles)
            {
                #region processptsfile
                int TotalFilePointCounter = 0;
                if (File.Exists(FileName))
                {
                    Console.WriteLine("Reading:" + FileName);
                    FileStream MyInputFileStream = new FileStream(FileName, FileMode.Open, FileAccess.Read);
                    StreamReader MyInputStreamReader = new StreamReader(MyInputFileStream);
                    string[] Current;

                    string Str = string.Empty;
                    try
                    {
                        Str = MyInputStreamReader.ReadLine();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Error: " + ex.Message);
                        return;
                    }

                    while (Str != null)
                    {
                        //flush buffer when it gets full
                        if (PointsInMemory >= DictionaryBufferSize)
                        {
                            string MyFlushError = string.Empty;
                            if (FlushBufferToDisk(DestinationFolder, CubeBufferDictionary, ref PointsInMemory, out MyFlushError) == false)
                            {
                                Console.WriteLine("Error," + MyFlushError);
                                return;
                            }
                        }
                        Current = Str.Split(SeparatorCharArray, StringSplitOptions.RemoveEmptyEntries);
                        if (Current.Length == 1)
                        {
                            Console.WriteLine("Number of scan points announced in file:" + Current[0]);
                        }
                        #region processpoint
                        if (Current.Length > 1)
                        {
                            //Console.WriteLine("Col0:" + Current[0] + " Col1:" + Current[1] + " Col2:" + Current[2]);

                            resx = decimal.TryParse(Current[xcolumn], style, culture, out CoordX);
                            resy = decimal.TryParse(Current[ycolumn], style, culture, out CoordY);
                            resz = decimal.TryParse(Current[zcolumn], style, culture, out CoordZ);
                            //only increase the counter by one if we got a vadid point
                            if (resx == true && resy == true && resz == true)
                            {
                                CubeNoX = Convert.ToInt32(Math.Ceiling(CoordX / CubeLengthX));
                                CubeNoY = Convert.ToInt32(Math.Ceiling(CoordY / CubeLengthY));
                                CubeNoZ = Convert.ToInt32(Math.Ceiling(CoordZ / CubeLengthZ));
                                //Console.WriteLine("Debug this point belongs in cube : [" + CubeNoX + " " + CubeNoY + " " + CubeNoZ + "]");
                                MyDictionaryKey = (CubeNoX + " " + CubeNoY + " " + CubeNoZ);
                                MyCubeFullPath = DestinationFolder + @"\" + MyDictionaryKey + ".ptstemp";
                                bool newcube = false;
                                if (!CubeDictionary.ContainsKey(MyDictionaryKey))
                                {
                                    newcube = true;
                                    try
                                    {
                                        /*we start with an empty cube file first time
                                         * we see a new cube */
                                        if (File.Exists(MyCubeFullPath))
                                        {
                                            File.Delete(MyCubeFullPath);
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        Console.WriteLine("Error: " + ex.Message);
                                        return;
                                    }
                                }


                                //update the cube dictionary
                                if (newcube == true)
                                {
                                    CubeDictionary.Add(MyDictionaryKey, 1);
                                    

                                }
                                else
                                {
                                    CubeDictionary[MyDictionaryKey]++;
                                    

                                    
                                    /*remember to flush buffer and write points to disk
                                     * buffersize should go against total no. of points
                                     * in CubeBufferDictionary and not number of points
                                     * in each cube. this would force update some cubefiles
                                     * early after few points but it gives a more true
                                     * picture of the total memory used before flush*/
                                }
                                /*we keep things simple by deleting everything in
                                 * the buffer dictionary, so we have to check separately
                                 * if the key exist in the buffer */
                                if (!CubeBufferDictionary.ContainsKey(MyDictionaryKey))
                                {
                                    List<string> BufferPointList = new List<string>();
                                    BufferPointList.Add(Str);
                                    CubeBufferDictionary.Add(MyDictionaryKey, BufferPointList);
                                }
                                else {
                                    CubeBufferDictionary[MyDictionaryKey].Add(Str);
                                }
                                PointsInMemory++;
                                TotalFilePointCounter++;
                                TotalPointCounter++;
                            }
                        }
                        #endregion
                        #region debugpointlimitor
                        int MyDebugStop = 17000000;
                        if (TotalPointCounter >= MyDebugStop)
                        {
                            Console.WriteLine("debug safety stop  - stopping after " + MyDebugStop + " points");
                            Console.WriteLine("Time elapsed: {0}", stopwatch.Elapsed);
                            Console.WriteLine("Press any key to continue...");
                            Console.ReadKey(true);
                            return;
                        }
                        
                        /* Time elapsed: 00:01:13.9126781 (1min14s) version1
                             * with stream open and close for each point
                             * Time elapsed: 00:00:00.3980194 (0min0.4s) version2
                             * enabled buffer size set to 1000
                             */
                        #endregion
                    

                        try
                        {
                            Str = MyInputStreamReader.ReadLine();
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("Error: " + ex.Message);
                            return;
                        }

                    }
                    if (MyInputStreamReader != null)
                    {
                        MyInputStreamReader.Close();
                    }
                    if (MyInputFileStream != null)
                    {
                        MyInputFileStream.Close();
                    }


                    Console.WriteLine("Number of points in " + FileName + " : " + TotalFilePointCounter);

                }
                #endregion
            }

            //final buffer flush
            if (PointsInMemory > 0)
            {
                Console.WriteLine("============================================================");
                Console.WriteLine("Debug, flushing remaining PointsInMemory:" + PointsInMemory);
                Console.WriteLine("============================================================");
                string MyFlushError = string.Empty;
                if (FlushBufferToDisk(DestinationFolder, CubeBufferDictionary, ref PointsInMemory,  out MyFlushError) == false)
                {
                    Console.WriteLine("Error," + MyFlushError);
                    return;
                }
            }
            
            if (CubeDictionary.Count > 0)
            {
                foreach (KeyValuePair<string, ulong> kvp in CubeDictionary)
                {
                    Console.WriteLine("Cube: [" + kvp.Key + "] Points:" + kvp.Value);
                }
                Console.WriteLine("Number of cubes: " + CubeDictionary.Count);
            }

            stopwatch.Stop();
            Console.WriteLine("Number of points in total: " + TotalPointCounter);
            Console.WriteLine(System.Windows.Forms.Application.ProductName + " completed");
            Console.WriteLine("Time elapsed: {0}", stopwatch.Elapsed);
            //System.Threading.Thread.Sleep(2500);
            Console.WriteLine("Press any key to continue...");
            Console.ReadKey(true);
        }

        private static bool FlushBufferToDisk(string DestinationFolder, Dictionary<string, List<string>> CubeBufferDictionary, ref int PointsInMemory,out string MyResultStr)
        {
            MyResultStr = string.Empty;
            bool MyResultBool = true;
            Console.WriteLine("Debug flush buffer");
            //add code here to flush buffer to disk  
            foreach (KeyValuePair<string, List<string>> kvp in CubeBufferDictionary)
            {
                Console.WriteLine("Debug key:" + kvp.Key);

                //Console.WriteLine("Debug List to strings:" + item);

                StreamWriter MyOutputStreamWriter = null;
                string MyCubeFullPath = DestinationFolder + @"\" + kvp.Key + ".ptstemp";
                Console.WriteLine("Debug output file: " + MyCubeFullPath);
                try
                {
                    MyOutputStreamWriter = new StreamWriter(MyCubeFullPath, true);
                    //EmptyHeaderStr
                    if (MyOutputStreamWriter.BaseStream.Length == 0)
                    {
                        MyOutputStreamWriter.WriteLine(EmptyHeaderStr);
                    }
                        foreach (string item in kvp.Value)
                    {
                    
                        MyOutputStreamWriter.WriteLine(item);
                    }
                    
                    MyOutputStreamWriter.Flush();
                    
                }
                catch (Exception ex)
                {
                    //Console.WriteLine("Error: " + ex.Message);
                    MyResultStr = ex.Message;
                    MyResultBool = false;
                }
                finally
                {
                    if (MyOutputStreamWriter != null)
                    {
                        MyOutputStreamWriter.Close();
                    }
                }

            }
            CubeBufferDictionary.Clear();
            PointsInMemory = 0;
            return MyResultBool;
        }

        private static bool ClipSanityCheck(bool xlowerbool, int xlowerclip, bool xupperbool, int xupperclip, bool ylowerbool, int ylowerclip, bool yupperbool, int yupperclip, bool zlowerbool, int zlowerclip, bool zupperbool, int zupperclip)
        {
            bool check = true;
            /*if one of the values are false and the other one
             * true we should accept it because sometimes
             * a user might want a upper limit only*/

            if (xlowerbool == true && xupperbool == true)
            {
                if (xlowerclip >= xupperclip)
                {
                    check = false;
                }
            }

            if (ylowerbool == true && yupperbool == true)
            {
                if (ylowerclip >= yupperclip)
                {
                    check = false;
                }
            }
            if (zlowerbool == true && zupperbool == true)
            {
                if (zlowerclip >= zupperclip)
                {
                    check = false;
                }
            }
            return check;
        }
    }
}
