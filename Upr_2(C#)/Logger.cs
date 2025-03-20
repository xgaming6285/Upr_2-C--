using System;
using System.IO;

namespace Upr_2
{
    public class Logger
    {
        private static string logFilePath;
        private static readonly object lockObject = new object();

        static Logger()
        {
            // Create logs directory if it doesn't exist
            string logsDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
            Directory.CreateDirectory(logsDirectory);

            // Set log file path with timestamp
            logFilePath = Path.Combine(logsDirectory, $"log_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.txt");
        }

        public static void Log(string message)
        {
            try
            {
                lock (lockObject)
                {
                    string logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}";
                    File.AppendAllText(logFilePath, logMessage + Environment.NewLine);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error writing to log file: {ex.Message}");
            }
        }

        public static void LogError(string message, Exception ex)
        {
            string errorMessage = $"ERROR: {message}\nException: {ex.Message}\nStackTrace: {ex.StackTrace}";
            Log(errorMessage);
        }

        public static string GetCurrentLogFilePath()
        {
            return logFilePath;
        }
    }
}