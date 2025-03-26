using System;
using System.Net.Http;
using System.Threading.Tasks;
using HtmlAgilityPack;
using Microsoft.Extensions.Options;
using Upr_2.Settings;

namespace Upr_2.Services
{
    public class TimeService
    {
        private readonly HttpClient _httpClient;
        private readonly UrlSettings _urlSettings;

        public TimeService(IHttpClientFactory httpClientFactory, IOptions<UrlSettings> urlSettingsOptions)
        {
            _httpClient = httpClientFactory.CreateClient("DefaultClient");
            _urlSettings = urlSettingsOptions.Value;
        }

        public async Task<(string Time, string Date)?> GetSofiaTimeAsync()
        {
             if (string.IsNullOrEmpty(_urlSettings.TimeServiceUrl))
             {
                 Logger.LogError("TimeServiceUrl is not configured.");
                 return null; // Indicate failure due to configuration
             }

            try
            {
                Logger.Log($"Fetching time from {_urlSettings.TimeServiceUrl}");
                string html = await _httpClient.GetStringAsync(_urlSettings.TimeServiceUrl).ConfigureAwait(false);
                var htmlDoc = new HtmlDocument();
                htmlDoc.LoadHtml(html);

                // Prefer IDs for selectors as they are usually more stable
                var timeNode = htmlDoc.DocumentNode.SelectSingleNode("//span[@id='ct']");
                string time = timeNode?.InnerText.Trim() ?? "Time not found";

                var dateNode = htmlDoc.DocumentNode.SelectSingleNode("//span[@id='ctdat']");
                string date = dateNode?.InnerText.Trim() ?? "Date not found";

                if (time == "Time not found" || date == "Date not found")
                {
                     Logger.LogWarning($"Could not find time/date elements on {_urlSettings.TimeServiceUrl}. Page structure might have changed.");
                }
                else
                {
                    Logger.Log($"Successfully retrieved time: {time}, date: {date}");
                }
                return (time, date);
            }
            catch (HttpRequestException ex)
            {
                 Logger.LogError($"HTTP error fetching time from {_urlSettings.TimeServiceUrl}", ex);
                 Console.WriteLine($"Error fetching time: {ex.Message}"); // Keep user feedback
                 return null; // Indicate failure
            }
             catch (HtmlWebException ex) // Catch HtmlAgilityPack specific errors
            {
                 Logger.LogError($"HTML parsing error fetching time from {_urlSettings.TimeServiceUrl}", ex);
                 Console.WriteLine($"Error parsing time page: {ex.Message}");
                 return null;
            }
            catch (Exception ex) // Catch unexpected errors
            {
                Logger.LogError($"Unexpected error fetching time from {_urlSettings.TimeServiceUrl}", ex);
                Console.WriteLine($"An unexpected error occurred: {ex.Message}");
                return null;
            }
        }
    }
}