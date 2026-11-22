using System.Net;
using System.Text;
using System.Text.Json;
using FUD.Settings.pattern;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FUD.Infrastructure.AI
{
    public class GeminiMeterOcrService : IMeterOcrService
    {
        private const string DefaultModel = "gemini-3.5-flash-lite";
        private const string DefaultBaseUrl = "https://generativelanguage.googleapis.com/v1beta";
        private const decimal MaxValidReading = 999_999_999m;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private readonly HttpClient _httpClient;
        private readonly GeminiSettings _settings;
        private readonly IConfiguration _configuration;
        private readonly ILogger<GeminiMeterOcrService> _logger;

        public GeminiMeterOcrService(
            HttpClient httpClient,
            IOptions<GeminiSettings> options,
            IConfiguration configuration,
            ILogger<GeminiMeterOcrService> logger)
        {
            _httpClient = httpClient;
            _settings = options.Value;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<MeterOcrRawResult> ExtractMeterReadingAsync(
            byte[] imageBytes,
            string mimeType,
            string? expectedMeterType = null,
            CancellationToken cancellationToken = default)
        {
            var apiKey = ResolveSetting(_settings.ApiKey, "Gemini:ApiKey", "Gemini__ApiKey");
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new InvalidOperationException(
                    "Chưa cấu hình Gemini API Key (Gemini__ApiKey). Vui lòng kiểm tra file .env hoặc appsettings.json.");
            }

            var model = ResolveSetting(_settings.Model, "Gemini:Model", "Gemini__Model");
            if (string.IsNullOrWhiteSpace(model))
                model = DefaultModel;
            if (model.StartsWith("models/", StringComparison.OrdinalIgnoreCase))
                model = model["models/".Length..];

            var baseUrl = ResolveSetting(_settings.BaseUrl, "Gemini:BaseUrl", "Gemini__BaseUrl");
            if (string.IsNullOrWhiteSpace(baseUrl))
                baseUrl = DefaultBaseUrl;
            baseUrl = baseUrl.TrimEnd('/');

            var endpoint = $"{baseUrl}/models/{Uri.EscapeDataString(model)}:generateContent";
            var payload = BuildRequestPayload(imageBytes, mimeType, expectedMeterType, model);
            var payloadJson = JsonSerializer.Serialize(payload);

            int[] retryDelaysMs = { 500, 1200 };
            for (int attempt = 0; ; attempt++)
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
                request.Headers.Add("x-goog-api-key", apiKey);
                request.Content = new StringContent(payloadJson, Encoding.UTF8, "application/json");

                HttpResponseMessage response;
                try
                {
                    response = await _httpClient.SendAsync(request, cancellationToken);
                }
                catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    throw new TimeoutException(
                        "Hết thời gian chờ phân tích ảnh công tơ từ AI. Vui lòng thử lại.");
                }
                catch (HttpRequestException ex)
                {
                    _logger.LogWarning("Gemini OCR network error: {Message}", ex.Message);
                    throw new InvalidOperationException(
                        "Không thể kết nối tới dịch vụ AI nhận diện ảnh công tơ. Vui lòng thử lại sau.");
                }

                using (response)
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                        var errorDetail = ExtractSafeErrorMessage(errorBody, apiKey);
                        _logger.LogWarning("Gemini OCR returned HTTP {StatusCode} for model {Model} (attempt {Attempt}): {Detail}",
                            (int)response.StatusCode, model, attempt + 1, errorDetail ?? "(no details)");

                        bool isTransientServerError = response.StatusCode is
                            HttpStatusCode.ServiceUnavailable or
                            HttpStatusCode.InternalServerError or
                            HttpStatusCode.BadGateway or
                            HttpStatusCode.GatewayTimeout;

                        if (isTransientServerError && attempt < retryDelaysMs.Length)
                        {
                            await Task.Delay(retryDelaysMs[attempt], cancellationToken);
                            continue;
                        }

                        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                        {
                            throw new InvalidOperationException(
                                "Gemini API Key không hợp lệ hoặc không có quyền truy cập.");
                        }

                        if (response.StatusCode == HttpStatusCode.TooManyRequests)
                        {
                            throw new InvalidOperationException(
                                "Hệ thống AI đang quá tải hoặc hết hạn mức (429). Vui lòng thử lại sau ít phút hoặc nhập tay.");
                        }

                        if (response.StatusCode == HttpStatusCode.NotFound)
                        {
                            throw new InvalidOperationException(
                                $"Model Gemini '{model}' không khả dụng (HTTP 404). Vui lòng kiểm tra cấu hình Gemini__Model trong .env.");
                        }

                        if (response.StatusCode == HttpStatusCode.ServiceUnavailable)
                        {
                            throw new InvalidOperationException(
                                "Máy chủ AI Gemini đang quá tải tạm thời (HTTP 503). Vui lòng bấm chụp lại sau vài giây hoặc nhập tay.");
                        }

                        throw new InvalidOperationException(
                            $"Dịch vụ AI nhận diện ảnh gặp lỗi (HTTP {(int)response.StatusCode}).");
                    }

                    var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                    var rawResult = ParseGeminiResponse(responseBody);
                    return SanitizeResult(rawResult);
                }
            }
        }

        private string? ResolveSetting(string? optionValue, string primaryKey, string envKey)
        {
            if (!string.IsNullOrWhiteSpace(optionValue))
                return optionValue.Trim();

            var value = _configuration[primaryKey] ?? _configuration[envKey];
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private static object BuildRequestPayload(byte[] imageBytes, string mimeType, string? expectedMeterType, string model)
        {
            var prompt = BuildPrompt(expectedMeterType);

            var generationConfig = new Dictionary<string, object>
            {
                ["temperature"] = 0,
                ["responseMimeType"] = "application/json",
                ["responseSchema"] = new
                {
                    type = "OBJECT",
                    properties = new
                    {
                        isMeterImage = new { type = "BOOLEAN" },
                        detectedMeterType = new
                        {
                            type = "STRING",
                            @enum = new[] { "Electricity", "Water", "Unknown" }
                        },
                        readingValue = new { type = "NUMBER", nullable = true },
                        rawDigits = new { type = "STRING", nullable = true },
                        confidence = new { type = "NUMBER" },
                        notes = new { type = "STRING", nullable = true }
                    },
                    required = new[] { "isMeterImage", "detectedMeterType", "confidence" }
                }
            };

            if (model.StartsWith("gemini-3", StringComparison.OrdinalIgnoreCase))
            {
                generationConfig["thinkingConfig"] = new { thinkingLevel = "low" };
            }
            else if (model.StartsWith("gemini-2.5", StringComparison.OrdinalIgnoreCase))
            {
                generationConfig["thinkingConfig"] = new { thinkingBudget = 0 };
            }

            return new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new object[]
                        {
                            new { text = prompt },
                            new
                            {
                                inline_data = new
                                {
                                    mime_type = mimeType,
                                    data = Convert.ToBase64String(imageBytes)
                                }
                            }
                        }
                    }
                },
                generationConfig
            };
        }

        private static string? ExtractSafeErrorMessage(string? responseBody, string apiKey)
        {
            if (string.IsNullOrWhiteSpace(responseBody))
                return null;

            try
            {
                using var doc = JsonDocument.Parse(responseBody);
                if (doc.RootElement.TryGetProperty("error", out var err) &&
                    err.TryGetProperty("message", out var msg) &&
                    msg.ValueKind == JsonValueKind.String)
                {
                    var text = msg.GetString();
                    if (string.IsNullOrWhiteSpace(text))
                        return null;

                    if (!string.IsNullOrWhiteSpace(apiKey))
                        text = text.Replace(apiKey, "[REDACTED]", StringComparison.Ordinal);

                    return text.Length > 300 ? text[..300] : text;
                }
            }
            catch (JsonException)
            {
                // Ignore non-JSON error body
            }

            return null;
        }

        private static string BuildPrompt(string? expectedMeterType)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Bạn là hệ thống đọc chỉ số công tơ điện và đồng hồ nước tại ký túc xá Việt Nam.");
            sb.AppendLine("Hãy phân tích ảnh và trả về duy nhất JSON theo đúng schema:");
            sb.AppendLine("- Đồng hồ điện (Electricity, đơn vị kWh): Với công tơ cơ, dãy bánh số đen là phần nguyên kWh, ô khung đỏ cuối cùng (nếu có) là phần thập phân 0.1 kWh. Với công tơ điện tử LCD, chỉ lấy chỉ số điện năng tổng kWh, bỏ qua điện áp (V), dòng điện (A), tần số (Hz), mã seri và ngày giờ.");
            sb.AppendLine("- Đồng hồ nước (Water, đơn vị m³): Dãy chữ số đen là phần nguyên m³; chỉ lấy phần thập phân màu đỏ trên dãy số chính (tối đa 2 chữ số sau dấu phẩy) nếu nhìn rõ, bỏ qua số seri in trên vỏ và các đồng hồ kim tròn phụ.");
            sb.AppendLine("- Nếu ảnh KHÔNG phải công tơ điện/nước hoặc quá mờ không đọc được chữ số: đặt isMeterImage = false (hoặc readingValue = null), confidence <= 0.3 và giải thích ngắn gọn bằng tiếng Việt trong notes.");

            if (string.Equals(expectedMeterType, "Electricity", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(expectedMeterType, "Water", StringComparison.OrdinalIgnoreCase))
            {
                var label = string.Equals(expectedMeterType, "Electricity", StringComparison.OrdinalIgnoreCase)
                    ? "Điện (Electricity)"
                    : "Nước (Water)";
                sb.AppendLine($"- Ngữ cảnh: Người dùng đang mở ô nhập cho đồng hồ {label}. Tuy nhiên vẫn PHẢI nhận diện trung thực đúng loại thực tế trong ảnh vào detectedMeterType (nếu ảnh chụp nhầm loại công tơ còn lại thì báo đúng loại thực tế).");
            }

            return sb.ToString();
        }

        private static MeterOcrRawResult ParseGeminiResponse(string responseBody)
        {
            try
            {
                using var doc = JsonDocument.Parse(responseBody);
                if (!doc.RootElement.TryGetProperty("candidates", out var candidates) ||
                    candidates.ValueKind != JsonValueKind.Array ||
                    candidates.GetArrayLength() == 0)
                {
                    throw new InvalidOperationException("Dịch vụ AI không trả về kết quả phân tích hợp lệ.");
                }

                var firstCandidate = candidates[0];
                if (!firstCandidate.TryGetProperty("content", out var content) ||
                    !content.TryGetProperty("parts", out var parts) ||
                    parts.ValueKind != JsonValueKind.Array)
                {
                    throw new InvalidOperationException("Dịch vụ AI không trả về nội dung đọc chỉ số.");
                }

                string? jsonText = null;
                foreach (var part in parts.EnumerateArray())
                {
                    if (part.TryGetProperty("thought", out var thoughtProp) &&
                        thoughtProp.ValueKind == JsonValueKind.True)
                    {
                        continue;
                    }

                    if (part.TryGetProperty("text", out var textProp) &&
                        textProp.ValueKind == JsonValueKind.String)
                    {
                        var candidateText = textProp.GetString();
                        if (!string.IsNullOrWhiteSpace(candidateText))
                        {
                            jsonText = candidateText;
                            break;
                        }
                    }
                }

                if (string.IsNullOrWhiteSpace(jsonText))
                {
                    throw new InvalidOperationException("Dịch vụ AI không trả về dữ liệu JSON nhận diện.");
                }

                jsonText = StripMarkdownCodeFence(jsonText);
                var parsed = JsonSerializer.Deserialize<MeterOcrRawResult>(jsonText, JsonOptions);
                if (parsed == null)
                {
                    throw new InvalidOperationException("Không thể phân tích dữ liệu JSON từ dịch vụ AI.");
                }

                return parsed;
            }
            catch (JsonException)
            {
                throw new InvalidOperationException("Dữ liệu phản hồi từ dịch vụ AI không đúng định dạng JSON.");
            }
        }

        private static string StripMarkdownCodeFence(string text)
        {
            var trimmed = text.Trim();
            if (!trimmed.StartsWith("```", StringComparison.Ordinal))
                return trimmed;

            var firstNewLine = trimmed.IndexOf('\n');
            if (firstNewLine >= 0)
            {
                trimmed = trimmed[(firstNewLine + 1)..];
            }

            if (trimmed.EndsWith("```", StringComparison.Ordinal))
            {
                trimmed = trimmed[..^3];
            }

            return trimmed.Trim();
        }

        private static MeterOcrRawResult SanitizeResult(MeterOcrRawResult raw)
        {
            var detectedType = raw.DetectedMeterType?.Trim() switch
            {
                var t when string.Equals(t, "Electricity", StringComparison.OrdinalIgnoreCase) => "Electricity",
                var t when string.Equals(t, "Water", StringComparison.OrdinalIgnoreCase) => "Water",
                _ => "Unknown"
            };

            var confidence = double.IsNaN(raw.Confidence) || double.IsInfinity(raw.Confidence)
                ? 0.0
                : Math.Clamp(raw.Confidence, 0.0, 1.0);

            var notes = string.IsNullOrWhiteSpace(raw.Notes) ? null : raw.Notes.Trim();
            var rawDigits = string.IsNullOrWhiteSpace(raw.RawDigits) ? null : raw.RawDigits.Trim();
            decimal? readingValue = raw.ReadingValue;

            if (!raw.IsMeterImage)
            {
                readingValue = null;
            }
            else if (readingValue.HasValue && (readingValue.Value < 0m || readingValue.Value > MaxValidReading))
            {
                readingValue = null;
                const string outOfRangeNote = "Chỉ số AI đọc nằm ngoài phạm vi hợp lệ.";
                notes = string.IsNullOrWhiteSpace(notes) ? outOfRangeNote : $"{notes} {outOfRangeNote}";
            }

            if (rawDigits != null && rawDigits.Length > 50)
                rawDigits = rawDigits[..50];

            if (notes != null && notes.Length > 300)
                notes = notes[..300];

            return new MeterOcrRawResult
            {
                IsMeterImage = raw.IsMeterImage,
                DetectedMeterType = detectedType,
                ReadingValue = readingValue,
                RawDigits = rawDigits,
                Confidence = confidence,
                Notes = notes
            };
        }
    }
}
