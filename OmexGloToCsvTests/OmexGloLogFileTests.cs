namespace OmexGloToCsv.Tests
{
    [TestClass()]
    public class OmexGloLogFileTests
    {
        public TestContext TestContext { get; set; }

        [TestMethod()]
        [DeploymentItem(@"testdata\2025-03-30-21-25-01.glo")]
        public void LoadGloFile_CorrectlyIdentifySubFilesAndChannels()
        {
            string deploymentDirectory = TestContext.DeploymentDirectory;
            Console.WriteLine($"Deployment Directory: {deploymentDirectory}");

            // Check we have the test file deployed via the TestMethod's DeploymentItem attribute and the 'Copy To Output Directory' property in the solution
            string filePath = @"2025-03-30-21-25-01.glo";  // A small happy path .glo file 
            Assert.IsTrue(File.Exists(filePath), "Test file from the solution's testdata folder was not found");

            try
            {
                using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                using (var reader = new BinaryReader(fileStream))
                {
                    // Create the OmexGloLogFile, which will read the file and identify the log channels
                    OmexGloLogFile logFile = new OmexGloLogFile(reader, new SimpleLogger(LogLevel.Debug));

                    const int EXPECTED_SUBFILE_COUNT = 12;
                    const int EXPECTED_CHANNEL_COUNT = 5;

                    Assert.IsTrue(logFile.SubFiles.Count == EXPECTED_SUBFILE_COUNT, "Wrong number of subfiles found in the .glo file");
                    Assert.IsTrue(logFile.LogChannelParsers.Count == EXPECTED_CHANNEL_COUNT, "Wrong number of channels found in the .glo file");

                }
            }
            catch (Exception ex)
            {
                Assert.Fail($"{ex.Message}");
            }
        }

        [TestMethod()]
        [DeploymentItem(@"testdata\2025-03-30-21-25-01.glo")]
        public void LoadGloFile_CorrectlyLoadLogChannelProperties()
        {
            // Check we have the test file deployed via the TestMethod's DeploymentItem attribute and the 'Copy To Output Directory' property in the solution
            string filePath = @"2025-03-30-21-25-01.glo";  // A small happy path .glo file 
            Assert.IsTrue(File.Exists(filePath), "Test file from the solution's testdata folder was not found");

            try
            {
                using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                using (var reader = new BinaryReader(fileStream))
                {
                    // Create the OmexGloLogFile, which will read the file and identify the log channels
                    OmexGloLogFile logFile = new OmexGloLogFile(reader, new SimpleLogger(LogLevel.Debug));

                    foreach (OmexGloLogChannelReader logChannel in logFile.LogChannelParsers)
                    {
                        logChannel.ReadChannelFormatXml(reader);
                    }

                    // Check the channel properties
                    Assert.IsTrue(logFile.LogChannelParsers[0].LogChannel.OutputName == "Engine Load", "Wrong channel name");
                    Assert.IsTrue(logFile.LogChannelParsers[1].LogChannel.OutputName == "Oxygen raw1", "Wrong channel name");
                    Assert.IsTrue(logFile.LogChannelParsers[2].LogChannel.OutputName == "Throttle", "Wrong channel name");
                    Assert.IsTrue(logFile.LogChannelParsers[3].LogChannel.OutputName == "Engine Speed", "Wrong channel name");
                    Assert.IsTrue(logFile.LogChannelParsers[4].LogChannel.OutputName == "Lambda1", "Wrong channel name");
                }
            }
            catch (Exception ex)
            {
                Assert.Fail($"{ex.Message}");
            }
        }


        [TestMethod()]
        [DeploymentItem(@"testdata\2025-03-30-21-25-01.glo")]
        public void LoadGloFile_CorrectlyLoadLogChannelData()
        {
            // Check we have the test file deployed via the TestMethod's DeploymentItem attribute and the 'Copy To Output Directory' property in the solution
            string filePath = @"2025-03-30-21-25-01.glo";  // A small happy path .glo file 
            Assert.IsTrue(File.Exists(filePath), "Test file from the solution's testdata folder was not found");

            try
            {
                using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                using (var reader = new BinaryReader(fileStream))
                {
                    // Create the OmexGloLogFile, which will read the file and identify the log channels
                    OmexGloLogFile logFile = new OmexGloLogFile(reader, new SimpleLogger(LogLevel.Debug));

                    const int THROTTLE_CHANNEL_INDEX = 2;

                    OmexGloLogChannelReader logChannel = logFile.LogChannelParsers[THROTTLE_CHANNEL_INDEX];
                    logChannel.ReadChannelFormatXml(reader);
                    
                    // Check the channel properties
                    Assert.IsTrue(logChannel.LogChannel.OutputName == "Throttle", "Wrong channel name");

                    logChannel.LoadRawDataBlocks(reader);

                    int recordCount = 0;
                    var logRecordProcessorForTest = new LogRecordProcessorForTest
                    {
                        OnProcessLogRecord = (timeInMillis, logValueInBits) =>
                        {
                            recordCount++;  // count if we have all the records we expect
                        }
                    };

                    logChannel.ProcessLogDataWithCallback(logRecordProcessorForTest);

                    Assert.IsTrue(recordCount == 2248, "Wrong number of records for Throttle channel");


                }
            }
            catch (Exception ex)
            {
                Assert.Fail($"{ex.Message}");
            }
        }

        // Define a concrete implementation of the ILogRecordProcessor interface
        internal class LogRecordProcessorForTest : ILogRecordProcessor
        {
            public Action<uint, uint> OnProcessLogRecord { get; set; }

            public void ProcessLogEntry(uint timeInMillis, uint logValueInBits)
            {
                OnProcessLogRecord?.Invoke(timeInMillis, logValueInBits);
            }
        }
    }
}