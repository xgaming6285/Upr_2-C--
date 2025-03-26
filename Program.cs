using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting; // Use Hosting for better configuration and lifetime management
using System;
using System.IO;
using System.Threading.Tasks;
using Upr_2.Services;
using Upr_2.Settings;

namespace Upr_2
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // Use UTF8 for console output
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            // Set up configuration
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                // Add environment variables, command line args if needed
                .Build();

            // Add try-catch around initial logging
            try
            {
                Logger.Log("Application starting...");
                Logger.Log($"Log file: {Logger.GetCurrentLogFilePath()}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to initialize logging: {ex.Message}");
            }

            // Set up Dependency Injection using Generic Host
            // This provides configuration, logging, DI, and lifetime management
            var host = Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration(builder =>
                 {
                     // Configuration is already built, but can add more sources here if needed
                     // builder.AddConfiguration(configuration); // Can add the pre-built one
                 })
                .ConfigureServices((context, services) => // context.Configuration is available here
                {
                    // Configure Options pattern
                    services.Configure<ApiSettings>(context.Configuration.GetSection("ApiSettings"));
                    services.Configure<UrlSettings>(context.Configuration.GetSection("UrlSettings"));

                    // Register HttpClientFactory
                    services.AddHttpClient("DefaultClient", client =>
                    {
                        // Configure default client if needed (e.g., timeout)
                        client.Timeout = TimeSpan.FromSeconds(30);
                    });
                    services.AddHttpClient("IpApiClient", client =>
                   {
                       client.Timeout = TimeSpan.FromSeconds(15); // Shorter timeout for IP?
                                                                  // Base address and User-Agent can be set here or in service
                   });
                    services.AddHttpClient("WeatherClient"); // Configure as needed
                    services.AddHttpClient("CurrencyClient"); // Configure as needed

                    // Register custom services (use Singleton or Scoped based on need, Transient usually fine for services)
                    services.AddTransient<IpLookupService>();
                    services.AddTransient<TimeService>();
                    services.AddSingleton<NewsService>(); // Singleton might be better if it holds state like favorites
                    services.AddTransient<WeatherService>();
                    services.AddTransient<CurrencyService>();

                    // Register the main application runner
                    services.AddTransient<AppRunner>();

                    // Optional: Register Microsoft Logging if you want to replace the static Logger
                    // services.AddLogging(configure => configure.AddConsole().AddDebug());

                })
                .Build(); // Build the host

            Logger.Log("Host built, services configured.");

            // Get the AppRunner service and run the application
            var appRunner = host.Services.GetRequiredService<AppRunner>();

            try
            {
                await appRunner.RunAsync();
            }
            catch (Exception ex)
            {
                Logger.LogError("Unhandled exception in application root", ex);
                Console.WriteLine($"\n[КРИТИЧНА ГРЕШКА]: {ex.Message}");
                Console.WriteLine("Приложението ще се затвори. Проверете лог файла за повече детайли.");
                // Optional: Keep console open to see the error
                Console.WriteLine("\nНатиснете Enter за изход...");
                Console.ReadLine();
            }
            finally
            {
                Logger.Log("Application finished.");
                // Optional: Dispose host if needed, though usually handles itself on exit
                // host.Dispose();
            }
        }
    }
}