# OmexGloToCSV

OmexGloToCSV is a .Net 8 command line utilty to process .glo log files output from Omex MAP4000 ECU engine tuning software and extract the log data in .csv format, suitable for loading in a spreadsheet like Excel or Google Sheets.

## Usage

OmexGloToCsv \<cmd> [-DEBUG] -input <.glo filename>

> Warning: OmexGloToCsv will overwrite .csv files with the same name without asking

  Where \<cmd> is one of:

  | Command | Description |
  |-----|-------------|
  |` -I `| Outputs each channel in individual CSV files with original timestamps e.g. MyLog1-Throttle.csv |
  |` -A `| Output all channels in a single CSV file with timestamps aligned to 30ms intervals e.g. MyLog1-All.csv |
  |` -M `| Generate an AFR map e.g. MyLog1-AFR Map.csv.  glo log file must contain Engine Speed, Engine Load and Lambda channels. |
  |` -MM `| Generate an AFR map with max/min/avg e.g. MyLog1-AFR Map.csv.  glo log file must contain Engine Speed, Engine Load and Lambda channels. |
  

Mandatory Parameters

    -input <filename> - filename / full path to the file to process e.g. MyLog1.glo  Put double quotes round it if there are spaces in the path. 


Optional Parameters

    -DEBUG causes a lot of extra logging to be output to the console.  If there are problems the -I command is most likely to work

**Example**

    OmexGloToCsv -I -input "C:\Users\kuresarf\Documents\OMEX\MAP4000\Logs\MyLog1.glo"

  OmexGloToCsv reads an Omex MAP4000 .glo log file, extracts log data and saves each channel to a .csv file  


## Background
Omex Technology's MAP4000 software is used for configuring a standalone Omex 600 engine ECU in my car but it is very old software that has not been updated in 10+ years.
One of the features in MAP4000 is the ability to capture a live log from the engine but I could not find any way to get the data into a more portable format to analyse it, so I decided to write this utility.

The ultimate goal was to use the logged data from MAP4000 to help tune the engine fuel map by analysing lambda (air fuel ratios) without having access to a rolling road/dyno.

## Development Environment
I developed and tested with the following environment:
- Windows 11 Pro 64 bit, v22H2 (OS build 2261.4317)
- Visual Studio 2022 Community Edition
- .Net Runtime.  The solution targets .Net 8 and I have the following installed (output from "dotnet --list-runtimes" command):
  - Microsoft.NETCore.App 8.0.14 [C:\Program Files\dotnet\shared\Microsoft.NETCore.App]
  - Microsoft.NETCore.App 9.0.3 [C:\Program Files\dotnet\shared\Microsoft.NETCore.App]
  - Microsoft.WindowsDesktop.App 8.0.14 [C:\Program Files\dotnet\shared\Microsoft.WindowsDesktop.App]
  - Microsoft.WindowsDesktop.App 9.0.3 [C:\Program Files\dotnet\shared\Microsoft.WindowsDesktop.App]

Other Software  
- Omex MAP4000 v4.1.48 build 4040 (2014-2-14) - downloaded from https://www.omextechnology.com/product-support/ecu-product-support
- GEMS Data Analysis v4.20 - downloaded from https://gems.co.uk/products/software/gda/
- HxD Hex Editor v2.5

Installed in the car:
- Omex 600 ECU v0.70A
- Zeitronix ZT3 Wideband AFR
- USB Serial Port Adapter (between PC and ECU)

