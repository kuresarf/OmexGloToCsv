using System;

namespace OmexGloToCsv
{
    public class OmexGloSubFileDescriptor
    {
        private ILogger logger;

        public OmexGloSubFileDescriptor(byte[] subFileHeader, ILogger logger)
        {
            this.logger = logger;

            if (subFileHeader.Length < 0x2C)
            {
                logger.Log(LogLevel.Error, "Invalid subfile header length");
                throw new ArgumentException("Invalid subfile header length");
            }

            // Extract the location of the first data block from the header and add it as the first record in the list (there may be others later)
            uint intVal = extractDWORD(subFileHeader, 0);
            DataBlockAddresses = new List<uint>();
            DataBlockAddresses.Add(intVal);

            // Extract the size of the data blocks from the header in bytes (typically 4Kb blocks)
            intVal = extractDWORD(subFileHeader, 32);
            SubFileSize = (uint)intVal;

            // Extract subfile name length from the header
            // The subfile name length is stored in the 0x0B position of the header
            intVal = extractDWORD(subFileHeader, 40);
            SubFileNameLength = (int)intVal;

            SubFileName = "";
        }


        public int SubFileNameLength { get; set; }
        public string SubFileName { get; set; }
        public List<uint> DataBlockAddresses { get; set; }
        public uint SubFileSize { get; set; }


        private uint extractDWORD(byte[] rawBytes, int offset)
        {
            byte[] dwordBytes = new byte[4];
            Array.Copy(rawBytes, offset, dwordBytes, 0, 4);
            uint dwordValue = BitConverter.ToUInt32(dwordBytes, 0);
            return dwordValue;
        }
    }
}

