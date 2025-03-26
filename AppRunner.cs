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
    public class AppRunner
    {
        // Use static readonly for regex if it's constant
        private static readonly Regex Ipv4Regex = new Regex(@"^(?:(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.){3}(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)$", RegexOptions.Compiled);

        private readonly IpLookupService _ipLookupService;
        private readonly TimeService _timeService;
        private readonly NewsService _newsService;
        private readonly WeatherService _weatherService;
        private readonly CurrencyService _currencyService;
        private readonly ApiSettings _apiSettings; // To check key availability

        // Inject services via constructor
        public AppRunner(
            IpLookupService ipLookupService,
            TimeService timeService,
            NewsService newsService,
            WeatherService weatherService,
            CurrencyService currencyService,
            IOptions<ApiSettings> apiSettingsOptions) // Inject IOptions
        {
            _ipLookupService = ipLookupService;
            _timeService = timeService;
            _newsService = newsService;
            _weatherService = weatherService;
            _currencyService = currencyService;
            _apiSettings = apiSettingsOptions.Value; // Get the settings value
        }

        public async Task RunAsync()
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Logger.Log("Application runner started.");
            Logger.Log($"Weather API Key status: {(_apiSettings.HasWeatherApiKey ? "Available" : "Not configured or invalid")}");
            Logger.Log($"Currency API Key status: {(_apiSettings.HasCurrencyApiKey ? "Available" : "Not configured or invalid")}");


            while (true)
            {
                PrintMainMenu();
                string? choice = Console.ReadLine();
                Logger.Log($"User selected menu option: {choice}");

                // Use switch expression for conciseness if preferred (C# 8.0+)
                 // Or keep standard switch
                 switch (choice)
                {
                    case "1":
                        await RunIpLookupAsync();
                        break;
                    case "2":
                        await RunTimeCheckAsync();
                        break;
                    case "3":
                        await RunNewsScraperAsync();
                        break;
                    case "4":
                         if (_apiSettings.HasWeatherApiKey)
                             await RunWeatherCheckAsync();
                         else
                             DisplayApiNotAvailable("Weather");
                        break;
                    case "5":
                        if (_apiSettings.HasCurrencyApiKey)
                            await RunCurrencyConverterAsync();
                        else
                            DisplayApiNotAvailable("Currency");
                        break;
                    case "6":
                        await ManageFavoriteNewsAsync(); // Make async if needed later
                        break;
                    case "7":
                        Logger.Log("Application shutting down by user request.");
                        Console.WriteLine("Изход...");
                        return; // Exit the loop and method
                    default:
                        Logger.LogWarning($"Invalid menu choice entered: {choice}");
                        Console.WriteLine("Невалиден избор! Моля изберете отново.");
                        Pause();
                        break;
                }
            }
        }

         private void PrintMainMenu()
        {
             Console.Clear(); // Clear console for better readability
             Console.WriteLine("\n=== Главно меню ===");
             Console.WriteLine("1. Проверка на IP адрес");
             Console.WriteLine("2. Проверка на време в София");
             Console.WriteLine("3. Извличане на новини от Mediapool");
             Console.WriteLine($"4. Проверка на времето {(!_apiSettings.HasWeatherApiKey ? "(API ключ не е наличен)" : "")}");
             Console.WriteLine($"5. Конвертор на валути {(!_apiSettings.HasCurrencyApiKey ? "(API ключ не е наличен)" : "")}");
             Console.WriteLine("6. Управление на любими новини");
             Console.WriteLine("7. Изход");
             Console.Write("\nИзберете опция (1-7): ");
        }

        private void DisplayApiNotAvailable(string featureName)
        {
             Logger.LogWarning($"{featureName} feature selected but API key is missing.");
             Console.WriteLine($"\n[!] API ключ за '{featureName}' не е конфигуриран в appsettings.json.");
             Console.WriteLine("    Моля, добавете валиден API ключ и рестартирайте приложението, за да използвате тази функция.");
             Pause();
        }


        private async Task RunTimeCheckAsync()
        {
            Logger.Log("Time check operation started");
            Console.WriteLine("\nПроверка на времето в София...");

            try
            {
                var timeInfo = await _timeService.GetSofiaTimeAsync();

                 if (timeInfo.HasValue)
                 {
                    var (time, date) = timeInfo.Value;
                    Console.WriteLine("\n=== Време в София ===");
                    Console.WriteLine($"Час: {time}");
                    Console.WriteLine($"Дата: {date}");
                    Logger.Log("Time check operation completed successfully.");
                 }
                 else
                 {
                      Console.WriteLine("\nНеуспешно извличане на времето. Проверете лог файла за детайли.");
                      Logger.LogWarning("Time check operation failed or returned no data.");
                 }
            }
            catch (Exception ex)
            {
                 // Logging is done in the service, just inform user
                 Console.WriteLine($"\nГрешка при извличане на времето: {ex.Message}");
                 Logger.LogError("Exception caught in AppRunner during time check", ex);
            }
            Pause();
        }

        private async Task RunIpLookupAsync()
        {
            Logger.Log("IP lookup operation started");
            while (true)
            {
                Console.Write("\nВъведете IPv4 адрес (или 'back' за връщане): ");
                string? input = Console.ReadLine()?.Trim();

                if (string.IsNullOrEmpty(input) || string.Equals(input, "back", StringComparison.OrdinalIgnoreCase))
                {
                    Logger.Log("User exited IP lookup.");
                    break;
                }

                if (!Ipv4Regex.IsMatch(input))
                {
                    Logger.LogWarning($"Invalid IP address format entered: {input}");
                    Console.WriteLine("[!] Невалиден IPv4 адрес! Моля опитайте отново.");
                    continue;
                }

                try
                {
                    Console.WriteLine("Изпращане на заявка...");
                    var locationInfo = await _ipLookupService.GetLocationInfoAsync(input);
                    DisplayLocationInfo(locationInfo); // Separate display logic
                }
                catch (HttpRequestException httpEx)
                {
                    // Handle specific HTTP errors like 429 or 404 if needed
                     if (httpEx.StatusCode == System.Net.HttpStatusCode.TooManyRequests) {
                         Console.WriteLine($"\n[!] Грешка: {httpEx.Message}"); // Message from service is user-friendly
                     } else if (httpEx.StatusCode == System.Net.HttpStatusCode.NotFound) {
                         Console.WriteLine($"\n[!] Грешка: Информация за IP адрес '{input}' не е намерена.");
                     }
                     else {
                         Console.WriteLine($"\n[!] Грешка при мрежова заявка: {httpEx.Message}");
                     }
                     // Logging done in service
                }
                catch (TimeoutException) {
                     Console.WriteLine("\n[!] Грешка: Заявката отне твърде дълго време (timeout).");
                      // Logging done in service
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"\n[!] Възникна неочаквана грешка: {ex.Message}");
                    // Service should have logged details
                }
            }
             // No Pause() here, loop continues until 'back'
        }

         private void DisplayLocationInfo(IpInfo? info)
        {
            if (info == null)
            {
                Console.WriteLine("\nНеуспешно извличане на информация.");
                return;
            }
            if (info.Error == true)
            {
                 Console.WriteLine($"\nГрешка от API: {info.Reason ?? "Неизвестна грешка"}");
                 return;
            }
            if (info.Reserved == true)
            {
                Console.WriteLine($"\nIP адресът {info.Ip} е резервиран и няма публична информация за местоположение.");
                return;
            }


            Console.WriteLine("\n=== Информация за местоположението ===");
            Console.WriteLine("----------------------------------------");

            // Main info - Green
            Console.ForegroundColor = ConsoleColor.Green;
            PrintInfo("IP Адрес", info.Ip);
            PrintInfo("Държава", info.CountryName);
            PrintInfo("Град", info.City);
            PrintInfo("Регион", info.Region);
            PrintInfo("Пощенски код", info.Postal);
            Console.ResetColor();

            Console.WriteLine("\n--- Допълнителна информация ---");

            // Secondary info - Magenta
            Console.ForegroundColor = ConsoleColor.Magenta;
             if (info.Latitude.HasValue && info.Longitude.HasValue)
            {
                Console.WriteLine($"Координати: {info.Latitude.Value:F5}, {info.Longitude.Value:F5}");
                Console.Write("Google Maps: ");
                Console.ForegroundColor = ConsoleColor.Yellow; // Link color
                Console.WriteLine(info.GoogleMapsLink);
                Console.ForegroundColor = ConsoleColor.Magenta; // Back to Magenta
            }
            PrintInfo("Часова зона", info.Timezone);
            PrintInfo("UTC отместване", info.UtcOffset);
            PrintInfo("ASN", info.Asn);
            PrintInfo("Организация", info.Org);
            PrintInfo("Валута", $"{info.Currency} ({info.CurrencyName})");
            PrintInfo("Езици", info.Languages);
            // Use nullable types correctly and format numbers
            PrintInfo("Площ на държава (km²)", info.CountryArea?.ToString("N0")); // Format number
            PrintInfo("Население", info.CountryPopulation?.ToString("N0")); // Format number
            Console.ResetColor();

            Console.WriteLine("----------------------------------------");
            // No Pause() here, let the IP loop manage flow
        }

        // Helper to print only if value is not empty
        private void PrintInfo(string label, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                Console.WriteLine($"{label}: {value}");
            }
        }


        private async Task RunNewsScraperAsync()
        {
            Logger.Log("News scraping operation started by user.");
            Console.WriteLine("\nИзвличане на новини от Mediapool...");
            Console.WriteLine("Това може да отнеме известно време...");

            try
            {
                var articles = await _newsService.ScrapeMediapoolNewsAsync();

                if (articles.Count == 0)
                {
                    Console.WriteLine("\nНе са намерени новини или възникна грешка при извличането.");
                    Logger.LogWarning("News scraping returned 0 articles.");
                }
                else
                {
                    Console.WriteLine($"\nУспешно извлечени {articles.Count} новини.");
                    Console.WriteLine("Генериран е HTML отчет и е направен опит за отварянето му в браузъра.");
                    // Display basic stats in console
                    DisplayNewsStats(articles);
                }
                 // Option to manage favorites is now part of the main menu or HTML report interaction
            }
            catch (Exception ex)
            {
                 Console.WriteLine($"\n[!] Грешка при извличане на новини: {ex.Message}");
                 // Logging should be done within NewsService
            }
            Pause();
        }

        private void DisplayNewsStats(List<NewsArticle> articles)
        {
             if (articles == null || !articles.Any()) return;

            var categoryStats = articles
                .GroupBy(a => a.Category)
                .Select(g => new { Category = g.Key, Count = g.Count(), Emoji = g.First().CategoryEmoji })
                .OrderByDescending(s => s.Count);

            Console.WriteLine("\n--- Статистика по категории ---");
            foreach (var stat in categoryStats)
            {
                Console.WriteLine($"{stat.Emoji} {stat.Category}: {stat.Count} новини");
            }
            Console.WriteLine("-----------------------------");
        }

        private async Task RunWeatherCheckAsync()
        {
             // API Key check is done before calling this method
            Logger.Log("Weather check operation started.");

            while (true)
            {
                Console.Write("\nВъведете име на град (или 'back' за връщане): ");
                string? input = Console.ReadLine()?.Trim();

                if (string.IsNullOrEmpty(input) || string.Equals(input, "back", StringComparison.OrdinalIgnoreCase))
                {
                    Logger.Log("User exited weather check.");
                    break;
                }

                try
                {
                    Console.WriteLine($"Търсене на времето за '{input}'...");
                    var weather = await _weatherService.GetWeatherAsync(input);

                    if (weather != null) {
                         Console.WriteLine("\n=== Времето ===");
                         Console.WriteLine($"Град: {weather.City}"); // Use the name returned by API
                         Console.WriteLine($"Температура: {weather.Temperature:F1}°C");
                         Console.WriteLine($"Усеща се като: {weather.FeelsLike:F1}°C");
                         Console.WriteLine($"Описание: {weather.Description}");
                         Console.WriteLine($"Влажност: {weather.Humidity}%");
                         Console.WriteLine($"Скорост на вятъра: {weather.WindSpeed} m/s");
                         Logger.Log($"Displayed weather for {weather.City}");
                    } else {
                         // Should not happen if service throws exceptions, but as fallback
                         Console.WriteLine("\nНеуспешно извличане на времето.");
                    }
                }
                 catch (HttpRequestException httpEx) when (httpEx.StatusCode == System.Net.HttpStatusCode.NotFound)
                 {
                     Console.WriteLine($"\n[!] Грешка: Град '{input}' не е намерен.");
                     Logger.LogWarning($"Weather lookup failed for city '{input}': Not Found (404)");
                 }
                 catch (HttpRequestException httpEx)
                 {
                     Console.WriteLine($"\n[!] Грешка при мрежова заявка: {httpEx.Message}");
                     // Service logs details
                 }
                 catch (TimeoutException) {
                     Console.WriteLine("\n[!] Грешка: Заявката за времето отне твърде дълго.");
                     // Service logs details
                 }
                 catch (InvalidOperationException opEx) // Catch API key not available if thrown by service
                 {
                      Console.WriteLine($"\n[!] Грешка: {opEx.Message}");
                 }
                catch (Exception ex)
                {
                    Console.WriteLine($"\n[!] Възникна неочаквана грешка: {ex.Message}");
                    // Service logs details
                }
            }
            // No Pause() here, loop manages flow
        }

         private async Task RunCurrencyConverterAsync()
        {
             // API Key check is done before calling this method
             Logger.Log("Currency converter operation started.");
             Dictionary<string, string>? currencies = null;

             try
             {
                 Console.WriteLine("\nИзвличане на налични валути...");
                 currencies = await _currencyService.GetAvailableCurrenciesAsync();

                 if (currencies == null || currencies.Count == 0)
                 {
                     Console.WriteLine("[!] Неуспешно извличане на списъка с валути.");
                     Pause();
                     return;
                 }

                 // Optional: Display available currencies only once or on demand
                 // Console.WriteLine("\n=== Налични валути ===");
                 // foreach (var kvp in currencies.OrderBy(c => c.Key)) { Console.WriteLine($"{kvp.Key}: {kvp.Value}"); }

             }
             catch (HttpRequestException httpEx) { Console.WriteLine($"\n[!] Грешка при извличане на валути: {httpEx.Message}"); Pause(); return; }
             catch (TimeoutException) { Console.WriteLine("\n[!] Грешка: Заявката за валути отне твърде дълго."); Pause(); return; }
             catch (InvalidOperationException opEx) { Console.WriteLine($"\n[!] Грешка: {opEx.Message}"); Pause(); return; }
             catch (Exception ex) { Console.WriteLine($"\n[!] Неочаквана грешка при извличане на валути: {ex.Message}"); Pause(); return; }


             // Conversion Loop
            while (true)
            {
                Console.WriteLine("\n--- Конвертиране на Валута ---");
                Console.WriteLine("Пример: USD, EUR, BGN (въведете 'list' за списък, 'back' за изход)");

                Console.Write("Въведете изходна валута: ");
                string? fromCurrency = Console.ReadLine()?.Trim().ToUpperInvariant();

                 if (string.IsNullOrEmpty(fromCurrency) || fromCurrency == "BACK") break;
                 if (fromCurrency == "LIST") { DisplayCurrencies(currencies); continue; }
                 if (!currencies.ContainsKey(fromCurrency)) { Console.WriteLine("[!] Невалидна изходна валута!"); continue; }

                 Console.Write("Въведете целева валута: ");
                 string? toCurrency = Console.ReadLine()?.Trim().ToUpperInvariant();

                 if (string.IsNullOrEmpty(toCurrency) || toCurrency == "BACK") break;
                 if (toCurrency == "LIST") { DisplayCurrencies(currencies); continue; }
                 if (!currencies.ContainsKey(toCurrency)) { Console.WriteLine("[!] Невалидна целева валута!"); continue; }

                 Console.Write("Въведете сума: ");
                 if (!double.TryParse(Console.ReadLine()?.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double amount) || amount < 0)
                 {
                     Console.WriteLine("[!] Невалидна сума!");
                     continue;
                 }

                 // Perform Conversion
                 try
                 {
                     Console.WriteLine("Изчисляване...");
                     var result = await _currencyService.ConvertCurrencyAsync(fromCurrency, toCurrency, amount);

                     if (result.HasValue)
                     {
                         var (rate, convertedAmount) = result.Value;
                         Console.ForegroundColor = ConsoleColor.Cyan;
                         Console.WriteLine($"\n>>> {amount} {fromCurrency} = {convertedAmount:N2} {toCurrency}"); // N2 format for currency
                         Console.ResetColor();
                         Console.WriteLine($"    (Обменен курс: 1 {fromCurrency} = {rate:F4} {toCurrency})"); // F4 for rate
                         Logger.Log($"Currency conversion success: {amount} {fromCurrency} -> {convertedAmount:N2} {toCurrency}");
                     }
                     else {
                          // Should not happen if service throws exceptions
                          Console.WriteLine("[!] Неуспешно конвертиране.");
                     }
                 }
                 catch (HttpRequestException httpEx) { Console.WriteLine($"\n[!] Грешка при конвертиране: {httpEx.Message}"); }
                 catch (TimeoutException) { Console.WriteLine("\n[!] Грешка: Заявката за конвертиране отне твърде дълго."); }
                 catch (InvalidOperationException opEx) { Console.WriteLine($"\n[!] Грешка: {opEx.Message}"); } // Should not happen here if checked above
                 catch (Exception ex) { Console.WriteLine($"\n[!] Неочаквана грешка при конвертиране: {ex.Message}"); }
             }
              // No Pause() here, loop manages flow
        }

         private void DisplayCurrencies(Dictionary<string, string>? currencies) {
             if (currencies == null || currencies.Count == 0) {
                 Console.WriteLine("\nСписъкът с валути не е наличен.");
                 return;
             }
             Console.WriteLine("\n--- Налични Валути ---");
             int count = 0;
             foreach (var kvp in currencies.OrderBy(c => c.Key)) {
                 Console.Write($"{kvp.Key,-5} ({kvp.Value})  ");
                 if (++count % 3 == 0) Console.WriteLine(); // 3 columns
             }
             Console.WriteLine("\n------------------------");
         }


        // Keep ManageFavoriteNews synchronous for now, or make async if needed
        private Task ManageFavoriteNewsAsync()
        {
            Logger.Log("Managing favorite news.");

            while (true)
            {
                Console.WriteLine("\n=== Управление на любими новини ===");
                Console.WriteLine("1. Преглед на любими новини");
                Console.WriteLine("2. Премахване на новина от любими");
                Console.WriteLine("3. Назад");
                Console.Write("\nИзберете опция (1-3): ");

                string? choice = Console.ReadLine();
                 Logger.Log($"Favorite management choice: {choice}");

                switch (choice)
                {
                    case "1":
                        DisplayFavorites();
                        break;
                    case "2":
                        RemoveFavorite();
                        break;
                    case "3":
                        return Task.CompletedTask; // Exit management
                    default:
                        Console.WriteLine("Невалиден избор!");
                        break;
                }
                Pause(); // Pause after each action in this sub-menu
            }
        }

        private void DisplayFavorites()
        {
             var favorites = _newsService.GetFavoriteArticles(); // Gets a copy
             if (favorites.Count == 0)
             {
                 Console.WriteLine("\nНямате запазени любими новини.");
                 return;
             }

             Console.WriteLine("\n--- Любими новини ---");
             for (int i = 0; i < favorites.Count; i++)
             {
                 Console.WriteLine($"\n{i + 1}. {favorites[i].Title}");
                 Console.WriteLine($"   Дата: {favorites[i].FormattedDate} {favorites[i].FormattedTime}");
                 Console.WriteLine($"   URL: {favorites[i].Url}");
             }
             Console.WriteLine("----------------------");
        }

         private void RemoveFavorite()
         {
              var favorites = _newsService.GetFavoriteArticles(); // Gets a copy
              if (favorites.Count == 0)
              {
                  Console.WriteLine("\nНямате запазени любими новини за премахване.");
                  return;
              }

              Console.WriteLine("\nИзберете номер на новина за премахване (или 0 за отказ):");
              for (int i = 0; i < favorites.Count; i++)
              {
                  Console.WriteLine($"{i + 1}. {favorites[i].Title}");
              }
              Console.Write("Номер: ");

              if (int.TryParse(Console.ReadLine(), out int index) && index > 0 && index <= favorites.Count)
              {
                  try
                  {
                     var articleToRemove = favorites[index - 1]; // Get from the *copy*
                     _newsService.RemoveFromFavorites(articleToRemove); // Pass to service to remove from *internal* list
                     Console.WriteLine("\nНовината е премахната от любими.");
                     Logger.Log($"User removed article from favorites: {articleToRemove.Title}");
                  }
                  catch(Exception ex)
                  {
                       Console.WriteLine($"\nГрешка при премахване: {ex.Message}");
                       Logger.LogError("Error during favorite removal", ex);
                  }
              }
              else if (index == 0)
              {
                  Console.WriteLine("\nОтказ.");
              }
              else
              {
                  Console.WriteLine("\nНевалиден избор!");
              }
         }


        // Helper for pausing execution
        private void Pause()
        {
            Console.WriteLine("\nНатиснете Enter за да продължите...");
            Console.ReadLine();
        }
    }
}