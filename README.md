# 🛠️ Utility Console Application

[![.NET](https://img.shields.io/badge/.NET-%235C2D91.svg?style=for-the-badge&logo=.net&logoColor=white)](https://dotnet.microsoft.com/)
[![C#](https://img.shields.io/badge/C%23-%23239120.svg?style=for-the-badge&logo=c-sharp&logoColor=white)](https://docs.microsoft.com/en-us/dotnet/csharp/)

A powerful C# console application that provides multiple utility functions including IP address validation, time checking, weather forecasts, currency conversion, and news management capabilities.

---

## ✨ Features

### 1. 🌐 IP Address Validation and Lookup
   - ✔️ Validates IPv4 addresses
   - 📍 Retrieves detailed location information for valid IP addresses
   - 🗺️ Displays comprehensive geolocation data

### 2. ⏰ Time Check for Sofia
   - 🕒 Retrieves and displays current time in Sofia, Bulgaria
   - 📅 Shows both time and date information
   - 🔄 Uses time service API for accurate results

### 3. 📰 Mediapool News Scraper
   - 📑 Scrapes latest news articles from Mediapool
   - 📝 Extracts article titles, dates, and URLs
   - 💾 Saves news data to local files
   - ⭐ Includes favorite news management system

### 4. ☁️ Weather Information
   - 🌤️ Provides weather forecasts using OpenWeatherMap API
   - 🔑 Requires API key for functionality
   - 🌡️ Displays detailed weather information

### 5. 💱 Currency Converter
   - 💰 Supports currency conversion between different currencies
   - 📊 Uses real-time exchange rates
   - 🔑 Requires API key for functionality

### 6. ⭐ Favorite News Management
   - 💫 Save and manage favorite news articles
   - 📚 View saved articles
   - 🗑️ Remove articles from favorites

---

## 📁 Project Structure

- `Program.cs` - 🎯 Main application entry point and menu handling
- `NewsService.cs` - 📰 News scraping and management functionality
- `TimeService.cs` - ⏰ Time checking service implementation
- `WeatherService.cs` - ☁️ Weather forecast functionality
- `CurrencyService.cs` - 💱 Currency conversion service
- `Logger.cs` - 📝 Logging functionality for the application

## 📦 Dependencies

The project uses the following external packages:
- 🕸️ HtmlAgilityPack - For web scraping
- 📋 System.Text.Json - For JSON processing
- 🌐 System.Net.Http - For making HTTP requests

## 🔑 API Keys Required

The application uses several external APIs that require keys:
- ☁️ Weather API (OpenWeatherMap)
- 💱 Currency Conversion API (ExchangeRate-API)

### How to Obtain API Keys

#### 🌤️ OpenWeatherMap API
1. Visit [OpenWeatherMap](https://openweathermap.org/api)
2. Click "Sign Up" and create a free account
3. After registration, go to your account dashboard
4. Your API key will be generated automatically
5. Note: New API keys may take a few hours to activate
6. Copy your API key and paste it in `Program.cs` for the `weatherApiKey` variable

#### 💱 ExchangeRate-API
1. Visit [ExchangeRate-API](https://www.exchangerate-api.com/)
2. Click "Get Free API Key"
3. Sign up with your email
4. Verify your email address
5. Access your dashboard to get your API key
6. Copy your API key and paste it in `Program.cs` for the `currencyApiKey` variable

> ⚠️ Important Notes:
> - Keep your API keys secure and never share them publicly
> - Both services offer free tiers that are sufficient for this application
> - Check the API documentation for rate limits and usage restrictions
> - Store your API keys in a secure configuration file in production

## 🚀 Getting Started

1. ✅ Ensure you have .NET SDK installed on your system
2. 📥 Clone the repository
3. 🔧 Configure API keys in Program.cs (optional)
4. 📂 Navigate to the project directory
5. ▶️ Run the application using:
   ```bash
   dotnet run
   ```

## 📱 Usage

When you run the application, you'll be presented with a menu with the following options:

1. 🌐 IP Address Check
2. ⏰ Check Time in Sofia
3. 📰 Extract News from Mediapool
4. ☁️ Check Weather (requires API key)
5. 💱 Currency Converter (requires API key)
6. ⭐ Manage Favorite News
7. 🚪 Exit

> 💡 Simply choose an option by entering the corresponding number (1-7).

## 📝 Logging

The application includes comprehensive logging functionality that tracks:
- 🟢 Application start and shutdown
- 👆 User menu selections
- 📊 Operation results and errors
- 🔐 API key availability status

## ⚠️ Error Handling

The application includes comprehensive error handling for:
- ❌ Invalid IP addresses
- 🌐 Network connection issues
- 🔌 API failures
- ⌨️ Invalid user input
- 🔑 Missing API keys

## 📂 Output Files

- 📰 News articles are saved to text files with timestamps in the filename (stored in "\bin\Debug\net9.0\news\")
- ⭐ Favorite news are maintained in a separate storage
- 📝 Log files are maintained for tracking application activity ("\bin\Debug\net9.0\logs\")

## 📌 Note

> 🔔 Some features (Weather and Currency conversion) require valid API keys to function. The application will indicate if these features are available based on the presence of valid API keys.

---

<div align="center">

### 🌟 Made with ❤️ using C# and .NET

</div>