
using System.Runtime.InteropServices.Marshalling;
using System.Text;
using System.Xml;
using static OmexGloToCsv.Program;

namespace OmexGloToCsv
{

    public class OmexLogRecord
    {
        public uint TimeInMillis { get; set; }
        public uint MapAddress { get; set; }
        public float LogValueReal { get; set; }
        public uint LogValueRaw { get; set; }

        public OmexLogRecord(uint timeInMillis)
        {
            this.TimeInMillis = timeInMillis;
            MapAddress = 0;
            LogValueReal = 0;
            LogValueRaw = 0;
        }
    }

    // Interface for callbacks when processing log records
    public interface ILogRecordProcessor
    {
        // Callback method to be called when a log record is processed
        void ProcessLogEntry(uint timeInMillis, uint logValueInBits);
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


    public class OmexLogChannel
    {
        internal string outputName;
        internal int outputSize;
        internal float outputMin;
        internal float outputMax;
        internal float outputRange;
        internal float outputValuePerBit;
        internal int outputSigned;
        internal string outputUnits;

        internal Dictionary<uint, OmexLogRecord> allLogRecords;

        public const int TIMESTAMP_ALIGNMENT_MILLIS = 40;

        public string OutputName { get => outputName; }
        public int OutputSize { get => outputSize; }
        public float OutputMin { get => outputMin; }
        public float OutputMax { get => outputMax; }
        public float OutputRange { get => outputRange; }
        public float OutputValuePerBit { get => outputValuePerBit; }
        public int OutputSigned { get => outputSigned; }
        public string OutputUnits { get => outputUnits; }


        public Dictionary<uint, OmexLogRecord> LogRecords
        {
            get { return allLogRecords; }
        }

        public OmexLogChannel()
        {
            outputName = "";
            outputSize = 0;
            outputMin = 0;
            outputMax = 0;
            outputRange = 0;
            outputValuePerBit = 0;
            outputSigned = 0;
            outputUnits = "";

            allLogRecords = new Dictionary<uint, OmexLogRecord>();
        }

        public void AddLogRecord(uint timeInMillis, uint rawValue)
        {
            OmexLogRecord logRecord = new OmexLogRecord(timeInMillis);
            logRecord.LogValueRaw = rawValue;
            logRecord.LogValueReal = ConvertRawValueToRealValueAsFloat(rawValue);

            // Used TryAdd to handle duplicate timeInMillis values - assuming that if the time key is the same the data will be the same
            allLogRecords.TryAdd(logRecord.TimeInMillis, logRecord);
        }

        internal string ConvertRawValueToRealValueAsString(uint logValueUint16)
        {
            float realValue = ConvertRawValueToRealValueAsFloat(logValueUint16);
            return realValue.ToString();
        }

        internal float ConvertRawValueToRealValueAsFloat(uint logValueUint16)
        {
            float realValue = logValueUint16 * OutputValuePerBit;
            // Round the real value to 3 decimal places
            realValue = (float)Math.Round(realValue, 3);
            return realValue;
        }
    }


    public class OmexGloLogChannelReader
    {
        private Boolean isXmlFormatLoaded = false;
        private OmexGloSubFileDescriptor channelFormatSubFile;
        private OmexGloSubFileDescriptor channelLogDataSubFile;
        private byte[] allLogDataBytes = null;

        private ILogger logger;

        private OmexLogChannel theLogChannel;


        public OmexGloLogChannelReader(OmexGloSubFileDescriptor channelFormatSubFile, OmexGloSubFileDescriptor channelLogDataSubFile, ILogger logger)
        {
            this.channelFormatSubFile = channelFormatSubFile;
            this.channelLogDataSubFile = channelLogDataSubFile;
            this.logger = logger;

            theLogChannel = null;
        }

        public OmexGloSubFileDescriptor LogDataSubFile
        {
            get { return channelLogDataSubFile; }
            set { channelLogDataSubFile = value; }
        }

        public OmexGloSubFileDescriptor ChannelFormatSubFile
        {
            get { return channelFormatSubFile; }
            set { channelFormatSubFile = value; }
        }

        public OmexLogChannel LogChannel
        {
            get { return theLogChannel; }            
        }

        private void doLog(LogLevel level, string logString)
        {
            if (logger != null)
            {
                logger.Log(level, logString);
            }
        }

        public void ReadChannelFormatXml(BinaryReader reader)
        {
            // Read the xml descriptor from the .gcf file
            string xmlStr = "";

            doLog(LogLevel.Debug, "  Reading channel format XML for " + this.ChannelFormatSubFile.SubFileName);

            // Check if the channel format file descriptor is valid - if not, return immediately
            if ((ChannelFormatSubFile.DataBlockAddresses.Count == 0) ||
                    (ChannelFormatSubFile.DataBlockAddresses[0] == 0))
            {
                doLog(LogLevel.Error, "Error: invalid .gcf channel file");
                return;
            }

            // Seek to the start of the subfile's first data chunk, plus 8 bytes to skip the 2 DWORD header
            reader.BaseStream.Seek(ChannelFormatSubFile.DataBlockAddresses[0] + 8, SeekOrigin.Begin);
            byte[] gcfHeaderBytes = reader.ReadBytes(30);
            // Check if the header is valid: "GEMS v4 LOGGING CHANNEL FORMAT"
            if (gcfHeaderBytes.Length == 30)
            {
                //Log("    GCF Header: " + Encoding.Default.GetString(gcfHeaderBytes));

                reader.ReadBytes(1); // Skip 1 byte (0x1A)

                reader.ReadBytes(4); // Skip 4 bytes (0x00 0x00 0x00 0x00)

                // Read DWORD containing length of the XML string in widechars (i.e. 2 * length in bytes)
                byte[] dwordBytes = reader.ReadBytes(4);
                uint xmlLength = BitConverter.ToUInt32(dwordBytes, 0) * 2;

                // Read the XML string
                byte[] xmlBytes = reader.ReadBytes((int)xmlLength);
                if (xmlBytes.Length == xmlLength)
                {
                    xmlStr = Encoding.Default.GetString(xmlBytes);
                    // convert widechar string to normal string
                    xmlStr = xmlStr.Replace("\0", ""); // remove null characters
                    xmlStr = xmlStr.Replace("\r", ""); // remove carriage returns
                    xmlStr = xmlStr.Replace("\n", ""); // remove new lines
                    xmlStr = xmlStr.Replace("\t", ""); // remove tabs
                    xmlStr = xmlStr.Replace("  ", ""); // remove double spaces

                    // remove weird escape sequences used for unit names
                    xmlStr = xmlStr.Replace("&micro;", "micro");  //for micro seconds
                    xmlStr = xmlStr.Replace("&deg;", "degrees");  //for degrees spark advance

                    doLog(LogLevel.Debug, "    Channel Format XML: " + xmlStr);
                }
                else
                {
                    doLog(LogLevel.Error, "    Error: invalid XML string length");
                }
            }

            ExtractChannelPropertiesFromXml(xmlStr);
        }



        private void ExtractChannelPropertiesFromXml(string xmlFormatString)
        {
            // Load the xmlFormatString into an XML document
            XmlDocument channelXmlDoc = new XmlDocument();
            channelXmlDoc.LoadXml(xmlFormatString);

            // select the channel node in the xml document
            XmlNode channelOutputNode = channelXmlDoc.SelectSingleNode("/channel/output");
            if (channelOutputNode == null)
            {
                doLog(LogLevel.Error, "Error: No channel output node found in XML");
                return;
            }

            // Create a new log channel object to store the meta data for the channel
            theLogChannel = new OmexLogChannel();

            // Get the channel name
            string channelOutputProperty = channelOutputNode.Attributes["name"].Value;
            doLog(LogLevel.Info, "    Channel Name: " + channelOutputProperty);
            theLogChannel.outputName = channelOutputProperty;

            // Get the channel size
            channelOutputProperty = channelOutputNode.Attributes["size"].Value;
            doLog(LogLevel.Debug, "    Channel Size: " + channelOutputProperty);
            theLogChannel.outputSize = int.Parse(channelOutputProperty);

            // Get the channel min
            channelOutputProperty = channelOutputNode.Attributes["min"].Value;
            doLog(LogLevel.Debug, "    Channel Min: " + channelOutputProperty);
            theLogChannel.outputMin = float.Parse(channelOutputProperty);

            // Get the channel max
            channelOutputProperty = channelOutputNode.Attributes["max"].Value;
            doLog(LogLevel.Debug, "    Channel Max: " + channelOutputProperty);
            theLogChannel.outputMax = float.Parse(channelOutputProperty);

            theLogChannel.outputRange = theLogChannel.outputMax - theLogChannel.outputMin;

            // Get the channel signed
            channelOutputProperty = channelOutputNode.Attributes["signed"].Value;
            doLog(LogLevel.Debug, "    Channel Signed: " + channelOutputProperty);
            theLogChannel.outputSigned = int.Parse(channelOutputProperty);

            // Get the channel units
            channelOutputProperty = channelOutputNode.Attributes["units"].Value;
            doLog(LogLevel.Debug, "    Channel Units: " + channelOutputProperty);
            theLogChannel.outputUnits = channelOutputProperty;

            // select the scalar node in the xml document
            XmlNode channelScalarNode = channelXmlDoc.SelectSingleNode("/channel/output/scalar");
            if (channelScalarNode == null)
            {
                doLog(LogLevel.Warning, "    Warning: No channel output scalar node found in XML, defaulting ValuePerBit to 1");
                theLogChannel.outputValuePerBit = 1;  // Default to 1 if not found
                return;
            }

            // Get the value-per-bit for the channel used to calculate the real value from the 8 or 16 bit raw value
            channelOutputProperty = channelScalarNode.Attributes["m_adjusted"].Value;
            doLog(LogLevel.Debug, "    Channel Real Value Per Bit: " + channelOutputProperty);
            theLogChannel.outputValuePerBit = float.Parse(channelOutputProperty);

            isXmlFormatLoaded = true;
        }

        public void LoadRawDataBlocks(BinaryReader reader)
        {
            // If we've not loaded the channel format XML yet, do it now
            if ((!isXmlFormatLoaded) || (theLogChannel == null))
            {
                ReadChannelFormatXml(reader);
            }

            // Allocate buffer big enough to hold all the blocks of data
            allLogDataBytes = new byte[channelLogDataSubFile.DataBlockAddresses.Count * (channelLogDataSubFile.SubFileSize - 8)];
            uint index = 0;

            // Loop round all the data blocks in the chain, fetch the log data and append it to one large buffer   
            foreach (uint dataBlockAddress in channelLogDataSubFile.DataBlockAddresses)
            {
                // read a single data block
                byte[] singleBlockBytes = ReadSingleDataBlock(reader, dataBlockAddress);

                // skip the first 8 bytes of each block (header) and append the rest to the dataBlockBytes buffer
                Array.Copy(singleBlockBytes, 8, allLogDataBytes, index, singleBlockBytes.Length - 8);
                index = index + (uint)singleBlockBytes.Length - 8;
            }

        }

        private byte[] ReadSingleDataBlock(BinaryReader reader, uint dataBlockOffset)
        {
            // Seek to the start of the subfile's first data chunk and read the full block (typically 4Kb)
            reader.BaseStream.Seek(dataBlockOffset, SeekOrigin.Begin);
            byte[] dataBlockBytes = reader.ReadBytes((int)channelLogDataSubFile.SubFileSize);

            if (dataBlockBytes.Length != channelLogDataSubFile.SubFileSize)
            {
                doLog(LogLevel.Error, "    Error: could not read log data - invalid data block, too short");
            }

            return dataBlockBytes;
        }

        public OmexLogChannel LoadLogChannel(Boolean alignTimeStamps)
        {
            // Check if the log data has been loaded
            if ((allLogDataBytes == null) || (theLogChannel == null))
            {
                doLog(LogLevel.Error, "Error: Log data not loaded, ensure you call LoadAllDataBlocks first");
                throw new ArgumentException("Log data not loaded yet");
            }                                   

            // Handle the timeInMillis value overflowing every 65535 milliseconds (65.535 seconds)
            uint previousHighestTime = 0;
            uint timeOffset = 0;

            // Get all the time and data records out the data block and add into the log record list for easier access
            var logRecordProcessor = new LogRecordProcessor
            {
                OnProcessLogRecord = (timeInMillis, logValueInBits) =>
                {
                    if (timeInMillis < previousHighestTime)
                    {
                        // Time has overflowed, so add 65536 (2 byte max val) to the time offset value
                        timeOffset = timeOffset + 65536;  
                    }
                    
                    uint theTimeStamp = (timeOffset + timeInMillis);
                    if (alignTimeStamps)
                    {
                        // Round down the time to the nearest TIMESTAMP_ALIGNMENT_MILLIS (30ms) interval, to make it easier to align with other channels
                        theTimeStamp = (uint)(theTimeStamp - (theTimeStamp % OmexLogChannel.TIMESTAMP_ALIGNMENT_MILLIS));                        
                    }            

                    // Create a record to hold the data values 
                    theLogChannel.AddLogRecord(theTimeStamp, logValueInBits);

                    previousHighestTime = timeInMillis;  // remember the latest time value to check if we rolled over
                }
            };

            ProcessLogDataWithCallback(logRecordProcessor);

            return theLogChannel;
        }



        public void ProcessLogDataWithCallback(ILogRecordProcessor logRecordProcessor)
        {
            // Check if the channel format XML has been loaded
            if (!isXmlFormatLoaded)
            {
                doLog(LogLevel.Error, "Error: Channel format XML not loaded, ensure you call ReadChannelFormatXml first");
                return;
            }

            // Check if the log data has been loaded
            if ((allLogDataBytes == null) || (theLogChannel == null))
            {
                doLog(LogLevel.Error, "Error: Log data not loaded, ensure you call LoadAllDataBlocks first");
                return;
            }

            // Now we have the full data block, we process it to add each time/ value record to a CSV string
            // Repeating log value records, with the first 2 bytes being the time in milliseconds, and the following byte or bytes being the logged data value
            // Length of the data value varies depending on the channel format XML (e.g. 1 byte for some, 2 bytes for others)

            // Read until we get nulls, indicating end of data
            // Then we scale the byte value using the min/max range from the channel format xml to get the real value

            int recordSizeInBytes = theLogChannel.OutputSize;
            int recordCount = 0;
            uint index = 0;
            byte[] timingBytes = new byte[2];

            while (index < allLogDataBytes.Length)
            {
                // Check if we're going to read beyond the buffer, or if there are 3 null bytes (indicating no more data in the block))
                if ((index + 2 >= allLogDataBytes.Length) || ((index > 0) && (allLogDataBytes[index] == 0x00 && allLogDataBytes[index + 1] == 0x00 && allLogDataBytes[index + 2] == 0x00)))
                {
                    break; // End of data block
                }

                // Read the first 2 bytes (time in milliseconds)
                Array.Copy(allLogDataBytes, index, timingBytes, 0, 2);
                uint timeValue = BitConverter.ToUInt16(timingBytes, 0);
                index += 2;

                // Use the channel format XML OuputSize to determine how many bytes to read for each data value
                byte[] dataBytes = new byte[recordSizeInBytes];
                Array.Copy(allLogDataBytes, index, dataBytes, 0, recordSizeInBytes);
                index += (uint)recordSizeInBytes;

                recordCount++;

                // Convert the data bytes to an integer value based on the channel format XML's 'size' attribute, then scale that to a real value
                switch (recordSizeInBytes)
                {
                    case 1:
                        byte logRawValueByte = dataBytes[0];
                        
                        // Call the callback method to process the log entry
                        logRecordProcessor.ProcessLogEntry(timeValue, logRawValueByte);
                        break;

                    default:  //assume 2 bytes unsigned
                        uint logRawValueUint16 = BitConverter.ToUInt16(dataBytes, 0);
                        
                        // Call the callback method to process the log entry
                        logRecordProcessor.ProcessLogEntry(timeValue, logRawValueUint16);
                        break;

                }

            }

            doLog(LogLevel.Info, $"  Processed {recordCount} records in {theLogChannel.OutputName}");

        }
    }

}