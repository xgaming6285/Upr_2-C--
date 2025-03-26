using System;
using System.Net.Http;
using System.Threading.Tasks;
using HtmlAgilityPack; // Library for parsing HTML documents
using Microsoft.Extensions.Options; // Used for accessing configuration settings
using Upr_2.Settings; // Namespace containing UrlSettings

namespace Upr_2.Services
{
    /// <summary>
    /// Service responsible for fetching the current time and date for Sofia
    /// by scraping a specific web page.
    /// </summary>
    public class TimeService
    {
        private readonly HttpClient _httpClient; // Client for making HTTP requests
        private readonly UrlSettings _urlSettings; // Configuration settings containing the target URL

        /// <summary>
        /// Initializes a new instance of the <see cref="TimeService"/> class.
        /// </summary>
        /// <param name="httpClientFactory">Factory to create HttpClient instances.</param>
        /// <param name="urlSettingsOptions">Options accessor for URL settings.</param>
        public TimeService(IHttpClientFactory httpClientFactory, IOptions<UrlSettings> urlSettingsOptions)
        {
            // Create an HttpClient instance using the factory (best practice for managing HttpClient lifetime)
            _httpClient = httpClientFactory.CreateClient("DefaultClient");
            // Get the configured URL settings
            _urlSettings = urlSettingsOptions.Value;
        }

        /// <summary>
        /// Asynchronously fetches the current time and date from the configured web service URL.
        /// </summary>
        /// <returns>
        /// A task that represents the asynchronous operation.
        /// The task result contains a tuple with the time and date strings,
        /// or null if the operation fails or the URL is not configured.
        /// </returns>
        public async Task<(string Time, string Date)?> GetSofiaTimeAsync()
        {
            // Check if the URL for the time service is configured in settings
            if (string.IsNullOrEmpty(_urlSettings.TimeServiceUrl))
            {
                // Log an error if the URL is missing
                Logger.LogError("TimeServiceUrl is not configured.");
                return null; // Indicate failure due to configuration issue
            }

            try
            {
                // Log the attempt to fetch data
                Logger.Log($"Fetching time from {_urlSettings.TimeServiceUrl}");
                // Perform an asynchronous GET request to the specified URL
                // ConfigureAwait(false) helps avoid potential deadlocks in certain contexts
                string html = await _httpClient.GetStringAsync(_urlSettings.TimeServiceUrl).ConfigureAwait(false);

                // Initialize HtmlAgilityPack document
                var htmlDoc = new HtmlDocument();
                // Load the fetched HTML string into the document
                htmlDoc.LoadHtml(html);

                // Use XPath to select the HTML node containing the time (identified by id='ct')
                // Prefer IDs for selectors as they are usually more stable than classes or structure
                var timeNode = htmlDoc.DocumentNode.SelectSingleNode("//span[@id='ct']");
                // Extract the inner text, trim whitespace, or use a default message if not found
                string time = timeNode?.InnerText.Trim() ?? "Time not found";

                // Use XPath to select the HTML node containing the date (identified by id='ctdat')
                var dateNode = htmlDoc.DocumentNode.SelectSingleNode("//span[@id='ctdat']");
                // Extract the inner text, trim whitespace, or use a default message if not found
                string date = dateNode?.InnerText.Trim() ?? "Date not found";

                // Check if either the time or date could not be found
                if (time == "Time not found" || date == "Date not found")
                {
                    // Log a warning if elements weren't found, suggesting the page structure might have changed
                    Logger.LogWarning($"Could not find time/date elements on {_urlSettings.TimeServiceUrl}. Page structure might have changed.");
                }
                else
                {
                    // Log success if both time and date were retrieved
                    Logger.Log($"Successfully retrieved time: {time}, date: {date}");
                }

                // Return the extracted time and date as a tuple
                return (time, date);
            }
            catch (HttpRequestException ex) // Catch errors related to the HTTP request itself (e.g., network issues, DNS errors, invalid URL)
            {
                // Log the HTTP error details
                Logger.LogError($"HTTP error fetching time from {_urlSettings.TimeServiceUrl}", ex);
                // Output a user-friendly message to the console
                Console.WriteLine($"Error fetching time: {ex.Message}"); // Keep user feedback
                return null; // Indicate failure
            }
            catch (HtmlWebException ex) // Catch errors specific to HtmlAgilityPack during parsing
            {
                // Log the HTML parsing error details
                Logger.LogError($"HTML parsing error fetching time from {_urlSettings.TimeServiceUrl}", ex);
                // Output a user-friendly message to the console
                Console.WriteLine($"Error parsing time page: {ex.Message}");
                return null; // Indicate failure
            }
            catch (Exception ex) // Catch any other unexpected errors
            {
                // Log the unexpected error details
                Logger.LogError($"Unexpected error fetching time from {_urlSettings.TimeServiceUrl}", ex);
                // Output a generic error message to the console
                Console.WriteLine($"An unexpected error occurred: {ex.Message}");
                return null; // Indicate failure
            }
        }
    }
}