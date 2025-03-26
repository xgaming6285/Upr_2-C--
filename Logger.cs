using System;
using System.IO;

namespace Upr_2
{
    // Simple static logger. Consider using Microsoft.Extensions.Logging for more robust scenarios.
    public static class Logger
    {
        private static readonly string logFilePath;
        private static readonly object lockObject = new object();

        static Logger()
        {
            try
            {
                // Create logs directory if it doesn't exist
                string logsDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory ?? ".", "logs");
                Directory.CreateDirectory(logsDirectory);

                // Set log file path with timestamp
                logFilePath = Path.Combine(logsDirectory, $"log_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.txt");
                Log("Logger initialized.");
            }
            catch (Exception ex)
            {
                // Fallback to console if file logging fails during init
                Console.WriteLine($"FATAL ERROR initializing logger: {ex.Message}");
                logFilePath = string.Empty; // Prevent further file write attempts if init failed
            }
        }

        public static void Log(string message)
        {
            WriteLog($"[INFO] {message}");
        }

        public static void LogWarning(string message)
        {
             WriteLog($"[WARN] {message}");
        }

        public static void LogError(string message, Exception? ex = null)
        {
            string errorMessage = $"[ERROR] {message}";
            if (ex != null)
            {
                errorMessage += $"{Environment.NewLine}Exception: {ex.GetType().Name}: {ex.Message}{Environment.NewLine}StackTrace: {ex.StackTrace}";
            }
            WriteLog(errorMessage);
        }

        private static void WriteLog(string logEntry)
        {
             if (string.IsNullOrEmpty(logFilePath)) return; // Don't try to write if init failed

            try
            {
                lock (lockObject)
                {
                    string formattedMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {logEntry}";
                    File.AppendAllText(logFilePath, formattedMessage + Environment.NewLine);
                }
            }
            catch (Exception writeEx)
            {
                // Fallback to console if writing fails
                Console.WriteLine($"Error writing to log file: {writeEx.Message}");
                Console.WriteLine($"Original log message: {logEntry}");
            }
        }


        public static string GetCurrentLogFilePath()
        {
            return logFilePath ?? "Log file path not available.";
        }
    }
}