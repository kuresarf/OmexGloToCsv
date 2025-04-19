# OmexGloToCSV

OmexGloToCSV is a .Net command line utilty to process .glo log files output from Omex MAP4000 ECU tuning software and extract the log data in .csv format, suitable for loading in a spreadsheet like Excel or Google Sheets.

## Background
Omex Technology's MAP4000 software is used for configuring a standalone Omex 600 engine ECU in my car but it is very old software that has not been updated in 10+ years.
One of the features in MAP4000 is the ability to capture a live log from the engine but I could not find any way to get the data into a more portable format to analyse it, so I decided to write this utility.
The ultimate goal was to use the logged data from MAP4000 to help tune the engine fuel map by analysing lambda (air fuel ratios) without having access to a rolling road/dyno.

## Development Environment
I developed and tested with the following environment:
- Windows 11 Pro 64 bit, v22H2 (OS build 2261.4317) 
- Visual Studio 2022 Community Edition
- Omex MAP4000 v4.1.48 build 4040 (2014-2-14) - downloaded from https://www.omextechnology.com/product-support/ecu-product-support

Installed in the car:
- Omex 600 ECU v0.70A
- Zeitronix ZT3 Wideband AFR
