using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Upr_2.Models;
using Upr_2.Services;
using Upr_2.Settings;

namespace Upr_2
{
    /// <summary>
    /// Main class responsible for running the console application,
    /// handling user interaction, and coordinating calls to various services.
    /// Uses dependency injection to get required services and settings.
    /// </summary>
    // Define the primary constructor with parameters
    public partial class AppRunner(
        IpLookupService ipLookupService, // Service for IP geolocation lookups
        TimeService timeService,         // Service for getting the current time
        NewsService newsService,         // Service for scraping and managing news articles
        WeatherService weatherService,   // Service for fetching weather information
        CurrencyService currencyService, // Service for currency conversion
        IOptions<ApiSettings> apiSettingsOptions) // Inject IOptions here to access configured API settings
    {
        // Define the partial method with the GeneratedRegex attribute
        // This utilizes source generators (C# 9+) for optimized regex compilation at build time.
        [GeneratedRegex(@"^(?:(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.){3}(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)$", RegexOptions.Compiled)]
        private static partial Regex Ipv4Regex();

        // Keep the field for ApiSettings, initialized from the primary constructor parameter
        private readonly ApiSettings _apiSettings = apiSettingsOptions.Value; // Get the actual settings object from IOptions

        /// <summary>
        /// Starts the main application loop, displaying the menu and handling user input.
        /// </summary>
        public async Task RunAsync()
        {
            // Ensure console supports UTF8 characters (e.g., for Bulgarian text, currency symbols)
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Logger.Log("Application runner started.");
            // Log the status of API keys for easier debugging
            Logger.Log($"Weather API Key status: {(_apiSettings.HasWeatherApiKey ? "Available" : "Not configured or invalid")}");
            Logger.Log($"Currency API Key status: {(_apiSettings.HasCurrencyApiKey ? "Available" : "Not configured or invalid")}");


            // Main application loop
            while (true)
            {
                PrintMainMenu(); // Display the main menu options
                string? choice = Console.ReadLine(); // Get user input
                Logger.Log($"User selected menu option: {choice}");

                // Process user choice
                // Use switch expression for conciseness if preferred (C# 8.0+)
                // Or keep standard switch
                switch (choice)
                {
                    case "1":
                        // Run the IP lookup feature
                        await RunIpLookupAsync(ipLookupService);
                        break;
                    case "2":
                        // Run the time check feature
                        await RunTimeCheckAsync(timeService);
                        break;
                    case "3":
                        // Run the news scraping feature
                        await RunNewsScraperAsync(newsService);
                        break;
                    case "4":
                        // Check if the Weather API key is available before running the feature
                        if (_apiSettings.HasWeatherApiKey)
                            await RunWeatherCheckAsync(weatherService);
                        else
                            DisplayApiNotAvailable("Weather"); // Inform user if key is missing
                        break;
                    case "5":
                        // Check if the Currency API key is available before running the feature
                        if (_apiSettings.HasCurrencyApiKey)
                            await RunCurrencyConverterAsync(currencyService);
                        else
                            DisplayApiNotAvailable("Currency"); // Inform user if key is missing
                        break;
                    case "6":
                        // Run the favorite news management feature
                        await ManageFavoriteNewsAsync(newsService);
                        break;
                    case "7":
                        // Exit the application
                        Logger.Log("Application shutting down by user request.");
                        Console.WriteLine("Изход...");
                        return; // Exit the loop and method
                    default:
                        // Handle invalid input
                        Logger.LogWarning($"Invalid menu choice entered: {choice}");
                        Console.WriteLine("Невалиден избор! Моля изберете отново.");
                        Pause(); // Wait for user acknowledgement
                        break;
                }
            }
        }

        /// <summary>
        /// Clears the console and prints the main menu options.
        /// Indicates if API-dependent features are unavailable due to missing keys.
        /// </summary>
        private void PrintMainMenu()
        {
            Console.Clear(); // Clear console for better readability
            Console.WriteLine("\n=== Главно меню ===");
            Console.WriteLine("1. Проверка на IP адрес");
            Console.WriteLine("2. Проверка на време в София");
            Console.WriteLine("3. Извличане на новини от Mediapool");
            // Display availability status next to features requiring API keys
            Console.WriteLine($"4. Проверка на времето {(!_apiSettings.HasWeatherApiKey ? "(API ключ не е наличен)" : "")}");
            Console.WriteLine($"5. Конвертор на валути {(!_apiSettings.HasCurrencyApiKey ? "(API ключ не е наличен)" : "")}");
            Console.WriteLine("6. Управление на любими новини");
            Console.WriteLine("7. Изход");
            Console.Write("\nИзберете опция (1-7): ");
        }

        /// <summary>
        /// Displays a message indicating that an API key is missing for a specific feature.
        /// </summary>
        /// <param name="featureName">The name of the feature that requires the API key.</param>
        private static void DisplayApiNotAvailable(string featureName)
        {
            Logger.LogWarning($"{featureName} feature selected but API key is missing.");
            Console.WriteLine($"\n[!] API ключ за '{featureName}' не е конфигуриран в appsettings.json.");
            Console.WriteLine("    Моля, добавете валиден API ключ и рестартирайте приложението, за да използвате тази функция.");
            Pause(); // Wait for user acknowledgement
        }


        /// <summary>
        /// Executes the time check operation using the provided TimeService.
        /// Displays the current time and date in Sofia.
        /// </summary>
        /// <param name="timeService">The service used to get time information.</param>
        private static async Task RunTimeCheckAsync(TimeService timeService)
        {
            Logger.Log("Time check operation started");
            Console.WriteLine("\nПроверка на времето в София...");

            try
            {
                // Asynchronously get the time information from the service
                var timeInfo = await timeService.GetSofiaTimeAsync();

                // Check if the service returned valid data
                if (timeInfo.HasValue)
                {
                    var (time, date) = timeInfo.Value; // Deconstruct the tuple
                    Console.WriteLine("\n=== Време в София ===");
                    Console.WriteLine($"Час: {time}");
                    Console.WriteLine($"Дата: {date}");
                    Logger.Log("Time check operation completed successfully.");
                }
                else
                {
                    // Inform user if data retrieval failed (details should be in logs)
                    Console.WriteLine("\nНеуспешно извличане на времето. Проверете лог файла за детайли.");
                    Logger.LogWarning("Time check operation failed or returned no data.");
                }
            }
            catch (Exception ex)
            {
                // Catch any unexpected exceptions during the operation
                // Logging is expected to be done within the service, but log here as well for context
                Console.WriteLine($"\nГрешка при извличане на времето: {ex.Message}");
                Logger.LogError("Exception caught in AppRunner during time check", ex);
            }
            Pause(); // Wait for user acknowledgement
        }

        /// <summary>
        /// Executes the IP lookup operation using the provided IpLookupService.
        /// Prompts the user for an IP address and displays location information.
        /// Loops until the user enters 'back'.
        /// </summary>
        /// <param name="ipLookupService">The service used for IP lookups.</param>
        private async Task RunIpLookupAsync(IpLookupService ipLookupService)
        {
            Logger.Log("IP lookup operation started");
            // Loop to allow multiple lookups without returning to the main menu
            while (true)
            {
                Console.Write("\nВъведете IPv4 адрес (или 'back' за връщане): ");
                string? input = Console.ReadLine()?.Trim(); // Read and trim user input

                // Check if the user wants to exit this feature
                if (string.IsNullOrEmpty(input) || string.Equals(input, "back", StringComparison.OrdinalIgnoreCase))
                {
                    Logger.Log("User exited IP lookup.");
                    break; // Exit the loop
                }

                // Validate the input format using the generated regex
                // Update the usage to call the generated method
                if (!Ipv4Regex().IsMatch(input))
                {
                    Logger.LogWarning($"Invalid IP address format entered: {input}");
                    Console.WriteLine("[!] Невалиден IPv4 адрес! Моля опитайте отново.");
                    continue; // Ask for input again
                }

                try
                {
                    Console.WriteLine("Изпращане на заявка...");
                    // Call the service to get location information for the IP
                    var locationInfo = await ipLookupService.GetLocationInfoAsync(input);
                    DisplayLocationInfo(locationInfo); // Display the results
                }
                // Handle specific exceptions that might occur during the API call
                catch (HttpRequestException httpEx)
                {
                    // Handle specific HTTP errors like 429 (Too Many Requests) or 404 (Not Found)
                    if (httpEx.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                    {
                        Console.WriteLine($"\n[!] Грешка: {httpEx.Message}"); // Message from service is expected to be user-friendly
                    }
                    else if (httpEx.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        Console.WriteLine($"\n[!] Грешка: Информация за IP адрес '{input}' не е намерена.");
                    }
                    else
                    {
                        // Generic network error message
                        Console.WriteLine($"\n[!] Грешка при мрежова заявка: {httpEx.Message}");
                    }
                    // Detailed logging is expected to be done in the service
                }
                catch (TimeoutException)
                {
                    // Handle request timeouts
                    Console.WriteLine("\n[!] Грешка: Заявката отне твърде дълго време (timeout).");
                    // Detailed logging is expected to be done in the service
                }
                catch (Exception ex)
                {
                    // Handle any other unexpected errors
                    Console.WriteLine($"\n[!] Възникна неочаквана грешка: {ex.Message}");
                    // Service should have logged details
                }
                // No Pause() here, the loop continues until the user enters 'back'
            }
            // No Pause() here, loop continues until 'back'
        }

        /// <summary>
        /// Displays the location information retrieved for an IP address.
        /// Handles cases where the info is null, indicates an error, or is for a reserved IP.
        /// Formats the output with colors for better readability.
        /// </summary>
        /// <param name="info">The IpInfo object containing location data, or null.</param>
        private void DisplayLocationInfo(IpInfo? info)
        {
            // Handle cases where the service failed to return data
            if (info == null)
            {
                Console.WriteLine("\nНеуспешно извличане на информация.");
                return;
            }
            // Handle cases where the API returned an error flag
            if (info.Error == true)
            {
                Console.WriteLine($"\nГрешка от API: {info.Reason ?? "Неизвестна грешка"}");
                return;
            }
            // Handle cases where the IP address is reserved (e.g., private network)
            if (info.Reserved == true)
            {
                Console.WriteLine($"\nIP адресът {info.Ip} е резервиран и няма публична информация за местоположение.");
                return;
            }


            Console.WriteLine("\n=== Информация за местоположението ===");
            Console.WriteLine("----------------------------------------");

            // Display primary information in Green
            Console.ForegroundColor = ConsoleColor.Green;
            PrintInfo("IP Адрес", info.Ip);
            PrintInfo("Държава", info.CountryName);
            PrintInfo("Град", info.City);
            PrintInfo("Регион", info.Region);
            PrintInfo("Пощенски код", info.Postal);
            Console.ResetColor(); // Reset color to default

            Console.WriteLine("\n--- Допълнителна информация ---");

            // Display secondary information in Magenta
            Console.ForegroundColor = ConsoleColor.Magenta;
            // Display coordinates and Google Maps link if available
            if (info.Latitude.HasValue && info.Longitude.HasValue)
            {
                Console.WriteLine($"Координати: {info.Latitude.Value:F5}, {info.Longitude.Value:F5}"); // Format coordinates
                Console.Write("Google Maps: ");
                Console.ForegroundColor = ConsoleColor.Yellow; // Use Yellow for the link
                Console.WriteLine(info.GoogleMapsLink);
                Console.ForegroundColor = ConsoleColor.Magenta; // Switch back to Magenta
            }
            PrintInfo("Часова зона", info.Timezone);
            PrintInfo("UTC отместване", info.UtcOffset);
            PrintInfo("ASN", info.Asn);
            PrintInfo("Организация", info.Org);
            PrintInfo("Валута", $"{info.Currency} ({info.CurrencyName})");
            PrintInfo("Езици", info.Languages);
            // Use nullable types correctly and format numbers with thousands separators
            PrintInfo("Площ на държава (km²)", info.CountryArea?.ToString("N0")); // Format number
            PrintInfo("Население", info.CountryPopulation?.ToString("N0")); // Format number
            Console.ResetColor(); // Reset color to default

            Console.WriteLine("----------------------------------------");
            // No Pause() here, let the IP lookup loop manage the flow
        }

        /// <summary>
        /// Helper method to print a label and value only if the value is not null or whitespace.
        /// </summary>
        /// <param name="label">The label to print.</param>
        /// <param name="value">The value to print.</param>
        private static void PrintInfo(string label, string? value)
        {
            // Avoid printing empty lines for missing data
            if (!string.IsNullOrWhiteSpace(value))
            {
                Console.WriteLine($"{label}: {value}");
            }
        }


        /// <summary>
        /// Executes the news scraping operation using the provided NewsService.
        /// Displays status messages and basic statistics about scraped articles.
        /// </summary>
        /// <param name="newsService">The service used for scraping news.</param>
        private static async Task RunNewsScraperAsync(NewsService newsService)
        {
            Logger.Log("News scraping operation started by user.");
            Console.WriteLine("\nИзвличане на новини от Mediapool...");
            Console.WriteLine("Това може да отнеме известно време...");

            try
            {
                // Asynchronously scrape news articles
                var articles = await newsService.ScrapeMediapoolNewsAsync();

                // Check if any articles were found
                if (articles.Count == 0)
                {
                    Console.WriteLine("\nНе са намерени новини или възникна грешка при извличането.");
                    Logger.LogWarning("News scraping returned 0 articles.");
                }
                else
                {
                    // Inform user about success and generated report
                    Console.WriteLine($"\nУспешно извлечени {articles.Count} новини.");
                    Console.WriteLine("Генериран е HTML отчет и е направен опит за отварянето му в браузъра.");
                    // Display basic statistics in the console
                    DisplayNewsStats(articles);
                }
                // Option to manage favorites is now part of the main menu or potentially HTML report interaction
            }
            catch (Exception ex)
            {
                // Handle any exceptions during scraping
                Console.WriteLine($"\n[!] Грешка при извличане на новини: {ex.Message}");
                // Detailed logging is expected to be done within NewsService
            }
            Pause(); // Wait for user acknowledgement
        }

        /// <summary>
        /// Displays statistics about the scraped news articles, grouped by category.
        /// </summary>
        /// <param name="articles">The list of scraped news articles.</param>
        private static void DisplayNewsStats(List<NewsArticle> articles)
        {
            // Ensure there are articles to process
            if (articles == null || !articles.Any()) return;

            // Use LINQ to group articles by category and count them
            var categoryStats = articles
                .GroupBy(a => a.Category) // Group by the Category property
                .Select(g => new
                { // Project each group into an anonymous type
                    Category = g.Key, // The category name
                    Count = g.Count(), // The number of articles in this category
                    Emoji = g.First().CategoryEmoji // Get the emoji from the first article in the group
                })
                .OrderByDescending(s => s.Count); // Order categories by count, descending

            Console.WriteLine("\n--- Статистика по категории ---");
            // Iterate through the grouped and ordered stats
            foreach (var stat in categoryStats)
            {
                Console.WriteLine($"{stat.Emoji} {stat.Category}: {stat.Count} новини");
            }
            Console.WriteLine("-----------------------------");
        }

        /// <summary>
        /// Executes the weather check operation using the provided WeatherService.
        /// Prompts the user for a city name and displays weather information.
        /// Loops until the user enters 'back'. Assumes API key availability is checked beforehand.
        /// </summary>
        /// <param name="weatherService">The service used for fetching weather data.</param>
        private static async Task RunWeatherCheckAsync(WeatherService weatherService)
        {
            // API Key availability check is done in RunAsync before calling this method
            Logger.Log("Weather check operation started.");

            // Loop to allow multiple weather checks
            while (true)
            {
                Console.Write("\nВъведете име на град (или 'back' за връщане): ");
                string? input = Console.ReadLine()?.Trim(); // Read and trim user input

                // Check if the user wants to exit
                if (string.IsNullOrEmpty(input) || string.Equals(input, "back", StringComparison.OrdinalIgnoreCase))
                {
                    Logger.Log("User exited weather check.");
                    break; // Exit the loop
                }

                try
                {
                    Console.WriteLine($"Търсене на времето за '{input}'...");
                    // Call the service to get weather information for the city
                    var weather = await weatherService.GetWeatherAsync(input);

                    // Display weather details if successful
                    if (weather != null)
                    {
                        Console.WriteLine("\n=== Времето ===");
                        Console.WriteLine($"Град: {weather.City}"); // Use the name returned by API for consistency
                        Console.WriteLine($"Температура: {weather.Temperature:F1}°C"); // Format temperature
                        Console.WriteLine($"Усеща се като: {weather.FeelsLike:F1}°C"); // Format feels-like temperature
                        Console.WriteLine($"Описание: {weather.Description}");
                        Console.WriteLine($"Влажност: {weather.Humidity}%");
                        Console.WriteLine($"Скорост на вятъра: {weather.WindSpeed} m/s");
                        Logger.Log($"Displayed weather for {weather.City}");
                    }
                    else
                    {
                        // Fallback message, though the service should ideally throw exceptions on failure
                        Console.WriteLine("\nНеуспешно извличане на времето.");
                    }
                }
                // Handle specific exceptions
                catch (HttpRequestException httpEx) when (httpEx.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    // Handle city not found error (404)
                    Console.WriteLine($"\n[!] Грешка: Град '{input}' не е намерен.");
                    Logger.LogWarning($"Weather lookup failed for city '{input}': Not Found (404)");
                }
                catch (HttpRequestException httpEx)
                {
                    // Handle other network errors
                    Console.WriteLine($"\n[!] Грешка при мрежова заявка: {httpEx.Message}");
                    // Service is expected to log details
                }
                catch (TimeoutException)
                {
                    // Handle request timeouts
                    Console.WriteLine("\n[!] Грешка: Заявката за времето отне твърде дълго.");
                    // Service is expected to log details
                }
                catch (InvalidOperationException opEx) // Catch API key issues if thrown by service (though checked earlier)
                {
                    Console.WriteLine($"\n[!] Грешка: {opEx.Message}");
                }
                catch (Exception ex)
                {
                    // Handle any other unexpected errors
                    Console.WriteLine($"\n[!] Възникна неочаквана грешка: {ex.Message}");
                    // Service is expected to log details
                }
                // No Pause() here, the loop manages the flow
            }
            // No Pause() here, loop manages flow
        }

        /// <summary>
        /// Executes the currency conversion operation using the provided CurrencyService.
        /// Fetches available currencies, then prompts the user for conversion details (from/to currency, amount).
        /// Loops until the user enters 'back'. Assumes API key availability is checked beforehand.
        /// </summary>
        /// <param name="currencyService">The service used for currency operations.</param>
        private async Task RunCurrencyConverterAsync(CurrencyService currencyService)
        {
            // API Key availability check is done in RunAsync before calling this method
            Logger.Log("Currency converter operation started.");
            Dictionary<string, string>? currencies = null; // To store available currencies

            // --- Fetch Available Currencies ---
            try
            {
                Console.WriteLine("\nИзвличане на налични валути...");
                // Get the list of supported currencies from the service
                currencies = await currencyService.GetAvailableCurrenciesAsync();

                // Check if fetching currencies was successful
                if (currencies == null || currencies.Count == 0)
                {
                    Console.WriteLine("[!] Неуспешно извличане на списъка с валути.");
                    Pause(); // Wait before returning to main menu
                    return; // Exit the feature if currencies cannot be loaded
                }

                // Optional: Display available currencies only once or on demand (e.g., via 'list' command)
                // Console.WriteLine("\n=== Налични валути ===");
                // foreach (var kvp in currencies.OrderBy(c => c.Key)) { Console.WriteLine($"{kvp.Key}: {kvp.Value}"); }

            }
            // Handle exceptions during currency list fetching
            catch (HttpRequestException httpEx) { Console.WriteLine($"\n[!] Грешка при извличане на валути: {httpEx.Message}"); Pause(); return; }
            catch (TimeoutException) { Console.WriteLine("\n[!] Грешка: Заявката за валути отне твърде дълго."); Pause(); return; }
            catch (InvalidOperationException opEx) { Console.WriteLine($"\n[!] Грешка: {opEx.Message}"); Pause(); return; } // e.g., API key issue
            catch (Exception ex) { Console.WriteLine($"\n[!] Неочаквана грешка при извличане на валути: {ex.Message}"); Pause(); return; }


            // --- Conversion Loop ---
            while (true)
            {
                Console.WriteLine("\n--- Конвертиране на Валута ---");
                Console.WriteLine("Пример: USD, EUR, BGN (въведете 'list' за списък, 'back' за изход)");

                // Get 'From' currency
                Console.Write("Въведете изходна валута: ");
                string? fromCurrency = Console.ReadLine()?.Trim().ToUpperInvariant(); // Normalize to uppercase

                if (string.IsNullOrEmpty(fromCurrency) || fromCurrency == "BACK") break; // Exit loop
                if (fromCurrency == "LIST") { DisplayCurrencies(currencies); continue; } // Show list and restart loop
                if (!currencies.ContainsKey(fromCurrency)) { Console.WriteLine("[!] Невалидна изходна валута!"); continue; } // Validate input

                // Get 'To' currency
                Console.Write("Въведете целева валута: ");
                string? toCurrency = Console.ReadLine()?.Trim().ToUpperInvariant(); // Normalize to uppercase

                if (string.IsNullOrEmpty(toCurrency) || toCurrency == "BACK") break; // Exit loop
                if (toCurrency == "LIST") { DisplayCurrencies(currencies); continue; } // Show list and restart loop
                if (!currencies.ContainsKey(toCurrency)) { Console.WriteLine("[!] Невалидна целева валута!"); continue; } // Validate input

                // Get amount
                Console.Write("Въведете сума: ");
                // Try parsing the amount, allowing for decimal points and commas, ensuring it's non-negative
                if (!double.TryParse(Console.ReadLine()?.Replace(',', '.'), // Replace comma with dot for invariant culture parsing
                                     System.Globalization.NumberStyles.Any, // Allow various number styles
                                     System.Globalization.CultureInfo.InvariantCulture, // Use invariant culture for consistency
                                     out double amount) || amount < 0)
                {
                    Console.WriteLine("[!] Невалидна сума!");
                    continue; // Ask for input again
                }

                // --- Perform Conversion ---
                try
                {
                    Console.WriteLine("Изчисляване...");
                    // Call the service to perform the currency conversion
                    var result = await currencyService.ConvertCurrencyAsync(fromCurrency, toCurrency, amount);

                    // Display the result if successful
                    if (result.HasValue)
                    {
                        var (rate, convertedAmount) = result.Value; // Deconstruct the result tuple
                        Console.ForegroundColor = ConsoleColor.Cyan; // Use color for emphasis
                        // Display the converted amount, formatted to 2 decimal places (N2)
                        Console.WriteLine($"\n>>> {amount} {fromCurrency} = {convertedAmount:N2} {toCurrency}");
                        Console.ResetColor();
                        // Display the exchange rate, formatted to 4 decimal places (F4)
                        Console.WriteLine($"    (Обменен курс: 1 {fromCurrency} = {rate:F4} {toCurrency})");
                        Logger.Log($"Currency conversion success: {amount} {fromCurrency} -> {convertedAmount:N2} {toCurrency}");
                    }
                    else
                    {
                        // Fallback message, though service should throw exceptions on failure
                        Console.WriteLine("[!] Неуспешно конвертиране.");
                    }
                }
                // Handle exceptions during conversion
                catch (HttpRequestException httpEx) { Console.WriteLine($"\n[!] Грешка при конвертиране: {httpEx.Message}"); }
                catch (TimeoutException) { Console.WriteLine("\n[!] Грешка: Заявката за конвертиране отне твърде дълго."); }
                catch (InvalidOperationException opEx) { Console.WriteLine($"\n[!] Грешка: {opEx.Message}"); } // Should not happen if API key checked above
                catch (Exception ex) { Console.WriteLine($"\n[!] Неочаквана грешка при конвертиране: {ex.Message}"); }
                // Loop continues for another conversion
            }
            // No Pause() here, the loop manages the flow until 'back' is entered
        }

        /// <summary>
        /// Displays the list of available currencies in a formatted multi-column layout.
        /// </summary>
        /// <param name="currencies">The dictionary of currency codes and names.</param>
        private static void DisplayCurrencies(Dictionary<string, string>? currencies)
        {
            // Check if the currency list is available
            if (currencies == null || currencies.Count == 0)
            {
                Console.WriteLine("\nСписъкът с валути не е наличен.");
                return;
            }
            Console.WriteLine("\n--- Налични Валути ---");
            int count = 0;
            // Iterate through currencies, ordered by code (Key)
            foreach (var kvp in currencies.OrderBy(c => c.Key))
            {
                // Print code (left-aligned in 5 chars) and name
                Console.Write($"{kvp.Key,-5} ({kvp.Value})  ");
                // Start a new line after every 3 currencies to create columns
                if (++count % 3 == 0) Console.WriteLine();
            }
            Console.WriteLine("\n------------------------");
            // No Pause here, called within the converter loop
        }


        /// <summary>
        /// Manages the sub-menu for favorite news articles.
        /// Allows viewing and removing favorites.
        /// </summary>
        /// <param name="newsService">The service used to access favorite articles.</param>
        private Task ManageFavoriteNewsAsync(NewsService newsService)
        {
            Logger.Log("Managing favorite news.");

            // Loop for the favorite management sub-menu
            while (true)
            {
                Console.WriteLine("\n=== Управление на любими новини ===");
                Console.WriteLine("1. Преглед на любими новини");
                Console.WriteLine("2. Премахване на новина от любими");
                Console.WriteLine("3. Назад"); // Option to return to the main menu
                Console.Write("\nИзберете опция (1-3): ");

                string? choice = Console.ReadLine();
                Logger.Log($"Favorite management choice: {choice}");

                // Process user choice for favorite management
                switch (choice)
                {
                    case "1":
                        DisplayFavorites(newsService); // Show the list of favorites
                        break;
                    case "2":
                        RemoveFavorite(newsService); // Allow removing a favorite
                        break;
                    case "3":
                        return Task.CompletedTask; // Exit this sub-menu and return to the main loop
                    default:
                        Console.WriteLine("Невалиден избор!");
                        break;
                }
                Pause(); // Pause after each action within this sub-menu
            }
        }

        /// <summary>
        /// Displays the list of currently saved favorite news articles.
        /// </summary>
        /// <param name="newsService">The service used to retrieve favorites.</param>
        private static void DisplayFavorites(NewsService newsService)
        {
            // Get a copy of the current favorite articles list from the service
            var favorites = newsService.GetFavoriteArticles();
            // Check if there are any favorites saved
            if (favorites.Count == 0)
            {
                Console.WriteLine("\nНямате запазени любими новини.");
                return;
            }

            Console.WriteLine("\n--- Любими новини ---");
            // Iterate through the favorites and display details
            for (int i = 0; i < favorites.Count; i++)
            {
                Console.WriteLine($"\n{i + 1}. {favorites[i].Title}"); // Display index (1-based) and title
                Console.WriteLine($"   Дата: {favorites[i].FormattedDate} {favorites[i].FormattedTime}"); // Display formatted date/time
                Console.WriteLine($"   URL: {favorites[i].Url}"); // Display URL
            }
            Console.WriteLine("----------------------");
        }

        /// <summary>
        /// Prompts the user to select a favorite article to remove and calls the service to remove it.
        /// </summary>
        /// <param name="newsService">The service used to manage favorites.</param>
        private static void RemoveFavorite(NewsService newsService)
        {
            // Get a copy of the current favorites to display for selection
            var favorites = newsService.GetFavoriteArticles();
            if (favorites.Count == 0)
            {
                Console.WriteLine("\nНямате запазени любими новини за премахване.");
                return;
            }

            Console.WriteLine("\nИзберете номер на новина за премахване (или 0 за отказ):");
            // Display the list of favorites with numbers for selection
            for (int i = 0; i < favorites.Count; i++)
            {
                Console.WriteLine($"{i + 1}. {favorites[i].Title}");
            }
            Console.Write("Номер: ");

            // Try parsing the user's input as an integer
            if (int.TryParse(Console.ReadLine(), out int index) && index > 0 && index <= favorites.Count)
            {
                // Valid index selected
                try
                {
                    // Get the article to remove from the *local copy* based on the 1-based index
                    var articleToRemove = favorites[index - 1];
                    // Pass this article object to the service to remove it from the *internal* list
                    newsService.RemoveFromFavorites(articleToRemove);
                    Console.WriteLine("\nНовината е премахната от любими.");
                    Logger.Log($"User removed article from favorites: {articleToRemove.Title}");
                }
                catch (Exception ex)
                {
                    // Handle potential errors during removal (e.g., if the article was somehow already removed)
                    Console.WriteLine($"\nГрешка при премахване: {ex.Message}");
                    Logger.LogError("Error during favorite removal", ex);
                }
            }
            else if (index == 0)
            {
                // User chose to cancel
                Console.WriteLine("\nОтказ.");
            }
            else
            {
                // Invalid number entered
                Console.WriteLine("\nНевалиден избор!");
            }
        }


        /// <summary>
        /// Helper method to pause execution and wait for the user to press Enter.
        /// Used to keep messages visible before clearing the console or proceeding.
        /// </summary>
        private static void Pause()
        {
            Console.WriteLine("\nНатиснете Enter за да продължите...");
            Console.ReadLine(); // Wait for user input (Enter key)
        }
    }
}