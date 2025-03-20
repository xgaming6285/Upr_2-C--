using System;
using System.Net.Http;
using System.Threading.Tasks;
using HtmlAgilityPack;

namespace Upr_2
{
    public class TimeService
    {
        private static readonly HttpClient client = new HttpClient();
        private const string TIME_URL = "https://www.timeanddate.com/worldclock/bulgaria/sofia";

        public async Task<(string time, string date)> GetSofiaTimeAsync()
        {
            try
            {
                Logger.Log($"Fetching time from {TIME_URL}");
                string html = await client.GetStringAsync(TIME_URL);
                var htmlDoc = new HtmlDocument();
                htmlDoc.LoadHtml(html);

                // Get the time
                var timeNode = htmlDoc.DocumentNode.SelectSingleNode("//span[@id='ct']");
                string time = timeNode?.InnerText.Trim() ?? "Time not found";

                // Get the date
                var dateNode = htmlDoc.DocumentNode.SelectSingleNode("//span[@id='ctdat']");
                string date = dateNode?.InnerText.Trim() ?? "Date not found";

                Logger.Log($"Successfully retrieved time: {time}, date: {date}");
                return (time, date);
            }
            catch (Exception ex)
            {
                Logger.LogError("Error fetching time from external service", ex);
                Console.WriteLine($"Error fetching time: {ex.Message}");
                return ("Error", "Error");
            }
        }
    }
}