using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FUD.Settings.pattern;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FUD.Infrastructure.AI
{
    public class GeminiMeterOcrService : IMeterOcrService
    {
        private const string DefaultModel = "gemini-3.5-flash";
        private const string DefaultBaseUrl = "https://generativelanguage.googleapis.com/v1beta";
        private const decimal MaxValidReading = 999_999_999m;
        private const int PerModelFallbackTimeoutSeconds = 25;
        private static readonly TimeSpan QuotaCooldownDuration = TimeSpan.FromSeconds(60);
        private static readonly TimeSpan AuthCooldownDuration = TimeSpan.FromMinutes(10);

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private static int _roundRobinIndex = -1;
        private static readonly ConcurrentDictionary<string, DateTimeOffset> KeyCooldownUntil = new(StringComparer.Ordinal);
        private static readonly object ConcurrencyLock = new();
        private static SemaphoreSlim _concurrencyGate = new(3, 3);
        private static int _concurrencyGateLimit = 3;

        private readonly HttpClient _httpClient;
        private readonly GeminiSettings _settings;
        private readonly IConfiguration _configuration;
        private readonly ILogger<GeminiMeterOcrService> _logger;
        private readonly IMemoryCache? _memoryCache;

        public GeminiMeterOcrService(
            HttpClient httpClient,
            IOptions<GeminiSettings> options,
            IConfiguration configuration,
            ILogger<GeminiMeterOcrService> logger,
            IMemoryCache? memoryCache = null)
        {
            _httpClient = httpClient;
            _settings = options.Value;
            _configuration = configuration;
            _logger = logger;
            _memoryCache = memoryCache;
        }

        public async Task<MeterOcrRawResult> ExtractMeterReadingAsync(
            byte[] imageBytes,
            string mimeType,
            string? expectedMeterType = null,
            CancellationToken cancellationToken = default)
        {
            var apiKeys = ResolveApiKeys();
            if (apiKeys.Count == 0)
            {
                throw new InvalidOperationException(
                    "Chưa cấu hình Gemini API Key (Gemini__ApiKey / Gemini__ApiKeys). Vui lòng kiểm tra file .env hoặc appsettings.json.");
            }

            var models = ResolveModels();
            var baseUrl = ResolveSetting(_settings.BaseUrl, "Gemini:BaseUrl", "Gemini__BaseUrl");
            if (string.IsNullOrWhiteSpace(baseUrl))
                baseUrl = DefaultBaseUrl;
            baseUrl = baseUrl.TrimEnd('/');

            var cacheMinutes = ResolveIntSetting(_settings.CacheMinutes, "Gemini:CacheMinutes", "Gemini__CacheMinutes", 20);
            string? cacheKey = null;
            if (_memoryCache != null && cacheMinutes > 0 && imageBytes.Length > 0)
            {
                cacheKey = BuildCacheKey(imageBytes, expectedMeterType);
                if (_memoryCache.TryGetValue<MeterOcrRawResult>(cacheKey, out var cached) && cached != null)
                {
                    return CloneResult(cached);
                }
            }

            var maxConcurrent = Math.Clamp(
                ResolveIntSetting(_settings.MaxConcurrentRequests, "Gemini:MaxConcurrentRequests", "Gemini__MaxConcurrentRequests", 3),
                1,
                20);
            var gate = GetConcurrencyGate(maxConcurrent);

            await gate.WaitAsync(cancellationToken);
            try
            {
                if (cacheKey != null && _memoryCache != null &&
                    _memoryCache.TryGetValue<MeterOcrRawResult>(cacheKey, out var cachedAfterWait) &&
                    cachedAfterWait != null)
                {
                    return CloneResult(cachedAfterWait);
                }

                int startOffset = (int)((uint)Interlocked.Increment(ref _roundRobinIndex) % (uint)apiKeys.Count);

                bool saw429 = false;
                bool sawAuthError = false;
                bool saw5xx = false;
                bool saw404 = false;
                bool sawTimeout = false;
                bool sawNetworkError = false;
                HttpStatusCode? lastStatusCode = null;
                string lastFailedModel = models[0];

                for (int modelIndex = 0; modelIndex < models.Count; modelIndex++)
                {
                    var model = models[modelIndex];
                    bool hasNextModel = modelIndex < models.Count - 1;
                    var endpoint = $"{baseUrl}/models/{Uri.EscapeDataString(model)}:generateContent";
                    var payload = BuildRequestPayload(imageBytes, mimeType, expectedMeterType, model);
                    var payloadJson = JsonSerializer.Serialize(payload);

                    var orderedKeys = OrderKeysForAttempt(apiKeys, startOffset, DateTimeOffset.UtcNow);
                    int maxTransientRetries = hasNextModel ? Math.Min(orderedKeys.Count, 2) : 2;
                    int transientRetriesUsed = 0;
                    int authFailuresThisModel = 0;

                    for (int keyIdx = 0; keyIdx < orderedKeys.Count; keyIdx++)
                    {
                        var apiKey = orderedKeys[keyIdx];
                        var keyFp = ComputeKeyFingerprint(apiKey);

                        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
                        request.Headers.Add("x-goog-api-key", apiKey);
                        request.Content = new StringContent(payloadJson, Encoding.UTF8, "application/json");

                        using var attemptCts = hasNextModel
                            ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
                            : null;
                        attemptCts?.CancelAfter(TimeSpan.FromSeconds(PerModelFallbackTimeoutSeconds));
                        var effectiveCt = attemptCts?.Token ?? cancellationToken;

                        HttpResponseMessage response;
                        try
                        {
                            response = await _httpClient.SendAsync(request, effectiveCt);
                        }
                        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                        {
                            sawTimeout = true;
                            _logger.LogWarning("Gemini OCR timeout for model {Model} (key #{KeyFp}).", model, keyFp[..6]);
                            if (hasNextModel)
                                break;

                            throw new TimeoutException(
                                "Hết thời gian chờ phân tích ảnh công tơ từ AI. Vui lòng thử lại.");
                        }
                        catch (HttpRequestException ex)
                        {
                            sawNetworkError = true;
                            _logger.LogWarning("Gemini OCR network error for model {Model}: {Message}", model, ex.Message);
                            if (hasNextModel)
                                break;

                            throw new InvalidOperationException(
                                "Không thể kết nối tới dịch vụ AI nhận diện ảnh công tơ. Vui lòng thử lại sau.");
                        }

                        using (response)
                        {
                            if (!response.IsSuccessStatusCode)
                            {
                                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                                var errorDetail = ExtractSafeErrorMessage(errorBody, apiKeys);
                                _logger.LogWarning(
                                    "Gemini OCR returned HTTP {StatusCode} for model {Model} (key #{KeyFp}, retry {Retry}): {Detail}",
                                    (int)response.StatusCode, model, keyFp[..6], transientRetriesUsed, errorDetail ?? "(no details)");

                                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                                {
                                    saw429 = true;
                                    lastStatusCode = response.StatusCode;
                                    KeyCooldownUntil[keyFp] = DateTimeOffset.UtcNow.Add(QuotaCooldownDuration);
                                    continue;
                                }

                                if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                                {
                                    sawAuthError = true;
                                    authFailuresThisModel++;
                                    lastStatusCode = response.StatusCode;
                                    KeyCooldownUntil[keyFp] = DateTimeOffset.UtcNow.Add(AuthCooldownDuration);
                                    continue;
                                }

                                bool isTransientServerError = response.StatusCode is
                                    HttpStatusCode.ServiceUnavailable or
                                    HttpStatusCode.InternalServerError or
                                    HttpStatusCode.BadGateway or
                                    HttpStatusCode.GatewayTimeout;

                                if (isTransientServerError)
                                {
                                    saw5xx = true;
                                    lastStatusCode = response.StatusCode;

                                    if (transientRetriesUsed < maxTransientRetries)
                                    {
                                        transientRetriesUsed++;
                                        int delayMs = hasNextModel
                                            ? 300 + Random.Shared.Next(0, 201)
                                            : (transientRetriesUsed == 1 ? 500 : 1200);
                                        await Task.Delay(delayMs, cancellationToken);
                                        if (keyIdx + 1 >= orderedKeys.Count)
                                        {
                                            keyIdx--;
                                        }
                                        continue;
                                    }

                                    break;
                                }

                                if (response.StatusCode == HttpStatusCode.NotFound)
                                {
                                    saw404 = true;
                                    lastFailedModel = model;
                                    lastStatusCode = response.StatusCode;
                                    break;
                                }

                                throw new InvalidOperationException(
                                    $"Dịch vụ AI nhận diện ảnh gặp lỗi (HTTP {(int)response.StatusCode}).");
                            }

                            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                            var rawResult = ParseGeminiResponse(responseBody);
                            var sanitized = SanitizeResult(rawResult);

                            if (cacheKey != null && _memoryCache != null &&
                                sanitized.IsMeterImage &&
                                sanitized.ReadingValue.HasValue &&
                                sanitized.Confidence >= 0.6)
                            {
                                _memoryCache.Set(cacheKey, CloneResult(sanitized), TimeSpan.FromMinutes(cacheMinutes));
                            }

                            return sanitized;
                        }
                    }

                    if (orderedKeys.Count > 0 && authFailuresThisModel == orderedKeys.Count)
                    {
                        break;
                    }
                }

                if (sawAuthError && !saw429 && !saw5xx && !saw404 && !sawTimeout && !sawNetworkError)
                {
                    throw new InvalidOperationException(
                        "Gemini API Key không hợp lệ hoặc không có quyền truy cập.");
                }

                if (saw429)
                {
                    throw new InvalidOperationException(
                        "Hệ thống AI đang quá tải hoặc hết hạn mức (429). Vui lòng thử lại sau ít phút hoặc nhập tay.");
                }

                if (lastStatusCode == HttpStatusCode.ServiceUnavailable)
                {
                    throw new InvalidOperationException(
                        "Máy chủ AI Gemini đang quá tải tạm thời (HTTP 503). Vui lòng bấm chụp lại sau vài giây hoặc nhập tay.");
                }

                if (sawTimeout)
                {
                    throw new TimeoutException(
                        "Hết thời gian chờ phân tích ảnh công tơ từ AI. Vui lòng thử lại.");
                }

                if (saw404)
                {
                    throw new InvalidOperationException(
                        $"Model Gemini '{lastFailedModel}' không khả dụng (HTTP 404). Vui lòng kiểm tra cấu hình Gemini__Model trong .env.");
                }

                if (sawNetworkError)
                {
                    throw new InvalidOperationException(
                        "Không thể kết nối tới dịch vụ AI nhận diện ảnh công tơ. Vui lòng thử lại sau.");
                }

                throw new InvalidOperationException(
                    $"Dịch vụ AI nhận diện ảnh gặp lỗi (HTTP {(int)(lastStatusCode ?? HttpStatusCode.InternalServerError)}).");
            }
            finally
            {
                gate.Release();
            }
        }

        private List<string> ResolveApiKeys()
        {
            var keys = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);

            void AddKey(string? raw)
            {
                if (string.IsNullOrWhiteSpace(raw))
                    return;

                foreach (var part in raw.Split(new[] { ',', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var trimmed = part.Trim();
                    if (trimmed.Length > 0 && seen.Add(trimmed))
                    {
                        keys.Add(trimmed);
                    }
                }
            }

            AddKey(ResolveSetting(_settings.ApiKey, "Gemini:ApiKey", "Gemini__ApiKey"));
            AddKey(ResolveSetting(_settings.ApiKeys, "Gemini:ApiKeys", "Gemini__ApiKeys"));
            return keys;
        }

        private List<string> ResolveModels()
        {
            var models = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            void AddModel(string? raw)
            {
                if (string.IsNullOrWhiteSpace(raw))
                    return;

                foreach (var part in raw.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var trimmed = part.Trim();
                    if (trimmed.StartsWith("models/", StringComparison.OrdinalIgnoreCase))
                        trimmed = trimmed["models/".Length..];

                    if (trimmed.Length > 0 && seen.Add(trimmed))
                    {
                        models.Add(trimmed);
                    }
                }
            }

            var primary = ResolveSetting(_settings.Model, "Gemini:Model", "Gemini__Model");
            AddModel(string.IsNullOrWhiteSpace(primary) ? DefaultModel : primary);
            AddModel(ResolveSetting(_settings.FallbackModels, "Gemini:FallbackModels", "Gemini__FallbackModels"));
            return models;
        }

        private static List<string> OrderKeysForAttempt(IReadOnlyList<string> apiKeys, int startOffset, DateTimeOffset now)
        {
            var healthy = new List<string>(apiKeys.Count);
            var cooling = new List<(string Key, DateTimeOffset Until)>();

            for (int i = 0; i < apiKeys.Count; i++)
            {
                var key = apiKeys[(startOffset + i) % apiKeys.Count];
                var fp = ComputeKeyFingerprint(key);

                if (KeyCooldownUntil.TryGetValue(fp, out var until))
                {
                    if (until <= now)
                    {
                        KeyCooldownUntil.TryRemove(fp, out _);
                        healthy.Add(key);
                    }
                    else
                    {
                        cooling.Add((key, until));
                    }
                }
                else
                {
                    healthy.Add(key);
                }
            }

            if (healthy.Count > 0)
                return healthy;

            return cooling.OrderBy(x => x.Until).Select(x => x.Key).ToList();
        }

        private static string ComputeKeyFingerprint(string apiKey)
        {
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(apiKey));
            return Convert.ToHexString(hash)[..16];
        }

        private static string BuildCacheKey(byte[] imageBytes, string? expectedMeterType)
        {
            var imageHash = Convert.ToHexString(SHA256.HashData(imageBytes));
            var typePart = string.IsNullOrWhiteSpace(expectedMeterType)
                ? "any"
                : expectedMeterType.Trim().ToLowerInvariant();
            return $"meter_ocr_v2:{imageHash}:{typePart}";
        }

        private static MeterOcrRawResult CloneResult(MeterOcrRawResult source) => new()
        {
            IsMeterImage = source.IsMeterImage,
            DetectedMeterType = source.DetectedMeterType,
            IntegerDigits = source.IntegerDigits,
            DecimalDigits = source.DecimalDigits,
            ReadingValue = source.ReadingValue,
            RawDigits = source.RawDigits,
            Confidence = source.Confidence,
            Notes = source.Notes
        };

        private static SemaphoreSlim GetConcurrencyGate(int maxConcurrent)
        {
            if (_concurrencyGateLimit == maxConcurrent)
                return _concurrencyGate;

            lock (ConcurrencyLock)
            {
                if (_concurrencyGateLimit != maxConcurrent)
                {
                    _concurrencyGate = new SemaphoreSlim(maxConcurrent, maxConcurrent);
                    _concurrencyGateLimit = maxConcurrent;
                }

                return _concurrencyGate;
            }
        }

        private string? ResolveSetting(string? optionValue, string primaryKey, string envKey)
        {
            if (!string.IsNullOrWhiteSpace(optionValue))
                return optionValue.Trim();

            var value = _configuration[primaryKey] ?? _configuration[envKey];
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private int ResolveIntSetting(int optionValue, string primaryKey, string envKey, int fallback)
        {
            if (optionValue > 0)
                return optionValue;

            var raw = _configuration[primaryKey] ?? _configuration[envKey];
            return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) && parsed > 0
                ? parsed
                : fallback;
        }

        private static object BuildRequestPayload(byte[] imageBytes, string mimeType, string? expectedMeterType, string model)
        {
            var prompt = BuildPrompt(expectedMeterType);

            var generationConfig = new Dictionary<string, object>
            {
                ["temperature"] = 0,
                ["mediaResolution"] = "MEDIA_RESOLUTION_HIGH",
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
                        integerDigits = new { type = "STRING", nullable = true },
                        decimalDigits = new { type = "STRING", nullable = true },
                        rawDigits = new { type = "STRING", nullable = true },
                        readingValue = new { type = "NUMBER", nullable = true },
                        confidence = new { type = "NUMBER" },
                        notes = new { type = "STRING", nullable = true }
                    },
                    required = new[] { "isMeterImage", "detectedMeterType", "integerDigits", "decimalDigits", "confidence" }
                }
            };

            if (model.StartsWith("gemini-3", StringComparison.OrdinalIgnoreCase))
            {
                var level = model.Contains("lite", StringComparison.OrdinalIgnoreCase) ? "medium" : "low";
                generationConfig["thinkingConfig"] = new { thinkingLevel = level };
            }
            else if (model.StartsWith("gemini-2.5", StringComparison.OrdinalIgnoreCase))
            {
                generationConfig["thinkingConfig"] = new { thinkingBudget = 1024 };
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

        private static string? ExtractSafeErrorMessage(string? responseBody, IReadOnlyList<string> apiKeys)
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

                    foreach (var key in apiKeys)
                    {
                        if (!string.IsNullOrWhiteSpace(key))
                            text = text.Replace(key, "[REDACTED]", StringComparison.Ordinal);
                    }

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
            sb.AppendLine("Bạn là hệ thống OCR chuyên đọc chỉ số công tơ điện và đồng hồ nước tại ký túc xá Việt Nam.");
            sb.AppendLine("Hãy quan sát thật kỹ từng ô bánh số từ TRÁI sang PHẢI và trả về duy nhất JSON theo đúng schema:");
            sb.AppendLine("1. Quy tắc tách phần nguyên (integerDigits) và phần thập phân (decimalDigits) theo MÀU NỀN BÁNH SỐ:");
            sb.AppendLine("   - Đồng hồ điện cơ (Electricity, đơn vị kWh, VD: EMIC CV140): Các bánh số nền ĐEN (thường 5 ô, bên dưới ghi nhãn 10000 | 1000 | 100 | 10 | 1) là PHẦN NGUYÊN -> đọc ĐỦ TỪNG Ô từ trái sang phải (giữ nguyên số 0 ở đầu) vào integerDigits (VD: \"01482\"). Ô cuối cùng nền ĐỎ hoặc viền khung ĐỎ (nhãn 1/10) là phần thập phân -> ghi vào decimalDigits (VD: \"4\"). Tuyệt đối KHÔNG bỏ sót ô hàng đơn vị (nhãn 1) và KHÔNG dịch dấu phẩy sang trái.");
            sb.AppendLine("   - Công tơ điện tử LCD (Electricity, kWh): Chỉ đọc chỉ số điện năng tổng kWh, tách phần trước dấu chấm vào integerDigits và sau dấu chấm vào decimalDigits. Bỏ qua điện áp (V), dòng (A), tần số (Hz), Số SX/seri, năm sản xuất, tem kiểm định.");
            sb.AppendLine("   - Đồng hồ nước cơ (Water, đơn vị m³, VD: ZR, Zenner, Asahi, Sensus): Các bánh số nền ĐEN (thường 4 hoặc 5 ô trước dấu tam giác/dấu phẩy) là PHẦN NGUYÊN m³ -> ghi ĐỦ TỪNG Ô (giữ số 0 ở đầu) vào integerDigits (VD: \"00127\"). TOÀN BỘ các bánh số nền ĐỎ hoặc chữ ĐỎ đứng sau phần đen (có thể có 1, 2 hoặc 3 ô đỏ) là PHẦN THẬP PHÂN -> ghi ĐỦ TẤT CẢ các chữ số đỏ từ trái sang phải (giữ nguyên số 0 đầu) vào decimalDigits (VD: 3 ô đỏ 0, 4, 3 -> decimalDigits = \"043\"). TUYỆT ĐỐI KHÔNG đẩy chữ số 0 màu đỏ sang integerDigits và KHÔNG cắt bớt số chữ số đỏ. Bỏ qua đồng hồ kim tròn phụ (x0.0001, x0.001...), số seri trên vành và ký hiệu CE/Q3/MAP.");
            sb.AppendLine("2. Quy tắc nhận dạng chữ số chính xác:");
            sb.AppendLine("   - Soi kỹ nét kín/hở để phân biệt 5 vs 6 vs 8 vs 9 vs 0 trên mặt kính cong/lóa sáng.");
            sb.AppendLine("   - Nếu bánh số đang quay dở giữa 2 chữ số, ưu tiên chữ số nhỏ hơn đã đi qua vạch ngang giữa (trừ khi chữ số tiếp theo đã vào gần trọn khung).");
            sb.AppendLine("   - Ghép rawDigits = integerDigits + \".\" + decimalDigits (nếu có decimalDigits, VD: \"01482.4\" hoặc \"00127.043\") và tính readingValue chính xác bằng số thập phân tương ứng (VD: 1482.4 hoặc 127.043).");
            sb.AppendLine("3. Nếu ảnh KHÔNG phải công tơ điện/nước hoặc quá mờ không thể đọc chữ số: đặt isMeterImage = false, integerDigits = null, decimalDigits = null, readingValue = null, confidence <= 0.3 và giải thích ngắn gọn bằng tiếng Việt trong notes.");

            if (string.Equals(expectedMeterType, "Electricity", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(expectedMeterType, "Water", StringComparison.OrdinalIgnoreCase))
            {
                var label = string.Equals(expectedMeterType, "Electricity", StringComparison.OrdinalIgnoreCase)
                    ? "Điện (Electricity)"
                    : "Nước (Water)";
                sb.AppendLine($"4. Ngữ cảnh: Người dùng đang mở ô nhập cho đồng hồ {label}. Tuy nhiên vẫn PHẢI nhận diện trung thực đúng loại thực tế trong ảnh vào detectedMeterType (nếu ảnh chụp nhầm loại công tơ còn lại thì báo đúng loại thực tế).");
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

        private static string ExtractDigitsOnly(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            var sb = new StringBuilder(value.Length);
            foreach (var ch in value)
            {
                if (ch is >= '0' and <= '9')
                    sb.Append(ch);
            }
            return sb.ToString();
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
            var intDigits = ExtractDigitsOnly(raw.IntegerDigits);
            var decDigits = ExtractDigitsOnly(raw.DecimalDigits);
            decimal? readingValue = raw.ReadingValue;

            if (!raw.IsMeterImage)
            {
                readingValue = null;
                intDigits = string.Empty;
                decDigits = string.Empty;
            }
            else
            {
                // Ghép quyết định từ integerDigits (ô đen) + decimalDigits (ô đỏ) để tránh AI đặt nhầm dấu thập phân.
                if (intDigits.Length > 0)
                {
                    // Fallback: nếu model bỏ trống decimalDigits nhưng rawDigits có dạng "<intDigits>.<dec>"
                    if (decDigits.Length == 0 && !string.IsNullOrWhiteSpace(rawDigits))
                    {
                        var normalizedRaw = rawDigits.Replace(',', '.');
                        var dotIdx = normalizedRaw.IndexOf('.');
                        if (dotIdx > 0 && dotIdx < normalizedRaw.Length - 1)
                        {
                            var rawIntPart = ExtractDigitsOnly(normalizedRaw[..dotIdx]);
                            var rawDecPart = ExtractDigitsOnly(normalizedRaw[(dotIdx + 1)..]);
                            if (rawIntPart == intDigits && rawDecPart.Length > 0)
                            {
                                decDigits = rawDecPart;
                            }
                        }
                    }

                    var combined = decDigits.Length > 0 ? $"{intDigits}.{decDigits}" : intDigits;
                    if (decimal.TryParse(combined, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var parsedFromParts))
                    {
                        readingValue = Math.Round(parsedFromParts, 2, MidpointRounding.AwayFromZero);
                        rawDigits = combined;
                    }
                }
                else if (readingValue.HasValue)
                {
                    readingValue = Math.Round(readingValue.Value, 2, MidpointRounding.AwayFromZero);
                }

                if (readingValue.HasValue && (readingValue.Value < 0m || readingValue.Value > MaxValidReading))
                {
                    readingValue = null;
                    const string outOfRangeNote = "Chỉ số AI đọc nằm ngoài phạm vi hợp lệ.";
                    notes = string.IsNullOrWhiteSpace(notes) ? outOfRangeNote : $"{notes} {outOfRangeNote}";
                }
            }

            if (rawDigits != null && rawDigits.Length > 50)
                rawDigits = rawDigits[..50];

            if (notes != null && notes.Length > 300)
                notes = notes[..300];

            return new MeterOcrRawResult
            {
                IsMeterImage = raw.IsMeterImage,
                DetectedMeterType = detectedType,
                IntegerDigits = intDigits.Length > 0 ? intDigits : null,
                DecimalDigits = decDigits.Length > 0 ? decDigits : null,
                ReadingValue = readingValue,
                RawDigits = rawDigits,
                Confidence = confidence,
                Notes = notes
            };
        }
    }
}
