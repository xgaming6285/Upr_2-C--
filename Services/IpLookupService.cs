using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Upr_2.Settings;

namespace Upr_2.Services
{
    /// <summary>
    /// Custom exception for handling IP API specific errors.
    /// Provides additional context about the IP address and error reason.
    /// </summary>
    public class IpApiException : Exception
    {
        public string? IpAddress { get; }
        public string? ErrorReason { get; }

        public IpApiException(string message, string? ipAddress = null, string? errorReason = null, Exception? innerException = null)
            : base(message, innerException)
        {
            IpAddress = ipAddress;
            ErrorReason = errorReason;
        }
    }

    /// <summary>
    /// Model class representing the response from ipapi.co service.
    /// Contains geographical, network, and error information for an IP address.
    /// All properties are nullable to handle partial or error responses.
    /// </summary>
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
        public double? CountryArea { get; set; } // Assuming area can be large

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

    /// <summary>
    /// Service for looking up geographical and network information for IP addresses using ipapi.co.
    /// Implements rate limiting, exponential backoff, and comprehensive error handling.
    /// </summary>
    public class IpLookupService
    {
        private readonly HttpClient _httpClient;
        private readonly UrlSettings _urlSettings;
        private DateTime _lastRequestTime = DateTime.MinValue;

        // Client-side rate limiting: Ensures minimum delay between requests
        private static readonly TimeSpan RequestCooldown = TimeSpan.FromSeconds(2);

        // Exponential backoff configuration
        private const int MaxRetries = 3;              // Maximum number of retry attempts for rate-limited requests
        private const int BaseDelayMs = 1000;         // Initial delay (1 second) for exponential backoff

        /// <summary>
        /// Initializes a new instance of IpLookupService with HTTP client factory and URL settings.
        /// Sets up the HTTP client with appropriate headers for API communication.
        /// </summary>
        public IpLookupService(IHttpClientFactory httpClientFactory, IOptions<UrlSettings> urlSettingsOptions)
        {
            _httpClient = httpClientFactory.CreateClient("IpApiClient"); // Use a specific client if needed
            _urlSettings = urlSettingsOptions.Value;
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Upr2-ConsoleApp/1.1");
        }

        /// <summary>
        /// Implements exponential backoff retry mechanism for handling rate-limited requests.
        /// Retries the operation with increasing delays: 1s -> 2s -> 4s
        /// Only retries on HTTP 429 (Too Many Requests) responses.
        /// </summary>
        /// <param name="operation">The async operation to retry</param>
        /// <param name="ipAddress">IP address for context in error messages</param>
        /// <returns>IpInfo if successful, or throws exception after max retries</returns>
        /// <exception cref="IpApiException">Thrown when max retries are exceeded</exception>
        private static async Task<IpInfo?> RetryWithExponentialBackoffAsync(Func<Task<IpInfo?>> operation, string ipAddress)
        {
            for (int attempt = 0; attempt <= MaxRetries; attempt++)
            {
                try
                {
                    return await operation().ConfigureAwait(false);
                }
                catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                {
                    if (attempt == MaxRetries)
                    {
                        Logger.LogError($"Max retries ({MaxRetries}) exceeded for IP: {ipAddress}");
                        throw new IpApiException(
                            $"Rate limit exceeded after {MaxRetries} retries",
                            ipAddress,
                            "Too many requests",
                            ex);
                    }

                    int delayMs = BaseDelayMs * (int)Math.Pow(2, attempt); // Exponential backoff
                    Logger.Log($"Rate limit hit, attempt {attempt + 1}/{MaxRetries}. Waiting {delayMs / 1000.0:F1} seconds before retry...");
                    await Task.Delay(delayMs).ConfigureAwait(false);
                }
            }

            // This should never be reached due to the throw above, but compiler needs it
            throw new Exception("Unexpected end of retry loop");
        }

        /// <summary>
        /// Asynchronously retrieves location and network information for an IP address.
        /// Features:
        /// - Client-side rate limiting to prevent overwhelming the API
        /// - Exponential backoff for handling server-side rate limits
        /// - Comprehensive error handling for API responses
        /// - Detailed logging for debugging and monitoring
        /// </summary>
        /// <param name="ipAddress">The IP address to look up</param>
        /// <returns>
        /// - Successful response: IpInfo object with location data
        /// - Reserved IP: IpInfo object with Reserved=true
        /// - Error cases: Throws appropriate exceptions
        /// </returns>
        /// <exception cref="InvalidOperationException">When API URL is not configured</exception>
        /// <exception cref="IpApiException">When API returns an error response</exception>
        /// <exception cref="HttpRequestException">For HTTP-related errors</exception>
        public async Task<IpInfo?> GetLocationInfoAsync(string ipAddress)
        {
            // Validate configuration
            if (string.IsNullOrEmpty(_urlSettings.IpApiBaseUrl))
            {
                Logger.LogError("IpApiBaseUrl is not configured.");
                throw new InvalidOperationException("IP lookup service URL is not configured.");
            }

            // Wrap the entire operation in exponential backoff retry mechanism
            return await RetryWithExponentialBackoffAsync(async () =>
            {
                // Implement client-side rate limiting
                var timeSinceLastRequest = DateTime.Now - _lastRequestTime;
                if (timeSinceLastRequest < RequestCooldown)
                {
                    var waitTime = RequestCooldown - timeSinceLastRequest;
                    Logger.Log($"Rate limit self-imposed, waiting for {waitTime.TotalSeconds:F1} seconds");
                    await Task.Delay(waitTime).ConfigureAwait(false);
                }

                // Construct and send the API request
                string requestUrl = $"{_urlSettings.IpApiBaseUrl.TrimEnd('/')}/{ipAddress}/json/";
                Logger.Log($"Sending IP lookup request to: {requestUrl}");

                var response = await _httpClient.GetAsync(requestUrl).ConfigureAwait(false);
                _lastRequestTime = DateTime.Now; // Update last request timestamp

                if (response.IsSuccessStatusCode)
                {
                    var ipInfo = await response.Content.ReadFromJsonAsync<IpInfo>().ConfigureAwait(false);

                    // Handle API-level errors in successful HTTP responses
                    if (ipInfo?.Error == true)
                    {
                        Logger.LogWarning($"IP API returned error for {ipAddress}: {ipInfo.Reason}");
                        throw new IpApiException($"IP API returned error: {ipInfo.Reason}", ipAddress, ipInfo.Reason);
                    }
                    // Handle reserved IP addresses (special case)
                    if (ipInfo?.Reserved == true)
                    {
                        Logger.Log($"IP address {ipAddress} is reserved.");
                        return ipInfo;
                    }

                    Logger.Log($"Successfully retrieved location info for {ipAddress}");
                    return ipInfo;
                }
                // Handle rate limiting with exponential backoff
                else if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                {
                    Logger.LogError($"Rate limit exceeded (429) from API for IP: {ipAddress}");
                    throw new HttpRequestException("API rate limit exceeded. Please wait and try again.", null, response.StatusCode);
                }
                // Handle all other HTTP errors
                else
                {
                    Logger.LogError($"API request failed for IP {ipAddress}. Status: {response.StatusCode}, Reason: {response.ReasonPhrase}");
                    throw new HttpRequestException($"IP lookup failed: {response.ReasonPhrase} ({(int)response.StatusCode})", null, response.StatusCode);
                }
            }, ipAddress).ConfigureAwait(false);
        }
    }
}