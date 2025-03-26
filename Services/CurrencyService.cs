using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Upr_2.Settings;

namespace Upr_2.Services
{
    /// <summary>
    /// Service for handling currency-related operations using the ExchangeRate-API.
    /// This service provides functionality to:
    /// 1. Retrieve available currency codes and their descriptions
    /// 2. Convert amounts between different currencies
    /// </summary>

    // Response model for the currency codes endpoint
    public class CurrencyCodesResponse
    {
        [JsonPropertyName("result")]
        public string? Result { get; set; } // Indicates API call success/failure

        [JsonPropertyName("supported_codes")]
        public List<List<string>>? SupportedCodes { get; set; } // Contains pairs of [currency code, currency name]

        [JsonPropertyName("error-type")]
        public string? ErrorType { get; set; } // Contains error description if the API call fails
    }

    /// <summary>
    /// Response model for currency conversion endpoint.
    /// Contains details about the conversion including source/target currencies,
    /// conversion rate, and the final converted amount.
    /// </summary>
    public class CurrencyPairResponse
    {
        [JsonPropertyName("result")]
        public string? Result { get; set; } // Indicates API call success/failure

        [JsonPropertyName("base_code")]
        public string? BaseCode { get; set; } // Source currency code

        [JsonPropertyName("target_code")]
        public string? TargetCode { get; set; } // Target currency code

        [JsonPropertyName("conversion_rate")]
        public double? ConversionRate { get; set; } // Exchange rate between the currencies

        [JsonPropertyName("conversion_result")]
        public double? ConversionResult { get; set; } // Final converted amount

        [JsonPropertyName("error-type")]
        public string? ErrorType { get; set; } // Contains error description if the API call fails
    }

    /// <summary>
    /// Service class that handles all currency-related operations.
    /// Uses dependency injection to get HTTP client and configuration settings.
    /// </summary>
    public class CurrencyService(
        IHttpClientFactory httpClientFactory,
        IOptions<ApiSettings> apiSettingsOptions,
        IOptions<UrlSettings> urlSettingsOptions)
    {
        private readonly HttpClient _httpClient = httpClientFactory.CreateClient("CurrencyClient");
        private readonly ApiSettings _apiSettings = apiSettingsOptions.Value;
        private readonly UrlSettings _urlSettings = urlSettingsOptions.Value;

        /// <summary>
        /// Retrieves a list of all available currencies from the API.
        /// </summary>
        /// <returns>
        /// A dictionary where the key is the currency code (e.g., "USD") and
        /// the value is the currency name (e.g., "United States Dollar")
        /// </returns>
        /// <exception cref="InvalidOperationException">Thrown when API configuration is missing</exception>
        /// <exception cref="HttpRequestException">Thrown when API request fails</exception>
        /// <exception cref="Exception">Thrown for various other error conditions</exception>
        public async Task<Dictionary<string, string>?> GetAvailableCurrenciesAsync()
        {
            if (!_apiSettings.HasCurrencyApiKey)
            {
                Logger.LogWarning("Currency API key is not configured or invalid.");
                throw new InvalidOperationException("Currency API key is not available.");
            }
            if (string.IsNullOrEmpty(_urlSettings.CurrencyApiBaseUrl))
            {
                Logger.LogError("CurrencyApiBaseUrl is not configured.");
                throw new InvalidOperationException("Currency API base URL is not configured.");
            }

            string apiKey = _apiSettings.CurrencyApiKey!;
            string requestUrl = $"{_urlSettings.CurrencyApiBaseUrl.TrimEnd('/')}/{apiKey}/codes";
            Logger.Log($"Requesting available currencies from {requestUrl}");

            try
            {
                var response = await _httpClient.GetAsync(requestUrl).ConfigureAwait(false);

                if (response.IsSuccessStatusCode)
                {
                    var apiResponse = await response.Content.ReadFromJsonAsync<CurrencyCodesResponse>().ConfigureAwait(false);

                    if (apiResponse?.Result != "success" || apiResponse.SupportedCodes == null)
                    {
                        string error = apiResponse?.ErrorType ?? "Unknown error";
                        Logger.LogError($"Currency API returned error while fetching codes: {error}");
                        throw new Exception($"Failed to get currency codes from API: {error}");
                    }

                    // Convert List<List<string>> to Dictionary
                    var currencies = apiResponse.SupportedCodes
                        .Where(pair => pair != null && pair.Count == 2 && !string.IsNullOrEmpty(pair[0]))
                        .ToDictionary(pair => pair[0], pair => pair[1] ?? pair[0], StringComparer.OrdinalIgnoreCase);

                    Logger.Log($"Successfully retrieved {currencies.Count} available currencies.");
                    return currencies;
                }
                else
                {
                    // Handle API error response
                    string errorBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    string apiErrorMessage = $"Status: {response.StatusCode}.";
                    try
                    {
                        var errorResponse = JsonSerializer.Deserialize<CurrencyCodesResponse>(errorBody);
                        apiErrorMessage = errorResponse?.ErrorType ?? apiErrorMessage;
                    }
                    catch { } // Ignore deserialize error on error body

                    Logger.LogError($"Failed to get available currencies. {apiErrorMessage}");
                    throw new HttpRequestException($"Failed to get currencies: {apiErrorMessage}", null, response.StatusCode);
                }

            }
            catch (JsonException jsonEx)
            {
                Logger.LogError("Failed to parse currency codes JSON response", jsonEx);
                throw new Exception("Failed to process the currency codes data.", jsonEx);
            }
            catch (HttpRequestException httpEx)
            {
                Logger.LogError("HTTP request failed during currency codes fetch", httpEx);
                throw;
            }
            catch (TaskCanceledException cancelEx)
            {
                Logger.LogError("Currency codes request timed out", cancelEx);
                throw new TimeoutException("The currency codes request timed out.", cancelEx);
            }
            catch (Exception ex)
            {
                Logger.LogError("Unexpected error fetching available currencies", ex);
                throw new Exception("An unexpected error occurred while fetching currencies.", ex);
            }
        }

        /// <summary>
        /// Converts an amount from one currency to another using current exchange rates.
        /// </summary>
        /// <param name="fromCurrency">Source currency code (e.g., "USD")</param>
        /// <param name="toCurrency">Target currency code (e.g., "EUR")</param>
        /// <param name="amount">The amount to convert</param>
        /// <returns>
        /// A tuple containing:
        /// - Rate: The exchange rate between the currencies
        /// - ConvertedAmount: The final converted amount
        /// </returns>
        /// <exception cref="InvalidOperationException">Thrown when API configuration is missing</exception>
        /// <exception cref="HttpRequestException">Thrown when API request fails</exception>
        /// <exception cref="TimeoutException">Thrown when request times out</exception>
        /// <exception cref="Exception">Thrown for various other error conditions</exception>
        public async Task<(double Rate, double ConvertedAmount)?> ConvertCurrencyAsync(string fromCurrency, string toCurrency, double amount)
        {
            if (!_apiSettings.HasCurrencyApiKey)
            {
                Logger.LogWarning("Currency API key is not configured or invalid.");
                throw new InvalidOperationException("Currency API key is not available.");
            }
            if (string.IsNullOrEmpty(_urlSettings.CurrencyApiBaseUrl))
            {
                Logger.LogError("CurrencyApiBaseUrl is not configured.");
                throw new InvalidOperationException("Currency API base URL is not configured.");
            }

            string apiKey = _apiSettings.CurrencyApiKey!;
            // Ensure amount is formatted correctly for URL
            string amountStr = amount.ToString(System.Globalization.CultureInfo.InvariantCulture);
            string requestUrl = $"{_urlSettings.CurrencyApiBaseUrl.TrimEnd('/')}/{apiKey}/pair/{fromCurrency.ToUpperInvariant()}/{toCurrency.ToUpperInvariant()}/{amountStr}";

            Logger.Log($"Requesting currency conversion: {amount} {fromCurrency} to {toCurrency}");

            try
            {
                var response = await _httpClient.GetAsync(requestUrl).ConfigureAwait(false);

                if (response.IsSuccessStatusCode)
                {
                    var apiResponse = await response.Content.ReadFromJsonAsync<CurrencyPairResponse>().ConfigureAwait(false);

                    if (apiResponse?.Result != "success" || !apiResponse.ConversionRate.HasValue || !apiResponse.ConversionResult.HasValue)
                    {
                        string error = apiResponse?.ErrorType ?? "Unknown conversion error";
                        Logger.LogError($"Currency conversion failed: {error} (From: {fromCurrency}, To: {toCurrency})");
                        throw new Exception($"Currency conversion failed: {error}");
                    }

                    Logger.Log($"Successfully converted currency. Rate: {apiResponse.ConversionRate.Value}");
                    return (apiResponse.ConversionRate.Value, apiResponse.ConversionResult.Value);
                }
                else
                {
                    // Handle API error response
                    string errorBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    string apiErrorMessage = $"Status: {response.StatusCode}.";
                    try
                    {
                        var errorResponse = JsonSerializer.Deserialize<CurrencyPairResponse>(errorBody);
                        apiErrorMessage = errorResponse?.ErrorType ?? apiErrorMessage;
                    }
                    catch { }

                    Logger.LogError($"Failed currency conversion ({fromCurrency} to {toCurrency}). {apiErrorMessage}");
                    throw new HttpRequestException($"Currency conversion failed: {apiErrorMessage}", null, response.StatusCode);
                }
            }
            catch (JsonException jsonEx)
            {
                Logger.LogError($"Failed to parse currency conversion JSON response ({fromCurrency} to {toCurrency})", jsonEx);
                throw new Exception("Failed to process the currency conversion data.", jsonEx);
            }
            catch (HttpRequestException httpEx)
            {
                Logger.LogError($"HTTP request failed during currency conversion ({fromCurrency} to {toCurrency})", httpEx);
                throw;
            }
            catch (TaskCanceledException cancelEx)
            {
                Logger.LogError($"Currency conversion request timed out ({fromCurrency} to {toCurrency})", cancelEx);
                throw new TimeoutException("The currency conversion request timed out.", cancelEx);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Unexpected error converting currency ({fromCurrency} to {toCurrency})", ex);
                throw new Exception("An unexpected error occurred during currency conversion.", ex);
            }
        }
    }
}