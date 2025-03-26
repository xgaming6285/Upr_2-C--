using System;
using System.Linq;
using System.Text.Json.Serialization;

namespace Upr_2.Models
{
    /// <summary>
    /// Represents a news article with its associated metadata and formatting capabilities.
    /// This class handles news article data including title, date/time, category, and various formatting options.
    /// </summary>
    public class NewsArticle
    {
        // Private backing field for Title property with empty string default
        private string title = string.Empty;

        /// <summary>
        /// Gets or sets the title of the news article.
        /// Automatically trims whitespace and handles null values.
        /// </summary>
        public string Title
        {
            get => title;
            set => title = value?.Trim() ?? string.Empty;
        }

        /// <summary>
        /// Gets or sets the original datetime string as received from the source.
        /// </summary>
        public string RawDateTime { get; set; }

        /// <summary>
        /// Gets the formatted date string in Bulgarian culture format (dd MMMM yyyy).
        /// This is automatically set when RawDateTime is processed.
        /// </summary>
        public string FormattedDate { get; private set; } = string.Empty;

        /// <summary>
        /// Gets the formatted time string in 24-hour format (HH:mm).
        /// This is automatically set when RawDateTime is processed.
        /// </summary>
        public string FormattedTime { get; private set; } = string.Empty;

        /// <summary>
        /// Gets or sets the category of the news article.
        /// </summary>
        public string Category { get; set; }

        /// <summary>
        /// Gets the emoji representation of the article category.
        /// This is automatically set based on the Category property.
        /// </summary>
        public string CategoryEmoji { get; private set; }

        /// <summary>
        /// Gets or sets the URL of the news article.
        /// </summary>
        public string Url { get; set; }

        /// <summary>
        /// Gets or sets whether the article is marked as favorite.
        /// </summary>
        public bool IsFavorite { get; set; }

        /// <summary>
        /// Gets the word count of the article title.
        /// This is automatically calculated when the title is set.
        /// </summary>
        public int WordCount { get; private set; }

        /// <summary>
        /// Gets or sets the author of the article. Can be null if no author is specified.
        /// </summary>
        public string? Author { get; set; }

        /// <summary>
        /// Default constructor required for JSON deserialization.
        /// Initializes all properties with default values.
        /// </summary>
        public NewsArticle()
        {
            Title = string.Empty;
            RawDateTime = string.Empty;
            FormattedDate = string.Empty;
            FormattedTime = string.Empty;
            Category = "Други"; // "Others" in Bulgarian
            Url = string.Empty;
            CategoryEmoji = GetCategoryEmoji(Category);
            WordCount = 0;
        }

        /// <summary>
        /// Main constructor for creating a news article with all required information.
        /// </summary>
        /// <param name="title">The article title</param>
        /// <param name="rawDateTime">The raw date/time string</param>
        /// <param name="url">The article URL</param>
        /// <param name="category">The article category</param>
        /// <param name="author">Optional author name</param>
        /// <param name="isFavorite">Whether the article is favorited</param>
        [JsonConstructor]
        public NewsArticle(string title, string rawDateTime, string url, string category, string? author = null, bool isFavorite = false)
        {
            Title = title?.Trim() ?? string.Empty;
            RawDateTime = rawDateTime ?? string.Empty;
            Url = url?.Trim() ?? string.Empty;
            Category = category?.Trim() ?? "Други";
            Author = author?.Trim();
            IsFavorite = isFavorite;

            // Process and format the date/time
            FormatDateTime();
            // Calculate the word count for the title
            CalculateWordCount();
            // Set the appropriate emoji for the category
            CategoryEmoji = GetCategoryEmoji(Category);
        }

        /// <summary>
        /// Formats the raw date/time string into separate date and time components.
        /// Attempts to parse using both Bulgarian and invariant cultures.
        /// </summary>
        private void FormatDateTime()
        {
            if (string.IsNullOrEmpty(RawDateTime))
            {
                FormattedDate = "N/A";
                FormattedTime = "N/A";
                return;
            }

            // Try parsing with Bulgarian culture first, then fall back to invariant culture
            if (DateTime.TryParse(RawDateTime, System.Globalization.CultureInfo.GetCultureInfo("bg-BG"), System.Globalization.DateTimeStyles.None, out var dt) ||
                DateTime.TryParse(RawDateTime, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out dt))
            {
                FormattedDate = dt.ToString("dd MMMM yyyy", System.Globalization.CultureInfo.GetCultureInfo("bg-BG"));
                FormattedTime = dt.ToString("HH:mm");
            }
            else
            {
                // If parsing fails, keep original string for date and set time as N/A
                FormattedDate = RawDateTime;
                FormattedTime = "N/A";
                Logger.LogWarning($"Could not parse date/time string: {RawDateTime}");
            }
        }

        /// <summary>
        /// Calculates the number of words in the article title.
        /// Words are separated by spaces, tabs, or newlines.
        /// </summary>
        private void CalculateWordCount()
        {
            if (string.IsNullOrEmpty(Title))
            {
                WordCount = 0;
                return;
            }
            WordCount = Title.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Length;
        }

        /// <summary>
        /// Maps article categories to corresponding emoji representations.
        /// </summary>
        /// <param name="category">The category to get an emoji for</param>
        /// <returns>An emoji string representing the category</returns>
        private static string GetCategoryEmoji(string category)
        {
            // Returns appropriate emoji based on category name
            return category switch
            {
                "България" => "🇧🇬",           // Bulgaria
                "Бизнес" => "💼",              // Business
                "Свят" => "🌍",                // World
                "Общество" => "👥",            // Society
                "Спорт" => "⚽",               // Sports
                "Greenpool" => "🌿",           // Environmental news
                "Парламентарни избори" => "🗳️", // Parliamentary elections
                "Евроизбори 2024" => "🇪🇺",     // European elections 2024
                "Война в Украйна" => "⚔️",      // War in Ukraine
                "Израел срещу Хамас" => "🕊️",   // Israel-Hamas conflict
                "Еврокомпас" => "🧭",          // Euro compass
                "Дело срещу Mediapool" => "⚖️", // Mediapool case
                "Европейски съюз" => "🇪🇺",     // European Union
                "НАТО" => "🛡️",               // NATO
                "САЩ" => "🇺🇸",               // USA
                "Русия" => "🇷🇺",             // Russia
                "Украйна" => "🇺🇦",           // Ukraine
                _ => "📰"                     // Default news icon
            };
        }

        /// <summary>
        /// Determines whether two NewsArticle objects are equal based on their URLs.
        /// Two articles are considered equal if they have the same non-empty URL.
        /// </summary>
        public override bool Equals(object? obj)
        {
            if (obj == null || GetType() != obj.GetType())
                return false;
            return obj is NewsArticle article && !string.IsNullOrEmpty(Url) && Url == article.Url;
        }

        /// <summary>
        /// Gets a hash code for the current NewsArticle based on its URL.
        /// This is used when the object is stored in hash-based collections.
        /// </summary>
        public override int GetHashCode()
        {
            return HashCode.Combine(Url);
        }
    }
}