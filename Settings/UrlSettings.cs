namespace Upr_2.Settings
{
    public class UrlSettings
    {
        public string IpApiBaseUrl { get; set; } = string.Empty;
        public string WeatherApiBaseUrl { get; set; } = string.Empty;
        public string CurrencyApiBaseUrl { get; set; } = string.Empty;
        public string TimeServiceUrl { get; set; } = string.Empty;
        public string NewsServiceUrl { get; set; } = string.Empty; // Base URL for relative links
    }
}