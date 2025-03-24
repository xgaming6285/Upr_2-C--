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

        public NewsArticle(string title, string dateTime, string url, string category)
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

            // Set category and emoji
            Category = category;
            CategoryEmoji = GetCategoryEmoji(category);
        }

        private string GetCategoryEmoji(string category)
        {
            return category switch
            {
                "България" => "🇧🇬",
                "Бизнес" => "💼",
                "Свят" => "🌍",
                "Общество" => "👥",
                "Спорт" => "⚽",
                "Greenpool" => "🌿",
                "Парламентарни избори" => "🗳️",
                "Евроизбори 2024" => "🇪🇺",
                "Война в Украйна" => "⚔️",
                "Израел срещу Хамас" => "🕊️",
                "Еврокомпас" => "🧭",
                "Дело срещу Mediapool" => "⚖️",
                "Европейски съюз" => "🇪🇺",
                "НАТО" => "🛡️",
                "САЩ" => "🇺🇸",
                "Русия" => "🇷🇺",
                "Украйна" => "🇺🇦",
                _ => "📰"
            };
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

                if (titleNode != null)
                {
                    string url = titleNode.GetAttributeValue("href", "").Trim();

                    // If URL is relative, make it absolute
                    if (!string.IsNullOrEmpty(url) && !url.StartsWith("http"))
                    {
                        url = "https://www.mediapool.bg" + (url.StartsWith("/") ? url : "/" + url);
                    }

                    // Load the article page to get the actual title, author, and date
                    var web = new HtmlWeb();
                    var articleDoc = web.Load(url);

                    // Get title from the specified h1 element path
                    var articleTitleNode = articleDoc.DocumentNode.SelectSingleNode("//h1[@class='c-heading c-heading_size_1 c-heading_spaced']");
                    string title = articleTitleNode?.InnerText?.Trim() ?? titleNode.InnerText.Trim();

                    // Get category from the specified XPath
                    var categoryNode = articleDoc.DocumentNode.SelectSingleNode("/html/body/article/div[1]/div/div[1]/header/nav/a[2]");
                    string categoryText = categoryNode?.InnerText?.Trim() ?? "Други";

                    // Get author from the specified class
                    var authorNode = articleDoc.DocumentNode.SelectSingleNode("//*[@class='u-highlight-accent u-compact-font c-article__author']");
                    string? author = authorNode?.InnerText?.Trim();

                    // Get date from the specified class
                    var dateNode = articleDoc.DocumentNode.SelectSingleNode("//*[@class='u-highlight-insignificant u-compact-font c-site-content__header-item']");
                    string dateTime = dateNode?.InnerText?.Trim() ?? DateTime.Now.ToString("yyyy-MM-dd HH:mm");

                    if (!ContainsCovid19Keywords(title))
                    {
                        var article = new NewsArticle(title, dateTime, url, categoryText);
                        article.Author = author;
                        article.IsFavorite = _favoriteArticles.Any(a => a.Url == url);
                        articles.Add(article);
                        Console.WriteLine($"Added article: {title} (Category: {categoryText})");
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
                            margin-bottom: 30px;
                            padding: 20px;
                            border-bottom: 1px solid #444;
                            position: sticky;
                            top: 0;
                            background-color: var(--bg-color);
                            z-index: 100;
                            box-shadow: 0 2px 10px rgba(0, 0, 0, 0.1);
                        }

                        .header h1 {
                            margin: 0;
                            color: var(--text-primary);
                            font-size: 2.5em;
                        }

                        .controls {
                            display: flex;
                            justify-content: center;
                            gap: 20px;
                            margin: 20px 0;
                            flex-wrap: wrap;
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
                            grid-template-columns: repeat(auto-fill, minmax(300px, 1fr));
                            gap: 20px;
                            margin-top: 30px;
                        }

                        .article { 
                            margin: 0;
                            padding: 20px;
                            background-color: var(--card-bg);
                            border-radius: 15px;
                            box-shadow: 0 4px 6px rgba(0, 0, 0, 0.1);
                            transition: all 0.3s ease;
                            display: flex;
                            flex-direction: column;
                            height: 100%;
                            position: relative;
                            overflow: hidden;
                            border: 1px solid rgba(255, 255, 255, 0.1);
                        }

                        .article:hover {
                            transform: translateY(-5px);
                            box-shadow: 0 8px 15px rgba(0, 0, 0, 0.2);
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
                            transition: opacity 0.3s ease;
                        }

                        .article:hover::before {
                            opacity: 1;
                        }

                        .article.is-favorite {
                            background: linear-gradient(145deg, var(--card-bg), rgba(52, 152, 219, 0.1));
                            border-color: var(--favorite-color);
                        }

                        .article.is-favorite::after {
                            content: '';
                            position: absolute;
                            top: 0;
                            right: 0;
                            width: 50px;
                            height: 50px;
                            background: linear-gradient(135deg, var(--favorite-color) 50%, transparent 50%);
                            opacity: 0.2;
                        }

                        .title { 
                            color: var(--text-primary);
                            font-size: 1.4em;
                            font-weight: bold;
                            margin-bottom: 15px;
                            line-height: 1.4;
                            padding: 10px;
                            border-radius: 8px;
                            background-color: rgba(0, 0, 0, 0.1);
                            border-left: 4px solid var(--accent-color);
                        }

                        .metadata {
                            display: flex;
                            flex-wrap: wrap;
                            gap: 12px;
                            margin: 15px 0;
                            padding: 10px;
                            background-color: rgba(0, 0, 0, 0.05);
                            border-radius: 8px;
                        }

                        .metadata-item {
                            display: flex;
                            align-items: center;
                            gap: 6px;
                            padding: 4px 10px;
                            background-color: rgba(255, 255, 255, 0.1);
                            border-radius: 15px;
                            font-size: 0.9em;
                        }

                        .metadata-item .emoji {
                            font-size: 1.2em;
                        }

                        .url-container {
                            margin-top: auto;
                            padding-top: 15px;
                            border-top: 1px solid rgba(255, 255, 255, 0.1);
                            display: flex;
                            justify-content: center;
                        }

                        .url-button {
                            display: inline-flex;
                            align-items: center;
                            gap: 8px;
                            background-color: var(--accent-color);
                            color: white;
                            padding: 10px 20px;
                            border-radius: 20px;
                            text-decoration: none;
                            transition: all 0.3s ease;
                            font-size: 0.95em;
                            width: auto;
                            min-width: 200px;
                            justify-content: center;
                            box-shadow: 0 2px 4px rgba(0, 0, 0, 0.2);
                        }

                        .url-button:hover {
                            background-color: #2980b9;
                            transform: translateY(-2px);
                            box-shadow: 0 4px 8px rgba(0, 0, 0, 0.3);
                        }

                        .favorite-indicator {
                            position: absolute;
                            top: 15px;
                            right: 15px;
                            font-size: 1.5em;
                            color: var(--favorite-color);
                            opacity: 0.5;
                            transition: all 0.3s ease;
                            z-index: 1;
                            text-shadow: 0 2px 4px rgba(0, 0, 0, 0.2);
                        }

                        .article.is-favorite .favorite-indicator {
                            opacity: 1;
                            animation: pulse 2s infinite;
                        }

                        @keyframes pulse {
                            0% { transform: scale(1); }
                            50% { transform: scale(1.1); }
                            100% { transform: scale(1); }
                        }

                        @media (max-width: 768px) {
                            body {
                                padding: 10px;
                            }
                            .header h1 {
                                font-size: 2em;
                            }
                            .search-box {
                                width: 100%;
                                max-width: 300px;
                            }
                            .articles-grid {
                                grid-template-columns: 1fr;
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
                    await writer.WriteLineAsync("</style>");
                    await writer.WriteLineAsync(@"
                        <script>
                            document.addEventListener('DOMContentLoaded', function() {
                                // Theme toggle
                                const themeToggle = document.getElementById('theme-toggle');
                                const root = document.documentElement;

                                themeToggle.addEventListener('click', () => {
                                    const currentTheme = root.getAttribute('data-theme');
                                    const newTheme = currentTheme === 'light' ? 'dark' : 'light';
                                    root.setAttribute('data-theme', newTheme);
                                    themeToggle.textContent = `Switch to ${currentTheme === 'light' ? 'Light' : 'Dark'} Theme`;
                                });

                                // Search functionality
                                const searchBox = document.getElementById('search');
                                const articles = document.querySelectorAll('.article');
                                const noResults = document.querySelector('.no-results');

                                searchBox.addEventListener('input', filterArticles);

                                // Category filters
                                const categoryFilters = document.querySelectorAll('.category-filter');
                                let activeCategory = 'all';

                                categoryFilters.forEach(filter => {
                                    filter.addEventListener('click', () => {
                                        categoryFilters.forEach(f => f.classList.remove('active'));
                                        filter.classList.add('active');
                                        activeCategory = filter.dataset.category;
                                        filterArticles();
                                    });
                                });

                                function filterArticles() {
                                    const searchTerm = searchBox.value.toLowerCase();
                                    let visibleCount = 0;

                                    articles.forEach(article => {
                                        const title = article.querySelector('.title').textContent.toLowerCase();
                                        const category = article.dataset.category.toLowerCase();
                                        const matchesSearch = title.includes(searchTerm);
                                        const matchesCategory = activeCategory === 'all' || category === activeCategory.toLowerCase();

                                        if (matchesSearch && matchesCategory) {
                                            article.style.display = 'flex';
                                            visibleCount++;
                                        } else {
                                            article.style.display = 'none';
                                        }
                                    });

                                    noResults.style.display = visibleCount === 0 ? 'block' : 'none';
                                }
                            });
                        </script>
                    ");
                    await writer.WriteLineAsync("</head><body>");
                    await writer.WriteLineAsync("<div class='container'>");

                    // Add header with current date and controls
                    await writer.WriteLineAsync("<div class='header'>");
                    await writer.WriteLineAsync($"<h1>📰 Новини от Mediapool</h1>");
                    await writer.WriteLineAsync($"<div class='datetime'>Извлечено на: {DateTime.Now:dd MMMM yyyy}</div>");

                    // Add controls
                    await writer.WriteLineAsync("<div class='controls'>");
                    await writer.WriteLineAsync("<input type='text' id='search' class='search-box' placeholder='Търсене на новини...'>");
                    await writer.WriteLineAsync("<button id='theme-toggle' class='theme-toggle'>Switch to Light Theme</button>");
                    await writer.WriteLineAsync("</div>");

                    // Add category filters
                    await writer.WriteLineAsync("<div class='category-filters'>");
                    await writer.WriteLineAsync("<button class='category-filter active' data-category='all'>Всички</button>");
                    var uniqueCategories = articles.Select(a => a.Category).Distinct().OrderBy(c => c);
                    foreach (var category in uniqueCategories)
                    {
                        var emoji = articles.First(a => a.Category == category).CategoryEmoji;
                        await writer.WriteLineAsync($"<button class='category-filter' data-category='{category}'>{emoji} {category}</button>");
                    }
                    await writer.WriteLineAsync("</div>");
                    await writer.WriteLineAsync("</div>");

                    // Add loading indicator
                    await writer.WriteLineAsync("<div class='loading'><div class='loading-spinner'></div></div>");

                    // Add no results message
                    await writer.WriteLineAsync("<div class='no-results'>Няма намерени новини</div>");

                    // Add articles grid
                    await writer.WriteLineAsync("<div class='articles-grid'>");

                    foreach (var article in articles)
                    {
                        await writer.WriteLineAsync($"<div class='article {(article.IsFavorite ? "is-favorite" : "")}' data-category='{article.Category}'>");

                        // Favorite indicator
                        await writer.WriteLineAsync("<div class='favorite-indicator'>★</div>");

                        // Title with category emoji
                        await writer.WriteLineAsync($"<div class='title'><span class='emoji'>{article.CategoryEmoji}</span>{HttpUtility.HtmlEncode(article.Title)}</div>");

                        // Metadata section
                        await writer.WriteLineAsync("<div class='metadata'>");
                        await writer.WriteLineAsync($"<div class='metadata-item'><span class='emoji'>📅</span>{article.FormattedDate}</div>");
                        await writer.WriteLineAsync($"<div class='metadata-item'><span class='emoji'>📌</span>{article.Category}</div>");
                        if (!string.IsNullOrEmpty(article.Author))
                        {
                            await writer.WriteLineAsync($"<div class='metadata-item'><span class='emoji'>✍️</span>{HttpUtility.HtmlEncode(article.Author)}</div>");
                        }
                        await writer.WriteLineAsync("</div>");

                        // URL button
                        await writer.WriteLineAsync("<div class='url-container'>");
                        await writer.WriteLineAsync($"<a href='{article.Url}' target='_blank' class='url-button'><span class='emoji'>🔗</span>Прочети повече</a>");
                        await writer.WriteLineAsync("</div>");

                        await writer.WriteLineAsync("</div>");
                    }

                    await writer.WriteLineAsync("</div>"); // Close articles-grid

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