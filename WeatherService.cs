using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Text.Json;

namespace Upr_2
{
    public class WeatherInfo
    {
        public string City { get; set; }
        public double Temperature { get; set; }
        public string Description { get; set; }
        public double FeelsLike { get; set; }
        public int Humidity { get; set; }
        public double WindSpeed { get; set; }

        public WeatherInfo(string city, double temperature, string description, double feelsLike, int humidity, double windSpeed)
        {
            City = city;
            Temperature = temperature;
            Description = description;
            FeelsLike = feelsLike;
            Humidity = humidity;
            WindSpeed = windSpeed;
        }
    }

    public class WeatherService
    {
        private readonly HttpClient _client;
        private readonly string? _apiKey;

        public WeatherService(string? apiKey)
        {
            _client = new HttpClient();
            _apiKey = apiKey;
        }

        public async Task<WeatherInfo> GetWeatherAsync(string city)
        {
            if (string.IsNullOrEmpty(_apiKey))
            {
                throw new InvalidOperationException("API ключ не е предоставен за WeatherService");
            }

            try
            {
                var response = await _client.GetStringAsync(
                    $"http://api.openweathermap.org/data/2.5/weather?q={city}&appid={_apiKey}&units=metric");

                var jsonDoc = JsonDocument.Parse(response);
                var root = jsonDoc.RootElement;

                var main = root.GetProperty("main");
                var weather = root.GetProperty("weather")[0];
                var wind = root.GetProperty("wind");

                return new WeatherInfo(
                    city: city,
                    temperature: main.GetProperty("temp").GetDouble(),
                    description: weather.GetProperty("description").GetString() ?? "Unknown",
                    feelsLike: main.GetProperty("feels_like").GetDouble(),
                    humidity: main.GetProperty("humidity").GetInt32(),
                    windSpeed: wind.GetProperty("speed").GetDouble()
                );
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error fetching weather for {city}", ex);
                throw new Exception($"Failed to get weather information for {city}: {ex.Message}");
            }
        }
    }
}