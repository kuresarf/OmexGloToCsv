using System.Text;

namespace OmexGloToCsv
{
    internal class Program
    {
        static void Main(string[] args)
        {

            if (args.Length < 1)
            {
                Log("Usage: OmexGloToCsv \"C:\\Users\\john\\Documents\\OMEX\\MAP4000\\Logs\\MyLog1.glo\"");
                Log("Reads an Omex Map4000 .glo log file, extracts each ECU log channel and saves it to a .csv file named using the input filename and the channel name e.g. MyLog1-Throttle.csv");
                return;
            }

            string filePath = args[0];

            if (!File.Exists(filePath))
            {
                Log($"Error: File '{filePath}' not found.");
                return;
            }

            try
            {
                using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                using (var reader = new BinaryReader(fileStream))
                {
                    Log("  ");
                    Log("Processing " + filePath);

                    // Create the OmexGloLogFile, which will read the file and identify the log channels
                    OmexGloLogFile logFile = new OmexGloLogFile(reader, new SimpleLogger(LogLevel.Info));

                    Log($"Found {logFile.SubFiles.Count} subfiles");
                    Log($"Found {logFile.LogChannels.Count} log channels");

                    SaveEachLogChannelToCsvFile(filePath, reader, logFile);
                }
            }
            catch (Exception ex)
            {
                Log($"Error reading file: {ex.Message}");
            }

            Log("Done!");
        }

        private static void SaveEachLogChannelToCsvFile(string filePath, BinaryReader reader, OmexGloLogFile logFile)
        {
            foreach (OmexGloLogChannel logChannel in logFile.LogChannels)
            {
                // Read each log channel data and save it to a CSV file
                Log($"  Reading log data from {logChannel.ChannelFormatSubFile.SubFileName} ");
                StringBuilder csvStr = ReadSingleLogChannelIntoCsvString(reader, logChannel);
                SaveCsvDataToFile(csvStr, Path.GetFileNameWithoutExtension(filePath) + "-" + logChannel.OutputName + ".csv");

            }
        }

        // Update the code to use the concrete implementation
        private static StringBuilder ReadSingleLogChannelIntoCsvString(BinaryReader reader, OmexGloLogChannel logChannel)
        {
            logChannel.LoadAllDataBlocks(reader);

            StringBuilder csvStr = new StringBuilder("\"Time (ms)\",\"" + logChannel.OutputName + " (" + logChannel.OutputUnits + ")\"" + Environment.NewLine);

            // Create a new LogRecordProcessor callback to process each log entry, adding the time and data value to a CSV string
            var logRecordProcessor = new LogRecordProcessor
            {
                OnProcessLogRecord = (timeInMillis, logValueInBits) =>
                {
                    csvStr.Append(timeInMillis.ToString() + "," + logChannel.ConvertRawValueToRealValue(logValueInBits) + Environment.NewLine);
                }
            };

            logChannel.ProcessLogData(logRecordProcessor);

            return csvStr;
        }


        private static void SaveCsvDataToFile(StringBuilder csvStr, string csvFileName)
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
                Log($"  Saved channel data to {csvFileName}");
            }
            catch (Exception ex)
            {
                Log($"Error saving csv file: {ex.Message}");
            }
        }


        // Define a concrete implementation of the ILogRecordProcessor interface
        internal class LogRecordProcessor : ILogRecordProcessor
        {
            public Action<uint, uint> OnProcessLogRecord { get; set; }

            public void ProcessLogEntry(uint timeInMillis, uint logValueInBits)
            {
                OnProcessLogRecord?.Invoke(timeInMillis, logValueInBits);
            }
        }


        private static void Log(string logString)
        {
            Console.WriteLine(logString);
        }
    }
}
