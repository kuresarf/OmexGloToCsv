using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Text;
using static OmexGloToCsv.Program;

namespace OmexGloToCsv
{
    internal class Program
    {        

        static void Main(string[] args)
        {

            if (args.Length < 2)
            {
                Console.WriteLine("Usage: OmexGloToCsv <Command> [-DEBUG] -input <.glo filename>");               
                Console.WriteLine("  Where <Command> is one of: ");
                Console.WriteLine("    -I Outputs each channel in individual CSV files with original timestamps e.g. MyLog1-Throttle.csv");
                Console.WriteLine("    -A Output all channels in a single CSV file with timestamps aligned to 30ms intervals e.g. MyLog1-All.csv ");
                Console.WriteLine("    -M Generate an AFR map.  glo log file must contain Engine Speed, Engine Load and Lambda channels.");
                Console.WriteLine("    -MM Generate an AFR map with max/min/avg.  glo log file must contain Engine Speed, Engine Load and Lambda channels.");
                Console.WriteLine(" ");
                Console.WriteLine("Optional Parameters: ");
                Console.WriteLine("  -DEBUG causes a lot of extra logging to be output to the console.  If there are problems the -I command is most likely to work");
                Console.WriteLine(" ");
                Console.WriteLine("Example: OmexGloToCsv -I -input \"C:\\Users\\kuresarf\\Documents\\OMEX\\MAP4000\\Logs\\MyLog1.glo\"");
                Console.WriteLine("  OmexGloToCsv reads an Omex MAP4000 .glo log file, extracts log data and saves each channel to a .csv file");

                return;
            }

            string command = args[0];
            if (command != "-I" && command != "-A" && command != "-M" && command != "-MM")
            {
                Console.WriteLine("Error: Invalid command. Run OmexGloToCsv with no parameters to display usage instructions.");
                return;
            }

            // Define mandatory and optional parameters
            List<string> mandatoryParams = new List<string> { "-input" };
            Dictionary<string, string> parsedParams = new Dictionary<string, string>();

            // Process arguments, adding them into the parsedParams dictionary for use later
            for (int i = 0; i < args.Length; i++)
            {
                if (mandatoryParams.Contains(args[i]) || args[i].StartsWith("-"))
                {
                    if (i + 1 < args.Length && !args[i + 1].StartsWith("-"))                 
                    {
                        // if it is a parameter with a value (e.g. -input "C:\\MyLog.glo") add the value 
                        parsedParams[args[i]] = args[i + 1];
                        i++;
                    }
                    else
                    {
                        parsedParams[args[i]] = "true"; // Flag-style switch
                    }
                }
            }

            // Validate mandatory parameters were passed in
            foreach (var param in mandatoryParams)
            {
                if (!parsedParams.ContainsKey(param))
                {
                    Console.WriteLine($"Error: Missing mandatory parameter {param}");
                    return;
                }
            }

            // Check the input parameter points to a valid file
            string filePath = parsedParams.GetValueOrDefault("-input", "");
            if (!File.Exists(filePath))
            {
                Log($"Error: File '{filePath}' not found, check the -input parameter.");
                Log($"Full path is '{Path.GetFullPath(filePath)}'");
                
                return;
            }

            try
            {
                using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                using (var reader = new BinaryReader(fileStream))
                {
                    Log("  ");
                    Log("Processing " + filePath);

                    // Determine the log level based on the -DEBUG parameter
                    SimpleLogger logger;
                    if (parsedParams.GetValueOrDefault("-DEBUG", "false") == "true")
                        logger = new SimpleLogger(LogLevel.Debug);
                    else
                        logger = new SimpleLogger(LogLevel.Info);

                    // Create the OmexGloLogFile, which will read the file and identify the log channels
                    OmexGloLogFile logFile = new OmexGloLogFile(reader, logger);

                    Log($"Found {logFile.SubFiles.Count} subfiles");
                    Log($"Found {logFile.LogChannelParsers.Count} log channels");

                    if (command == "-I")
                    {
                        Log("Saving each log channel to individual CSV files");
                        SaveEachLogChannelToCsvFile(filePath, reader, logFile);
                    }
                    else if (command == "-A")
                    {
                        Log("Saving all log channels to a single CSV file");
                        SaveAllLogChannelsToSingleCsvFile(filePath, reader, logFile);

                    }
                    else if (command == "-M")
                    {
                        Log("Creating AFR map");
                        SaveAfrMap(filePath, reader, logFile, logger, false);

                    }
                    else if (command == "-MM")
                    {
                        Log("Creating AFR map with max/min/avg");
                        SaveAfrMap(filePath, reader, logFile, logger, true);

                    }
                }
            }
            catch (Exception ex)
            {
                Log($"Error reading file: {ex.Message}");
            }

            Log("Done!");
        }



        private static void SaveAfrMap(string filePath, BinaryReader reader, OmexGloLogFile logFile,SimpleLogger logger, Boolean includeMaxMin)
        {                                 
            
            OmexGloLogChannelReader engineLoadLogChannelReader = null;
            OmexGloLogChannelReader engineSpeedLogChannelReader = null;
            OmexGloLogChannelReader lambdaLogChannelReader = null;

            foreach (OmexGloLogChannelReader logChannelReader in logFile.LogChannelParsers)
            {
                // Load the channel descriptor and data blocks ready for processing
                logChannelReader.ReadChannelFormatXml(reader);

                // Keep a reference to the engine load and engine speed channels to let us calculate the fuel map addresses
                if (logChannelReader.LogChannel.OutputName == "Engine Load")
                {
                    engineLoadLogChannelReader = logChannelReader;
                }
                else if (logChannelReader.LogChannel.OutputName == "Engine Speed")
                {
                    engineSpeedLogChannelReader = logChannelReader;
                }
                else if (logChannelReader.LogChannel.OutputName == "Lambda1")
                {
                    lambdaLogChannelReader = logChannelReader; 
                }
            }

            if (engineLoadLogChannelReader == null || engineSpeedLogChannelReader == null || lambdaLogChannelReader == null)
            {
                Log("Error: Missing required channels for AFR map generation.  Log must include Engine Load, Engine Speed and Lambda");
                return;
            }

            OmexMapGrid afrMap = new OmexMapGrid( engineLoadLogChannelReader, engineSpeedLogChannelReader, lambdaLogChannelReader, logger);

            afrMap.LoadMapData(reader);
            StringBuilder csvStr = afrMap.GetMapCsvString(filePath + " - " + logFile.LogNotes, includeMaxMin);
            SaveStringBuilderToFile(csvStr, Path.GetFileNameWithoutExtension(filePath) + "-AFR Map.csv");
        }


        private static void SaveAllLogChannelsToSingleCsvFile(string filePath, BinaryReader reader, OmexGloLogFile logFile)
        {
            List<StringBuilder> csvStrings = new List<StringBuilder>();
            HashSet<uint> uniqueTimestamps = new HashSet<uint>();

            // Build the CSV header string from all the channel names / units
            StringBuilder csvHeaderStr = new StringBuilder("\"Time (ms)\"");
            foreach (OmexGloLogChannelReader logChannelParser in logFile.LogChannelParsers)
            {
                // Load the channel descriptor and data blocks ready for processing
                logChannelParser.LoadRawDataBlocks(reader);
                OmexLogChannel tempLogChannel = logChannelParser.LoadLogChannel(true);  // alignTimestamps = true, may lose some records, but aligns data on common time axis

                csvHeaderStr.Append(",\"" + logChannelParser.LogChannel.OutputName + " (" + logChannelParser.LogChannel.OutputUnits + ")\"");

                // Loop through the log records and add the time to the list of unique timestamps
                foreach (OmexLogRecord logRecord in tempLogChannel.LogRecords.Values)
                {
                    // Add the time to the list of unique timestamps
                    uniqueTimestamps.Add(logRecord.TimeInMillis);
                }
            }

            // Store the CSV header
            csvStrings.Add(csvHeaderStr);

            // Sort the timestamps so we keep the data in the right order
            SortedSet<uint> sortedTimestamps = new SortedSet<uint>(uniqueTimestamps);

            // Use the unique time stamps as a common time axis for all channels and add each channel's data 
            foreach (uint timeStampInMillis in sortedTimestamps)
            {
                StringBuilder sb = new StringBuilder(timeStampInMillis.ToString());
                csvStrings.Add(sb);

                // Loop round and add the log data value from every channel to the CSV
                foreach (OmexGloLogChannelReader logChannelParser in logFile.LogChannelParsers)
                {
                    OmexLogChannel logChannel = logChannelParser.LogChannel;

                    if (logChannel.LogRecords.ContainsKey(timeStampInMillis))
                    {
                        OmexLogRecord logRecord = logChannel.LogRecords[timeStampInMillis];
                        sb.Append("," + logRecord.LogValueReal.ToString());
                    }
                    else
                    {
                        // If the log channel doesn't have a record for this time, add a blank value
                        sb.Append(",");
                    }

                }
            }

            SaveStringListToFile(csvStrings, Path.GetFileNameWithoutExtension(filePath) + "-All.csv");
        }

        private static void SaveEachLogChannelToCsvFile(string filePath, BinaryReader reader, OmexGloLogFile logFile)
        {
            foreach (OmexGloLogChannelReader logChannelParser in logFile.LogChannelParsers)
            {
                // Read each log channel data and save it to a CSV file
                Log($"  Reading log data from {logChannelParser.ChannelFormatSubFile.SubFileName} ");
                StringBuilder csvStr = ReadSingleLogChannelIntoCsvString(reader, logChannelParser);
                SaveStringBuilderToFile(csvStr, Path.GetFileNameWithoutExtension(filePath) + "-" + logChannelParser.LogChannel.OutputName + ".csv");

            }
        }

        // Update the code to use the concrete implementation
        private static StringBuilder ReadSingleLogChannelIntoCsvString(BinaryReader reader, OmexGloLogChannelReader logChannelReader)
        {
            logChannelReader.LoadRawDataBlocks(reader);
            OmexLogChannel logChannel = logChannelReader.LoadLogChannel(false);
            
            StringBuilder csvStr = new StringBuilder("\"Time (ms)\",\"" + logChannel.OutputName + " (" + logChannel.OutputUnits + ")\"" + Environment.NewLine);

            // process each log record, adding the time and data value to a CSV string
            foreach (OmexLogRecord logRecord in logChannel.LogRecords.Values)
            {
                csvStr.Append(logRecord.TimeInMillis.ToString() + "," + logRecord.LogValueReal.ToString() + Environment.NewLine);
            }

            return csvStr;
        }



        private static void SaveStringListToFile(List<StringBuilder> csvStrings, string csvFileName)
        {
            // Remove invalid characters from the file name
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                csvFileName = csvFileName.Replace(c, '_');
            }

            try
            {
                using (StreamWriter writer = new StreamWriter(csvFileName))
                {
                    foreach (var csvStr in csvStrings)
                    {
                        writer.WriteLine(csvStr.ToString());
                    }                    
                }

                csvFileName = Path.GetFullPath(csvFileName);
                Log($"  Saved CSV file to {csvFileName}");
            }
            catch (Exception ex)
            {
                Log($"Error saving csv file: {ex.Message}");
            }
        }


        private static void SaveStringBuilderToFile(StringBuilder csvStr, string csvFileName)
        {
            // Remove invalid characters from the file name
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                csvFileName = csvFileName.Replace(c, '_');
            }

            try
            {
                using (StreamWriter writer = new StreamWriter(csvFileName))
                {
                    writer.WriteLine(csvStr.ToString());
                }

                csvFileName = Path.GetFullPath(csvFileName);
                Log($"  Saved data to {csvFileName}");
            }
            catch (Exception ex)
            {
                Log($"Error saving csv file: {ex.Message}");
            }
        }





        private static void Log(string logString)
        {
            Console.WriteLine(logString);
        }
    }
}
