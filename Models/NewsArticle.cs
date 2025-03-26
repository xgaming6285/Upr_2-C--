using System;
using System.Linq;
using System.Text.Json.Serialization;

namespace Upr_2.Models
{
    public class NewsArticle
    {
        private string title = string.Empty;
        public string Title
        {
            get => title;
            set => title = value?.Trim() ?? string.Empty;
        }
        public string RawDateTime { get; set; } // Store the original string
        public string FormattedDate { get; private set; } = string.Empty;
        public string FormattedTime { get; private set; } = string.Empty;
        public string Category { get; set; }
        public string CategoryEmoji { get; private set; }
        public string Url { get; set; }
        public bool IsFavorite { get; set; }
        public int WordCount { get; private set; }
        public string? Author { get; set; }

        // Parameterless constructor for JSON deserialization
        public NewsArticle()
        {
            Title = string.Empty;
            RawDateTime = string.Empty;
            FormattedDate = string.Empty;
            FormattedTime = string.Empty;
            Category = "Други";
            Url = string.Empty;
            CategoryEmoji = GetCategoryEmoji(Category); // Default
            WordCount = 0;
        }


        [JsonConstructor] // Helps ensure correct constructor is used during deserialization if multiple exist
        public NewsArticle(string title, string rawDateTime, string url, string category, string? author = null, bool isFavorite = false)
        {
            Title = title?.Trim() ?? string.Empty;
            RawDateTime = rawDateTime ?? string.Empty;
            Url = url?.Trim() ?? string.Empty;
            Category = category?.Trim() ?? "Други";
            Author = author?.Trim();
            IsFavorite = isFavorite;

            FormatDateTime();
            CalculateWordCount();
            CategoryEmoji = GetCategoryEmoji(Category);
        }

        private void FormatDateTime()
        {
            if (string.IsNullOrEmpty(RawDateTime))
            {
                FormattedDate = "N/A";
                FormattedTime = "N/A";
                return;
            }

            // Attempt parsing with specific Bulgarian culture if needed, or invariant
            if (DateTime.TryParse(RawDateTime, System.Globalization.CultureInfo.GetCultureInfo("bg-BG"), System.Globalization.DateTimeStyles.None, out var dt) ||
                DateTime.TryParse(RawDateTime, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out dt))
            {
                FormattedDate = dt.ToString("dd MMMM yyyy", System.Globalization.CultureInfo.GetCultureInfo("bg-BG"));
                FormattedTime = dt.ToString("HH:mm");
            }
            else
            {
                // Fallback if parsing fails
                FormattedDate = RawDateTime; // Keep original string
                FormattedTime = "N/A";
                Logger.LogWarning($"Could not parse date/time string: {RawDateTime}");
            }
        }

        private void CalculateWordCount()
        {
            if (string.IsNullOrEmpty(Title))
            {
                WordCount = 0;
                return;
            }
            WordCount = Title.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Length;
        }

        // Make this static if it doesn't rely on instance state
        private static string GetCategoryEmoji(string category)
        {
            // Consider making this data-driven (e.g., Dictionary) if it grows large
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
                _ => "📰" // Default
            };
        }

        // Override Equals and GetHashCode if storing in collections that rely on equality (like HashSet or Dictionary keys)
        public override bool Equals(object? obj)
        {
            if (obj == null || GetType() != obj.GetType())
                return false;
            return obj is NewsArticle article && !string.IsNullOrEmpty(Url) && Url == article.Url;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Url);
        }
    }
}