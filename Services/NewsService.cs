using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web;
using HtmlAgilityPack;
using Microsoft.Extensions.Options;
using Upr_2.Models;
using Upr_2.Settings;

namespace Upr_2.Services
{
    public class NewsService
    {
        private List<NewsArticle> _favoriteArticles;
        private readonly string _favoritesFilePath;
        private readonly string _newsDirectory;
        private readonly HttpClient _httpClient;
        private readonly UrlSettings _urlSettings;
        private static readonly HashSet<string> CovidKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "covid-19", "covid", "коронавирус", "пандемия", "ковид"
        };

        public NewsService(IHttpClientFactory httpClientFactory, IOptions<UrlSettings> urlSettingsOptions)
        {
            _httpClient = httpClientFactory.CreateClient("DefaultClient"); // Use named or typed client
            _urlSettings = urlSettingsOptions.Value;

            // Use AppData or UserProfile for better practice than BaseDirectory for user data
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string appSpecificFolder = Path.Combine(appDataPath, "Upr2ConsoleApp"); // Choose a suitable name
            Directory.CreateDirectory(appSpecificFolder);

            _favoritesFilePath = Path.Combine(appSpecificFolder, "favorites.json");
            _newsDirectory = Path.Combine(appSpecificFolder, "news_reports");
            Directory.CreateDirectory(_newsDirectory);

            _favoriteArticles = LoadFavorites();
        }

        private List<NewsArticle> LoadFavorites()
        {
            try
            {
                if (File.Exists(_favoritesFilePath))
                {
                    string json = File.ReadAllText(_favoritesFilePath);
                    return JsonSerializer.Deserialize<List<NewsArticle>>(json) ?? new List<NewsArticle>();
                }
            }
            catch (JsonException ex)
            {
                Logger.LogError($"Error deserializing favorites file: {_favoritesFilePath}", ex);
                // Consider renaming corrupted file and starting fresh
            }
            catch (IOException ex)
            {
                Logger.LogError($"Error reading favorites file: {_favoritesFilePath}", ex);
            }
            catch (Exception ex)
            {
                Logger.LogError("Unexpected error loading favorites", ex);
            }
            return new List<NewsArticle>(); // Return empty list on failure
        }

        private void SaveFavorites()
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(_favoriteArticles, options);
                File.WriteAllText(_favoritesFilePath, json);
            }
            catch (IOException ex)
            {
                Logger.LogError($"Error writing favorites file: {_favoritesFilePath}", ex);
            }
            catch (Exception ex)
            {
                Logger.LogError("Unexpected error saving favorites", ex);
            }
        }

        public void AddToFavorites(NewsArticle article)
        {
            // Use Any with equality check based on URL (or use HashSet for performance)
            if (!_favoriteArticles.Any(a => a.Url == article.Url))
            {
                article.IsFavorite = true; // Update state of the passed article if needed
                                           // Create a copy if necessary to avoid modifying the original list's instance if it came from elsewhere
                _favoriteArticles.Add(article);
                SaveFavorites();
                Logger.Log($"Article added to favorites: {article.Title}");
            }
            else
            {
                Logger.Log($"Article already in favorites: {article.Title}");
            }
        }

        public void RemoveFromFavorites(NewsArticle article)
        {
            var existingArticle = _favoriteArticles.FirstOrDefault(a => a.Url == article.Url);
            if (existingArticle != null)
            {
                _favoriteArticles.Remove(existingArticle);
                // article.IsFavorite = false; // Optionally update the state of the *input* article
                SaveFavorites();
                Logger.Log($"Article removed from favorites: {article.Title}");
            }
            else
            {
                Logger.Log($"Article not found in favorites for removal: {article.Title}");
            }
        }

        public List<NewsArticle> GetFavoriteArticles()
        {
            // Return a copy to prevent external modification of the internal list
            return new List<NewsArticle>(_favoriteArticles);
        }

        public async Task<List<NewsArticle>> ScrapeMediapoolNewsAsync()
        {
            var articles = new List<NewsArticle>();
            var web = new HtmlWeb();
            string targetUrl = _urlSettings.NewsServiceUrl; // Use configured URL

            if (string.IsNullOrEmpty(targetUrl))
            {
                Logger.LogError("NewsServiceUrl is not configured in UrlSettings.");
                throw new InvalidOperationException("News service URL is not configured.");
            }

            try
            {
                Logger.Log($"Starting scrape of {targetUrl}");
                // Use HttpClient for loading, potentially more efficient than HtmlWeb's internal loading
                // var html = await _httpClient.GetStringAsync(targetUrl);
                // var doc = new HtmlDocument();
                // doc.LoadHtml(html);
                // Or stick with HtmlWeb if preferred:
                var doc = await web.LoadFromWebAsync(targetUrl).ConfigureAwait(false);
                Logger.Log("Successfully loaded the webpage content.");

                // Refined selectors (prefer classes/IDs, less brittle)
                // Combine multiple selectors using comma in XPath
                var articleNodes = doc.DocumentNode.SelectNodes(
                    "//div[contains(@class, 'leading-news')]//article | " +
                    "//div[contains(@class, 'regular-news')]//article | " +
                    "//div[contains(@class, 'latest-news')]//article"
                    ) ?? Enumerable.Empty<HtmlNode>(); // Handle null case gracefully

                if (!articleNodes.Any())
                {
                    // Broader search as fallback
                    articleNodes = doc.DocumentNode.SelectNodes("//article") ?? Enumerable.Empty<HtmlNode>();
                    if (articleNodes.Any())
                    {
                        Logger.LogWarning("Primary selectors failed, using fallback //article selector.");
                    }
                }

                int processedCount = 0;
                int skippedCovidCount = 0;
                int skippedDuplicateCount = 0;
                var uniqueUrls = new HashSet<string>(); // Track URLs to avoid duplicates from overlapping selectors

                foreach (var node in articleNodes)
                {
                    NewsArticle? article = await ProcessArticleNodeAsync(node, web).ConfigureAwait(false); // Pass HtmlWeb instance
                    if (article != null)
                    {
                        if (uniqueUrls.Add(article.Url)) // Check for duplicates
                        {
                            if (ContainsCovid19Keywords(article.Title))
                            {
                                skippedCovidCount++;
                            }
                            else
                            {
                                article.IsFavorite = _favoriteArticles.Any(fav => fav.Url == article.Url);
                                articles.Add(article);
                            }
                        }
                        else
                        {
                            skippedDuplicateCount++;
                        }
                    }
                    processedCount++;
                }

                Logger.Log($"Scraping finished. Processed: {processedCount}, Added: {articles.Count}, Skipped (COVID): {skippedCovidCount}, Skipped (Duplicate): {skippedDuplicateCount}.");

                if (articles.Count == 0 && processedCount > 0)
                {
                    Logger.LogWarning("Processed article nodes but extracted 0 valid news items. Website structure might have changed.");
                }
                else if (articles.Count == 0 && processedCount == 0)
                {
                    Logger.LogWarning("No article nodes found on the page. Website structure might have changed significantly.");
                    Console.WriteLine("Debug info:"); // Keep console output for immediate feedback during scraping issues
                    Console.WriteLine($"Page title: {doc.DocumentNode.SelectSingleNode("//title")?.InnerText}");
                    Console.WriteLine($"Total <article> tags found: {doc.DocumentNode.SelectNodes("//article")?.Count ?? 0}");
                }


                await StoreNewsArticlesAsHtmlAsync(articles).ConfigureAwait(false);

                return articles;
            }
            catch (HttpRequestException ex)
            {
                Logger.LogError($"HTTP error accessing {targetUrl}", ex);
                throw new Exception($"Failed to access Mediapool news: {ex.Message}", ex); // Wrap original exception
            }
            catch (HtmlWebException ex)
            {
                Logger.LogError($"HTML Agility Pack error accessing {targetUrl}", ex);
                throw new Exception($"Failed to parse Mediapool news page: {ex.Message}", ex);
            }
            catch (Exception ex) // Catch unexpected errors
            {
                Logger.LogError($"Unexpected error during news scraping from {targetUrl}", ex);
                throw; // Re-throw unexpected exceptions
            }
        }

        // Consider making this async if HtmlWeb.Load becomes a bottleneck
        private async Task<NewsArticle?> ProcessArticleNodeAsync(HtmlNode node, HtmlWeb web)
        {
            try
            {
                // Simpler selector for the link/title first
                var linkNode = node.SelectSingleNode(".//h2/a | .//h3/a | .//a[contains(@class,'title')] | .//a");
                if (linkNode == null) return null; // Skip if no link found

                string relativeUrl = linkNode.GetAttributeValue("href", "").Trim();
                if (string.IsNullOrEmpty(relativeUrl)) return null;

                // Construct absolute URL
                Uri baseUri = new Uri(_urlSettings.NewsServiceUrl);
                Uri absoluteUri = new Uri(baseUri, relativeUrl);
                string url = absoluteUri.ToString();

                // *** Efficiency Consideration ***
                // Loading the full article page here for metadata is SLOW.
                // If possible, get metadata (author, date, category) from the LISTING page (node).
                // If not possible, this remains the only way.

                // Attempt to get data from the listing node first (Example - adjust selectors)
                string titleFromList = linkNode.InnerText?.Trim() ?? "Untitled";
                string dateFromList = node.SelectSingleNode(".//time")?.Attributes["datetime"]?.Value ??
                                      node.SelectSingleNode(".//span[contains(@class,'date')]")?.InnerText?.Trim() ??
                                      DateTime.Now.ToString("yyyy-MM-dd HH:mm"); // Fallback date
                string categoryFromList = node.SelectSingleNode(".//a[contains(@class,'category')]")?.InnerText?.Trim() ?? "Други"; // Example selector


                // Decide whether to load the full page (e.g., if author is needed and not on list page)
                bool loadFullPage = true; // Set to true to keep original behavior, false to try list data first
                string title, categoryText, dateTime, author;

                if (loadFullPage)
                {
                    // Load the article page (Original behavior - potentially slow)
                    var articleDoc = await web.LoadFromWebAsync(url).ConfigureAwait(false);

                    // Use more robust selectors if possible, fallback to specific ones
                    var titleNode = articleDoc.DocumentNode.SelectSingleNode("//h1[contains(@class,'c-heading')]") ??
                                    articleDoc.DocumentNode.SelectSingleNode("//h1"); // Broader H1
                    title = HttpUtility.HtmlDecode(titleNode?.InnerText?.Trim() ?? titleFromList); // Use list title as fallback

                    var categoryNode = articleDoc.DocumentNode.SelectSingleNode("//nav[contains(@class,'breadcrumb')]//a[position()=2]") ?? // Try breadcrumb
                                       articleDoc.DocumentNode.SelectSingleNode("//a[contains(@class,'article-category')]"); // Try specific class
                    categoryText = HttpUtility.HtmlDecode(categoryNode?.InnerText?.Trim() ?? categoryFromList);

                    // Selector refinement for date and author
                    var dateNode = articleDoc.DocumentNode.SelectSingleNode("//time[@datetime]")?.Attributes["datetime"]?.Value ?? // Prefer <time> tag
                                  articleDoc.DocumentNode.SelectSingleNode("//*[contains(@class,'article__timestamp')] | //*[contains(@class,'article__date')] | //*[contains(@class,'u-highlight-insignificant')]")?.InnerText?.Trim();
                    dateTime = dateNode ?? dateFromList;

                    var authorNode = articleDoc.DocumentNode.SelectSingleNode("//*[contains(@class,'c-article__author')] | //a[contains(@rel,'author')]");
                    author = HttpUtility.HtmlDecode(authorNode?.InnerText?.Trim() ?? string.Empty);
                }
                else
                {
                    // Use data extracted from the listing page (Potentially faster)
                    title = HttpUtility.HtmlDecode(titleFromList);
                    categoryText = HttpUtility.HtmlDecode(categoryFromList);
                    dateTime = dateFromList;
                    author = string.Empty; // Assume author not available on list page
                }


                // Create article object
                return new NewsArticle(title, dateTime, url, categoryText, string.IsNullOrWhiteSpace(author) ? null : author);

            }
            catch (HttpRequestException ex)
            {
                Logger.LogError($"HTTP error processing article node: {ex.Message}", ex);
                return null; // Skip this article on error
            }
            catch (HtmlWebException ex)
            {
                Logger.LogError($"HTML Agility Pack error processing article node: {ex.Message}", ex);
                return null;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Unexpected error processing article node: {ex.Message}", ex);
                return null; // Skip on unexpected errors
            }
        }


        private static bool ContainsCovid19Keywords(string title)
        {
            // Check if any keyword exists in the title
            return CovidKeywords.Any(keyword => title.Contains(keyword, StringComparison.OrdinalIgnoreCase));
        }

        // Store articles to HTML file
        private async Task StoreNewsArticlesAsHtmlAsync(List<NewsArticle> articles)
        {
            if (articles == null || !articles.Any())
            {
                Logger.Log("No articles to store in HTML file.");
                return;
            }

            string fileName = $"articles_{DateTime.Now:yyyyMMdd_HHmmss}.html";
            string filePath = Path.Combine(_newsDirectory, fileName);
            var htmlBuilder = new StringBuilder();

            try
            {
                // --- HTML Header ---
                htmlBuilder.AppendLine("<!DOCTYPE html>");
                htmlBuilder.AppendLine("<html lang=\"bg\"><head>"); // Added lang attribute
                htmlBuilder.AppendLine("<meta charset='UTF-8'>");
                htmlBuilder.AppendLine("<meta name='viewport' content='width=device-width, initial-scale=1.0'>");
                htmlBuilder.AppendLine("<title>Новини от Mediapool</title>"); // Added Title
                htmlBuilder.AppendLine("<style>");
                // --- CSS (Keep as is or externalize) ---
                // (Your existing CSS goes here - Use @"" string literal for multiline)
                htmlBuilder.AppendLine(@"
                :root {
                    --font-family: 'Inter', sans-serif;
                    --bg-color: #121212; /* Darker background */
                    --card-bg: #1e1e1e; /* Slightly lighter card */
                    --text-primary: #e0e0e0; /* Off-white */
                    --text-secondary: #a0a0a0; /* Grey */
                    --accent-color: #007bff; /* Vibrant blue */
                    --accent-hover: #0056b3;
                    --favorite-color: #ffc107; /* Amber */
                    --category-color: #dc3545; /* Red */
                    --border-color: rgba(255, 255, 255, 0.1);
                    --shadow-color: rgba(0, 0, 0, 0.5);
                    --card-border-radius: 16px;
                    --button-border-radius: 25px;
                    --transition-speed: 0.3s;
                }

                :root[data-theme='light'] {
                    --bg-color: #f8f9fa;
                    --card-bg: #ffffff;
                    --text-primary: #212529;
                    --text-secondary: #6c757d;
                    --accent-color: #007bff;
                    --accent-hover: #0056b3;
                    --border-color: rgba(0, 0, 0, 0.1);
                    --shadow-color: rgba(0, 0, 0, 0.1);
                }

                *, *::before, *::after {
                    box-sizing: border-box;
                }

                body {
                    font-family: var(--font-family);
                    background-color: var(--bg-color);
                    color: var(--text-primary);
                    margin: 0;
                    padding: 0; /* Remove default padding */
                    line-height: 1.6;
                    transition: background-color var(--transition-speed) ease, color var(--transition-speed) ease;
                    overflow-x: hidden; /* Prevent horizontal scroll */
                }

                .container {
                    max-width: 1400px; /* Wider container */
                    margin: 0 auto;
                    padding: 0 20px; /* Padding on sides */
                }

                /* --- Sticky Header --- */
                .header {
                    position: sticky;
                    top: 0;
                    background-color: var(--bg-color);
                    z-index: 1000;
                    padding: 20px 0;
                    border-bottom: 1px solid var(--border-color);
                    box-shadow: 0 2px 10px var(--shadow-color);
                    transition: all var(--transition-speed) ease;
                    transform: translateY(0);
                }

                .header.hidden {
                    transform: translateY(-100%);
                }

                .header-content {
                     max-width: 1400px;
                     margin: 0 auto;
                     padding: 0 20px;
                     display: flex;
                     flex-direction: column;
                     align-items: center;
                     gap: 15px;
                }

                /* Category expansion button */
                .category-expand-btn {
                    position: absolute;
                    right: 0;
                    top: 50%;
                    transform: translateY(-50%);
                    background: linear-gradient(90deg, transparent, var(--bg-color) 30%);
                    padding: 8px 15px;
                    border: none;
                    color: var(--text-primary);
                    cursor: pointer;
                    display: none; /* Hidden by default */
                    font-size: 1.2em;
                    z-index: 2;
                    transition: all var(--transition-speed) ease;
                }

                .category-expand-btn:hover {
                    color: var(--accent-color);
                }

                .category-filters-container {
                    width: 100%;
                    position: relative;
                    overflow: hidden;
                    max-height: 60px; /* Height for single row */
                    transition: max-height var(--transition-speed) ease;
                }

                .category-filters {
                    display: flex;
                    gap: 10px;
                    justify-content: flex-start;
                    margin: 10px 0 0 0;
                    transition: all var(--transition-speed) ease;
                    padding: 5px 30px 5px 0; /* Added right padding for button */
                    overflow-x: auto;
                    scrollbar-width: none; /* Firefox */
                    -ms-overflow-style: none; /* IE/Edge */
                    flex-wrap: nowrap; /* Default to single line */
                }

                .category-filters.expanded {
                    flex-wrap: wrap;
                }

                .category-filters.expanded + .category-expand-btn {
                    transform: translateY(-50%) rotate(90deg);
                }

                .category-filters-container:has(.category-filters.expanded) {
                    max-height: 200px; /* Increased height when expanded */
                    overflow-y: auto;
                }

                .header.scrolled {
                    padding: 10px 0;
                    background-color: color-mix(in srgb, var(--bg-color) 95%, black); /* Slightly darker scrolled */
                }

                 .header h1 {
                     margin: 0;
                     color: var(--text-primary);
                     font-size: clamp(1.8em, 4vw, 2.5em); /* Responsive font size */
                     font-weight: 700;
                     transition: all var(--transition-speed) ease;
                 }

                .datetime {
                    color: var(--text-secondary);
                    font-size: 0.9em;
                    transition: all var(--transition-speed) ease;
                    /* Hide when scrolled */
                    max-height: 50px; /* Allow space */
                    opacity: 1;
                }

                .header.scrolled .datetime {
                    max-height: 0;
                    opacity: 0;
                    margin: 0;
                    padding: 0;
                    overflow: hidden;
                }


                /* --- Controls within Header --- */
                 .controls-container {
                    width: 100%;
                    display: flex;
                    flex-wrap: wrap;
                    justify-content: space-between; /* Space out controls */
                    align-items: center;
                    gap: 15px;
                     transition: all var(--transition-speed) ease;
                 }

                .search-container {
                     flex-grow: 1; /* Allow search to take space */
                     min-width: 250px; /* Minimum width */
                     max-width: 400px; /* Maximum width */
                }

                .search-box {
                    width: 100%; /* Fill container */
                    padding: 12px 20px;
                    border: 1px solid var(--border-color);
                    border-radius: var(--button-border-radius);
                    background-color: var(--card-bg);
                    color: var(--text-primary);
                    font-size: 1em;
                    transition: all var(--transition-speed) ease;
                }

                .search-box:focus {
                    outline: none;
                    box-shadow: 0 0 0 3px color-mix(in srgb, var(--accent-color) 30%, transparent);
                    border-color: var(--accent-color);
                }

                 .theme-toggle {
                    background-color: var(--card-bg);
                    color: var(--text-primary);
                    border: 1px solid var(--border-color);
                    padding: 10px 15px; /* Adjust padding */
                    border-radius: 50%; /* Make it round */
                    cursor: pointer;
                    font-size: 1.2em; /* Slightly larger icon */
                    transition: all var(--transition-speed) ease;
                    line-height: 1; /* Ensure icon centers */
                    width: 44px; /* Fixed size */
                    height: 44px;
                    display: flex;
                    justify-content: center;
                    align-items: center;
                }

                .theme-toggle:hover {
                    transform: scale(1.1) rotate(15deg);
                    border-color: var(--accent-color);
                    color: var(--accent-color);
                    box-shadow: 0 0 10px var(--shadow-color);
                }


                 /* --- Category Filters --- */
                 .category-filters-container {
                     width: 100%;
                     position: relative;
                 }

                .category-filters {
                    display: flex;
                    gap: 10px;
                    justify-content: flex-start;
                    margin: 10px 0 0 0;
                    transition: all var(--transition-speed) ease;
                    padding: 5px 0;
                }


                .category-filter {
                    background-color: var(--card-bg);
                    color: var(--text-secondary);
                    border: 1px solid var(--border-color);
                    padding: 8px 18px;
                    border-radius: var(--button-border-radius);
                    cursor: pointer;
                    font-size: 0.9em;
                    font-weight: 500;
                    white-space: nowrap;
                    transition: all var(--transition-speed) ease;
                }

                .category-filter:hover {
                    color: var(--text-primary);
                    border-color: var(--accent-color);
                    transform: translateY(-2px);
                }


                .category-filter.active {
                    background-color: var(--accent-color);
                    color: #ffffff;
                    border-color: var(--accent-color);
                    font-weight: 600;
                    box-shadow: 0 2px 5px var(--shadow-color);
                }

                /* --- Articles Grid --- */
                .articles-grid {
                    display: grid;
                    /* grid-template-columns: repeat(auto-fill, minmax(350px, 1fr)); */
                    grid-template-columns: repeat(auto-fill, minmax(min(100%, 400px), 1fr)); /* More robust responsive columns */
                    gap: 30px; /* Increased gap */
                    padding: 40px 0; /* More vertical padding */
                    transition: opacity var(--transition-speed) ease; /* Fade during filter */
                }

                /* --- Article Card Styling --- */
                .article {
                    background-color: var(--card-bg);
                    border-radius: var(--card-border-radius);
                    border: 1px solid var(--border-color);
                    box-shadow: 0 4px 15px var(--shadow-color);
                    transition: all 0.4s cubic-bezier(0.25, 0.8, 0.25, 1); /* Smoother transition */
                    display: flex;
                    flex-direction: column;
                    /* Removed fixed height, let content define height */
                    position: relative;
                    overflow: hidden; /* Needed for pseudo-elements */
                    padding: 30px; /* More padding */
                }

                 .article:hover {
                    transform: translateY(-8px) scale(1.01); /* Lift and slightly scale */
                    box-shadow: 0 10px 25px var(--shadow-color);
                    border-color: var(--accent-color);
                 }

                 /* Highlight Bar Effect */
                 .article::before {
                     content: '';
                     position: absolute;
                     top: 0;
                     left: 0;
                     width: 100%;
                     height: 6px; /* Thicker bar */
                     background: linear-gradient(90deg, var(--accent-color), color-mix(in srgb, var(--accent-color) 60%, var(--category-color)));
                     opacity: 0;
                     transform: scaleX(0);
                     transform-origin: left;
                     transition: opacity 0.4s ease, transform 0.5s cubic-bezier(0.25, 0.8, 0.25, 1);
                 }

                 .article:hover::before {
                     opacity: 1;
                     transform: scaleX(1);
                 }


                 .article.is-favorite {
                     border-left: 5px solid var(--favorite-color); /* Use left border for fav */
                     background: color-mix(in srgb, var(--card-bg) 90%, var(--favorite-color)); /* Subtle background tint */
                 }
                 .article.is-favorite:hover {
                    border-color: var(--favorite-color); /* Keep favorite border color on hover */
                 }
                 .article.is-favorite::before {
                     background: linear-gradient(90deg, var(--favorite-color), var(--accent-color)); /* Favorite gradient */
                 }


                 .title {
                     color: var(--text-primary);
                     font-size: clamp(1.2em, 2vw, 1.4em); /* Responsive Title */
                     font-weight: 600;
                     margin: 0 0 20px 0; /* Only bottom margin */
                     line-height: 1.4; /* Adjust line height */
                     letter-spacing: -0.01em;
                     display: flex; /* Align emoji */
                     align-items: flex-start;
                     gap: 10px;
                 }
                 .title .emoji {
                     font-size: 1.2em; /* Control emoji size relative to text */
                     line-height: 1.4;
                 }

                 .metadata {
                     display: flex;
                     flex-wrap: wrap;
                     gap: 12px; /* Adjust gap */
                     margin: 0 0 25px 0; /* Adjust margins */
                     color: var(--text-secondary);
                     font-size: 0.85em;
                 }

                .metadata-item {
                    display: inline-flex; /* Use inline-flex */
                    align-items: center;
                    gap: 6px;
                    padding: 5px 12px;
                    background-color: color-mix(in srgb, var(--card-bg) 50%, var(--bg-color)); /* Mix background colors */
                    border-radius: 15px;
                    border: 1px solid var(--border-color);
                    font-weight: 500;
                    transition: all var(--transition-speed) ease;
                }
                .metadata-item .emoji { /* Style emoji within metadata */
                     font-size: 1.1em;
                     opacity: 0.8;
                }

                .metadata-item:hover {
                     background-color: color-mix(in srgb, var(--accent-color) 15%, var(--card-bg));
                     color: var(--text-primary);
                     border-color: var(--accent-color);
                     transform: translateY(-1px);
                }

                 .url-container {
                     margin-top: auto; /* Push to bottom */
                     padding-top: 20px; /* Space above button */
                     border-top: 1px solid var(--border-color);
                     text-align: center; /* Center button */
                 }

                .url-button {
                    display: inline-flex;
                    align-items: center;
                    justify-content: center;
                    gap: 10px;
                    background-color: var(--accent-color);
                    color: #ffffff;
                    padding: 12px 30px; /* More padding */
                    border-radius: var(--button-border-radius);
                    text-decoration: none;
                    transition: all var(--transition-speed) ease;
                    font-size: 0.95em;
                    font-weight: 600;
                    letter-spacing: 0.02em;
                    border: none;
                    box-shadow: 0 4px 10px color-mix(in srgb, var(--shadow-color) 50%, transparent);
                }

                .url-button:hover {
                    background-color: var(--accent-hover);
                    transform: translateY(-3px);
                    box-shadow: 0 6px 15px color-mix(in srgb, var(--shadow-color) 70%, transparent);
                }
                 .url-button .emoji {
                     font-size: 1.2em;
                 }

                 .favorite-indicator {
                     position: absolute;
                     top: 15px; /* Adjust position */
                     right: 20px;
                     font-size: 1.8em;
                     color: var(--favorite-color);
                     opacity: 0; /* Hidden by default */
                     transition: all var(--transition-speed) ease;
                     z-index: 1;
                     text-shadow: 0 1px 3px var(--shadow-color);
                     transform: scale(0.8);
                 }

                 .article.is-favorite .favorite-indicator {
                     opacity: 0.8; /* Slightly transparent */
                     transform: scale(1);
                     animation: pulse 2.5s infinite ease-in-out;
                 }
                 .article:hover .favorite-indicator { /* Show on hover too if favorite */
                      opacity: 1;
                 }


                @keyframes pulse {
                    0% { transform: scale(1); opacity: 0.8; }
                    50% { transform: scale(1.15); opacity: 1; }
                    100% { transform: scale(1); opacity: 0.8; }
                }

                 /* --- Loading & No Results --- */
                 .status-message {
                     text-align: center;
                     padding: 60px 20px;
                     color: var(--text-secondary);
                     font-size: 1.2em;
                     font-weight: 500;
                     display: none; /* Hide by default */
                 }
                 .status-message.active { display: block; }

                 .loading-spinner {
                     width: 40px;
                     height: 40px;
                     border: 4px solid var(--border-color);
                     border-top-color: var(--accent-color); /* Only top colored */
                     border-radius: 50%;
                     animation: spin 1s linear infinite;
                     margin: 20px auto; /* Center spinner */
                 }

                @keyframes spin {
                    to { transform: rotate(360deg); }
                }

                /* --- Responsive Adjustments --- */
                @media (max-width: 768px) {
                    .container { padding: 0 15px; }
                    .header { padding: 15px 0; }
                    .header.scrolled { padding: 8px 0; }

                    .controls-container {
                        flex-direction: column; /* Stack controls */
                        align-items: stretch; /* Stretch controls full width */
                    }
                    .search-container { max-width: none; }
                    .theme-toggle {
                        position: absolute; /* Position top right */
                        top: 15px;
                        right: 15px;
                    }
                    .header.scrolled .theme-toggle { top: 8px; right: 15px; }


                    .category-filters { justify-content: flex-start; } /* Ensure scroll starts left */

                    .articles-grid {
                        grid-template-columns: 1fr; /* Single column */
                        gap: 25px;
                        padding: 30px 0;
                    }

                    .article { padding: 25px; }
                    .title { font-size: 1.2em; }
                    .metadata { font-size: 0.8em; }
                    .url-button { padding: 10px 25px; font-size: 0.9em; }
                }

                 @media (max-width: 480px) {
                     .header h1 { font-size: 1.5em; }
                     .datetime { font-size: 0.8em; }
                     .search-box { padding: 10px 15px; font-size: 0.9em; }
                     .category-filter { padding: 6px 14px; font-size: 0.85em; }
                     .article { padding: 20px; }
                     .title { font-size: 1.1em; }
                 }
                ");
                htmlBuilder.AppendLine("</style>");
                htmlBuilder.AppendLine("</head>");
                htmlBuilder.AppendLine("<body data-theme='dark'>"); // Default theme
                htmlBuilder.AppendLine("<div class='container'>");

                // --- Page Header ---
                htmlBuilder.AppendLine("<header class='header'>");
                htmlBuilder.AppendLine("<div class='header-content'>"); // Inner container for content alignment

                // --- Controls ---
                htmlBuilder.AppendLine("<div class='controls-container'>");
                htmlBuilder.AppendLine("<div class='search-container'>");
                htmlBuilder.AppendLine("<input type='text' id='search' class='search-box' placeholder='Търсене в заглавия...' aria-label='Search news titles'>");
                htmlBuilder.AppendLine("</div>");
                htmlBuilder.AppendLine("<button id='theme-toggle' class='theme-toggle' aria-label='Switch to light theme'>☀️</button>"); // Initial icon for dark->light
                htmlBuilder.AppendLine("</div>"); // end controls-container

                // --- Category Filters ---
                htmlBuilder.AppendLine("<div class='category-filters-container'>"); // Wrapper for scrolling
                htmlBuilder.AppendLine("<div class='category-filters'>");
                htmlBuilder.AppendLine("<button class='category-filter active' data-category='all'>Всички</button>");
                var uniqueCategories = articles.Select(a => a.Category).Distinct().OrderBy(c => c);
                foreach (var category in uniqueCategories)
                {
                    // Find first article of this category to get the emoji
                    var emoji = articles.FirstOrDefault(a => a.Category == category)?.CategoryEmoji ?? "❓";
                    // Use HtmlEncode for category name in data attribute and display
                    htmlBuilder.AppendLine($"<button class='category-filter' data-category='{HttpUtility.HtmlEncode(category)}'>{emoji} {HttpUtility.HtmlEncode(category)}</button>");
                }
                htmlBuilder.AppendLine("</div>"); // end category-filters
                htmlBuilder.AppendLine("<button class='category-expand-btn' aria-label='Show more categories'>→</button>");
                htmlBuilder.AppendLine("</div>"); // end category-filters-container

                htmlBuilder.AppendLine("</div>"); // end header-content
                htmlBuilder.AppendLine("</header>"); // end header

                // --- Loading/No Results ---
                // Combine loading and no-results into one status area
                htmlBuilder.AppendLine("<div id='status-message' class='status-message'>");
                htmlBuilder.AppendLine("  <div id='loading-indicator' style='display: none;'><div class='loading-spinner'></div>Зареждане...</div>");
                htmlBuilder.AppendLine("  <div id='no-results-message' style='display: none;'>Няма намерени новини, отговарящи на критериите.</div>");
                htmlBuilder.AppendLine("</div>");


                // --- Articles Grid ---
                htmlBuilder.AppendLine("<main id='articles-grid' class='articles-grid'>");

                if (!articles.Any())
                {
                    // Handled by JS now, but could add a server-side message if needed
                }
                else
                {
                    foreach (var article in articles) // Already sorted
                    {
                        string favoriteClass = article.IsFavorite ? "is-favorite" : "";
                        // Use HtmlEncode for category/title in data attributes
                        htmlBuilder.AppendLine($"<article class='article {favoriteClass}' data-category='{HttpUtility.HtmlEncode(article.Category)}' data-title='{HttpUtility.HtmlEncode(article.Title.ToLowerInvariant())}'>");

                        htmlBuilder.AppendLine("<div class='favorite-indicator' aria-hidden='true'>★</div>");

                        htmlBuilder.AppendLine($"<h2 class='title'><span class='emoji' aria-hidden='true'>{article.CategoryEmoji}</span> {HttpUtility.HtmlEncode(article.Title)}</h2>");

                        htmlBuilder.AppendLine("<div class='metadata'>");
                        htmlBuilder.AppendLine($"<div class='metadata-item'><span class='emoji' aria-hidden='true'>📅</span> {article.FormattedDate} {article.FormattedTime}</div>");
                        htmlBuilder.AppendLine($"<div class='metadata-item'><span class='emoji' aria-hidden='true'>📌</span> {HttpUtility.HtmlEncode(article.Category)}</div>");
                        if (!string.IsNullOrEmpty(article.Author))
                        {
                            htmlBuilder.AppendLine($"<div class='metadata-item'><span class='emoji' aria-hidden='true'>✍️</span> {HttpUtility.HtmlEncode(article.Author)}</div>");
                        }
                        htmlBuilder.AppendLine($"<div class='metadata-item'><span class='emoji' aria-hidden='true'>📊</span> {article.WordCount} думи</div>");
                        htmlBuilder.AppendLine("</div>"); // end metadata

                        htmlBuilder.AppendLine("<div class='url-container'>");
                        htmlBuilder.AppendLine($"<a href='{article.Url}' target='_blank' rel='noopener noreferrer' class='url-button'><span class='emoji' aria-hidden='true'>🔗</span> Прочети повече</a>");
                        htmlBuilder.AppendLine("</div>");

                        htmlBuilder.AppendLine("</article>");
                    }
                }
                htmlBuilder.AppendLine("</main>");
                htmlBuilder.AppendLine("</div>"); // end container

                // --- JavaScript ---
                htmlBuilder.AppendLine("<script>");
                // --- Enhanced JavaScript ---
                htmlBuilder.AppendLine(@"
                    document.addEventListener('DOMContentLoaded', function() {
                        const header = document.querySelector('.header');
                        const themeToggle = document.getElementById('theme-toggle');
                        const root = document.documentElement;
                        const searchBox = document.getElementById('search');
                        const articlesContainer = document.getElementById('articles-grid');
                        const articles = Array.from(articlesContainer.querySelectorAll('.article'));
                        const statusMessageContainer = document.getElementById('status-message');
                        const loadingIndicator = document.getElementById('loading-indicator');
                        const noResultsMessage = document.getElementById('no-results-message');
                        const categoryFiltersContainer = document.querySelector('.category-filters-container');
                        const categoryFilters = categoryFiltersContainer.querySelector('.category-filters');
                        const expandButton = document.querySelector('.category-expand-btn');
                        let activeCategory = 'all';
                        let searchTimeout;
                        let isFiltering = false;
                        let lastScrollY = window.scrollY;
                        let isExpanded = false;

                        // --- Header Scroll Hide/Show ---
                        function handleScroll() {
                            const currentScrollY = window.scrollY;
                            const scrollingDown = currentScrollY > lastScrollY;
                            const scrollOffset = 100; // Minimum scroll before hiding

                            if (scrollingDown && currentScrollY > scrollOffset) {
                                header.classList.add('hidden');
                            } else {
                                header.classList.remove('hidden');
                            }

                            lastScrollY = currentScrollY;
                        }

                        window.addEventListener('scroll', handleScroll, { passive: true });

                        // --- Category Expansion ---
                        function checkCategoryOverflow() {
                            if (!categoryFilters || !categoryFiltersContainer) return;
                            
                            const filtersWidth = categoryFilters.scrollWidth;
                            const containerWidth = categoryFiltersContainer.clientWidth;
                            
                            if (filtersWidth > containerWidth && !isExpanded) {
                                expandButton.style.display = 'block';
                            } else {
                                expandButton.style.display = isExpanded ? 'block' : 'none';
                            }
                        }

                        if (expandButton) {
                            expandButton.addEventListener('click', () => {
                                isExpanded = !isExpanded;
                                categoryFilters.classList.toggle('expanded');
                                expandButton.textContent = isExpanded ? '↑' : '→';
                                expandButton.setAttribute('aria-label', 
                                    isExpanded ? 'Show less categories' : 'Show more categories'
                                );
                                checkCategoryOverflow(); // Recheck after expanding
                            });

                            // Check overflow on load and resize
                            window.addEventListener('resize', checkCategoryOverflow);
                            // Initial check after a small delay to ensure proper layout
                            setTimeout(checkCategoryOverflow, 100);
                        }

                        // --- Initial Setup ---
                        function setupInitialTheme() {
                            const savedTheme = localStorage.getItem('theme') || 'dark'; // Default dark
                            root.setAttribute('data-theme', savedTheme);
                            updateThemeButton(savedTheme);
                        }

                        function updateThemeButton(theme) {
                             if (theme === 'light') {
                                 themeToggle.textContent = '🌙'; // Show moon icon in light mode
                                 themeToggle.setAttribute('aria-label', 'Switch to dark theme');
                             } else {
                                 themeToggle.textContent = '☀️'; // Show sun icon in dark mode
                                 themeToggle.setAttribute('aria-label', 'Switch to light theme');
                             }
                        }

                        setupInitialTheme(); // Set theme on load

                        // --- Theme Toggle ---
                        themeToggle.addEventListener('click', () => {
                            const currentTheme = root.getAttribute('data-theme');
                            const newTheme = currentTheme === 'light' ? 'dark' : 'light';
                            root.setAttribute('data-theme', newTheme);
                            updateThemeButton(newTheme);
                            localStorage.setItem('theme', newTheme); // Persist preference
                        });


                         // --- Search Functionality (Debounced) ---
                         searchBox.addEventListener('input', () => {
                             clearTimeout(searchTimeout);
                             // Only filter if not already filtering
                             if (!isFiltering) {
                                 searchTimeout = setTimeout(filterArticles, 300); // Debounce input slightly longer
                             }
                         });


                         // --- Category Filters (Event Delegation) ---
                         categoryFiltersContainer.addEventListener('click', (event) => {
                             const target = event.target;
                             if (target.classList.contains('category-filter') && !target.classList.contains('active')) {
                                 categoryFiltersContainer.querySelector('.active')?.classList.remove('active');
                                 target.classList.add('active');
                                 activeCategory = target.dataset.category || 'all';
                                 // Only filter if not already filtering
                                 if (!isFiltering) {
                                     filterArticles();
                                 }
                             }
                         });


                         // --- Core Filtering Logic ---
                         function filterArticles() {
                             if (isFiltering) return; // Prevent overlap
                             isFiltering = true;

                             // Show loading indicator (optional, might be too fast to notice)
                             // statusMessageContainer.classList.add('active');
                             // loadingIndicator.style.display = 'block';
                             // noResultsMessage.style.display = 'none';
                             // articlesContainer.style.opacity = '0.5'; // Dim container during filter

                             const searchTerm = searchBox.value.toLowerCase().trim();
                             let visibleCount = 0;

                             // Use requestAnimationFrame for smoother visual updates
                             requestAnimationFrame(() => {
                                 articles.forEach(article => {
                                     const title = article.dataset.title || '';
                                     const category = article.dataset.category || '';
                                     const matchesSearch = searchTerm === '' || title.includes(searchTerm);
                                     const matchesCategory = activeCategory === 'all' || category === activeCategory;

                                     if (matchesSearch && matchesCategory) {
                                         // Use 'grid' or 'flex' depending on your display setup, '' resets to stylesheet default
                                         article.style.display = '';
                                         visibleCount++;
                                     } else {
                                         article.style.display = 'none';
                                     }
                                 });

                                 // Update status message visibility
                                 if (visibleCount === 0) {
                                     statusMessageContainer.classList.add('active');
                                     noResultsMessage.style.display = 'block';
                                     loadingIndicator.style.display = 'none'; // Hide loader if no results
                                 } else {
                                     statusMessageContainer.classList.remove('active');
                                     noResultsMessage.style.display = 'none';
                                     loadingIndicator.style.display = 'none'; // Hide loader on success
                                 }

                                 // Restore container opacity
                                 // articlesContainer.style.opacity = '1';

                                 isFiltering = false; // Allow next filter
                            });
                         }

                         // Initial check in case page loaded with filters/search pre-filled
                         // Also handles the case where there were 0 articles initially
                         if (articles.length === 0) {
                            statusMessageContainer.classList.add('active');
                            noResultsMessage.style.display = 'block';
                         } else {
                             filterArticles(); // Run initial filter if articles exist
                         }
                    });
                 ");
                htmlBuilder.AppendLine("</script>");
                htmlBuilder.AppendLine("</body></html>");

                // --- Write to File ---
                await File.WriteAllTextAsync(filePath, htmlBuilder.ToString(), Encoding.UTF8).ConfigureAwait(false);

                Logger.Log($"Successfully stored {articles.Count} articles to HTML report: {filePath}");

                // --- Open File ---
                TryOpenFile(filePath);

            }
            catch (IOException ex)
            {
                Logger.LogError($"Error writing HTML report to {filePath}", ex);
                Console.WriteLine($"[!] Грешка при запис на HTML отчет: {ex.Message}"); // User feedback
            }
            catch (Exception ex)
            {
                Logger.LogError("Unexpected error generating HTML report", ex);
                Console.WriteLine($"[!] Неочаквана грешка при генериране на HTML отчет: {ex.Message}"); // User feedback
            }
        }

        private void TryOpenFile(string filePath)
        {
            try
            {
                var processStartInfo = new ProcessStartInfo(filePath)
                {
                    UseShellExecute = true
                };
                Process.Start(processStartInfo);
                Logger.Log($"Attempted to open HTML report in default browser: {filePath}");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error opening HTML report '{filePath}' in browser.", ex);
                // Provide user feedback in the console
                Console.WriteLine($"[!] Не можа автоматично да се отвори HTML отчета.");
                Console.WriteLine($"    Можете да го намерите тук: {filePath}");
            }
        }
    }
}