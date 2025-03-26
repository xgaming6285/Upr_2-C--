namespace Upr_2.Settings
{
    public class ApiSettings
    {
        public string? WeatherApiKey { get; set; }
        public string? CurrencyApiKey { get; set; }

        public bool HasWeatherApiKey => !string.IsNullOrEmpty(WeatherApiKey) && !WeatherApiKey.StartsWith("YOUR_");
        public bool HasCurrencyApiKey => !string.IsNullOrEmpty(CurrencyApiKey) && !CurrencyApiKey.StartsWith("YOUR_");
    }
}