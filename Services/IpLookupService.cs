using System;
using System.Net.Http;
using System.Net.Http.Json; // Requires System.Net.Http.Json package (usually included with Microsoft.Extensions.Http)
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Upr_2.Settings;
using System.Text.Json;

namespace Upr_2.Services
{
    // Model for the ipapi.co response
    public class IpInfo
    {
        [JsonPropertyName("ip")]
        public string? Ip { get; set; }

        [JsonPropertyName("country_name")]
        public string? CountryName { get; set; }

        [JsonPropertyName("city")]
        public string? City { get; set; }

        [JsonPropertyName("region")]
        public string? Region { get; set; }

        [JsonPropertyName("postal")]
        public string? Postal { get; set; }

        [JsonPropertyName("latitude")]
        public double? Latitude { get; set; }

        [JsonPropertyName("longitude")]
        public double? Longitude { get; set; }

        [JsonPropertyName("timezone")]
        public string? Timezone { get; set; }

        [JsonPropertyName("utc_offset")]
        public string? UtcOffset { get; set; }

        [JsonPropertyName("asn")]
        public string? Asn { get; set; }

        [JsonPropertyName("org")]
        public string? Org { get; set; }

        [JsonPropertyName("currency")]
        public string? Currency { get; set; }

        [JsonPropertyName("currency_name")]
        public string? CurrencyName { get; set; }

        [JsonPropertyName("languages")]
        public string? Languages { get; set; }

        [JsonPropertyName("country_area")]
        public double? CountryArea { get; set; } // Assuming area can be large, double?

        [JsonPropertyName("country_population")]
        public long? CountryPopulation { get; set; } // Population can be large

        // Fields for potential errors
        [JsonPropertyName("error")]
        public bool? Error { get; set; }

        [JsonPropertyName("reason")]
        public string? Reason { get; set; }

        [JsonPropertyName("reserved")]
        public bool? Reserved { get; set; } // Handle reserved IPs

        public string GoogleMapsLink => (Latitude.HasValue && Longitude.HasValue)
            ? $"https://www.google.com/maps/search/?api=1&query={Latitude.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)},{Longitude.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)}"
            : string.Empty;
    }


    public class IpLookupService
    {
        private readonly HttpClient _httpClient;
        private readonly UrlSettings _urlSettings;
        private DateTime _lastRequestTime = DateTime.MinValue;
        private static readonly TimeSpan RequestCooldown = TimeSpan.FromSeconds(2); // Adjusted cooldown

        public IpLookupService(IHttpClientFactory httpClientFactory, IOptions<UrlSettings> urlSettingsOptions)
        {
            _httpClient = httpClientFactory.CreateClient("IpApiClient"); // Use a specific client if needed
            _urlSettings = urlSettingsOptions.Value;
            // Set base address and headers on the client via DI configuration if preferred
            // _httpClient.BaseAddress = new Uri(_urlSettings.IpApiBaseUrl);
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Upr2-ConsoleApp/1.1");
        }

        public async Task<IpInfo?> GetLocationInfoAsync(string ipAddress)
        {
            if (string.IsNullOrEmpty(_urlSettings.IpApiBaseUrl))
            {
                Logger.LogError("IpApiBaseUrl is not configured.");
                throw new InvalidOperationException("IP lookup service URL is not configured.");
            }

            // Basic Rate Limiting (Client-Side)
            var timeSinceLastRequest = DateTime.Now - _lastRequestTime;
            if (timeSinceLastRequest < RequestCooldown)
            {
                var waitTime = RequestCooldown - timeSinceLastRequest;
                Logger.Log($"Rate limit self-imposed, waiting for {waitTime.TotalSeconds:F1} seconds");
                await Task.Delay(waitTime).ConfigureAwait(false);
            }

            string requestUrl = $"{_urlSettings.IpApiBaseUrl.TrimEnd('/')}/{ipAddress}/json/";
            Logger.Log($"Sending IP lookup request to: {requestUrl}");

            try
            {
                var response = await _httpClient.GetAsync(requestUrl).ConfigureAwait(false);
                _lastRequestTime = DateTime.Now; // Update time after request attempt

                if (response.IsSuccessStatusCode)
                {
                    var ipInfo = await response.Content.ReadFromJsonAsync<IpInfo>().ConfigureAwait(false);

                    if (ipInfo?.Error == true)
                    {
                         Logger.LogWarning($"IP API returned error for {ipAddress}: {ipInfo.Reason}");
                         // You might want to throw a specific exception here or return null/ipInfo
                         return ipInfo; // Return the object containing the error reason
                    }
                    if (ipInfo?.Reserved == true)
                    {
                        Logger.Log($"IP address {ipAddress} is reserved.");
                        // Handle reserved IPs appropriately
                        return ipInfo;
                    }

                    Logger.Log($"Successfully retrieved location info for {ipAddress}");
                    return ipInfo;
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                {
                    Logger.LogError($"Rate limit exceeded (429) from API for IP: {ipAddress}");
                    // Consider implementing exponential backoff here
                    throw new HttpRequestException("API rate limit exceeded. Please wait and try again.", null, response.StatusCode);
                }
                else
                {
                    Logger.LogError($"API request failed for IP {ipAddress}. Status: {response.StatusCode}, Reason: {response.ReasonPhrase}");
                     // Throw exception with details
                     throw new HttpRequestException($"IP lookup failed: {response.ReasonPhrase} ({(int)response.StatusCode})", null, response.StatusCode);
                }
            }
            // Catch specific exceptions first
            catch (JsonException jsonEx)
            {
                 Logger.LogError($"Failed to parse JSON response for IP {ipAddress}", jsonEx);
                 throw new Exception("Failed to process the response from the IP lookup service.", jsonEx);
            }
            catch (HttpRequestException httpEx)
            {
                // Logged above or re-throw if needed
                Logger.LogError($"HTTP request failed during IP lookup for {ipAddress}", httpEx);
                throw; // Re-throw the original HttpRequestException
            }
            catch (TaskCanceledException cancelEx) // Handle timeouts
            {
                Logger.LogError($"IP lookup request timed out for {ipAddress}", cancelEx);
                throw new TimeoutException("The IP lookup request timed out.", cancelEx);
            }
            catch (Exception ex) // Catch unexpected errors
            {
                Logger.LogError($"Unexpected error during IP lookup for {ipAddress}", ex);
                throw new Exception("An unexpected error occurred during IP lookup.", ex);
            }
        }
    }
}