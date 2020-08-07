# PointCloudSplitter
PointCloudSplitter splits huge laser scan point cloud pts files into multiple smaller cubes.
 GPLv2-licensed.
 
 
## Installation 
PointCloudSplitter does not come with a fancy install program. The reason for this is that all you need is one exe file (and the program does not depend on settings in registry). 

All configuration is done via a XML file. A sample configuration file called **Standard.config** is provided in the download section. 

* Verify that you have Microsoft .net4 installed on your computer [http://support.microsoft.com/kb/318785/en-us](http://support.microsoft.com/kb/318785/en-us) 
* Download and unzip PointCloudSplitter.exe. 
* Place the exe file in a suitable folder 
* Download the sample config file called **Standard.config** 
* Place the Standard.config file in a suitable folder 
* Edit Standard.config in a text editor such as notepad. Refer this page [The Configuration file](The-Configuration-file) for information about how to edit the configuration file

# Starting PointCloudSplitter 

PointCloudSplitter is a console application. This means you start the program from the command-line or you could write a batch file or script to simplify the start. 
The purpose of this program is to read one or more point cloud files, and then split the files into a number of smaller "cube files". Currently the program can only read and write point cloud files on the pts file format. 



### Start up arguments 
You need to enter a set of start arguments. 

* Full path to where your configuration file is located **/ConfigFile=** 
* Full path to the source folder where your "non split" pts files are located **/SourceFolder=** 
* Full path to a folder where you want to output the cube files **/DestinationFolder=** 
* A scan date. The scan date is added to the filename of the resulting cube files **/ScanDate=** 
* A facility code. The facility code is a text label which will be added to the cube files. Normally a short name for the site or plant you have scanned. **/FacilityCode=** 

To better understand the facility code and scan date start arguments, you should read the section about the configuration file. There you will find more information about how to control the cube file names. 

**Important** You should be aware that the program will try to split **all** pts files found in the source folder and all sub folders below. 
If you have 20 pts files in the source folder and/or in sub folders below, the files will automatically be split into for example 1000 cube files. It does not matter how the original pts files are divided into areas or similar. The program will add laser scan points to the cubes depending on: 
* the cube numbering system 
* the cube size setting 
* the clipping rules (if any) 

The program ignores the fact that the points came from different input files to begin with. 

## The Cube system 
Cube number **1 1 1** will contain points in the coordinate range from xyz 0,0,0 to xyz 5000,5000,5000 if you use the default cube size setting where cube xyz side lengths are set to 5000. Likewise cube number **1 1 2** will contain points with coordinates ranging from xyz 0,0,5001 to xzy 5000,5000,10000 and so on. 

## Disk space 
Before you start the program, we strongly recommend that you manually check the free space on the disk you use as destination for the cubes. The disk should have enough free space to hold the total amount of data in all your input pts files combined. Future versions of the program might be improved to check the disk space required automatically, but the current version does not do this. Think about the risk of running out of disk space before you start. 

## Built in help screen 
When started with no command-line arguments or the argument **/help** , the following help text will be displayed: 
```
---------------------------------------------------------------------------- 
PointCloudSplitter 0.1.4430.40048, Copyright (C) 2012 Ulrik Dan Christensen 
This program is comes with ABSOLUTELY NO WARRANTY. 
This is free software, and you are welcome to redistribute it under certain 
conditions. For license details, documentation and updates please visit 
http://pointcloudsplitter.codeplex.com
---------------------------------------------------------------------------- 

Possible arguments: 
Help Print this help 
ConfigFile Full path to the configuration file 
FacilityCode Facility code 
SourceFolder Source folder 
DestinationFolder Destination folder 
ScanDate Optional - Scan date YYYY-MM-DD if omitted timestamp from 
source pts file will be used 
Example - PointCloudSplitter.exe /ConfigFile="D:\PointCloudSplitter\Standard.con 
fig" /FacilityCode="ZZZ" /SourceFolder="D:\ScanData" /DestinationFolder="D:\Scan 
DataOutput" /ScanDate="2012-01-15" 
```

## Installation 
Information about how to install PointCloudSplitter can be found here: [Installation](Installation) 

## Configuration 
Information about the configuration can be found here: [The Configuration file](The-Configuration-file) 

## Performance 

Test run on 85GB pts data 

**Test computer** 
HP Workstation Z600 WD059AV 
Intel Xeon E5630 2.53 GHz 2 processors 
Barracuda 7200.12 SATA 3Gb/s 250GB Hard Drive 
Windows 7 Enterprise Service Pack 1 
Installed memory 8GB 

**Input** 
Read from pts files on local spindle hard drive 
17 pts files. 85,0 GB (91 363 252 221 bytes) in total 

**Output** 
Write to files on network share 
Total output 85,0 GB (91 363 473 632 bytes) 
Number of cubes in total: 6930 
Number of points in total: 1825336908 

**Timer** 
Time elapsed: 09:21:11.2750627 
54210 Points per second
