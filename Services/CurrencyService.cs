using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Upr_2.Settings;

namespace Upr_2.Services
{
    // Models for ExchangeRate-API responses
    public class CurrencyCodesResponse
    {
        [JsonPropertyName("result")]
        public string? Result { get; set; } // "success" or "error"

        [JsonPropertyName("supported_codes")]
        public List<List<string>>? SupportedCodes { get; set; } // [ ["USD", "United States Dollar"], ... ]

        [JsonPropertyName("error-type")]
        public string? ErrorType { get; set; }
    }

    public class CurrencyPairResponse
    {
        [JsonPropertyName("result")]
        public string? Result { get; set; }

        [JsonPropertyName("base_code")]
        public string? BaseCode { get; set; }

        [JsonPropertyName("target_code")]
        public string? TargetCode { get; set; }

        [JsonPropertyName("conversion_rate")]
        public double? ConversionRate { get; set; }

        [JsonPropertyName("conversion_result")]
        public double? ConversionResult { get; set; }

        [JsonPropertyName("error-type")]
        public string? ErrorType { get; set; }
    }


    public class CurrencyService
    {
        private readonly HttpClient _httpClient;
        private readonly ApiSettings _apiSettings;
        private readonly UrlSettings _urlSettings;

        public CurrencyService(
            IHttpClientFactory httpClientFactory,
            IOptions<ApiSettings> apiSettingsOptions,
            IOptions<UrlSettings> urlSettingsOptions)
        {
            _httpClient = httpClientFactory.CreateClient("CurrencyClient");
            _apiSettings = apiSettingsOptions.Value;
            _urlSettings = urlSettingsOptions.Value;
        }

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
                        .Where(pair => pair != null && pair.Count == 2 && !string.IsNullOrEmpty(pair[0])) // Basic validation
                        .ToDictionary(pair => pair[0], pair => pair[1] ?? pair[0], StringComparer.OrdinalIgnoreCase); // Use IgnoreCase comparer

                    Logger.Log($"Successfully retrieved {currencies.Count} available currencies.");
                    return currencies;
                }
                else
                {
                     // Handle API error response
                     string errorBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                     string apiErrorMessage = $"Status: {response.StatusCode}.";
                     try {
                          var errorResponse = JsonSerializer.Deserialize<CurrencyCodesResponse>(errorBody);
                          apiErrorMessage = errorResponse?.ErrorType ?? apiErrorMessage;
                     } catch {} // Ignore deserialize error on error body

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
            // Ensure amount is formatted correctly for URL (invariant culture)
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
                      try {
                          var errorResponse = JsonSerializer.Deserialize<CurrencyPairResponse>(errorBody);
                          apiErrorMessage = errorResponse?.ErrorType ?? apiErrorMessage;
                     } catch {}

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