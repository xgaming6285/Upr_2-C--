namespace Upr_2.Settings
{
    /// <summary>
    /// Holds API keys required for external services.
    /// </summary>
    public class ApiSettings
    {
        /// <summary>
        /// API key for the weather service.
        /// </summary>
        public string? WeatherApiKey { get; set; }
        /// <summary>
        /// API key for the currency conversion service.
        /// </summary>
        public string? CurrencyApiKey { get; set; }

        /// <summary>
        /// Checks if a valid Weather API key is configured.
        /// A key is considered valid if it's not null/empty and doesn't start with "YOUR_".
        /// </summary>
        public bool HasWeatherApiKey => !string.IsNullOrEmpty(WeatherApiKey) && !WeatherApiKey.StartsWith("YOUR_");
        /// <summary>
        /// Checks if a valid Currency API key is configured.
        /// A key is considered valid if it's not null/empty and doesn't start with "YOUR_".
        /// </summary>
        public bool HasCurrencyApiKey => !string.IsNullOrEmpty(CurrencyApiKey) && !CurrencyApiKey.StartsWith("YOUR_");
    }
}