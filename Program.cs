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
            bool clipsanitycheck = true;
            if (xlowerbool == true || xupperbool == true)
            {
                if (xlowerclip >= xupperclip)
                {
                    clipsanitycheck = false;
                }
            }
            if (ylowerbool == true || yupperbool == true)
            {
                if (ylowerclip >= yupperclip)
                {
                    clipsanitycheck = false;
                }
            }
            if (zlowerbool == true || zupperbool == true)
            {
                if (zlowerclip >= zupperclip)
                {
                    clipsanitycheck = false;
                }
            }
            if (clipsanitycheck == false)
            {
                Console.WriteLine("Error, invalid clipping value");
                return;
            }
            /* in order to parse number with dot as decimal point
             * on machines where default language uses comma
             * as decimal point
             * we use en-US because pts files normally uses dot not comma
             */
            NumberStyles style;
            CultureInfo culture;
            culture = CultureInfo.CreateSpecificCulture("en-US");
            style = NumberStyles.AllowDecimalPoint;

            decimal CubeLenghtX;
            decimal.TryParse(LoadedAppSettings["cubelenghtx"].Value, style, culture, out CubeLenghtX);
            decimal CubeLenghtY;
            decimal.TryParse(LoadedAppSettings["cubelenghty"].Value, style, culture, out CubeLenghtY);
            decimal CubeLenghtZ;
            decimal.TryParse(LoadedAppSettings["cubelenghtz"].Value, style, culture, out CubeLenghtZ);
            //tryparse set int to 0 for invalid strings
            //check for 0 cube lenght and negative lenght
            if (CubeLenghtX <= 0 ||
                CubeLenghtY <= 0 ||
                CubeLenghtZ <= 0
                )
            {
                // one or more strings are not a number.
                Console.WriteLine("Error in cube lenght configuration");
                return;
            }

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
            //*****************
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

            
            int CubeNoX;
            int CubeNoY;
            int CubeNoZ;
            decimal CoordX;
            decimal CoordY;
            decimal CoordZ;
            bool resx;
            bool resy;
            bool resz;
            // search all subfolders
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
            foreach (string FileName in ptsfiles)
            {
                if (File.Exists(FileName))
                {
                    Console.WriteLine("Reading:" + FileName);
                    FileStream Input = new FileStream(FileName, FileMode.Open, FileAccess.Read);
                    StreamReader SR = new StreamReader(Input);
                    string[] Current;
                    int Counter = 0;
                    
                    string Str = SR.ReadLine();
                    while (Str != null)
                    {
                        Current = Str.Split(SeparatorCharArray, StringSplitOptions.RemoveEmptyEntries);
                        if (Current.Length==1)
                        {
                            Console.WriteLine("Number of scan points announced in header:" + Current[0]);    
                        }
                        if (Current.Length > 1)
                        {
                            Console.WriteLine("Col0:" + Current[0] + " Col1:" + Current[1] + " Col2:" + Current[2]);
                            
                            resx = decimal.TryParse(Current[xcolumn], style, culture, out CoordX);
                            resy = decimal.TryParse(Current[ycolumn], style, culture, out CoordY);
                            resz = decimal.TryParse(Current[zcolumn], style, culture, out CoordZ);
                            if (resx==true && resx==true && resx==true)
                            {
                                CubeNoX = Convert.ToInt32(Math.Ceiling(CoordX / CubeLenghtX));
                                CubeNoY = Convert.ToInt32(Math.Ceiling(CoordY / CubeLenghtY));
                                CubeNoZ = Convert.ToInt32(Math.Ceiling(CoordZ / CubeLenghtZ));
                                Console.WriteLine("Debug this point belongs in cube : [" + CubeNoX + " " + CubeNoY + " " + CubeNoZ + "]");
                                Counter++;
                            }
                            // cubelenghtx;

                        }
                        /*
                        if (Counter >= 100000)
                        {
                            Console.WriteLine("debug safety stop  - stopping after 100000 points");
                            return;
                        }
                         */
    
                        Str = SR.ReadLine();
                    }
                    Console.WriteLine("Number of points in " + FileName + " :" + Counter);
                }
            }
            
            stopwatch.Stop();
            Console.WriteLine(System.Windows.Forms.Application.ProductName + " completed");
            Console.WriteLine("Time elapsed: {0}", stopwatch.Elapsed);
            //System.Threading.Thread.Sleep(2500);
            Console.WriteLine("Press any key to continue...");
            Console.ReadKey(true);
        }
    }
}
