using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HtmlAgilityPack;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.IO;
using System.Web;
using System.Diagnostics;

namespace Upr_2
{
    public class NewsArticle
    {
        public string Title { get; set; }
        public string FormattedDate { get; set; }
        public string FormattedTime { get; set; }
        public string Category { get; set; }
        public string CategoryEmoji { get; set; }
        public string Url { get; set; }
        public bool IsFavorite { get; set; }
        public int WordCount { get; set; }
        public string? Author { get; set; }

        public NewsArticle(string title, string dateTime, string url)
        {
            Title = title;
            Url = url;
            IsFavorite = false;

            // Calculate formatted date and time
            if (System.DateTime.TryParse(dateTime, out var dt))
            {
                FormattedDate = dt.ToString("dd MMMM yyyy");
                FormattedTime = dt.ToString("HH:mm");
            }
            else
            {
                FormattedDate = dateTime;
                FormattedTime = "";
            }

            // Calculate word count
            WordCount = title.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Length;

            // Determine category and emoji from URL
            (Category, CategoryEmoji) = DetermineCategory(url);
        }

        private (string category, string emoji) DetermineCategory(string url)
        {
            // Convert URL to lower case for case-insensitive matching
            url = url.ToLower();

            // World news
            if (url.Contains("/svyat") || url.Contains("/world") || url.Contains("world-") ||
                url.Contains("-world") || url.Contains("international"))
                return ("Свят", "🌍");

            // Bulgaria news
            if (url.Contains("/bulgaria") || url.Contains("bulgaria-") || url.Contains("-bulgaria") ||
                url.Contains("/bg") || url.Contains("-bg-"))
                return ("България", "🇧🇬");

            // Sports news
            if (url.Contains("/sport") || url.Contains("sport-") || url.Contains("-sport") ||
                url.Contains("football") || url.Contains("olympics"))
                return ("Спорт", "⚽");

            // Business news
            if (url.Contains("/business") || url.Contains("business-") || url.Contains("-business") ||
                url.Contains("economy") || url.Contains("finance") || url.Contains("-byudzhet") ||
                url.Contains("byudzhet-") || url.Contains("pari"))
                return ("Бизнес", "💼");

            // Culture news
            if (url.Contains("/culture") || url.Contains("culture-") || url.Contains("-culture") ||
                url.Contains("art") || url.Contains("music") || url.Contains("cinema") ||
                url.Contains("theatre") || url.Contains("kultura"))
                return ("Култура", "🎭");

            // Politics news
            if (url.Contains("politik") || url.Contains("parliament") || url.Contains("government") ||
                url.Contains("election") || url.Contains("-izbor") || url.Contains("izbori-"))
                return ("Политика", "⚖️");

            // Technology news
            if (url.Contains("tech") || url.Contains("technology") || url.Contains("digital") ||
                url.Contains("software") || url.Contains("hardware") || url.Contains("ai"))
                return ("Технологии", "💻");

            // Health news
            if (url.Contains("health") || url.Contains("medicine") || url.Contains("covid") ||
                url.Contains("hospital") || url.Contains("zdrave"))
                return ("Здраве", "🏥");

            // Default category
            return ("Други", "📰");
        }
    }

    public class NewsService
    {
        private List<NewsArticle> _favoriteArticles;
        private readonly string _favoritesFilePath;
        private readonly string _newsDirectory;

        public NewsService()
        {
            _favoriteArticles = new List<NewsArticle>();
            _favoritesFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "favorites.json");
            _newsDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "news");

            // Ensure news directory exists
            Directory.CreateDirectory(_newsDirectory);
            LoadFavorites();
        }

        private void LoadFavorites()
        {
            try
            {
                if (File.Exists(_favoritesFilePath))
                {
                    string json = File.ReadAllText(_favoritesFilePath);
                    _favoriteArticles = JsonSerializer.Deserialize<List<NewsArticle>>(json) ?? new List<NewsArticle>();
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Error loading favorites", ex);
                _favoriteArticles = new List<NewsArticle>();
            }
        }

        private void SaveFavorites()
        {
            try
            {
                string json = JsonSerializer.Serialize(_favoriteArticles, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_favoritesFilePath, json);
            }
            catch (Exception ex)
            {
                Logger.LogError("Error saving favorites", ex);
            }
        }

        public void AddToFavorites(NewsArticle article)
        {
            if (!_favoriteArticles.Any(a => a.Url == article.Url))
            {
                article.IsFavorite = true;
                _favoriteArticles.Add(article);
                SaveFavorites();
            }
        }

        public void RemoveFromFavorites(NewsArticle article)
        {
            var existingArticle = _favoriteArticles.FirstOrDefault(a => a.Url == article.Url);
            if (existingArticle != null)
            {
                _favoriteArticles.Remove(existingArticle);
                article.IsFavorite = false;
                SaveFavorites();
            }
        }

        public List<NewsArticle> GetFavoriteArticles()
        {
            return _favoriteArticles;
        }

        public async Task<List<NewsArticle>> ScrapeMediapoolNews()
        {
            var articles = new List<NewsArticle>();
            var web = new HtmlWeb();

            try
            {
                string url = "https://www.mediapool.bg/";
                var doc = await web.LoadFromWebAsync(url);
                Console.WriteLine("Successfully loaded the webpage");

                // Try multiple selectors for articles
                var articleNodes = new List<HtmlNode>();

                // Main news section
                var mainNewsNodes = doc.DocumentNode.SelectNodes("//div[contains(@class, 'leading-news')]//article") ??
                                  doc.DocumentNode.SelectNodes("//div[contains(@class, 'leading')]//article");
                if (mainNewsNodes != null)
                {
                    articleNodes.AddRange(mainNewsNodes);
                }

                // Regular news section
                var regularNewsNodes = doc.DocumentNode.SelectNodes("//div[contains(@class, 'regular-news')]//article") ??
                                     doc.DocumentNode.SelectNodes("//div[contains(@class, 'regular')]//article");
                if (regularNewsNodes != null)
                {
                    articleNodes.AddRange(regularNewsNodes);
                }

                // Latest news section
                var latestNewsNodes = doc.DocumentNode.SelectNodes("//div[contains(@class, 'latest-news')]//article");
                if (latestNewsNodes != null)
                {
                    articleNodes.AddRange(latestNewsNodes);
                }

                // If still no articles found, try a broader search
                if (!articleNodes.Any())
                {
                    var allArticles = doc.DocumentNode.SelectNodes("//article");
                    if (allArticles != null)
                    {
                        articleNodes.AddRange(allArticles);
                    }
                }

                foreach (var node in articleNodes)
                {
                    ProcessArticleNode(node, articles);
                }

                Console.WriteLine($"Found {articles.Count} articles");

                if (articles.Count == 0)
                {
                    Console.WriteLine("Debug info:");
                    Console.WriteLine($"Page title: {doc.DocumentNode.SelectSingleNode("//title")?.InnerText}");
                    Console.WriteLine($"Total <article> tags found: {doc.DocumentNode.SelectNodes("//article")?.Count ?? 0}");
                }

                // Update IsFavorite status for scraped articles
                foreach (var article in articles)
                {
                    article.IsFavorite = _favoriteArticles.Any(a => a.Url == article.Url);
                }

                // After successful scraping, store the articles
                await StoreNewsArticles(articles);

                return articles;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error accessing mediapool.bg: {ex.Message}", ex);
                throw;
            }
        }

        private void ProcessArticleNode(HtmlNode node, List<NewsArticle> articles)
        {
            try
            {
                // Try multiple selectors for title
                var titleNode = node.SelectSingleNode(".//a[@class='news-title']") ??
                              node.SelectSingleNode(".//h2/a") ??
                              node.SelectSingleNode(".//h3/a") ??
                              node.SelectSingleNode(".//a[contains(@class, 'title')]") ??
                              node.SelectSingleNode(".//a");

                // Try multiple selectors for datetime
                var dateTimeNode = node.SelectSingleNode(".//div[@class='date-time']") ??
                                 node.SelectSingleNode(".//time") ??
                                 node.SelectSingleNode(".//*[contains(@class, 'date')]");

                if (titleNode != null)
                {
                    string title = titleNode.InnerText.Trim();
                    string url = titleNode.GetAttributeValue("href", "").Trim();

                    // If URL is relative, make it absolute
                    if (!string.IsNullOrEmpty(url) && !url.StartsWith("http"))
                    {
                        url = "https://www.mediapool.bg" + (url.StartsWith("/") ? url : "/" + url);
                    }

                    string dateTime = dateTimeNode?.InnerText?.Trim() ??
                                    DateTime.Now.ToString("yyyy-MM-dd HH:mm");

                    // Extract author if present (usually after the last space followed by timestamp)
                    string? author = null;
                    var authorMatch = System.Text.RegularExpressions.Regex.Match(title, @"(?:\s+|^)([А-Я][а-я]+(?:\s+[А-Я][а-я]+)+)(?:\s+Вчера\s+\d{2}:\d{2}|\s+\d{2}\.\d{2}\.\d{4}\s+\d{2}:\d{2}|\s+\d{2}:\d{2})?$");
                    if (authorMatch.Success)
                    {
                        author = authorMatch.Groups[1].Value.Trim();
                        title = title.Substring(0, title.Length - authorMatch.Value.Length).Trim();
                    }

                    // Remove timestamp patterns from title
                    var timestampPatterns = new[] {
                        @"\s+Вчера\s+\d{2}:\d{2}$",
                        @"\s+\d{2}\.\d{2}\.\d{4}\s+\d{2}:\d{2}$",
                        @"\s+\d{2}:\d{2}$"
                    };
                    foreach (var pattern in timestampPatterns)
                    {
                        title = System.Text.RegularExpressions.Regex.Replace(title, pattern, "");
                    }

                    if (!ContainsCovid19Keywords(title))
                    {
                        var article = new NewsArticle(title, dateTime, url);
                        article.Author = author;
                        article.IsFavorite = _favoriteArticles.Any(a => a.Url == url);
                        articles.Add(article);
                        Console.WriteLine($"Added article: {title}");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error processing an article: {ex.Message}", ex);
            }
        }

        private bool ContainsCovid19Keywords(string title)
        {
            var keywords = new[]
            {
                "covid-19",
                "covid",
                "коронавирус",
                "пандемия",
                "ковид"
            };

            return keywords.Any(keyword =>
                title.Contains(keyword, StringComparison.OrdinalIgnoreCase));
        }

        private async Task StoreNewsArticles(List<NewsArticle> articles)
        {
            try
            {
                // Create a filename with current date and time
                string fileName = $"articles_{DateTime.Now:yyyyMMdd_HHmmss}.html";
                string filePath = Path.Combine(_newsDirectory, fileName);

                using (StreamWriter writer = new StreamWriter(filePath, false, System.Text.Encoding.UTF8))
                {
                    // Write HTML header
                    await writer.WriteLineAsync("<!DOCTYPE html>");
                    await writer.WriteLineAsync("<html><head>");
                    await writer.WriteLineAsync("<meta charset='UTF-8'>");
                    await writer.WriteLineAsync("<meta name='viewport' content='width=device-width, initial-scale=1.0'>");
                    await writer.WriteLineAsync("<style>");
                    await writer.WriteLineAsync(@"
                        :root {
                            --bg-color: #1a1a1a;
                            --card-bg: #2d2d2d;
                            --text-primary: #ffffff;
                            --text-secondary: #b3b3b3;
                            --accent-color: #3498db;
                            --favorite-color: #f1c40f;
                            --category-color: #e74c3c;
                        }
                        
                        body { 
                            font-family: 'Segoe UI', Arial, sans-serif;
                            background-color: var(--bg-color);
                            color: var(--text-primary);
                            margin: 0;
                            padding: 20px;
                            line-height: 1.6;
                        }

                        .container {
                            max-width: 1200px;
                            margin: 0 auto;
                        }

                        .header {
                            text-align: center;
                            margin-bottom: 30px;
                            padding: 20px;
                            border-bottom: 1px solid #444;
                        }

                        .header h1 {
                            margin: 0;
                            color: var(--text-primary);
                            font-size: 2.5em;
                        }

                        .article { 
                            margin: 20px 0;
                            padding: 20px;
                            background-color: var(--card-bg);
                            border-radius: 10px;
                            box-shadow: 0 4px 6px rgba(0, 0, 0, 0.3);
                            transition: transform 0.2s ease, box-shadow 0.2s ease;
                        }

                        .article:hover {
                            transform: translateY(-5px);
                            box-shadow: 0 6px 12px rgba(0, 0, 0, 0.4);
                        }

                        .title { 
                            color: var(--text-primary);
                            font-size: 1.4em;
                            font-weight: bold;
                            margin-bottom: 15px;
                            line-height: 1.4;
                        }

                        .datetime { 
                            color: var(--text-secondary);
                            font-size: 0.9em;
                            margin: 10px 0;
                        }

                        .author { 
                            color: var(--text-secondary);
                            font-size: 0.9em;
                            margin: 10px 0;
                        }

                        .category { 
                            color: var(--category-color);
                            font-size: 0.95em;
                            margin: 10px 0;
                        }

                        .favorite { 
                            color: var(--favorite-color);
                            font-size: 0.95em;
                            margin: 10px 0;
                        }

                        .url { 
                            margin: 15px 0 5px 0;
                        }

                        .url a {
                            color: var(--accent-color);
                            text-decoration: none;
                            transition: color 0.2s ease;
                            word-break: break-all;
                        }

                        .url a:hover {
                            color: #2980b9;
                            text-decoration: underline;
                        }

                        .emoji {
                            font-size: 1.2em;
                            margin-right: 8px;
                        }

                        @media (max-width: 768px) {
                            body {
                                padding: 10px;
                            }
                            .article {
                                margin: 15px 0;
                                padding: 15px;
                            }
                            .title {
                                font-size: 1.2em;
                            }
                        }
                    ");
                    await writer.WriteLineAsync("</style>");
                    await writer.WriteLineAsync("</head><body>");
                    await writer.WriteLineAsync("<div class='container'>");

                    // Add header with current date
                    await writer.WriteLineAsync("<div class='header'>");
                    await writer.WriteLineAsync($"<h1>📰 Новини от Mediapool</h1>");
                    await writer.WriteLineAsync($"<div class='datetime'>Извлечено на: {DateTime.Now:dd MMMM yyyy}</div>");
                    await writer.WriteLineAsync("</div>");

                    foreach (var article in articles)
                    {
                        await writer.WriteLineAsync("<div class='article'>");

                        // Title with category emoji
                        await writer.WriteLineAsync($"<div class='title'><span class='emoji'>{article.CategoryEmoji}</span>{HttpUtility.HtmlEncode(article.Title)}</div>");

                        // Date only
                        await writer.WriteLineAsync($"<div class='datetime'><span class='emoji'>📅</span>{article.FormattedDate}</div>");

                        // Author if present
                        if (!string.IsNullOrEmpty(article.Author))
                        {
                            await writer.WriteLineAsync($"<div class='author'><span class='emoji'>✍️</span>Автор: {HttpUtility.HtmlEncode(article.Author)}</div>");
                        }

                        // Category
                        await writer.WriteLineAsync($"<div class='category'><span class='emoji'>📌</span>Категория: {article.Category}</div>");

                        // Favorite status
                        await writer.WriteLineAsync($"<div class='favorite'><span class='emoji'>★</span>{(article.IsFavorite ? "Любима" : "Не е любима")}</div>");

                        // URL
                        await writer.WriteLineAsync($"<div class='url'><span class='emoji'>🔗</span><a href='{article.Url}' target='_blank'>{article.Url}</a></div>");

                        await writer.WriteLineAsync("</div>");
                    }

                    await writer.WriteLineAsync("</div></body></html>");
                }

                Console.WriteLine($"Articles have been saved to: {filePath}");
                Logger.Log($"Successfully stored {articles.Count} articles to {filePath}");

                // Open the HTML file in the default browser
                try
                {
                    var processStartInfo = new ProcessStartInfo
                    {
                        FileName = filePath,
                        UseShellExecute = true
                    };
                    Process.Start(processStartInfo);
                    Logger.Log("Opened articles in default browser");
                }
                catch (Exception ex)
                {
                    Logger.LogError("Error opening articles in browser", ex);
                    Console.WriteLine("Could not automatically open the file. Please open it manually.");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Error storing news articles", ex);
                throw;
            }
        }

        private class NewsArticleComparer : IEqualityComparer<NewsArticle>
        {
            public bool Equals(NewsArticle? x, NewsArticle? y)
            {
                if (ReferenceEquals(x, y)) return true;
                if (x is null || y is null) return false;
                return x.Url == y.Url;
            }

            public int GetHashCode(NewsArticle? obj)
            {
                return obj?.Url?.GetHashCode() ?? 0;
            }
        }
    }
}