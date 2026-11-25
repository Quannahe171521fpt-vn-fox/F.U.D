using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using FUD.Infrastructure.AI;
using FUD.Settings.pattern;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using Xunit;

namespace FUD.Tests.Utilities;

public class GeminiMeterOcrServiceTests
{
    private static readonly byte[] SampleImageBytes = { 0xFF, 0xD8, 0xFF, 0xE0, 0x01, 0x02 };

    private static GeminiMeterOcrService CreateSut(
        HttpMessageHandler handler,
        GeminiSettings? settings = null,
        Dictionary<string, string?>? configValues = null,
        IMemoryCache? memoryCache = null)
    {
        var httpClient = new HttpClient(handler);
        var resolvedSettings = settings ?? new GeminiSettings
        {
            ApiKey = "test-secret-api-key",
            ApiKeys = "",
            Model = "gemini-3.5-flash-lite",
            FallbackModels = "",
            BaseUrl = "https://generativelanguage.googleapis.com/v1beta",
            TimeoutSeconds = 25
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues ?? new Dictionary<string, string?>())
            .Build();

        return new GeminiMeterOcrService(
            httpClient,
            Options.Create(resolvedSettings),
            configuration,
            NullLogger<GeminiMeterOcrService>.Instance,
            memoryCache);
    }

    private static Mock<HttpMessageHandler> CreateHandlerMock(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder)
    {
        var mock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        mock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Returns(responder);
        return mock;
    }

    private static HttpResponseMessage BuildGeminiJsonResponse(object candidatePartsPayload)
    {
        var wrapper = new
        {
            candidates = new[]
            {
                new
                {
                    content = new
                    {
                        parts = candidatePartsPayload
                    }
                }
            }
        };

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(wrapper), Encoding.UTF8, "application/json")
        };
    }

    [Fact]
    public async Task ExtractMeterReadingAsync_ValidElectricityResponse_ParsesAndSendsApiKeyHeaderAndThinkingBudgetZero()
    {
        HttpRequestMessage? capturedRequest = null;
        string? capturedBody = null;

        var handlerMock = CreateHandlerMock(async (req, ct) =>
        {
            capturedRequest = req;
            capturedBody = await req.Content!.ReadAsStringAsync(ct);

            var ocrJson = JsonSerializer.Serialize(new
            {
                isMeterImage = true,
                detectedMeterType = "Electricity",
                readingValue = 1245.5m,
                rawDigits = "01245.5",
                confidence = 0.95,
                notes = "Công tơ điện cơ rõ nét"
            });

            return BuildGeminiJsonResponse(new[] { new { text = ocrJson } });
        });

        var sut = CreateSut(handlerMock.Object);

        var result = await sut.ExtractMeterReadingAsync(SampleImageBytes, "image/jpeg", "Electricity");

        capturedRequest.Should().NotBeNull();
        capturedRequest!.RequestUri!.ToString().Should().NotContain("test-secret-api-key");
        capturedRequest.RequestUri!.ToString().Should().NotContain("?key=");
        capturedRequest.Headers.TryGetValues("x-goog-api-key", out var apiKeyValues).Should().BeTrue();
        apiKeyValues!.Single().Should().Be("test-secret-api-key");

        capturedBody.Should().NotBeNull();
        capturedBody.Should().Contain("\"thinkingLevel\":\"medium\"");
        capturedBody.Should().Contain("\"mediaResolution\":\"MEDIA_RESOLUTION_HIGH\"");
        capturedBody.Should().Contain("\"integerDigits\"");
        capturedBody.Should().Contain("\"decimalDigits\"");
        capturedBody.Should().Contain("\"responseMimeType\":\"application/json\"");

        result.IsMeterImage.Should().BeTrue();
        result.DetectedMeterType.Should().Be("Electricity");
        result.ReadingValue.Should().Be(1245.5m);
        result.RawDigits.Should().Be("01245.5");
        result.Confidence.Should().Be(0.95);
        result.Notes.Should().Be("Công tơ điện cơ rõ nét");
    }

    [Fact]
    public async Task ExtractMeterReadingAsync_ValidWaterResponse_ParsesDecimalsCorrectly()
    {
        var handlerMock = CreateHandlerMock((_, _) =>
        {
            var ocrJson = JsonSerializer.Serialize(new
            {
                isMeterImage = true,
                detectedMeterType = "Water",
                readingValue = 84.25m,
                rawDigits = "00084.25",
                confidence = 0.88,
                notes = "Đồng hồ nước hiển thị 84.25 m3"
            });

            return Task.FromResult(BuildGeminiJsonResponse(new[] { new { text = ocrJson } }));
        });

        var sut = CreateSut(handlerMock.Object);

        var result = await sut.ExtractMeterReadingAsync(SampleImageBytes, "image/png", "Water");

        result.IsMeterImage.Should().BeTrue();
        result.DetectedMeterType.Should().Be("Water");
        result.ReadingValue.Should().Be(84.25m);
        result.RawDigits.Should().Be("00084.25");
        result.Confidence.Should().Be(0.88);
    }

    [Fact]
    public async Task ExtractMeterReadingAsync_NonMeterImage_ReturnsIsMeterImageFalseAndNullReading()
    {
        var handlerMock = CreateHandlerMock((_, _) =>
        {
            var ocrJson = JsonSerializer.Serialize(new
            {
                isMeterImage = false,
                detectedMeterType = "Unknown",
                readingValue = 123m, // Even if model mistakenly outputs a number
                rawDigits = "123",
                confidence = 0.2,
                notes = "Ảnh không phải công tơ điện nước"
            });

            return Task.FromResult(BuildGeminiJsonResponse(new[] { new { text = ocrJson } }));
        });

        var sut = CreateSut(handlerMock.Object);

        var result = await sut.ExtractMeterReadingAsync(SampleImageBytes, "image/jpeg");

        result.IsMeterImage.Should().BeFalse();
        result.DetectedMeterType.Should().Be("Unknown");
        result.ReadingValue.Should().BeNull();
        result.Confidence.Should().Be(0.2);
    }

    [Fact]
    public async Task ExtractMeterReadingAsync_SkipsThoughtPartAndParsesJsonPart()
    {
        var handlerMock = CreateHandlerMock((_, _) =>
        {
            var ocrJson = JsonSerializer.Serialize(new
            {
                isMeterImage = true,
                detectedMeterType = "Electricity",
                readingValue = 502m,
                rawDigits = "00502",
                confidence = 0.91,
                notes = (string?)null
            });

            var parts = new object[]
            {
                new { thought = true, text = "Let me examine the digits..." },
                new { text = $"```json\n{ocrJson}\n```" }
            };

            return Task.FromResult(BuildGeminiJsonResponse(parts));
        });

        var sut = CreateSut(handlerMock.Object);

        var result = await sut.ExtractMeterReadingAsync(SampleImageBytes, "image/jpeg", "Electricity");

        result.IsMeterImage.Should().BeTrue();
        result.ReadingValue.Should().Be(502m);
        result.Confidence.Should().Be(0.91);
    }

    [Fact]
    public async Task ExtractMeterReadingAsync_ClampsConfidenceAndRejectsOutOfRangeReadingValue()
    {
        var handlerMock = CreateHandlerMock((_, _) =>
        {
            var ocrJson = JsonSerializer.Serialize(new
            {
                isMeterImage = true,
                detectedMeterType = "GasMeter",
                readingValue = 999_999_999_999m,
                rawDigits = new string('9', 80),
                confidence = 1.5,
                notes = "Giá trị vượt biên"
            });

            return Task.FromResult(BuildGeminiJsonResponse(new[] { new { text = ocrJson } }));
        });

        var sut = CreateSut(handlerMock.Object);

        var result = await sut.ExtractMeterReadingAsync(SampleImageBytes, "image/jpeg");

        result.IsMeterImage.Should().BeTrue();
        result.DetectedMeterType.Should().Be("Unknown");
        result.Confidence.Should().Be(1.0);
        result.ReadingValue.Should().BeNull();
        result.RawDigits!.Length.Should().Be(50);
        result.Notes.Should().Contain("ngoài phạm vi hợp lệ");
    }

    [Fact]
    public async Task ExtractMeterReadingAsync_MissingApiKey_ThrowsInvalidOperationException()
    {
        var handlerMock = CreateHandlerMock((_, _) =>
            throw new InvalidOperationException("Should not call HTTP when API key is missing"));

        var sut = CreateSut(
            handlerMock.Object,
            settings: new GeminiSettings { ApiKey = "" });

        var act = () => sut.ExtractMeterReadingAsync(SampleImageBytes, "image/jpeg");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Gemini API Key*");
    }

    [Fact]
    public async Task ExtractMeterReadingAsync_Http429_ThrowsFriendlyRateLimitErrorWithoutLeakingKey()
    {
        var handlerMock = CreateHandlerMock((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.TooManyRequests)
            {
                Content = new StringContent("{\"error\":{\"message\":\"Quota exceeded for test-secret-api-key\"}}")
            }));

        var sut = CreateSut(handlerMock.Object);

        var act = () => sut.ExtractMeterReadingAsync(SampleImageBytes, "image/jpeg");

        var ex = await act.Should().ThrowAsync<InvalidOperationException>();
        ex.Which.Message.Should().Contain("429");
        ex.Which.Message.Should().NotContain("test-secret-api-key");
    }

    [Fact]
    public async Task ExtractMeterReadingAsync_MalformedJson_ThrowsInvalidOperationException()
    {
        var handlerMock = CreateHandlerMock((_, _) =>
            Task.FromResult(BuildGeminiJsonResponse(new[] { new { text = "{ not valid json" } })));

        var sut = CreateSut(handlerMock.Object);

        var act = () => sut.ExtractMeterReadingAsync(SampleImageBytes, "image/jpeg");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*JSON*");
    }

    [Fact]
    public async Task ExtractMeterReadingAsync_HttpTimeout_ThrowsTimeoutException()
    {
        var handlerMock = CreateHandlerMock((_, _) =>
            Task.FromException<HttpResponseMessage>(new TaskCanceledException("Simulated timeout")));

        var sut = CreateSut(handlerMock.Object);

        var act = () => sut.ExtractMeterReadingAsync(SampleImageBytes, "image/jpeg");

        await act.Should().ThrowAsync<TimeoutException>()
            .WithMessage("*Hết thời gian chờ*");
    }

    [Fact]
    public async Task ExtractMeterReadingAsync_TransientHttp503_RetriesAndSucceeds()
    {
        int callCount = 0;
        var handlerMock = CreateHandlerMock((_, _) =>
        {
            callCount++;
            if (callCount == 1)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                {
                    Content = new StringContent("{\"error\":{\"code\":503,\"message\":\"high demand\"}}")
                });
            }

            var ocrJson = JsonSerializer.Serialize(new
            {
                isMeterImage = true,
                detectedMeterType = "Water",
                readingValue = 42.5m,
                rawDigits = "00042.5",
                confidence = 0.93,
                notes = "Đồng hồ nước rõ nét"
            });

            return Task.FromResult(BuildGeminiJsonResponse(new[] { new { text = ocrJson } }));
        });

        var sut = CreateSut(handlerMock.Object);

        var result = await sut.ExtractMeterReadingAsync(SampleImageBytes, "image/jpeg", "Water");

        callCount.Should().Be(2);
        result.IsMeterImage.Should().BeTrue();
        result.DetectedMeterType.Should().Be("Water");
        result.ReadingValue.Should().Be(42.5m);
    }

    [Fact]
    public async Task ExtractMeterReadingAsync_ReconstructsElectricityValueFromIntegerAndDecimalDigits()
    {
        var handlerMock = CreateHandlerMock((_, _) =>
        {
            var ocrJson = JsonSerializer.Serialize(new
            {
                isMeterImage = true,
                detectedMeterType = "Electricity",
                integerDigits = "03626",
                decimalDigits = "8",
                readingValue = 362.5m, // Even if model misplaces decimal in float field
                rawDigits = "362.5",
                confidence = 0.94,
                notes = "Công tơ điện cơ EMIC CV140"
            });

            return Task.FromResult(BuildGeminiJsonResponse(new[] { new { text = ocrJson } }));
        });

        var sut = CreateSut(handlerMock.Object);

        var result = await sut.ExtractMeterReadingAsync(SampleImageBytes, "image/jpeg", "Electricity");

        result.IsMeterImage.Should().BeTrue();
        result.DetectedMeterType.Should().Be("Electricity");
        result.IntegerDigits.Should().Be("03626");
        result.DecimalDigits.Should().Be("8");
        result.RawDigits.Should().Be("03626.8");
        result.ReadingValue.Should().Be(3626.8m);
    }

    [Fact]
    public async Task ExtractMeterReadingAsync_ReconstructsWaterThreeRedDigitsWithoutShiftingLeadingZero()
    {
        var handlerMock = CreateHandlerMock((_, _) =>
        {
            var ocrJson = JsonSerializer.Serialize(new
            {
                isMeterImage = true,
                detectedMeterType = "Water",
                integerDigits = "00001",
                decimalDigits = "056",
                readingValue = 10.58m, // Even if model shifts leading red zero
                rawDigits = "000010.56",
                confidence = 0.92,
                notes = "Đồng hồ nước ZR có 3 ô đỏ"
            });

            return Task.FromResult(BuildGeminiJsonResponse(new[] { new { text = ocrJson } }));
        });

        var sut = CreateSut(handlerMock.Object);

        var result = await sut.ExtractMeterReadingAsync(SampleImageBytes, "image/jpeg", "Water");

        result.IsMeterImage.Should().BeTrue();
        result.DetectedMeterType.Should().Be("Water");
        result.IntegerDigits.Should().Be("00001");
        result.DecimalDigits.Should().Be("056");
        result.RawDigits.Should().Be("00001.056");
        result.ReadingValue.Should().Be(1.06m);
    }

    [Fact]
    public async Task ExtractMeterReadingAsync_WhenDecimalDigitsOmitted_FallsBackToRawDigitsFraction()
    {
        var handlerMock = CreateHandlerMock((_, _) =>
        {
            var ocrJson = JsonSerializer.Serialize(new
            {
                isMeterImage = true,
                detectedMeterType = "Water",
                integerDigits = "00001",
                decimalDigits = (string?)null,
                readingValue = 1.056m,
                rawDigits = "00001.056",
                confidence = 0.99,
                notes = (string?)null
            });

            return Task.FromResult(BuildGeminiJsonResponse(new[] { new { text = ocrJson } }));
        });

        var sut = CreateSut(handlerMock.Object);

        var result = await sut.ExtractMeterReadingAsync(SampleImageBytes, "image/jpeg", "Water");

        result.IntegerDigits.Should().Be("00001");
        result.DecimalDigits.Should().Be("056");
        result.RawDigits.Should().Be("00001.056");
        result.ReadingValue.Should().Be(1.06m);
    }

    [Fact]
    public async Task ExtractMeterReadingAsync_RotatesAcrossApiKeysRoundRobin()
    {
        var usedKeys = new List<string>();
        string? capturedFlashBody = null;

        var handlerMock = CreateHandlerMock(async (req, ct) =>
        {
            usedKeys.Add(req.Headers.GetValues("x-goog-api-key").Single());
            capturedFlashBody ??= await req.Content!.ReadAsStringAsync(ct);

            var ocrJson = JsonSerializer.Serialize(new
            {
                isMeterImage = true,
                detectedMeterType = "Electricity",
                integerDigits = "03626",
                decimalDigits = "8",
                rawDigits = "03626.8",
                readingValue = 3626.8m,
                confidence = 0.99,
                notes = (string?)null
            });

            return BuildGeminiJsonResponse(new[] { new { text = ocrJson } });
        });

        var settings = new GeminiSettings
        {
            ApiKey = "rr-key-1",
            ApiKeys = "rr-key-2,rr-key-3",
            Model = "gemini-3.5-flash",
            FallbackModels = "",
            BaseUrl = "https://generativelanguage.googleapis.com/v1beta"
        };

        var sut = CreateSut(handlerMock.Object, settings);

        await sut.ExtractMeterReadingAsync(new byte[] { 1, 1, 1 }, "image/jpeg", "Electricity");
        await sut.ExtractMeterReadingAsync(new byte[] { 2, 2, 2 }, "image/jpeg", "Electricity");
        await sut.ExtractMeterReadingAsync(new byte[] { 3, 3, 3 }, "image/jpeg", "Electricity");

        usedKeys.Should().HaveCount(3);
        usedKeys.Distinct().Should().BeEquivalentTo(new[] { "rr-key-1", "rr-key-2", "rr-key-3" });
        capturedFlashBody.Should().Contain("\"thinkingLevel\":\"low\"");
    }

    [Fact]
    public async Task ExtractMeterReadingAsync_WhenFirstKeyHits429_SwitchesToSecondKeyOnSameModel()
    {
        var attempts = new List<(string Model, string Key)>();

        var handlerMock = CreateHandlerMock((req, _) =>
        {
            var key = req.Headers.GetValues("x-goog-api-key").Single();
            var url = req.RequestUri!.ToString();
            var modelSegment = url.Contains("gemini-3.5-flash-lite") ? "gemini-3.5-flash-lite" : "gemini-3.5-flash";
            attempts.Add((modelSegment, key));

            if (attempts.Count == 1)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.TooManyRequests)
                {
                    Content = new StringContent("{\"error\":{\"code\":429,\"message\":\"Quota exceeded\"}}")
                });
            }

            var ocrJson = JsonSerializer.Serialize(new
            {
                isMeterImage = true,
                detectedMeterType = "Electricity",
                integerDigits = "03626",
                decimalDigits = "8",
                rawDigits = "03626.8",
                readingValue = 3626.8m,
                confidence = 0.98,
                notes = (string?)null
            });

            return Task.FromResult(BuildGeminiJsonResponse(new[] { new { text = ocrJson } }));
        });

        var settings = new GeminiSettings
        {
            ApiKey = "quota429-key-1",
            ApiKeys = "quota429-key-2",
            Model = "gemini-3.5-flash",
            FallbackModels = "gemini-3.5-flash-lite",
            BaseUrl = "https://generativelanguage.googleapis.com/v1beta"
        };

        var sut = CreateSut(handlerMock.Object, settings);

        var result = await sut.ExtractMeterReadingAsync(SampleImageBytes, "image/jpeg", "Electricity");

        result.ReadingValue.Should().Be(3626.8m);
        attempts.Should().HaveCount(2);
        attempts.Select(a => a.Model).Should().AllBe("gemini-3.5-flash");
        attempts.Select(a => a.Key).Distinct().Should().HaveCount(2);
    }

    [Fact]
    public async Task ExtractMeterReadingAsync_WhenFirstKeyUnauthorized_FailsOverToSecondKey()
    {
        int calls = 0;

        var handlerMock = CreateHandlerMock((_, _) =>
        {
            calls++;
            if (calls == 1)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)
                {
                    Content = new StringContent("{\"error\":{\"code\":401,\"message\":\"Invalid API key\"}}")
                });
            }

            var ocrJson = JsonSerializer.Serialize(new
            {
                isMeterImage = true,
                detectedMeterType = "Water",
                integerDigits = "00001",
                decimalDigits = "056",
                rawDigits = "00001.056",
                readingValue = 1.056m,
                confidence = 0.97,
                notes = (string?)null
            });

            return Task.FromResult(BuildGeminiJsonResponse(new[] { new { text = ocrJson } }));
        });

        var settings = new GeminiSettings
        {
            ApiKey = "auth401-key-1",
            ApiKeys = "auth401-key-2",
            Model = "gemini-3.5-flash",
            FallbackModels = "gemini-3.5-flash-lite",
            BaseUrl = "https://generativelanguage.googleapis.com/v1beta"
        };

        var sut = CreateSut(handlerMock.Object, settings);

        var result = await sut.ExtractMeterReadingAsync(SampleImageBytes, "image/jpeg", "Water");

        calls.Should().Be(2);
        result.ReadingValue.Should().Be(1.06m);
    }

    [Fact]
    public async Task ExtractMeterReadingAsync_WhenAllKeysUnauthorized_ThrowsWithoutLeakingKeys()
    {
        int calls = 0;
        var handlerMock = CreateHandlerMock((req, _) =>
        {
            calls++;
            var key = req.Headers.GetValues("x-goog-api-key").Single();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = new StringContent($"{{\"error\":{{\"code\":401,\"message\":\"Invalid key {key}\"}}}}")
            });
        });

        var settings = new GeminiSettings
        {
            ApiKey = "all401-secret-1",
            ApiKeys = "all401-secret-2",
            Model = "gemini-3.5-flash",
            FallbackModels = "gemini-3.5-flash-lite",
            BaseUrl = "https://generativelanguage.googleapis.com/v1beta"
        };

        var sut = CreateSut(handlerMock.Object, settings);

        var act = () => sut.ExtractMeterReadingAsync(SampleImageBytes, "image/jpeg");

        var ex = await act.Should().ThrowAsync<InvalidOperationException>();
        ex.Which.Message.Should().Contain("API Key");
        ex.Which.Message.Should().NotContain("all401-secret-1");
        ex.Which.Message.Should().NotContain("all401-secret-2");
        calls.Should().Be(2);
    }

    [Fact]
    public async Task ExtractMeterReadingAsync_WhenPrimaryModelReturns503_FallsBackToSecondaryModel()
    {
        var requestedUrls = new List<string>();
        string? fallbackBody = null;

        var handlerMock = CreateHandlerMock(async (req, ct) =>
        {
            var url = req.RequestUri!.ToString();
            requestedUrls.Add(url);

            if (url.Contains("gemini-3.5-flash:"))
            {
                return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                {
                    Content = new StringContent("{\"error\":{\"code\":503,\"message\":\"high demand\"}}")
                };
            }

            fallbackBody = await req.Content!.ReadAsStringAsync(ct);
            var ocrJson = JsonSerializer.Serialize(new
            {
                isMeterImage = true,
                detectedMeterType = "Water",
                integerDigits = "00012",
                decimalDigits = "34",
                rawDigits = "00012.34",
                readingValue = 12.34m,
                confidence = 0.95,
                notes = (string?)null
            });

            return BuildGeminiJsonResponse(new[] { new { text = ocrJson } });
        });

        var settings = new GeminiSettings
        {
            ApiKey = "fallback503-key-1",
            ApiKeys = "",
            Model = "gemini-3.5-flash",
            FallbackModels = "gemini-3.5-flash-lite,gemini-3.1-flash-lite",
            BaseUrl = "https://generativelanguage.googleapis.com/v1beta"
        };

        var sut = CreateSut(handlerMock.Object, settings);

        var result = await sut.ExtractMeterReadingAsync(SampleImageBytes, "image/jpeg", "Water");

        result.ReadingValue.Should().Be(12.34m);
        requestedUrls.Count(u => u.Contains("gemini-3.5-flash:")).Should().Be(2);
        requestedUrls.Count(u => u.Contains("gemini-3.5-flash-lite:")).Should().Be(1);
        fallbackBody.Should().Contain("\"thinkingLevel\":\"medium\"");
    }

    [Fact]
    public async Task ExtractMeterReadingAsync_SameImageTwiceWithCache_CallsGeminiOnlyOnce()
    {
        int calls = 0;
        var handlerMock = CreateHandlerMock((_, _) =>
        {
            calls++;
            var ocrJson = JsonSerializer.Serialize(new
            {
                isMeterImage = true,
                detectedMeterType = "Electricity",
                integerDigits = "05500",
                decimalDigits = "5",
                rawDigits = "05500.5",
                readingValue = 5500.5m,
                confidence = 0.96,
                notes = (string?)null
            });

            return Task.FromResult(BuildGeminiJsonResponse(new[] { new { text = ocrJson } }));
        });

        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var settings = new GeminiSettings
        {
            ApiKey = "cache-key-1",
            ApiKeys = "",
            Model = "gemini-3.5-flash",
            FallbackModels = "",
            CacheMinutes = 20,
            BaseUrl = "https://generativelanguage.googleapis.com/v1beta"
        };

        var sut = CreateSut(handlerMock.Object, settings, memoryCache: memoryCache);
        var uniqueImage = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF, 0x42 };

        var first = await sut.ExtractMeterReadingAsync(uniqueImage, "image/jpeg", "Electricity");
        var second = await sut.ExtractMeterReadingAsync(uniqueImage, "image/jpeg", "Electricity");

        calls.Should().Be(1);
        first.ReadingValue.Should().Be(5500.5m);
        second.ReadingValue.Should().Be(5500.5m);
        second.Should().NotBeSameAs(first);
    }

    [Fact]
    public async Task ExtractMeterReadingAsync_WhenFirstKeyReturns503_RetriesWithSecondKeyAndSucceeds()
    {
        var usedKeys = new List<string>();
        var handlerMock = CreateHandlerMock((req, _) =>
        {
            var key = req.Headers.GetValues("x-goog-api-key").Single();
            usedKeys.Add(key);

            if (usedKeys.Count == 1)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                {
                    Content = new StringContent("{\"error\":{\"code\":503,\"message\":\"high demand\"}}")
                });
            }

            var ocrJson = JsonSerializer.Serialize(new
            {
                isMeterImage = true,
                detectedMeterType = "Electricity",
                integerDigits = "03626",
                decimalDigits = "8",
                rawDigits = "03626.8",
                readingValue = 3626.8m,
                confidence = 0.99,
                notes = (string?)null
            });

            return Task.FromResult(BuildGeminiJsonResponse(new[] { new { text = ocrJson } }));
        });

        var settings = new GeminiSettings
        {
            ApiKey = "retry503-key-1",
            ApiKeys = "retry503-key-2",
            Model = "gemini-3.5-flash",
            FallbackModels = "gemini-3-flash-preview",
            BaseUrl = "https://generativelanguage.googleapis.com/v1beta"
        };

        var sut = CreateSut(handlerMock.Object, settings);

        var result = await sut.ExtractMeterReadingAsync(SampleImageBytes, "image/jpeg", "Electricity");

        result.ReadingValue.Should().Be(3626.8m);
        usedKeys.Should().HaveCount(2);
        usedKeys.Distinct().Should().HaveCount(2);
    }
}
