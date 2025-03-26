namespace Upr_2.Settings
{
    /// <summary>
    /// Holds base URLs for various external services used by the application.
    /// </summary>
    public class UrlSettings
    {
        /// <summary>
        /// Base URL for the IP geolocation service.
        /// </summary>
        public string IpApiBaseUrl { get; set; } = string.Empty;
        /// <summary>
        /// Base URL for the weather service API.
        /// </summary>
        public string WeatherApiBaseUrl { get; set; } = string.Empty;
        /// <summary>
        /// Base URL for the currency conversion service API.
        /// </summary>
        public string CurrencyApiBaseUrl { get; set; } = string.Empty;
        /// <summary>
        /// URL for the time service.
        /// </summary>
        public string TimeServiceUrl { get; set; } = string.Empty;
        /// <summary>
        /// Base URL for the news service, used for resolving relative links if necessary.
        /// </summary>
        public string NewsServiceUrl { get; set; } = string.Empty; // Base URL for relative links
    }
}