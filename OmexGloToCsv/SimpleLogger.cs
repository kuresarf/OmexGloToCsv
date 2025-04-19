using System;

namespace OmexGloToCsv
{
    public enum LogLevel
    {
        // Define the types of log records
        Unknown = 0,
        Error = 1,
        Warning = 2,
        Info = 3,
        Debug = 4
    }

    // Interface for callbacks when processing log records
    public interface ILogger
    {
        // Callback method to be called when logging a message 
        void Log(string message);
        void Log(LogLevel type, string message);
    }

    public class SimpleLogger : ILogger
    {
        private LogLevel logLevel;

        public SimpleLogger(LogLevel logLevel)
        {
            this.logLevel = logLevel;
        }

        public void Log(string message)
        {
            Console.WriteLine(message);
        }

        public void Log(LogLevel level, string message)
        {
            if (level <= logLevel)
            {
                Console.WriteLine($"[{level}] {message}");
            }
        }
    }
}
