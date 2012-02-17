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
using Microsoft.Test.CommandLineParsing;
using System.Windows.Forms;
using System.Diagnostics;
using System.ComponentModel;
using System.IO;
using System.Configuration;
using System.Globalization;
using System.Threading;
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
        /* EmptyHeaderStr is a string with 30spaces.
         * do not change this
         * See explanation in the AddHeader method
         */
        private static string EmptyHeaderStr = "                              ";
        private static decimal UnitScale = 1;
        private static string OutputSeparator = " ";
        private static string TempFileExt=".ptstemp";
        private static int pcolumn;
        private static int xcolumn;
        private static int ycolumn;
        private static int zcolumn;
        private static int icolumn;
        private static int rcolumn;
        private static int gcolumn;
        private static int bcolumn;
        /* in order to parse number with dot as decimal point
         * on machines where default language uses comma
         * as decimal point
         * we use en-US because pts files normally uses dot not comma
         */
        private static CultureInfo culture = CultureInfo.CreateSpecificCulture("en-US");
        private static NumberStyles style = NumberStyles.AllowDecimalPoint;

        private static List<string> GetFiles(string path, List<string> searchPatterns, SearchOption searchOption)
        {
            var result = searchPatterns.SelectMany(x => Directory.GetFiles(path, x, searchOption));
            return result.ToList();
        }

        static void Main(string[] args)
        {
            Stopwatch stopwatch = new Stopwatch();
            // Begin application timing
            stopwatch.Start();
            Console.WriteLine("----------------------------------------------------------------------------");
            Console.WriteLine(System.Windows.Forms.Application.ProductName + " " + System.Windows.Forms.Application.ProductVersion + @", Copyright (C) 2012 Ulrik Dan Christensen");
            Console.WriteLine("This program is comes with ABSOLUTELY NO WARRANTY.");
            Console.WriteLine("This is free software, and you are welcome to redistribute it under certain");
            Console.WriteLine("conditions. For license details, documentation and updates please visit");
            Console.WriteLine("http://pointcloudsplitter.codeplex.com");
            Console.WriteLine("----------------------------------------------------------------------------");
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
            DateTime ScanDate = DateTime.Now;
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
            //UnitsScale
            #region UnitScale
            /* sometimes we get pts file i milimeters
             * but normally we need cube files in meters.
             * UnitScale fixes this if we set UnitScale to 0.001
             */
            bool UnitScaleBool = false;
            if (LoadedAppSettings["unitscale"] != null)
            {
                UnitScaleBool = decimal.TryParse(LoadedAppSettings["unitscale"].Value, style, culture, out UnitScale);
                /* tryparse returns 0 on false
                 * to make thing more logical default
                 * scale is always set to 1 */
                if (UnitScale == 0)
                {
                    UnitScale = 1;
                }
            }            
            #endregion
            #region buffersize            
            //support for fine tuning of buffer size
            uint DictionaryBufferSize = 0;
            bool DictionaryBufferSizeBool = false;
            if (LoadedAppSettings["bufferSize"] != null)
            {
                DictionaryBufferSizeBool = uint.TryParse(LoadedAppSettings["bufferSize"].Value, out DictionaryBufferSize);
            }
            if (DictionaryBufferSizeBool == false)
            {
                DictionaryBufferSize = 4000000;
            }
            else if (DictionaryBufferSize <= 10)
            {
                Console.WriteLine("Error, minimum buffer size is 10");
                return;
            }
            #endregion
            /*standard separator in pts files is whitespace
             * but SeparatorChar can be set in the configfile
             */
            char[] SeparatorCharArray;
            if (LoadedAppSettings["separatorchar"] != null)
            {
                SeparatorCharArray = LoadedAppSettings["separatorchar"].Value.ToCharArray();
            }
            else
            {
                /*
                 * We use default(Char[]) to separate on whitespace
                 * Note: Whiteespace matches more than just space
                 * 
                 * From msdn about using whitespace as separatorchar:
                 * --------------------------------------------------
                 * If the separator parameter is Nothing or contains no characters,
                 * white-space characters are assumed to be the delimiters.
                 * White-space characters are defined by the Unicode standard and
                 * return true if they are passed to the Char.IsWhiteSpace method.
                 * However, if the separator parameter in the call to this method overload
                 * is Nothing, compiler overload resolution fails. To unambiguously identify
                 * the called method, your code must indicate the type of the null.
                 */                   
                SeparatorCharArray = default(Char[]);
            }
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
            bool ClippingEnabled = false;
            bool ClipSanityCheckResult = false;
            /* if clipping is used:
             * check that the upper limit value is larger than lower limit value */
            if (xlowerbool == true || xupperbool == true || ylowerbool == true || yupperbool == true || zlowerbool == true || zupperbool == true)
            {
                ClippingEnabled = true;
                ClipSanityCheckResult = ClipSanityCheck(xlowerbool, xlowerclip, xupperbool, xupperclip, ylowerbool, ylowerclip, yupperbool, yupperclip, zlowerbool, zlowerclip, zupperbool, zupperclip);
                if (ClipSanityCheckResult == false)
                {
                    Console.WriteLine("Error, invalid clipping value");
                    return;
                }
            }
            #endregion
            #region readcubelength
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
                //set default syntax - (currently the code only need to locate the xyz values)
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
            pcolumn = ptssyntax.IndexOf("p");
            xcolumn = ptssyntax.IndexOf("x");
            ycolumn = ptssyntax.IndexOf("y");
            zcolumn = ptssyntax.IndexOf("z");
            icolumn = ptssyntax.IndexOf("i");
            rcolumn = ptssyntax.IndexOf("r");
            gcolumn = ptssyntax.IndexOf("g");
            bcolumn = ptssyntax.IndexOf("b");
            #endregion
            List<string> cubefilenamesyntax = null;
            if (LoadedAppSettings["cubefilenamesyntax"] != null)
            {
                if (LoadedAppSettings["cubefilenamesyntax"].Value.Length > 14)
                {
                    cubefilenamesyntax = new List<string>(LoadedAppSettings["cubefilenamesyntax"].Value.Split(new char[] { ';' }));
                }
                else
                {
                    Console.WriteLine("Error in cube filename syntax configuration");
                    return;
                }
            }
            else
            {
                //set default syntax
                cubefilenamesyntax = new List<string>();
                cubefilenamesyntax.Add("FacilityCode");
                cubefilenamesyntax.Add(" [");
                cubefilenamesyntax.Add("cubex");
                cubefilenamesyntax.Add(" ");
                cubefilenamesyntax.Add("cubey");
                cubefilenamesyntax.Add(" ");
                cubefilenamesyntax.Add("cubez");
                cubefilenamesyntax.Add("] ");
                cubefilenamesyntax.Add("YYYY");
                cubefilenamesyntax.Add("-");
                cubefilenamesyntax.Add("MM");
                cubefilenamesyntax.Add("-");
                cubefilenamesyntax.Add("DD");
                cubefilenamesyntax.Add(".pts");
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
            int CubeNoX = 0;
            int CubeNoY = 0;
            int CubeNoZ = 0;
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
                    Console.WriteLine("Reading points....");
                    while (Str != null)
                    {
                        //flush buffer when full
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
                        
                        #region processpoint
                        if (Current.Length > 1)
                        {
                            //Console.WriteLine("Debug Col0:" + Current[0] + " Col1:" + Current[1] + " Col2:" + Current[2]);
                            resx = decimal.TryParse(Current[xcolumn], style, culture, out CoordX);
                            resy = decimal.TryParse(Current[ycolumn], style, culture, out CoordY);
                            resz = decimal.TryParse(Current[zcolumn], style, culture, out CoordZ);
                            bool PointCheck = false;
                            if (resx == true && resy == true && resz == true)
                            {
                                if (ClippingEnabled == false)
                                {
                                    PointCheck = true;
                                }
                                else
                                {
                                    PointCheck = ClippingControl(xlowerbool, xlowerclip, xupperbool, xupperclip, ylowerbool, ylowerclip, yupperbool, yupperclip, zlowerbool, zlowerclip, zupperbool, zupperclip, CoordX, CoordY, CoordZ);
                                }
                            }
                            if (PointCheck == true)
                            {
                                CubeNoX = Convert.ToInt32(Math.Ceiling(CoordX / CubeLengthX));
                                CubeNoY = Convert.ToInt32(Math.Ceiling(CoordY / CubeLengthY));
                                CubeNoZ = Convert.ToInt32(Math.Ceiling(CoordZ / CubeLengthZ));
                                MyDictionaryKey = (CubeNoX + " " + CubeNoY + " " + CubeNoZ);
                                MyCubeFullPath = DestinationFolder + @"\" + MyDictionaryKey + TempFileExt;
                                /*
                                 * Code for unitscale:
                                 * scale is done after cube number is found because
                                 * normally we would specify cube side length in the
                                 * original unit
                                 */
                                if (UnitScale != 1)
                                {
                                    Current[xcolumn] = Convert.ToString(CoordX * UnitScale,culture);
                                    Current[ycolumn] = Convert.ToString(CoordY * UnitScale,culture);
                                    Current[zcolumn] = Convert.ToString(CoordZ * UnitScale,culture);
                                    Str = String.Join(OutputSeparator, Current);
                                }
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
                                else
                                {
                                    CubeBufferDictionary[MyDictionaryKey].Add(Str);
                                }
                                PointsInMemory++;
                                TotalFilePointCounter++;
                                TotalPointCounter++;
                            }
                        }
                        else if (Current.Length == 1)
                        {
                            Console.WriteLine("Number of points announced in file header:" + Current[0]);
                        }
                        #endregion
                        #region debugpointlimitor
                        /*
                        int MyDebugStop = 17000000;
                        if (TotalPointCounter >= MyDebugStop)
                        {
                            Console.WriteLine("debug safety stop  - stopping after " + MyDebugStop + " points");
                            Console.WriteLine("Time elapsed: {0}", stopwatch.Elapsed);
                            Console.WriteLine("Press any key to continue...");
                            Console.ReadKey(true);
                            return;
                        }
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
                    if (ClippingEnabled)
                    {
                        Console.WriteLine("Points in " + FileName + " after clipping : " + TotalFilePointCounter);
                    }
                    else
                    {
                        Console.WriteLine("Points in " + FileName + " : " + TotalFilePointCounter);
                    }
                }
                #endregion
            }

            //final buffer flush
            if (PointsInMemory > 0)
            {
                Console.WriteLine("Writing last data from buffer");
                string MyFlushError = string.Empty;
                if (!FlushBufferToDisk(DestinationFolder, CubeBufferDictionary, ref PointsInMemory, out MyFlushError))
                {
                    Console.WriteLine("Error," + MyFlushError);
                    return;
                }
            }            
            string MyFinalFileName = string.Empty;
            string FinalFileFullPath = string.Empty;
            string TempFileFullPath = string.Empty;
            if (CubeDictionary.Count > 0)
            {
                foreach (KeyValuePair<string, ulong> kvp in CubeDictionary)
                {
                    TempFileFullPath = DestinationFolder + @"\" + kvp.Key + ".ptstemp";
                    AddHeader(TempFileFullPath, kvp.Value.ToString());
                    MyFinalFileName = FinalCubeFileName(FacilityCode, ScanDate, cubefilenamesyntax, kvp.Key);
                    FinalFileFullPath = DestinationFolder + @"\" + MyFinalFileName;
                    try
                    {
                        if (File.Exists(FinalFileFullPath))
                        {
                            //Console.WriteLine("Deleting old cube: "  + FinalFileName);
                            File.Delete(FinalFileFullPath);
                        }
                        File.Move(TempFileFullPath, FinalFileFullPath);
                        Console.WriteLine("Cube complete:" + MyFinalFileName);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Error, " + ex.Message);
                        return;
                    }
                }
            }
            if (CubeDictionary.Count > 0)
            {
                Console.WriteLine();
                Console.WriteLine("======================================");
                Console.WriteLine("========  S U M M A R Y  =============");
                Console.WriteLine("======================================");
                foreach (KeyValuePair<string, ulong> kvp in CubeDictionary)
                {
                    Console.WriteLine("Cube number: [" + kvp.Key + "]  Points in cube: " + kvp.Value);
                }
                Console.WriteLine("Number of cubes in total: " + CubeDictionary.Count);
            }
            Console.WriteLine("Number of points in total: " + TotalPointCounter);
            Console.WriteLine(System.Windows.Forms.Application.ProductName + " completed");
            stopwatch.Stop();
            Console.WriteLine("Time elapsed: {0}", stopwatch.Elapsed);
        }

        private static string FinalCubeFileName(string FacilityCode, DateTime ScanDate, List<string> cubefilenamesyntax, string cubeno)
        {
            //Example syntax: FacilityCode; [;cubex; ;cubey; ;cubez;] ;YYYY;-;MM;-;DD;.pts
            StringBuilder SB = new StringBuilder();
            List<string> CubeNoList = new List<string>(cubeno.Split(new char[] { ' ' }));
            for (int i = 0; i < cubefilenamesyntax.Count; i++)
            {
                switch (cubefilenamesyntax[i])
                {
                    case "FacilityCode":
                        SB.Append(FacilityCode);
                        break;
                    case "cubex":
                        SB.Append(CubeNoList[0]);
                        break;
                    case "cubey":
                        SB.Append(CubeNoList[1]);
                        break;
                    case "cubez":
                        SB.Append(CubeNoList[2]);
                        break;
                    case "YYYY":
                        SB.Append(ScanDate.Year.ToString());
                        break;
                    case "MM":
                        SB.Append(AddLeadingZero(ScanDate.Month.ToString()));
                        break;
                    case "DD":
                        SB.Append(AddLeadingZero(ScanDate.Day.ToString()));
                        break;
                    default:
                        SB.Append(cubefilenamesyntax[i]);
                        break;
                }
            }
            return SB.ToString();
        }
        private static string AddLeadingZero(string MyStr)
        {
            if (MyStr.Length == 1)
            {
                MyStr = "0" + MyStr;
            }
            return MyStr;
        }
        private static bool ClippingControl(bool xlowerbool, int xlowerclip, bool xupperbool, int xupperclip, bool ylowerbool, int ylowerclip, bool yupperbool, int yupperclip, bool zlowerbool, int zlowerclip, bool zupperbool, int zupperclip, decimal CoordX, decimal CoordY, decimal CoordZ)
        {
            if (xlowerbool)
            {
                if (CoordX < xlowerclip)
                {
                    return false;
                }
            }
            if (xupperbool)
            {
                if (CoordX > xupperclip)
                {
                    return false;
                }
            }
            if (ylowerbool)
            {
                if (CoordY < ylowerclip)
                {
                    return false;
                }
            }
            if (yupperbool)
            {
                if (CoordY > yupperclip)
                {
                    return false;
                }
            }
            if (zlowerbool)
            {
                if (CoordZ < zlowerclip)
                {
                    return false;
                }
            }
            if (zupperbool)
            {
                if (CoordZ > zupperclip)
                {
                    return false;
                }
            }
            return true;
        }

        private static bool FlushBufferToDisk(string DestinationFolder, Dictionary<string, List<string>> CubeBufferDictionary, ref int PointsInMemory, out string MyResultStr)
        {
            MyResultStr = string.Empty;
            bool MyResultBool = true;
            foreach (KeyValuePair<string, List<string>> kvp in CubeBufferDictionary)
            {
                StreamWriter MyOutputStreamWriter = null;
                string MyCubeTempFile = kvp.Key + TempFileExt;
                string MyCubeFullPath = DestinationFolder + @"\" + MyCubeTempFile;
                Console.WriteLine("Writing to temp file: " + MyCubeTempFile);
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
                        MyOutputStreamWriter.Flush();
                        MyOutputStreamWriter.Close();
                    }
                }
            }
            CubeBufferDictionary.Clear();
            PointsInMemory = 0;
            return MyResultBool;
        }
        /* Comments regarding AddHeader
         * 
         * This way of adding the total point count in first line
         * is a bit dodgy. The reason is we write a line with many spaces
         * and after writing all the points go back and the replace
         * some of the spaces with numbers. This means
         * that the header will always have some spaces trailing the count number. 
         * Fortunately most pts readers don't mind these extra spaces
         * and this method is magnitudes faster than the alternative.
         * The alternative would be to loop all point twice:
         * either rewrite all points to disk in a new file or
         * loop all count points first
         * and then write points to disk in a second loop.
         */
        private static void AddHeader(string FullPath, string HeaderText)
        {
            FileStream fStream = null;
            UTF8Encoding utf8 = new UTF8Encoding();
            try
            {
                fStream = File.Open(FullPath, FileMode.Open, FileAccess.ReadWrite);
                fStream.Position = 0;
                fStream.Write(utf8.GetBytes(HeaderText), 0, utf8.GetByteCount(HeaderText));
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error while writing header");
                Console.WriteLine(ex.Message);
            }
            finally
            {
                if (fStream != null)
                {
                    fStream.Close();
                }
            }
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