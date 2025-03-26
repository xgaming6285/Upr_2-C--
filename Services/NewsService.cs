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
                    --bg-color: #1a1a1a;
                    --card-bg: #2d2d2d;
                    --text-primary: #ffffff;
                    --text-secondary: #b3b3b3;
                    --accent-color: #3498db;
                    --favorite-color: #f1c40f;
                    --category-color: #e74c3c;
                }

                :root[data-theme='light'] {
                    --bg-color: #f5f5f5;
                    --card-bg: #ffffff;
                    --text-primary: #333333;
                    --text-secondary: #666666;
                    --accent-color: #2980b9;
                }

                body { 
                    font-family: 'Segoe UI', Arial, sans-serif;
                    background-color: var(--bg-color);
                    color: var(--text-primary);
                    margin: 0;
                    padding: 20px;
                    line-height: 1.6;
                    transition: background-color 0.3s ease, color 0.3s ease;
                }

                .container {
                    max-width: 1200px;
                    margin: 0 auto;
                }

                .header {
                    text-align: center;
                    padding: 20px;
                    border-bottom: 1px solid rgba(255, 255, 255, 0.1);
                    position: sticky;
                    top: 0;
                    background-color: var(--bg-color);
                    z-index: 100;
                    box-shadow: 0 2px 10px rgba(0, 0, 0, 0.1);
                    transition: all 0.3s ease;
                }

                .header.scrolled {
                    padding: 10px;
                }

                .header h1 {
                    margin: 0;
                    color: var(--text-primary);
                    font-size: 2.5em;
                    transition: all 0.3s ease;
                }

                .datetime {
                    margin-top: 10px;
                    transition: all 0.3s ease;
                }

                .collapsible-content {
                    transition: all 0.3s ease;
                    overflow: hidden;
                }

                .header.scrolled .collapsible-content {
                    height: 0;
                    opacity: 0;
                    margin: 0;
                    padding: 0;
                }

                .controls {
                    display: flex;
                    justify-content: center;
                    gap: 20px;
                    margin: 20px 0;
                    flex-wrap: wrap;
                    transition: all 0.3s ease;
                }

                .header.scrolled .controls {
                    margin: 0;
                }

                .search-container {
                    position: relative;
                    z-index: 101;
                    transition: all 0.3s ease;
                }

                .search-box {
                    padding: 10px 15px;
                    border: 2px solid var(--accent-color);
                    border-radius: 25px;
                    width: 300px;
                    background-color: var(--card-bg);
                    color: var(--text-primary);
                    font-size: 16px;
                    transition: all 0.3s ease;
                }

                .search-box:focus {
                    outline: none;
                    box-shadow: 0 0 10px rgba(52, 152, 219, 0.3);
                    border-color: var(--accent-color);
                }

                .header.scrolled .search-box {
                    width: 200px;
                    padding: 8px 12px;
                    font-size: 14px;
                }

                .theme-toggle {
                    background-color: var(--accent-color);
                    color: white;
                    border: none;
                    padding: 10px 20px;
                    border-radius: 25px;
                    cursor: pointer;
                    font-size: 16px;
                    transition: all 0.3s ease;
                }

                .theme-toggle:hover {
                    transform: translateY(-2px);
                    box-shadow: 0 4px 8px rgba(0, 0, 0, 0.2);
                }

                .category-filters {
                    display: flex;
                    gap: 10px;
                    flex-wrap: wrap;
                    justify-content: center;
                    margin: 20px 0;
                }

                .category-filter {
                    background-color: var(--card-bg);
                    color: var(--text-primary);
                    border: 2px solid var(--accent-color);
                    padding: 8px 15px;
                    border-radius: 20px;
                    cursor: pointer;
                    transition: all 0.3s ease;
                }

                .category-filter.active {
                    background-color: var(--accent-color);
                    color: white;
                }

                .articles-grid {
                    display: grid;
                    grid-template-columns: repeat(auto-fill, minmax(350px, 1fr));
                    gap: 30px;
                    margin-top: 40px;
                    padding: 20px;
                }

                .article { 
                    margin: 0;
                    padding: 25px;
                    background-color: var(--card-bg);
                    border-radius: 20px;
                    box-shadow: 0 8px 16px rgba(0, 0, 0, 0.1);
                    transition: all 0.4s cubic-bezier(0.4, 0, 0.2, 1);
                    display: flex;
                    flex-direction: column;
                    height: 100%;
                    position: relative;
                    overflow: hidden;
                    border: 1px solid rgba(255, 255, 255, 0.1);
                    backdrop-filter: blur(10px);
                    -webkit-backdrop-filter: blur(10px);
                }

                .article:hover {
                    transform: translateY(-8px);
                    box-shadow: 0 12px 24px rgba(0, 0, 0, 0.15);
                    border-color: var(--accent-color);
                }

                .article::before {
                    content: '';
                    position: absolute;
                    top: 0;
                    left: 0;
                    width: 100%;
                    height: 4px;
                    background: linear-gradient(90deg, var(--accent-color), var(--category-color));
                    opacity: 0;
                    transition: opacity 0.4s ease;
                }

                .article:hover::before {
                    opacity: 1;
                }

                .article.is-favorite {
                    background: linear-gradient(145deg, var(--card-bg), rgba(52, 152, 219, 0.1));
                    border-color: var(--favorite-color);
                }

                .title { 
                    color: var(--text-primary);
                    font-size: 1.5em;
                    font-weight: 600;
                    margin-bottom: 20px;
                    line-height: 1.5;
                    padding: 15px;
                    border-radius: 12px;
                    background-color: rgba(0, 0, 0, 0.05);
                    border-left: 4px solid var(--accent-color);
                    letter-spacing: -0.02em;
                }

                .metadata {
                    display: flex;
                    flex-wrap: wrap;
                    gap: 15px;
                    margin: 20px 0;
                    padding: 15px;
                    background-color: rgba(0, 0, 0, 0.03);
                    border-radius: 12px;
                    backdrop-filter: blur(5px);
                    -webkit-backdrop-filter: blur(5px);
                }

                .metadata-item {
                    display: flex;
                    align-items: center;
                    gap: 8px;
                    padding: 6px 12px;
                    background-color: rgba(255, 255, 255, 0.1);
                    border-radius: 20px;
                    font-size: 0.95em;
                    box-shadow: 0 2px 4px rgba(0, 0, 0, 0.05);
                    transition: all 0.3s ease;
                }

                .metadata-item:hover {
                    transform: translateY(-2px);
                    box-shadow: 0 4px 8px rgba(0, 0, 0, 0.1);
                }

                .url-container {
                    margin-top: auto;
                    padding-top: 20px;
                    border-top: 1px solid rgba(255, 255, 255, 0.1);
                    display: flex;
                    justify-content: center;
                }

                .url-button {
                    display: inline-flex;
                    align-items: center;
                    gap: 10px;
                    background-color: var(--accent-color);
                    color: white;
                    padding: 12px 25px;
                    border-radius: 25px;
                    text-decoration: none;
                    transition: all 0.4s cubic-bezier(0.4, 0, 0.2, 1);
                    font-size: 1em;
                    width: auto;
                    min-width: 220px;
                    justify-content: center;
                    box-shadow: 0 4px 8px rgba(0, 0, 0, 0.15);
                    font-weight: 500;
                    letter-spacing: 0.02em;
                }

                .url-button:hover {
                    background-color: #2980b9;
                    transform: translateY(-3px);
                    box-shadow: 0 6px 12px rgba(0, 0, 0, 0.2);
                }

                .favorite-indicator {
                    position: absolute;
                    top: 20px;
                    right: 20px;
                    font-size: 1.8em;
                    color: var(--favorite-color);
                    opacity: 0.6;
                    transition: all 0.4s ease;
                    z-index: 1;
                    text-shadow: 0 2px 4px rgba(0, 0, 0, 0.2);
                    transform-origin: center;
                }

                .article.is-favorite .favorite-indicator {
                    opacity: 1;
                    animation: pulse 2.5s infinite;
                }

                @keyframes pulse {
                    0% { transform: scale(1); }
                    50% { transform: scale(1.15); }
                    100% { transform: scale(1); }
                }

                @media (max-width: 768px) {
                    .articles-grid {
                        grid-template-columns: 1fr;
                        gap: 25px;
                        padding: 15px;
                    }
                    
                    .article {
                        padding: 20px;
                    }
                    
                    .title {
                        font-size: 1.3em;
                        padding: 12px;
                    }
                    
                    .metadata {
                        padding: 12px;
                        gap: 12px;
                    }
                    
                    .url-button {
                        width: 100%;
                        padding: 10px 20px;
                    }
                }

                .loading {
                    display: none;
                    justify-content: center;
                    align-items: center;
                    margin: 20px 0;
                }

                .loading.active {
                    display: flex;
                }

                .loading-spinner {
                    width: 40px;
                    height: 40px;
                    border: 4px solid var(--accent-color);
                    border-top: 4px solid transparent;
                    border-radius: 50%;
                    animation: spin 1s linear infinite;
                }

                @keyframes spin {
                    0% { transform: rotate(0deg); }
                    100% { transform: rotate(360deg); }
                }

                .no-results {
                    text-align: center;
                    padding: 40px;
                    color: var(--text-secondary);
                    font-size: 1.2em;
                    display: none;
                }
                ");
                htmlBuilder.AppendLine("</style>");
                htmlBuilder.AppendLine("</head><body data-theme='dark'>"); // Default to dark theme
                htmlBuilder.AppendLine("<div class='container'>");

                 // --- Page Header ---
                htmlBuilder.AppendLine("<header class='header'>"); // Use semantic header
                htmlBuilder.AppendLine("<div class='collapsible-content'>");
                htmlBuilder.AppendLine($"<h1>📰 Новини от Mediapool</h1>");
                htmlBuilder.AppendLine($"<div class='datetime'>Извлечено на: {DateTime.Now:dd MMMM yyyy HH:mm:ss}</div>");
                htmlBuilder.AppendLine("</div>"); // end collapsible-content

                // --- Controls ---
                htmlBuilder.AppendLine("<div class='controls'>");
                htmlBuilder.AppendLine("<div class='search-container'>");
                htmlBuilder.AppendLine("<input type='text' id='search' class='search-box' placeholder='Търсене на новини...'>");
                htmlBuilder.AppendLine("</div>"); // end search-container
                 // Theme toggle should ideally be outside collapsible part if header shrinks
                htmlBuilder.AppendLine("<button id='theme-toggle' class='theme-toggle' aria-label='Switch theme'>🌙</button>"); // Use icon/aria-label

                htmlBuilder.AppendLine("</div>"); // end controls

                // --- Category Filters ---
                htmlBuilder.AppendLine("<div class='category-filters collapsible-content'>"); // Wrap filters too
                htmlBuilder.AppendLine("<button class='category-filter active' data-category='all'>Всички</button>");
                var uniqueCategories = articles.Select(a => a.Category).Distinct().OrderBy(c => c);
                foreach (var category in uniqueCategories)
                {
                    var emoji = articles.First(a => a.Category == category).CategoryEmoji;
                    // Use HtmlEncode for category name in data attribute just in case
                    htmlBuilder.AppendLine($"<button class='category-filter' data-category='{HttpUtility.HtmlEncode(category)}'>{emoji} {HttpUtility.HtmlEncode(category)}</button>");
                }
                htmlBuilder.AppendLine("</div>"); // end category-filters
                htmlBuilder.AppendLine("</header>"); // end header

                 // --- Loading/No Results ---
                htmlBuilder.AppendLine("<div class='loading'><div class='loading-spinner'></div></div>");
                htmlBuilder.AppendLine("<div class='no-results'>Няма намерени новини, отговарящи на критериите.</div>");

                // --- Articles Grid ---
                htmlBuilder.AppendLine("<main id='articles-grid' class='articles-grid'>"); // Use semantic main

                foreach (var article in articles.OrderByDescending(a => a.RawDateTime)) // Order here if needed
                {
                    string favoriteClass = article.IsFavorite ? "is-favorite" : "";
                    // Use HtmlEncode for category in data attribute
                    htmlBuilder.AppendLine($"<article class='article {favoriteClass}' data-category='{HttpUtility.HtmlEncode(article.Category)}' data-title='{HttpUtility.HtmlEncode(article.Title.ToLowerInvariant())}'>"); // Add data-title for easier JS filtering

                    // Favorite indicator
                    htmlBuilder.AppendLine("<div class='favorite-indicator' aria-hidden='true'>★</div>"); // Hide from screen readers

                    // Title
                    htmlBuilder.AppendLine($"<h2 class='title'><span class='emoji' aria-hidden='true'>{article.CategoryEmoji}</span> {HttpUtility.HtmlEncode(article.Title)}</h2>"); // Use H2 for article titles

                    // Metadata
                    htmlBuilder.AppendLine("<div class='metadata'>");
                    htmlBuilder.AppendLine($"<div class='metadata-item'><span class='emoji' aria-hidden='true'>📅</span> {article.FormattedDate} {article.FormattedTime}</div>"); // Combine Date/Time
                    htmlBuilder.AppendLine($"<div class='metadata-item'><span class='emoji' aria-hidden='true'>📌</span> {HttpUtility.HtmlEncode(article.Category)}</div>");
                    if (!string.IsNullOrEmpty(article.Author))
                    {
                        htmlBuilder.AppendLine($"<div class='metadata-item'><span class='emoji' aria-hidden='true'>✍️</span> {HttpUtility.HtmlEncode(article.Author)}</div>");
                    }
                     htmlBuilder.AppendLine($"<div class='metadata-item'><span class='emoji' aria-hidden='true'>📊</span> {article.WordCount} думи</div>"); // Show word count
                    htmlBuilder.AppendLine("</div>"); // end metadata

                    // URL button
                    htmlBuilder.AppendLine("<div class='url-container'>");
                    // Added rel="noopener noreferrer" for security when using target="_blank"
                    htmlBuilder.AppendLine($"<a href='{article.Url}' target='_blank' rel='noopener noreferrer' class='url-button'><span class='emoji' aria-hidden='true'>🔗</span> Прочети повече</a>");
                    htmlBuilder.AppendLine("</div>"); // end url-container

                    htmlBuilder.AppendLine("</article>"); // end article
                }

                htmlBuilder.AppendLine("</main>"); // end articles-grid
                htmlBuilder.AppendLine("</div>"); // end container

                // --- JavaScript ---
                htmlBuilder.AppendLine("<script>");
                // (Your existing JS goes here - Use @"" string literal)
                 htmlBuilder.AppendLine(@"
                    document.addEventListener('DOMContentLoaded', function() {
                        const header = document.querySelector('.header');
                        const themeToggle = document.getElementById('theme-toggle');
                        const root = document.documentElement;
                        const searchBox = document.getElementById('search');
                        const articlesContainer = document.getElementById('articles-grid');
                        const articles = articlesContainer.querySelectorAll('.article'); // Select only articles
                        const noResults = document.querySelector('.no-results');
                        const categoryFiltersContainer = document.querySelector('.category-filters');
                        let activeCategory = 'all';
                        let lastScrollTop = 0;

                        // Initial theme setup based on preference/storage
                        const savedTheme = localStorage.getItem('theme') || 'dark'; // Default to dark
                        root.setAttribute('data-theme', savedTheme);
                        themeToggle.textContent = savedTheme === 'light' ? '🌙' : '☀️'; // Update button icon
                        themeToggle.setAttribute('aria-label', `Switch to ${savedTheme === 'light' ? 'dark' : 'light'} theme`);


                        // Header scroll behavior
                        window.addEventListener('scroll', () => {
                           const scrollTop = window.pageYOffset || document.documentElement.scrollTop;
                            if (scrollTop > 100) { // Threshold to shrink header
                                header.classList.add('scrolled');
                            } else {
                                header.classList.remove('scrolled');
                            }
                           lastScrollTop = scrollTop <= 0 ? 0 : scrollTop; // For Mobile or negative scrolling
                        }, { passive: true }); // Improve scroll performance

                        // Theme toggle
                        themeToggle.addEventListener('click', () => {
                            const currentTheme = root.getAttribute('data-theme');
                            const newTheme = currentTheme === 'light' ? 'dark' : 'light';
                            root.setAttribute('data-theme', newTheme);
                            themeToggle.textContent = newTheme === 'light' ? '🌙' : '☀️'; // Update icon
                            themeToggle.setAttribute('aria-label', `Switch to ${newTheme === 'light' ? 'dark' : 'light'} theme`);
                            localStorage.setItem('theme', newTheme); // Persist preference
                        });

                        // Search functionality (Debounced for performance)
                         let searchTimeout;
                         searchBox.addEventListener('input', () => {
                             clearTimeout(searchTimeout);
                             searchTimeout = setTimeout(filterArticles, 250); // Debounce input
                         });


                        // Category filters (Event Delegation)
                        categoryFiltersContainer.addEventListener('click', (event) => {
                            if (event.target.classList.contains('category-filter')) {
                                categoryFiltersContainer.querySelector('.active')?.classList.remove('active');
                                event.target.classList.add('active');
                                activeCategory = event.target.dataset.category || 'all';
                                filterArticles();
                            }
                        });

                        function filterArticles() {
                             const searchTerm = searchBox.value.toLowerCase().trim();
                             let visibleCount = 0;

                             articles.forEach(article => {
                                 const title = article.dataset.title || ''; // Use data-title
                                 const category = article.dataset.category || '';
                                 const matchesSearch = searchTerm === '' || title.includes(searchTerm);
                                 const matchesCategory = activeCategory === 'all' || category === activeCategory;

                                 if (matchesSearch && matchesCategory) {
                                     article.style.display = ''; // Show using CSS grid rules
                                     visibleCount++;
                                 } else {
                                     article.style.display = 'none'; // Hide
                                 }
                             });

                             noResults.style.display = visibleCount === 0 ? 'block' : 'none';
                         }

                         // Initial filter call in case search box has value on load (e.g., back button)
                         filterArticles();
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
                // Optionally inform the user the report couldn't be saved
            }
            catch (Exception ex)
            {
                Logger.LogError("Unexpected error generating HTML report", ex);
                 // Optionally inform the user
            }
        }

        private void TryOpenFile(string filePath)
        {
             try
             {
                 // UseShellExecute is required to open files with default application
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
                 Console.WriteLine($"Could not automatically open the report file.");
                 Console.WriteLine($"You can find it at: {filePath}");
             }
        }

        // Removed NewsArticleComparer as NewsArticle now overrides Equals/GetHashCode
    }
}