namespace FUD.Settings.pattern
{
    public class GeminiSettings
    {
        public string ApiKey { get; set; } = string.Empty;
        public string ApiKeys { get; set; } = string.Empty;
        public string Model { get; set; } = "gemini-3.5-flash";
        public string FallbackModels { get; set; } = "gemini-3-flash-preview,gemini-3.5-flash-lite,gemini-flash-lite-latest";
        public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com/v1beta";
        public int TimeoutSeconds { get; set; } = 60;
        public int MaxConcurrentRequests { get; set; } = 3;
        public int CacheMinutes { get; set; } = 20;
    }
}
