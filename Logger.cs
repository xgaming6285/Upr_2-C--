using System;
using System.IO;

namespace Upr_2
{
    // Simple static logger. Consider using Microsoft.Extensions.Logging for more robust scenarios.
    /// <summary>
    /// Provides static methods for logging messages to a file and the console.
    /// Creates a new log file with a timestamp on application startup.
    /// </summary>
    public static class Logger
    {
        // Stores the full path to the current log file. Readonly ensures it's set only once.
        private static readonly string logFilePath;
        // Used for thread synchronization to prevent multiple threads writing to the log file simultaneously.
        private static readonly object lockObject = new object();

        /// <summary>
        /// Static constructor. Executes once when the class is first accessed.
        /// Initializes the logger by setting up the log directory and file path.
        /// </summary>
        static Logger()
        {
            try
            {
                // Determine the directory for log files (e.g., "logs" subdirectory in the application's base directory).
                string logsDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory ?? ".", "logs");
                // Ensure the logs directory exists. Creates it if it doesn't.
                Directory.CreateDirectory(logsDirectory);

                // Construct the log file name using the current date and time.
                // Example: log_2023-10-27_10-30-00.txt
                logFilePath = Path.Combine(logsDirectory, $"log_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.txt");
                // Log an initial message indicating the logger is ready.
                Log("Logger initialized.");
            }
            catch (Exception ex)
            {
                // If any error occurs during initialization (e.g., permissions issue),
                // log the error to the console as a fallback.
                Console.WriteLine($"FATAL ERROR initializing logger: {ex.Message}");
                // Set logFilePath to empty to prevent attempts to write to a non-existent/inaccessible file.
                logFilePath = string.Empty; // Prevent further file write attempts if init failed
            }
        }

        /// <summary>
        /// Logs an informational message.
        /// </summary>
        /// <param name="message">The message to log.</param>
        public static void Log(string message)
        {
            // Prepends "[INFO]" tag and passes to the core writing method.
            WriteLog($"[INFO] {message}");
        }

        /// <summary>
        /// Logs a warning message.
        /// </summary>
        /// <param name="message">The warning message to log.</param>
        public static void LogWarning(string message)
        {
            // Prepends "[WARN]" tag and passes to the core writing method.
            WriteLog($"[WARN] {message}");
        }

        /// <summary>
        /// Logs an error message, optionally including exception details.
        /// </summary>
        /// <param name="message">The error message to log.</param>
        /// <param name="ex">Optional Exception object to include details like type, message, and stack trace.</param>
        public static void LogError(string message, Exception? ex = null)
        {
            // Start with the basic error message prepended with "[ERROR]".
            string errorMessage = $"[ERROR] {message}";
            // If an exception object is provided, append its details.
            if (ex != null)
            {
                errorMessage += $"{Environment.NewLine}Exception: {ex.GetType().Name}: {ex.Message}{Environment.NewLine}StackTrace: {ex.StackTrace}";
            }
            // Pass the complete error message to the core writing method.
            WriteLog(errorMessage);
        }

        /// <summary>
        /// The core private method responsible for writing log entries to the file.
        /// Handles thread safety and fallback to console logging on error.
        /// </summary>
        /// <param name="logEntry">The pre-formatted log entry (including level tag like [INFO], [WARN], [ERROR]).</param>
        private static void WriteLog(string logEntry)
        {
            // If logFilePath is empty (due to initialization failure), do nothing.
            if (string.IsNullOrEmpty(logFilePath)) return; // Don't try to write if init failed

            try
            {
                // Acquire a lock on the lockObject to ensure only one thread writes at a time.
                // This prevents file corruption or interleaved log messages.
                lock (lockObject)
                {
                    // Format the final log message with a timestamp.
                    string formattedMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {logEntry}";
                    // Append the formatted message and a newline to the log file.
                    File.AppendAllText(logFilePath, formattedMessage + Environment.NewLine);
                }
            }
            catch (Exception writeEx)
            {
                // If an error occurs during file writing (e.g., disk full, file locked unexpectedly),
                // log the error to the console.
                Console.WriteLine($"Error writing to log file: {writeEx.Message}");
                // Also output the original message that failed to be logged to the file.
                Console.WriteLine($"Original log message: {logEntry}");
            }
        }


        /// <summary>
        /// Gets the full path of the current log file.
        /// </summary>
        /// <returns>The log file path, or a message indicating it's unavailable.</returns>
        public static string GetCurrentLogFilePath()
        {
            // Return the path, or a default message if logFilePath is null or empty (e.g., due to init failure).
            return logFilePath ?? "Log file path not available.";
        }
    }
}