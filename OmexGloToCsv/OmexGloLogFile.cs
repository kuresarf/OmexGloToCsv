using System;
using System.Text;

namespace OmexGloToCsv
{
    public class OmexGloLogFile
    {
        private BinaryReader reader;        
        private List<OmexGloSubFileDescriptor> subFiles = null;
        private List<OmexGloLogChannelReader> logChannelParsers = null;
        private ILogger logger;
        private string logNotes = "";

        public OmexGloLogFile(BinaryReader reader, ILogger logger)
        {
            this.reader = reader;
            this.logger = logger;

            IdentifyLogFileContents();
        }

        public List<OmexGloSubFileDescriptor> SubFiles
        {
            get { return subFiles; }            
        }

        public List<OmexGloLogChannelReader> LogChannelParsers
        {
            get { return logChannelParsers; }
        }

        public string LogNotes
        {
            get { return logNotes; }
        }

        private void doLog(LogLevel level, string logString)
        {
            if (logger != null)
            {
                logger.Log(level, logString);
            }            
        }

        /*
         * This method loads nformation about the log channels available in the .glo file.
         * It reads the compound file header, subfile descriptors, and identifies log channels.
         * It does not load the log data itself, just the channel information.
         */
        private void IdentifyLogFileContents()
        {
            ReadGemsCompoundFileHeader(reader);

            IdentifySubFiles(reader);

            IdentifyLogChannels(reader);
        }


        private void ReadGemsCompoundFileHeader(BinaryReader reader)
        {
            // Seek to the start of the stream, so we definitely start from the beginning
            reader.BaseStream.Seek(0, SeekOrigin.Begin);    

            // Read initial header
            byte[] header = reader.ReadBytes(0x24);

            // Read file type header "GEMS COMPOUND FILE v01.00"
            byte[] fileTypeHeader = reader.ReadBytes(25);
            if (fileTypeHeader.Length < 25)
            {
                doLog(LogLevel.Error, "Error: File is not a valid .glo");
                return;
            }
            doLog(LogLevel.Debug, "File Type: " + Encoding.Default.GetString(fileTypeHeader));

            reader.ReadBytes(1); // Skip 1 byte (0x1A)
        }


        private void IdentifyLogChannels(BinaryReader reader)
        {
            logChannelParsers = new List<OmexGloLogChannelReader>();

            foreach (var subFile in subFiles)
            {
                doLog(LogLevel.Debug, $"Processing {subFile.SubFileName}");
                BuildSubFileDataBlockChain(reader, subFile);

                if (subFile.SubFileName.Equals("ecu_info", StringComparison.CurrentCultureIgnoreCase))
                {
                    // read ECU info
                    doLog(LogLevel.Debug, $"  Found ecu_info");
                }
                else if (subFile.SubFileName.Equals("notes", StringComparison.CurrentCultureIgnoreCase))
                {
                    // read notes
                    doLog(LogLevel.Debug, $"  Found notes");
                    logNotes = ReadNotes(reader, subFile);
                }
                else if (subFile.SubFileName.Equals("markers.glm", StringComparison.CurrentCultureIgnoreCase))
                {
                    // read markers
                    doLog(LogLevel.Debug, $"  Found markers");
                }
                else if (subFile.SubFileName.EndsWith(".gcf", StringComparison.CurrentCultureIgnoreCase))
                {
                    // A .gcf subfile describes the format of a log channel 
                    doLog(LogLevel.Debug, $"  Searching for log data linked to {subFile.SubFileName}");

                    Boolean found = false;

                    // Find the matching *.gcd subfile containing the log data and then create a logChannel object
                    var logDataFilename = subFile.SubFileName.Replace(".gcf", ".gcd");
                    for (int i = 0; i < subFiles.Count; i++)
                    {
                        if (subFiles[i].SubFileName.Equals(logDataFilename, StringComparison.CurrentCultureIgnoreCase))
                        {
                            doLog(LogLevel.Debug, $"  Found log channel and log data subfile: {subFiles[i].SubFileName}");

                            OmexGloLogChannelReader logChannel = new OmexGloLogChannelReader(subFile, subFiles[i], logger);
                            logChannelParsers.Add(logChannel);
                            found = true;

                            break;
                        }
                    }

                    if (!found)
                    {
                        doLog(LogLevel.Error, $"Error: No matching log data subfile found for {subFile.SubFileName}, discarding log channel");
                        continue;
                    }

                }
                else if (subFile.SubFileName.EndsWith(".gcd", StringComparison.CurrentCultureIgnoreCase))
                {
                    // No need to to anything, should have been processed when we found the matching .gcf file
                    //Log($"Reading log data in {subFileDesc.SubFileName}");
                    continue;
                }
                else
                {
                    doLog(LogLevel.Warning, $"Unknown subfile type: {subFile.SubFileName}");
                    continue;
                }
            }
        }


        private String ReadNotes(BinaryReader reader, OmexGloSubFileDescriptor notesSubfile)
        {
            string notesString = "";

            // Seek to the start of the subfile's first data chunk, plus 8 bytes to skip the 2 DWORD header
            reader.BaseStream.Seek(notesSubfile.DataBlockAddresses[0] + 8, SeekOrigin.Begin);            

            // Read DWORD containing length of the XML string in widechars (i.e. 2 * length in bytes)
            byte[] dwordBytes = reader.ReadBytes(4);
            uint xmlLength = BitConverter.ToUInt32(dwordBytes, 0) * 2;

            // Read the Notes string
            byte[] notesBytes = reader.ReadBytes((int)xmlLength);
            if (notesBytes.Length == xmlLength)
            {
                notesString = Encoding.Default.GetString(notesBytes);
                // convert widechar string to normal string
                notesString = notesString.Replace("\0", ""); // remove null characters
                notesString = notesString.Replace("\r", ""); // remove carriage returns
                notesString = notesString.Replace("\n", ""); // remove new lines
                notesString = notesString.Replace("\t", ""); // remove tabs
                notesString = notesString.Replace("  ", ""); // remove double spaces

                doLog(LogLevel.Debug, "    Notes: " + notesString);
            }
            else
            {
                doLog(LogLevel.Error, "    Error: invalid notes string length");
            }

            return notesString;
        }


        private void IdentifySubFiles(BinaryReader reader)
        {
            long originalPosition;

            subFiles = new List<OmexGloSubFileDescriptor>();

            doLog(LogLevel.Info, $"Identifying subfiles");

            // Loop around the subfile records
            while (reader.BaseStream.Position < reader.BaseStream.Length)
            {
                originalPosition = reader.BaseStream.Position;

                // Read subfile record header
                byte[] subFileHeader = reader.ReadBytes(0x2C);
                if (subFileHeader.Length < 0x2C)
                {
                    doLog(LogLevel.Error, "Error: invalid .glo file - subfile record not found");
                    break;
                }

                // Create a new subfile descriptor object, parsing some details from the header bytes
                var subFileDesc = new OmexGloSubFileDescriptor(subFileHeader, logger);

                // if the subfile descriptor is valid, continue processing it
                if ((subFileDesc.DataBlockAddresses.Count > 0) && (subFileDesc.DataBlockAddresses[0] > 0))
                {
                    // Read subfile name
                    byte[] subFileNameBytes = reader.ReadBytes(subFileDesc.SubFileNameLength);
                    subFileDesc.SubFileName = Encoding.Default.GetString(subFileNameBytes);

                    subFiles.Add(subFileDesc);

                    doLog(LogLevel.Debug, $"Subfile Name: {subFileDesc.SubFileName}");
                }
                else
                {
                    // Skip back, we read too far (subfile data offset is 0)
                    reader.BaseStream.Seek(originalPosition, SeekOrigin.Begin);
                    doLog(LogLevel.Debug, "No more subfile descriptors found.");
                    break;
                }

            }
        }


        private void BuildSubFileDataBlockChain(BinaryReader reader, OmexGloSubFileDescriptor subFileDesc)
        {
            // Follow the offset address and check for a chain of subfile data blocks
            uint offset = subFileDesc.DataBlockAddresses[0];

            string hexOffset = offset.ToString("X8");
            doLog(LogLevel.Debug, "    Data block at: " + offset + " (hex 0x" + hexOffset + ")");

            while (offset != 0)
            {
                // Check if there is another offset address identifying another block in the chain
                reader.BaseStream.Seek(offset, SeekOrigin.Begin);
                byte[] dwordBytes = reader.ReadBytes(4);
                // Check if the data is valid
                if (dwordBytes.Length == 4)
                {
                    offset = BitConverter.ToUInt32(dwordBytes, 0);
                    if (offset != 0)
                    {
                        //convert offset to hex
                        hexOffset = offset.ToString("X8");
                        doLog(LogLevel.Debug, "    Data block at: " + offset + " (hex 0x" + hexOffset + ")");

                        subFileDesc.DataBlockAddresses.Add(offset);  // store the next offset address
                    }
                }
                else
                {
                    doLog(LogLevel.Error, $"Error: Invalid subfile data for {subFileDesc.SubFileName}");
                    offset = 0;
                }
            }

            doLog(LogLevel.Debug, $"  Found {subFileDesc.DataBlockAddresses.Count} blocks");
        }

    }
}




