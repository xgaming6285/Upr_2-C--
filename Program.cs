using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Text.Json;
using HtmlAgilityPack;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Upr_2;

namespace Upr_2
{
    class Program
    {
        private static readonly HttpClient client = new HttpClient();
        private static readonly Regex ipv4Regex = new Regex(@"^(?:(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.){3}(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)$");
        private static DateTime lastRequestTime = DateTime.MinValue;
        private static readonly string? weatherApiKey = null; // Optional: Enter your API key inside " " ... Set to null if no API key available
        private static readonly string? currencyApiKey = null; // Optional: Enter your API key inside " " ... Set to null if no API key available

        private static bool HasWeatherApiKey => !string.IsNullOrEmpty(weatherApiKey);
        private static bool HasCurrencyApiKey => !string.IsNullOrEmpty(currencyApiKey);

        static async Task Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Logger.Log("Application started");

            // Log API key status
            Logger.Log($"Weather API Key status: {(HasWeatherApiKey ? "Available" : "Not available")}");
            Logger.Log($"Currency API Key status: {(HasCurrencyApiKey ? "Available" : "Not available")}");

            while (true)
            {
                Console.WriteLine("\n=== Главно меню ===");
                Console.WriteLine("1. Проверка на IP адрес");
                Console.WriteLine("2. Проверка на време в София");
                Console.WriteLine("3. Извличане на новини от Mediapool");
                Console.WriteLine($"4. Проверка на времето {(HasWeatherApiKey ? "" : "(API ключ не е наличен)")}");
                Console.WriteLine($"5. Конвертор на валути {(HasCurrencyApiKey ? "" : "(API ключ не е наличен)")}");
                Console.WriteLine("6. Управление на любими новини");
                Console.WriteLine("7. Изход");
                Console.Write("\nИзберете опция (1-7): ");

                string? choice = Console.ReadLine();
                Logger.Log($"User selected menu option: {choice}");

                switch (choice)
                {
                    case "1":
                        await RunIpLookup();
                        break;
                    case "2":
                        await RunTimeCheck();
                        break;
                    case "3":
                        await RunNewsScraper();
                        break;
                    case "4":
                        await RunWeatherCheck();
                        break;
                    case "5":
                        await RunCurrencyConverter();
                        break;
                    case "6":
                        ManageFavoriteNews();
                        break;
                    case "7":
                        Logger.Log("Application shutting down");
                        return;
                    default:
                        Logger.Log($"Invalid menu choice entered: {choice}");
                        Console.WriteLine("Невалиден избор! Моля изберете отново.");
                        break;
                }
            }
        }

        private static async Task RunTimeCheck()
        {
            Logger.Log("Time check operation started");
            var timeService = new TimeService();
            Console.WriteLine("\nПроверка на времето в София...");
            var (time, date) = await timeService.GetSofiaTimeAsync();

            Console.WriteLine("\n=== Време в София ===");
            Console.WriteLine($"Час: {time}");
            Console.WriteLine($"Дата: {date}");
            Logger.Log("Time check operation completed");
            Console.WriteLine("\nНатиснете Enter за да продължите...");
            Console.ReadLine();
        }

        private static async Task RunIpLookup()
        {
            Logger.Log("IP lookup operation started");
            while (true)
            {
                Console.Write("\nВъведете IPv4 адрес (или 'back' за връщане в менюто): ");
                string? input = Console.ReadLine();

                if (input == null || string.Equals(input, "back", StringComparison.OrdinalIgnoreCase))
                {
                    Logger.Log("User exited IP lookup operation");
                    break;
                }

                if (!ipv4Regex.IsMatch(input))
                {
                    Logger.Log($"Invalid IP address entered: {input}");
                    Console.WriteLine("Невалиден IPv4 адрес! Моля опитайте отново.");
                    continue;
                }

                try
                {
                    Logger.Log($"Processing IP lookup for address: {input}");
                    var timeSinceLastRequest = DateTime.Now - lastRequestTime;
                    if (timeSinceLastRequest.TotalSeconds < 5)
                    {
                        var waitTime = TimeSpan.FromSeconds(5) - timeSinceLastRequest;
                        Logger.Log($"Rate limit reached, waiting for {waitTime.TotalSeconds:F1} seconds");
                        await Task.Delay(waitTime);
                    }

                    var locationInfo = await GetLocationInfoFromIpApi(input);
                    DisplayLocationInfo(locationInfo);
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Error during IP lookup for {input}", ex);
                    Console.WriteLine($"Грешка при заявката: {ex.Message}");
                }
            }
        }

        private static async Task<JsonDocument?> GetLocationInfoFromIpApi(string ip)
        {
            try
            {
                Logger.Log($"Sending API request for IP: {ip}");
                Console.WriteLine("Изпращане на заявка към API...");
                string response = await client.GetStringAsync($"http://ip-api.com/json/{ip}?fields=status,message,country,countryCode,region,regionName,city,zip,lat,lon,timezone,isp,org,as,query");

                lastRequestTime = DateTime.Now;
                Logger.Log("API request completed successfully");
                return JsonDocument.Parse(response);
            }
            catch (Exception ex)
            {
                Logger.LogError($"API request failed for IP: {ip}", ex);
                Console.WriteLine($"Грешка от API: {ex.Message}");
                return null;
            }
        }

        private static void DisplayLocationInfo(JsonDocument? jsonDoc)
        {
            if (jsonDoc == null)
            {
                Console.WriteLine("Няма налична информация.");
                return;
            }

            var root = jsonDoc.RootElement;

            if (!root.TryGetProperty("status", out var status) || status.GetString() != "success")
            {
                string errorMessage = root.TryGetProperty("message", out var message)
                    ? message.GetString() ?? "Неизвестна грешка"
                    : "Неуспешна заявка";
                Console.WriteLine($"Грешка: {errorMessage}");
                return;
            }

            Console.WriteLine("\n=== Информация за местоположението ===");
            Console.WriteLine("----------------------------------------");

            // Main information (displayed in green)
            Console.ForegroundColor = ConsoleColor.Green;
            PrintProperty(root, "IP Адрес", "query");
            PrintProperty(root, "Държава", "country");
            PrintProperty(root, "Град", "city");
            PrintProperty(root, "Регион", "regionName");
            PrintProperty(root, "Пощенски код", "zip");
            Console.ResetColor(); // Reset to default color

            Console.WriteLine("\n--- Допълнителна информация ---");

            // Secondary information (displayed in purple)
            Console.ForegroundColor = ConsoleColor.Magenta;
            // Geographic coordinates
            if (root.TryGetProperty("lat", out var lat) && root.TryGetProperty("lon", out var lon))
            {
                Console.WriteLine($"Координати: {lat.GetDouble().ToString("F5", System.Globalization.CultureInfo.InvariantCulture)}, {lon.GetDouble().ToString("F5", System.Globalization.CultureInfo.InvariantCulture)}");
                string mapsLink = $"https://www.google.com/maps/search/?api=1&query={lat.GetDouble().ToString("F5", System.Globalization.CultureInfo.InvariantCulture)},{lon.GetDouble().ToString("F5", System.Globalization.CultureInfo.InvariantCulture)}";
                Console.Write("Google Maps линк: ");
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine(mapsLink);
                Console.ForegroundColor = ConsoleColor.Magenta;
            }

            // Network information
            PrintProperty(root, "Часова зона", "timezone");
            PrintProperty(root, "Интернет доставчик", "isp");
            PrintProperty(root, "Организация", "org");
            PrintProperty(root, "AS номер/име", "as");
            Console.ResetColor(); // Reset to default color

            Console.WriteLine("----------------------------------------");
        }

        private static void PrintProperty(JsonElement root, string label, string property)
        {
            if (root.TryGetProperty(property, out var value) && !string.IsNullOrEmpty(value.GetString()))
            {
                Console.WriteLine($"{label}: {value.GetString()}");
            }
        }

        private static async Task RunNewsScraper()
        {
            Logger.Log("News scraping operation started");
            var newsService = new NewsService();

            while (true)
            {
                Console.WriteLine("\n=== Извличане на новини ===");
                Console.WriteLine("1. Преглед на новини");
                Console.WriteLine("2. Управление на любими новини");
                Console.WriteLine("3. Назад към главното меню");
                Console.Write("\nИзберете опция (1-3): ");

                string? choice = Console.ReadLine();

                switch (choice)
                {
                    case "1":
                        await DisplayNews(newsService);
                        break;

                    case "2":
                        ManageFavoriteNews();
                        break;

                    case "3":
                        return;

                    default:
                        Console.WriteLine("Невалиден избор! Моля изберете отново.");
                        break;
                }
            }
        }

        private static async Task DisplayNews(NewsService newsService)
        {
            try
            {
                Console.WriteLine("\nИзвличане на новини от Mediapool...");
                var articles = await newsService.ScrapeMediapoolNews();

                if (articles.Count == 0)
                {
                    Console.WriteLine("Не са намерени новини.");
                    return;
                }

                // Show statistics
                var categoryStats = articles.GroupBy(a => a.Category)
                                          .OrderByDescending(g => g.Count());

                Console.WriteLine("\n=== Статистика на новините ===");
                foreach (var stat in categoryStats)
                {
                    var emoji = articles.First(a => a.Category == stat.Key).CategoryEmoji;
                    Console.WriteLine($"{emoji} {stat.Key}: {stat.Count()} новини");
                }
                Console.WriteLine("============================\n");

                // Group articles by category
                var groupedArticles = articles.GroupBy(a => a.Category).OrderBy(g => g.Key);

                foreach (var group in groupedArticles)
                {
                    Console.WriteLine($"\n=== {group.First().CategoryEmoji} {group.Key} ===");
                    foreach (var article in group.OrderByDescending(article => $"{article.FormattedDate} {article.FormattedTime}"))
                    {
                        Console.ForegroundColor = article.IsFavorite ? ConsoleColor.Yellow : ConsoleColor.White;
                        Console.WriteLine($"{article.Title}");
                        Console.WriteLine($"Време: {article.FormattedTime}");
                        Console.WriteLine($"Линк: {article.Url}");
                        Console.WriteLine($"Брой думи: {article.WordCount}");
                        Console.WriteLine("----------------------------------------");
                        Console.ResetColor();
                    }
                }

                // Manage favorites
                Console.WriteLine("\nИскате ли да добавите/премахнете новина от любими? (да/не)");
                if (Console.ReadLine()?.ToLower() == "да")
                {
                    ManageFavoriteNews();
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Error displaying news", ex);
                Console.WriteLine($"Грешка при показване на новините: {ex.Message}");
            }
        }

        private static async Task RunWeatherCheck()
        {
            Logger.Log("Weather check operation started");
            if (!HasWeatherApiKey)
            {
                Console.WriteLine("\nAPI ключ не е предоставен. За да използвате тази функция, моля добавете валиден API ключ.");
                Console.WriteLine("Натиснете Enter за да продължите...");
                Console.ReadLine();
                return;
            }

            var weatherService = new WeatherService(weatherApiKey);

            while (true)
            {
                Console.Write("\nВъведете име на град (или 'back' за връщане в менюто): ");
                string? input = Console.ReadLine();

                if (string.IsNullOrEmpty(input) || input.ToLower() == "back")
                {
                    break;
                }

                try
                {
                    var weather = await weatherService.GetWeatherAsync(input);
                    Console.WriteLine("\n=== Времето ===");
                    Console.WriteLine($"Град: {weather.City}");
                    Console.WriteLine($"Температура: {weather.Temperature:F1}°C");
                    Console.WriteLine($"Усеща се като: {weather.FeelsLike:F1}°C");
                    Console.WriteLine($"Описание: {weather.Description}");
                    Console.WriteLine($"Влажност: {weather.Humidity}%");
                    Console.WriteLine($"Скорост на вятъра: {weather.WindSpeed} m/s");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Грешка: {ex.Message}");
                }
            }
        }

        private static async Task RunCurrencyConverter()
        {
            Logger.Log("Currency converter operation started");
            if (!HasCurrencyApiKey)
            {
                Console.WriteLine("\nAPI ключ не е предоставен. За да използвате тази функция, моля добавете валиден API ключ.");
                Console.WriteLine("Натиснете Enter за да продължите...");
                Console.ReadLine();
                return;
            }

            var currencyService = new CurrencyService(currencyApiKey);

            try
            {
                var currencies = await currencyService.GetAvailableCurrenciesAsync();

                Console.WriteLine("\n=== Налични валути ===");
                foreach (var currency in currencies)
                {
                    Console.WriteLine($"{currency.Key}: {currency.Value}");
                }

                while (true)
                {
                    Console.Write("\nВъведете изходна валута (или 'back' за връщане в менюто): ");
                    string? fromCurrency = Console.ReadLine()?.ToUpper();

                    if (string.IsNullOrEmpty(fromCurrency) || fromCurrency == "BACK")
                    {
                        break;
                    }

                    if (fromCurrency == null || !currencies.ContainsKey(fromCurrency))
                    {
                        Console.WriteLine("Невалидна валута!");
                        continue;
                    }

                    Console.Write("Въведете целева валута: ");
                    string? toCurrency = Console.ReadLine()?.ToUpper();

                    if (toCurrency == null || !currencies.ContainsKey(toCurrency))
                    {
                        Console.WriteLine("Невалидна валута!");
                        continue;
                    }

                    Console.Write("Въведете сума: ");
                    if (!double.TryParse(Console.ReadLine(), out double amount))
                    {
                        Console.WriteLine("Невалидна сума!");
                        continue;
                    }

                    var (rate, convertedAmount) = await currencyService.ConvertCurrencyAsync(fromCurrency, toCurrency, amount);
                    Console.WriteLine($"\nРезултат: {amount} {fromCurrency} = {convertedAmount:F2} {toCurrency}");
                    Console.WriteLine($"Обменен курс: 1 {fromCurrency} = {rate:F4} {toCurrency}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Грешка: {ex.Message}");
            }
        }

        private static void ManageFavoriteNews()
        {
            var newsService = new NewsService();

            while (true)
            {
                Console.WriteLine("\n=== Управление на любими новини ===");
                Console.WriteLine("1. Преглед на любими новини");
                Console.WriteLine("2. Премахване на новина от любими");
                Console.WriteLine("3. Назад към главното меню");
                Console.Write("\nИзберете опция (1-3): ");

                string? choice = Console.ReadLine();

                switch (choice)
                {
                    case "1":
                        var favorites = newsService.GetFavoriteArticles();
                        if (favorites.Count == 0)
                        {
                            Console.WriteLine("\nНямате запазени любими новини.");
                            continue;
                        }

                        Console.WriteLine("\n=== Любими новини ===");
                        for (int i = 0; i < favorites.Count; i++)
                        {
                            Console.WriteLine($"\n{i + 1}. {favorites[i].Title}");
                            Console.WriteLine($"   Дата: {favorites[i].FormattedDate} {favorites[i].FormattedTime}");
                            Console.WriteLine($"   URL: {favorites[i].Url}");
                        }
                        break;

                    case "2":
                        var articlesToRemove = newsService.GetFavoriteArticles();
                        if (articlesToRemove.Count == 0)
                        {
                            Console.WriteLine("\nНямате запазени любими новини.");
                            continue;
                        }

                        Console.WriteLine("\nИзберете номер на новина за премахване:");
                        for (int i = 0; i < articlesToRemove.Count; i++)
                        {
                            Console.WriteLine($"{i + 1}. {articlesToRemove[i].Title}");
                        }

                        if (int.TryParse(Console.ReadLine(), out int index) &&
                            index > 0 && index <= articlesToRemove.Count)
                        {
                            var articleToRemove = articlesToRemove[index - 1];
                            newsService.RemoveFromFavorites(articleToRemove);
                            Console.WriteLine("Новината е премахната от любими.");
                        }
                        else
                        {
                            Console.WriteLine("Невалиден избор!");
                        }
                        break;

                    case "3":
                        return;

                    default:
                        Console.WriteLine("Невалиден избор! Моля изберете отново.");
                        break;
                }
            }
        }
    }
}