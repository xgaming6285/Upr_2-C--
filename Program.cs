using Microsoft.Extensions.DependencyInjection; // Provides DI container functionality
using Microsoft.Extensions.Configuration; // Provides configuration handling (e.g., appsettings.json)
using Microsoft.Extensions.Hosting; // Provides generic host capabilities (DI, config, logging, lifetime)
using Upr_2.Services; // Namespace containing custom application services
using Upr_2.Settings; // Namespace containing application settings classes

namespace Upr_2
{
    class Program
    {
        // Main entry point of the application. Async Task allows await operations.
        static async Task Main(string[] args)
        {
            // Ensure console output supports Unicode characters (like Cyrillic)
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            // Set up configuration sources
            var configuration = new ConfigurationBuilder()
                // Set the base path for finding configuration files (e.g., appsettings.json)
                .SetBasePath(Directory.GetCurrentDirectory())
                // Add JSON configuration file. Optional=false means it must exist. ReloadOnChange=true reloads if file changes.
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                // Can also add environment variables, command line arguments, etc. here
                // .AddEnvironmentVariables()
                // .AddCommandLine(args)
                .Build(); // Build the configuration object

            // Attempt initial logging to capture startup and log file location
            try
            {
                Logger.Log("Application starting...");
                Logger.Log($"Log file: {Logger.GetCurrentLogFilePath()}");
            }
            catch (Exception ex)
            {
                // If logging fails early, output to console
                Console.WriteLine($"Failed to initialize logging: {ex.Message}");
            }

            // Set up the Generic Host for Dependency Injection, Configuration, Logging, and Application Lifetime
            var host = Host.CreateDefaultBuilder(args) // Creates a host with default settings (config, logging, etc.)
                .ConfigureAppConfiguration(builder =>
                 {
                     // Configuration is often loaded by CreateDefaultBuilder from appsettings.json, env vars, etc.
                     // We can add more sources or the pre-built 'configuration' object if needed.
                     // builder.AddConfiguration(configuration);
                 })
                // Configure services for the Dependency Injection container
                .ConfigureServices((context, services) => // 'context' provides access to HostBuilderContext, including Configuration
                {
                    // Configure the Options pattern for settings classes
                    // Binds sections from appsettings.json (or other config sources) to strongly-typed classes
                    services.Configure<ApiSettings>(context.Configuration.GetSection("ApiSettings"));
                    services.Configure<UrlSettings>(context.Configuration.GetSection("UrlSettings"));

                    // Register HttpClientFactory for efficient management of HttpClient instances
                    // Creates a default named client "DefaultClient"
                    services.AddHttpClient("DefaultClient", client =>
                    {
                        // Configure default settings for this client if needed
                        client.Timeout = TimeSpan.FromSeconds(30); // Set a default request timeout
                    });
                    // Creates a named client specifically for the IP API
                    services.AddHttpClient("IpApiClient", client =>
                   {
                       client.Timeout = TimeSpan.FromSeconds(15); // Use a potentially shorter timeout
                       // Base address and default headers (like User-Agent) can also be set here
                   });
                    // Register named clients for other services (configuration can be added later or within the service)
                    services.AddHttpClient("WeatherClient");
                    services.AddHttpClient("CurrencyClient");

                    // Register custom application services with the DI container
                    // Transient: A new instance is created every time it's requested
                    services.AddTransient<IpLookupService>();
                    services.AddTransient<TimeService>();
                    // Singleton: A single instance is created for the lifetime of the application
                    // Good for services holding state or expensive resources (use with caution regarding concurrency)
                    services.AddSingleton<NewsService>();
                    services.AddTransient<WeatherService>();
                    services.AddTransient<CurrencyService>();

                    // Register the main application logic class
                    services.AddTransient<AppRunner>();

                    // Optional: Integrate with Microsoft.Extensions.Logging framework
                    // This replaces the custom static Logger with a more standard logging approach
                    // services.AddLogging(configure => configure.AddConsole().AddDebug());

                })
                .Build(); // Build the configured host

            Logger.Log("Host built, services configured.");

            // Resolve the main application runner service from the DI container
            var appRunner = host.Services.GetRequiredService<AppRunner>();

            try
            {
                // Execute the main application logic asynchronously
                await appRunner.RunAsync();
            }
            catch (Exception ex)
            {
                // Catch any unhandled exceptions from the main application logic
                Logger.LogError("Unhandled exception in application root", ex);
                // Display a user-friendly error message in the console
                Console.WriteLine($"\n[КРИТИЧНА ГРЕШКА]: {ex.Message}");
                Console.WriteLine("Приложението ще се затвори. Проверете лог файла за повече детайли.");
                // Optional: Keep the console window open after a critical error to allow reading the message
                Console.WriteLine("\nНатиснете Enter за изход...");
                Console.ReadLine();
            }
            finally
            {
                // Log that the application is finishing
                Logger.Log("Application finished.");
                // The host typically handles resource cleanup on exit.
                // Explicit disposal might be needed in specific scenarios.
                // host.Dispose();
            }
        }
    }
}