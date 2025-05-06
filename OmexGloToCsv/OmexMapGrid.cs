using System;
using static OmexGloToCsv.Program;
using System.Reflection.PortableExecutable;
using System.IO;
using System.Text;

namespace OmexGloToCsv
{

    internal class OmexMapCell
    {
        internal List<OmexLogRecord> logRecords;

        public uint MapAddress { get; set; }
        
        public OmexMapCell(uint mapAddress)
        {
            this.MapAddress = mapAddress;
            
            logRecords = new List<OmexLogRecord>(); // List of lambda values per map site, to be averaged for the map log
        }

        public void AddLogRecord(OmexLogRecord logRecord)
        {
            logRecords.Add(logRecord);
        }
    }

    public class OmexMapGrid
    {
        private ILogger logger;

        private OmexGloLogChannelReader engineLoadLogChannelParser;
        private OmexGloLogChannelReader engineSpeedLogChannelParser;
        private OmexGloLogChannelReader otherLogChannelParser;

        private Dictionary<uint, OmexMapCell> mapCells;

        // These are the 11 values for the VE / Engine Load vertical axis on my fuel map in MAP4000, measured in kPa.  Needs to be configurable in future.
        private int[] loadScaleInKpa = { 35, 59, 87, 95, 103, 111, 119, 127, 135, 143, 151 };

        // These are the 21 values for the Engine Speed horizontal axis on my fuel map in MAP4000, measured in RPM.  Needs to be configurable in future.
        private int[] engineSpeedInRpm = { 600, 800, 1000, 1200, 1600, 1900, 2400, 2800, 3200, 3600, 4000, 4400, 4800, 5200, 5600, 6000, 6400, 6800, 7200, 7600, 8000 };

        public OmexMapGrid(OmexGloLogChannelReader engineLoadLogChannelParser, OmexGloLogChannelReader engineSpeedLogChannelParser, OmexGloLogChannelReader otherLogChannelParser, ILogger logger)
        {
            this.logger = logger;

            this.engineLoadLogChannelParser = engineLoadLogChannelParser;
            this.engineSpeedLogChannelParser = engineSpeedLogChannelParser;
            this.otherLogChannelParser = otherLogChannelParser;

            if (engineLoadLogChannelParser == null || engineSpeedLogChannelParser == null || otherLogChannelParser == null)
            {
                doLog(LogLevel.Error, "Error: Missing required channels for AFR map generation.  Log must include Engine Load, Engine Speed and Lambda");
                throw new ArgumentException("Need three valid map channels to create a map");
            }
            
            mapCells = new Dictionary<uint, OmexMapCell>();            
        }

        public void LoadMapData(BinaryReader reader)
        {
            OmexGloLogChannelReader[] omexGloLogChannelReaders = { engineLoadLogChannelParser, engineSpeedLogChannelParser, otherLogChannelParser };                      
       
            foreach (OmexGloLogChannelReader logChannelParser in omexGloLogChannelReaders)
            {
                // Load the channel descriptor and data blocks ready for processing
                logChannelParser.LoadRawDataBlocks(reader);
                OmexLogChannel tempLogChannel = logChannelParser.LoadLogChannel(true);  // alignTimestamps = true, may lose some records, but aligns data on common time axis
            }

            populateMapCells();
        }

        private void populateMapCells()
        {
            int goodCount = 0;
            int errorCount = 0;

            // Now we have the engine load, engine speed and lambda values, we can calculate the map address for each record
            // using Load and Speed then store the log record in the map cell
            foreach (var timeInMillis in otherLogChannelParser.LogChannel.LogRecords.Keys)
            {
                OmexLogRecord logRecord = null;
                otherLogChannelParser.LogChannel.LogRecords.TryGetValue(timeInMillis, out logRecord);

                OmexLogRecord engineLoadRecord = null;
                engineLoadLogChannelParser.LogChannel.LogRecords.TryGetValue(timeInMillis, out engineLoadRecord);

                OmexLogRecord engineSpeedRecord = null;
                engineSpeedLogChannelParser.LogChannel.LogRecords.TryGetValue(timeInMillis, out engineSpeedRecord);

                if ((logRecord == null) ||
                    (engineLoadRecord == null) ||
                    (engineSpeedRecord == null))
                {
                    doLog(LogLevel.Debug, "Missing log record for map cell population at " + timeInMillis.ToString() + "ms, data has been excluded");
                    errorCount++;
                    continue;
                }

                logRecord.MapAddress = GetMapAddress(engineLoadRecord.LogValueReal, engineSpeedRecord.LogValueReal);

                // Associate the log record with the cell based on the map address
                OmexMapCell cell;
                mapCells.TryGetValue(logRecord.MapAddress, out cell);
                if (cell == null)
                {
                    // If we didn't create a cell for this map address yet, create one now
                    cell = new OmexMapCell(logRecord.MapAddress);
                    cell.AddLogRecord(logRecord);

                    mapCells.Add(logRecord.MapAddress, cell);
                }
                else
                {
                    // Otherwise, just add the log record to the existing cell
                    cell.AddLogRecord(logRecord);
                }

                goodCount++;
            }

            if (errorCount > 0)
            {
                doLog(LogLevel.Warning, "Warning: " + goodCount.ToString() + " records included but " + errorCount.ToString() + " were excluded from the map cell population");
            }
        }

        public StringBuilder GetMapCsvString(String optionalHeaderString, Boolean includeMaxMin)
        {
            StringBuilder csvString = new StringBuilder();
            StringBuilder csvRow = new StringBuilder();
            StringBuilder csvMaxRow = new StringBuilder();
            StringBuilder csvMinRow = new StringBuilder();

            if (optionalHeaderString != null)
            {
                csvRow.Append(optionalHeaderString + Environment.NewLine);
            }

            csvRow.Append("Map of " + otherLogChannelParser.LogChannel.OutputName + Environment.NewLine);

            // Build RPM header row
            csvRow.Append("Load(KPa) / RPM,");
            foreach (var rpm in engineSpeedInRpm)
            {
                csvRow.Append(rpm.ToString() + ",");
            }
            csvRow.Append(Environment.NewLine);
            csvString.Append(csvRow);
            csvRow.Clear();

            int loadRowIndex = loadScaleInKpa.Length-1;

            // For each of the map cells, calculate the average lambda value and add to the CSV string
            for (int i = 230; i > -1; i--)
            {
                if (mapCells.ContainsKey((uint)i))
                {
                    OmexMapCell cell = mapCells[(uint)i];

                    float max = 0;
                    float min = 9999999;  //arbitrary large value, to be replaced by the first log value
                    float sum = 0;
                    foreach (var logRecord in cell.logRecords)
                    {
                        // identify highest and lowest lambda values that appear in the cell
                        if (logRecord.LogValueReal > max)
                        {
                            max = logRecord.LogValueReal;
                        }

                        if (logRecord.LogValueReal < min)
                        {
                            min = logRecord.LogValueReal;
                        }

                        sum += logRecord.LogValueReal;
                    }
                    float average = sum / cell.logRecords.Count;

                    average = (float)Math.Round(average, 2);  // Round to 3 decimal places
                    min = (float)Math.Round(min, 2);
                    max = (float)Math.Round(max, 2);

                    csvMaxRow.Insert(0, max.ToString());

                    // Add the average lambda values to the map CSV
                    csvRow.Insert(0, average.ToString());                                      
                    
                    // Add the min lambda values to the map CSV
                    csvMinRow.Insert(0, min.ToString());                                       
                }

                int horizontalIndex = i % 21;
                if (horizontalIndex == 0)
                {
                    // Add a new line every 21 times to build a map grid with 21 columns and 11 rows
                    csvMaxRow.Insert(0, loadScaleInKpa[loadRowIndex].ToString() + "-Max,");  // Add the load axis to the start of the row
                    csvMaxRow.Append(Environment.NewLine);                   

                    csvRow.Insert(0, loadScaleInKpa[loadRowIndex].ToString() + "-Avg,");  // Add the load axis to the start of the row
                    csvRow.Append(Environment.NewLine);

                    csvMinRow.Insert(0, loadScaleInKpa[loadRowIndex].ToString() + "-Min,");  // Add the load axis to the start of the row
                    csvMinRow.Append(Environment.NewLine);

                    if (includeMaxMin)
                    {
                        csvString.Append(csvMaxRow);
                    }
                    
                    csvString.Append(csvRow);
                    
                    if (includeMaxMin)
                    {
                        csvString.Append(csvMinRow);
                    }                      

                    csvMaxRow.Clear();
                    csvRow.Clear();
                    csvMinRow.Clear();

                    loadRowIndex--;
                }
                else
                {
                    // Add a comma to separate each cell value in a row
                    csvMaxRow.Insert(0, ",");
                    csvRow.Insert(0, ",");
                    csvMinRow.Insert(0, ",");
                }

            }

            csvString.Append(Environment.NewLine);  // add a trailing new line to the CSV string, makes it easier to append maps together later

            return csvString;
        }


        public uint GetMapAddress(float realEngineLoad, float realEngineSpeed)
        {
            // A fuel map in MAP4000 has 11 * 21 = 231 cells (probably had to be <255 to fit the cell address into 1 byte)

            int loadIndex = loadScaleInKpa.Length;
            for (int i = 0; i < loadScaleInKpa.Length; i++)
            {
                if (realEngineLoad < loadScaleInKpa[i])
                {
                    loadIndex = i;
                    break;
                }
            }
            
            int speedIndex = engineSpeedInRpm.Length;
            for (int i = 0; i < engineSpeedInRpm.Length; i++)
            {
                if (realEngineSpeed < engineSpeedInRpm[i])
                {
                    speedIndex = i;
                    break;
                }
            }

            // Calculate the map address based on the engine load index and speed index (each cell in the map will have an id from 0 to 230)
            uint mapAddress = (uint)((loadIndex * 21) + speedIndex);

            return mapAddress;
        }

        private void doLog(LogLevel level, string logString)
        {
            if (logger != null)
            {
                logger.Log(level, logString);
            }
        }




    }
}

