using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Text.Json;
using System.Collections.Generic;

namespace Upr_2
{
    public class CurrencyService
    {
        private readonly HttpClient _client;
        private readonly string? _apiKey;
        private const string BaseUrl = "https://v6.exchangerate-api.com/v6";

        public CurrencyService(string? apiKey)
        {
            _client = new HttpClient();
            _apiKey = apiKey;
        }

        public async Task<Dictionary<string, string>> GetAvailableCurrenciesAsync()
        {
            if (string.IsNullOrEmpty(_apiKey))
            {
                throw new InvalidOperationException("API ключ не е предоставен за CurrencyService");
            }

            try
            {
                var response = await _client.GetStringAsync($"{BaseUrl}/{_apiKey}/codes");
                var jsonDoc = JsonDocument.Parse(response);
                var codes = jsonDoc.RootElement.GetProperty("supported_codes");

                var currencies = new Dictionary<string, string>();
                foreach (var code in codes.EnumerateArray())
                {
                    var currencyCode = code[0].GetString();
                    var currencyName = code[1].GetString();
                    if (currencyCode != null && currencyName != null)
                    {
                        currencies.Add(currencyCode, currencyName);
                    }
                }

                return currencies;
            }
            catch (Exception ex)
            {
                Logger.LogError("Error fetching available currencies", ex);
                throw new Exception($"Failed to get available currencies: {ex.Message}");
            }
        }

        public async Task<(double rate, double amount)> ConvertCurrencyAsync(string fromCurrency, string toCurrency, double amount)
        {
            if (string.IsNullOrEmpty(_apiKey))
            {
                throw new InvalidOperationException("API ключ не е предоставен за CurrencyService");
            }

            try
            {
                var response = await _client.GetStringAsync(
                    $"{BaseUrl}/{_apiKey}/pair/{fromCurrency}/{toCurrency}/{amount}");

                var jsonDoc = JsonDocument.Parse(response);
                var root = jsonDoc.RootElement;

                if (root.GetProperty("result").GetString() == "success")
                {
                    var rate = root.GetProperty("conversion_rate").GetDouble();
                    var convertedAmount = root.GetProperty("conversion_result").GetDouble();
                    return (rate, convertedAmount);
                }
                else
                {
                    throw new Exception("Currency conversion failed");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error converting from {fromCurrency} to {toCurrency}", ex);
                throw new Exception($"Failed to convert currency: {ex.Message}");
            }
        }
    }
}