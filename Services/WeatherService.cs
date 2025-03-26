using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Upr_2.Settings;

namespace Upr_2.Services
{
    // Strongly-typed models for OpenWeatherMap response
    public class WeatherMain
    {
        [JsonPropertyName("temp")]
        public double Temp { get; set; }

        [JsonPropertyName("feels_like")]
        public double FeelsLike { get; set; }

        [JsonPropertyName("humidity")]
        public int Humidity { get; set; }
    }

    public class WeatherDescription
    {
        [JsonPropertyName("description")]
        public string? Description { get; set; }

         // Add icon if you want it later
         //[JsonPropertyName("icon")]
         //public string Icon { get; set; }
    }

    public class WeatherWind
    {
        [JsonPropertyName("speed")]
        public double Speed { get; set; }
    }

    public class WeatherApiResponse
    {
        [JsonPropertyName("weather")]
        public List<WeatherDescription>? Weather { get; set; }

        [JsonPropertyName("main")]
        public WeatherMain? Main { get; set; }

        [JsonPropertyName("wind")]
        public WeatherWind? Wind { get; set; }

        [JsonPropertyName("name")]
        public string? CityName { get; set; } // API returns the standardized city name

         // Error handling properties if relevant for this API
        [JsonPropertyName("cod")] // e.g., "404" for not found
        public object? Cod { get; set; } // Can be string or int

         [JsonPropertyName("message")] // Error message
         public string? Message { get; set; }

    }

    // Simplified WeatherInfo for internal use/display
     public class WeatherInfo
    {
        public string City { get; set; }
        public double Temperature { get; set; }
        public string Description { get; set; }
        public double FeelsLike { get; set; }
        public int Humidity { get; set; }
        public double WindSpeed { get; set; }

         // Private constructor, use factory method or direct initialization
         public WeatherInfo(string city, double temp, string desc, double feels, int hum, double wind)
         {
             City = city; Temperature = temp; Description = desc; FeelsLike = feels; Humidity = hum; WindSpeed = wind;
         }
    }


    public class WeatherService
    {
        private readonly HttpClient _httpClient;
        private readonly ApiSettings _apiSettings;
        private readonly UrlSettings _urlSettings;

        public WeatherService(
            IHttpClientFactory httpClientFactory,
            IOptions<ApiSettings> apiSettingsOptions,
            IOptions<UrlSettings> urlSettingsOptions)
        {
            _httpClient = httpClientFactory.CreateClient("WeatherClient"); // Specific client
            _apiSettings = apiSettingsOptions.Value;
            _urlSettings = urlSettingsOptions.Value;
        }

        public async Task<WeatherInfo?> GetWeatherAsync(string city)
        {
            if (!_apiSettings.HasWeatherApiKey)
            {
                 // Log or handle internally, throwing might be too disruptive if key is optional
                 Logger.LogWarning("Weather API key is not configured or invalid.");
                 throw new InvalidOperationException("Weather API key is not available.");
            }
             if (string.IsNullOrEmpty(_urlSettings.WeatherApiBaseUrl))
            {
                Logger.LogError("WeatherApiBaseUrl is not configured.");
                throw new InvalidOperationException("Weather API base URL is not configured.");
            }

            string apiKey = _apiSettings.WeatherApiKey!; // Not null here due to HasWeatherApiKey check
            string requestUrl = $"{_urlSettings.WeatherApiBaseUrl.TrimEnd('/')}/weather?q={Uri.EscapeDataString(city)}&appid={apiKey}&units=metric&lang=bg"; // Added lang=bg

            Logger.Log($"Requesting weather for {city} from {requestUrl}");

            try
            {
                 var response = await _httpClient.GetAsync(requestUrl).ConfigureAwait(false);

                 if (response.IsSuccessStatusCode)
                 {
                     var apiResponse = await response.Content.ReadFromJsonAsync<WeatherApiResponse>().ConfigureAwait(false);

                     // Basic validation of the response structure
                     if (apiResponse?.Main == null || apiResponse.Weather == null || !apiResponse.Weather.Any() || apiResponse.Wind == null)
                     {
                         Logger.LogError($"Incomplete weather data received for {city}.");
                         throw new Exception("Received incomplete weather data from API.");
                     }

                     var weatherDesc = apiResponse.Weather.First(); // Take the first description

                     // Use API's returned city name for consistency
                     string confirmedCity = apiResponse.CityName ?? city;

                     Logger.Log($"Successfully retrieved weather for {confirmedCity}");

                     return new WeatherInfo(
                         city: confirmedCity,
                         temp: apiResponse.Main.Temp,
                         desc: weatherDesc.Description ?? "N/A",
                         feels: apiResponse.Main.FeelsLike,
                         hum: apiResponse.Main.Humidity,
                         wind: apiResponse.Wind.Speed
                     );
                 }
                 else
                 {
                    // Attempt to read error message from API response body
                    string errorBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    string apiErrorMessage = $"Status: {response.StatusCode}.";
                    try {
                         var errorResponse = JsonSerializer.Deserialize<WeatherApiResponse>(errorBody);
                         if (!string.IsNullOrWhiteSpace(errorResponse?.Message)) {
                             apiErrorMessage = errorResponse.Message;
                         } else {
                             apiErrorMessage += $" Body: {errorBody.Substring(0, Math.Min(errorBody.Length, 200))}"; // Log snippet
                         }
                    } catch {
                         apiErrorMessage += $" Raw Body: {errorBody.Substring(0, Math.Min(errorBody.Length, 200))}"; // Log snippet if JSON parsing fails
                    }

                    Logger.LogError($"Failed to get weather for {city}. {apiErrorMessage}");
                    throw new HttpRequestException($"Failed to get weather: {apiErrorMessage}", null, response.StatusCode);
                 }
            }
            catch (JsonException jsonEx)
            {
                 Logger.LogError($"Failed to parse weather JSON response for {city}", jsonEx);
                 throw new Exception("Failed to process the weather data.", jsonEx);
            }
            catch (HttpRequestException httpEx)
            {
                Logger.LogError($"HTTP request failed during weather lookup for {city}", httpEx);
                throw; // Re-throw
            }
             catch (TaskCanceledException cancelEx) // Handle timeouts
            {
                Logger.LogError($"Weather request timed out for {city}", cancelEx);
                throw new TimeoutException("The weather request timed out.", cancelEx);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Unexpected error fetching weather for {city}", ex);
                throw new Exception($"An unexpected error occurred while fetching weather for {city}.", ex);
            }
        }
    }
}