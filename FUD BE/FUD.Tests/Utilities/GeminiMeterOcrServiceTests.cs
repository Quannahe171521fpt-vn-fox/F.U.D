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
        Dictionary<string, string?>? configValues = null)
    {
        var httpClient = new HttpClient(handler);
        var resolvedSettings = settings ?? new GeminiSettings
        {
            ApiKey = "test-secret-api-key",
            Model = "gemini-3.5-flash-lite",
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
            NullLogger<GeminiMeterOcrService>.Instance);
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
}
