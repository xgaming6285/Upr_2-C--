using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Upr_2.Settings;

namespace Upr_2.Services
{
    // Represents the 'main' object in the OpenWeatherMap API response.
    public class WeatherMain
    {
        // Temperature in Celsius (due to units=metric).
        [JsonPropertyName("temp")]
        public double Temp { get; set; }

        // 'Feels like' temperature in Celsius.
        [JsonPropertyName("feels_like")]
        public double FeelsLike { get; set; }

        // Humidity percentage.
        [JsonPropertyName("humidity")]
        public int Humidity { get; set; }
    }

    // Represents an item in the 'weather' array in the OpenWeatherMap API response.
    public class WeatherDescription
    {
        // Text description of the weather conditions (e.g., "clear sky").
        [JsonPropertyName("description")]
        public string? Description { get; set; }

        // Add icon if you want it later
        //[JsonPropertyName("icon")]
        //public string Icon { get; set; }
    }

    // Represents the 'wind' object in the OpenWeatherMap API response.
    public class WeatherWind
    {
        // Wind speed in meter/sec (due to units=metric).
        [JsonPropertyName("speed")]
        public double Speed { get; set; }
    }

    // Represents the overall structure of the JSON response from the OpenWeatherMap API.
    public class WeatherApiResponse
    {
        // List of weather condition descriptions (usually contains one item).
        [JsonPropertyName("weather")]
        public List<WeatherDescription>? Weather { get; set; }

        // Main weather parameters (temperature, humidity, etc.).
        [JsonPropertyName("main")]
        public WeatherMain? Main { get; set; }

        // Wind information.
        [JsonPropertyName("wind")]
        public WeatherWind? Wind { get; set; }

        // The city name as recognized and returned by the API.
        [JsonPropertyName("name")]
        public string? CityName { get; set; } // API returns the standardized city name

        // Error handling properties if relevant for this API
        // API status code (e.g., "404" for not found, 200 for success). Can be string or int.
        [JsonPropertyName("cod")] // e.g., "404" for not found
        public object? Cod { get; set; } // Can be string or int

        // Error message provided by the API in case of failure.
        [JsonPropertyName("message")] // Error message
        public string? Message { get; set; }

    }

    // Simplified class to hold the essential weather information for internal use or display.
    // Uses a primary constructor for concise initialization.
    public class WeatherInfo(string city, double temp, string desc, double feels, int hum, double wind)
    {
        public string City { get; set; } = city;
        public double Temperature { get; set; } = temp;
        public string Description { get; set; } = desc;
        public double FeelsLike { get; set; } = feels;
        public int Humidity { get; set; } = hum;
        public double WindSpeed { get; set; } = wind;
    }

    // Service responsible for fetching weather data from the OpenWeatherMap API.
    public class WeatherService(
        IHttpClientFactory httpClientFactory, // Factory to create HttpClient instances.
        IOptions<ApiSettings> apiSettingsOptions, // Provides access to API key settings.
        IOptions<UrlSettings> urlSettingsOptions) // Provides access to URL settings.
    {
        // HttpClient instance used for making API requests. Named client "WeatherClient" likely configured in Startup.cs or Program.cs.
        private readonly HttpClient _httpClient = httpClientFactory.CreateClient("WeatherClient"); // Initialize directly
        // Holds the API key settings.
        private readonly ApiSettings _apiSettings = apiSettingsOptions.Value; // Initialize directly
        // Holds the URL settings, including the base URL for the weather API.
        private readonly UrlSettings _urlSettings = urlSettingsOptions.Value; // Initialize directly

        /// <summary>
        /// Asynchronously fetches weather information for a specified city.
        /// </summary>
        /// <param name="city">The name of the city to get weather for.</param>
        /// <returns>A Task resulting in a WeatherInfo object, or null if fetching fails before making the request.</returns>
        /// <exception cref="InvalidOperationException">Thrown if API key or base URL is not configured.</exception>
        /// <exception cref="HttpRequestException">Thrown if the HTTP request fails (e.g., network error, non-success status code).</exception>
        /// <exception cref="JsonException">Thrown if the API response cannot be parsed.</exception>
        /// <exception cref="TimeoutException">Thrown if the request times out.</exception>
        /// <exception cref="Exception">Thrown for other unexpected errors.</exception>
        public async Task<WeatherInfo?> GetWeatherAsync(string city)
        {
            // Check if the API key is configured.
            if (!_apiSettings.HasWeatherApiKey)
            {
                // Log or handle internally, throwing might be too disruptive if key is optional
                Logger.LogWarning("Weather API key is not configured or invalid.");
                throw new InvalidOperationException("Weather API key is not available.");
            }
            // Check if the base URL for the weather API is configured.
            if (string.IsNullOrEmpty(_urlSettings.WeatherApiBaseUrl))
            {
                Logger.LogError("WeatherApiBaseUrl is not configured.");
                throw new InvalidOperationException("Weather API base URL is not configured.");
            }

            // Retrieve the API key (non-null assertion is safe due to the check above).
            string apiKey = _apiSettings.WeatherApiKey!; // Not null here due to HasWeatherApiKey check
            // Construct the full request URL, including city, API key, units (metric), and language (Bulgarian).
            string requestUrl = $"{_urlSettings.WeatherApiBaseUrl.TrimEnd('/')}/weather?q={Uri.EscapeDataString(city)}&appid={apiKey}&units=metric&lang=bg"; // Added lang=bg

            // Log the request details.
            Logger.Log($"Requesting weather for {city} from {requestUrl}");

            try
            {
                // Send the GET request to the OpenWeatherMap API. ConfigureAwait(false) avoids capturing the synchronization context.
                var response = await _httpClient.GetAsync(requestUrl).ConfigureAwait(false);

                // Check if the response status code indicates success (e.g., 200 OK).
                if (response.IsSuccessStatusCode)
                {
                    // Deserialize the JSON response body into our WeatherApiResponse object.
                    var apiResponse = await response.Content.ReadFromJsonAsync<WeatherApiResponse>().ConfigureAwait(false);

                    // Basic validation to ensure essential parts of the response are present.
                    if (apiResponse?.Main == null || apiResponse.Weather == null || apiResponse.Weather.Count == 0 || apiResponse.Wind == null)
                    {
                        Logger.LogError($"Incomplete weather data received for {city}.");
                        throw new Exception("Received incomplete weather data from API.");
                    }

                    // Extract the first weather description (API usually sends only one).
                    var weatherDesc = apiResponse.Weather.First(); // Take the first description

                    // Use the city name returned by the API for consistency, fallback to the input city name.
                    string confirmedCity = apiResponse.CityName ?? city;

                    // Log successful retrieval.
                    Logger.Log($"Successfully retrieved weather for {confirmedCity}");

                    // Create and return the simplified WeatherInfo object.
                    return new WeatherInfo(
                        city: confirmedCity,
                        temp: apiResponse.Main.Temp,
                        desc: weatherDesc.Description ?? "N/A", // Use "N/A" if description is null.
                        feels: apiResponse.Main.FeelsLike,
                        hum: apiResponse.Main.Humidity,
                        wind: apiResponse.Wind.Speed
                    );
                }
                else // Handle non-success status codes (e.g., 404 Not Found, 401 Unauthorized).
                {
                    // Attempt to read the error message from the API response body.
                    string errorBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    string apiErrorMessage = $"Status: {response.StatusCode}.";
                    try
                    {
                        // Try to parse the error body as a WeatherApiResponse to get the 'message' field.
                        var errorResponse = JsonSerializer.Deserialize<WeatherApiResponse>(errorBody);
                        if (!string.IsNullOrWhiteSpace(errorResponse?.Message))
                        {
                            apiErrorMessage = errorResponse.Message; // Use the API's specific error message.
                        }
                        else
                        {
                            // If no message field, include a snippet of the raw body in the error.
                            apiErrorMessage += $" Body: {errorBody.Substring(0, Math.Min(errorBody.Length, 200))}"; // Log snippet
                        }
                    }
                    catch // If JSON parsing of the error body fails.
                    {
                        // Include a snippet of the raw body in the error.
                        apiErrorMessage += $" Raw Body: {errorBody.Substring(0, Math.Min(errorBody.Length, 200))}"; // Log snippet if JSON parsing fails
                    }

                    // Log the failure details.
                    Logger.LogError($"Failed to get weather for {city}. {apiErrorMessage}");
                    // Throw an exception indicating the HTTP request failed, including the API error message and status code.
                    throw new HttpRequestException($"Failed to get weather: {apiErrorMessage}", null, response.StatusCode);
                }
            }
            // Catch errors during JSON deserialization.
            catch (JsonException jsonEx)
            {
                Logger.LogError($"Failed to parse weather JSON response for {city}", jsonEx);
                throw new Exception("Failed to process the weather data.", jsonEx);
            }
            // Catch general HTTP request errors (network issues, DNS errors, etc.).
            catch (HttpRequestException httpEx)
            {
                Logger.LogError($"HTTP request failed during weather lookup for {city}", httpEx);
                throw; // Re-throw the original exception.
            }
            // Catch task cancellations, often indicating a timeout.
            catch (TaskCanceledException cancelEx) // Handle timeouts
            {
                Logger.LogError($"Weather request timed out for {city}", cancelEx);
                throw new TimeoutException("The weather request timed out.", cancelEx);
            }
            // Catch any other unexpected exceptions.
            catch (Exception ex)
            {
                Logger.LogError($"Unexpected error fetching weather for {city}", ex);
                // Wrap the original exception in a new, more specific exception.
                throw new Exception($"An unexpected error occurred while fetching weather for {city}.", ex);
            }
        }
    }
}