using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using FUD.DTOs.Utilities;
using FUD.Infrastructure.AI;
using FUD.Infrastructure.Storage;
using FUD.Models;
using FUD.Repositories.Interfaces.Utilities;
using FUD.Services.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace FUD.Tests.Utilities;

public class EwPhotoScanServiceTests
{
    private static readonly byte[] ValidJpegBytes = { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46 };
    private static readonly byte[] ValidPngBytes = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00 };
    private static readonly byte[] ValidWebpBytes = { 0x52, 0x49, 0x46, 0x46, 0x24, 0x00, 0x00, 0x00, 0x57, 0x45, 0x42, 0x50 };
    private static readonly byte[] RiffWavBytes = { 0x52, 0x49, 0x46, 0x46, 0x24, 0x00, 0x00, 0x00, 0x57, 0x41, 0x56, 0x45 };
    private const string ValidCloudinaryUrl = "https://res.cloudinary.com/demo-cloud/image/upload/v1700000000/meter-readings/meter-01.jpg";

    private readonly Mock<IEwRepository> _ewRepoMock = new();
    private readonly Mock<IMeterOcrService> _ocrMock = new();
    private readonly Mock<IFileStorage> _fileStorageMock = new();

    private EwService CreateSut(Dictionary<string, string?>? configValues = null)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues ?? new Dictionary<string, string?>
            {
                ["Cloudinary:CloudName"] = "demo-cloud"
            })
            .Build();

        return new EwService(
            _ewRepoMock.Object,
            inAppNotificationService: null,
            meterOcrService: _ocrMock.Object,
            fileStorage: _fileStorageMock.Object,
            configuration: config);
    }

    private static IFormFile CreateFormFile(byte[] bytes, string fileName = "meter.jpg", string contentType = "image/jpeg")
    {
        var ms = new MemoryStream(bytes);
        return new FormFile(ms, 0, bytes.Length, "photo", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
    }

    [Fact]
    public async Task ScanMeterPhotoAsync_EmptyFile_ThrowsArgumentException()
    {
        var sut = CreateSut();
        var emptyFile = CreateFormFile(Array.Empty<byte>());

        var act = () => sut.ScanMeterPhotoAsync(emptyFile, "Electricity", 100m);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*chụp hoặc chọn ảnh*");
    }

    [Fact]
    public async Task ScanMeterPhotoAsync_FileExceeds10Mb_ThrowsArgumentException()
    {
        var sut = CreateSut();
        var mockFile = new Mock<IFormFile>();
        mockFile.SetupGet(f => f.Length).Returns(10L * 1024 * 1024 + 1);

        var act = () => sut.ScanMeterPhotoAsync(mockFile.Object, "Electricity", 100m);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*10MB*");
    }

    [Fact]
    public async Task ScanMeterPhotoAsync_InvalidMagicBytes_SpoofedJpegContentType_ThrowsArgumentException()
    {
        var sut = CreateSut();
        var spoofedFile = CreateFormFile(new byte[] { 0x3C, 0x73, 0x63, 0x72, 0x69, 0x70, 0x74, 0x3E }, "fake.jpg", "image/jpeg");

        var act = () => sut.ScanMeterPhotoAsync(spoofedFile, "Electricity", 100m);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*JPG, PNG hoặc WebP*");
        _ocrMock.Verify(o => o.ExtractMeterReadingAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ScanMeterPhotoAsync_RiffWavFile_RejectedNotMistakenForWebP()
    {
        var sut = CreateSut();
        var wavFile = CreateFormFile(RiffWavBytes, "audio.webp", "image/webp");

        var act = () => sut.ScanMeterPhotoAsync(wavFile, "Water", 10m);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*JPG, PNG hoặc WebP*");
    }

    [Fact]
    public async Task ScanMeterPhotoAsync_ValidJpeg_CallsOcrAndCloudinaryAndCalculatesConsumption()
    {
        var sut = CreateSut();
        var jpegFile = CreateFormFile(ValidJpegBytes, "elec.jpg", "application/octet-stream");

        _ocrMock.Setup(o => o.ExtractMeterReadingAsync(
                It.Is<byte[]>(b => b.Length == ValidJpegBytes.Length),
                "image/jpeg",
                "Electricity",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MeterOcrRawResult
            {
                IsMeterImage = true,
                DetectedMeterType = "Electricity",
                ReadingValue = 1350.5m,
                RawDigits = "01350.5",
                Confidence = 0.94,
                Notes = "Rõ nét"
            });

        int uploadedStreamLength = 0;
        _fileStorageMock.Setup(s => s.UploadImageAsync(It.IsAny<IFormFile>(), "meter-readings"))
            .Callback<IFormFile, string>((f, _) =>
            {
                using var stream = f.OpenReadStream();
                using var copy = new MemoryStream();
                stream.CopyTo(copy);
                uploadedStreamLength = (int)copy.Length;
            })
            .ReturnsAsync(ValidCloudinaryUrl);

        var result = await sut.ScanMeterPhotoAsync(jpegFile, "Electricity", previousValue: 1200m, saveEvidence: true);

        result.IsMeterImage.Should().BeTrue();
        result.ReadingValue.Should().Be(1350.5m);
        result.DetectedMeterType.Should().Be("Electricity");
        result.MeterTypeMismatch.Should().BeFalse();
        result.ConfidenceLevel.Should().Be("High");
        result.PreviousValue.Should().Be(1200m);
        result.EstimatedConsumption.Should().Be(150.5m);
        result.EvidenceImageUrl.Should().Be(ValidCloudinaryUrl);
        result.ValidationStatus.Should().Be("Valid");
        result.Warnings.Should().BeEmpty();
        uploadedStreamLength.Should().Be(ValidJpegBytes.Length);
    }

    [Fact]
    public async Task ScanMeterPhotoAsync_SaveEvidenceFalse_SkipsCloudinaryUpload()
    {
        var sut = CreateSut();
        var webpFile = CreateFormFile(ValidWebpBytes, "meter.webp", "image/webp");

        _ocrMock.Setup(o => o.ExtractMeterReadingAsync(
                It.IsAny<byte[]>(),
                "image/webp",
                "Water",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MeterOcrRawResult
            {
                IsMeterImage = true,
                DetectedMeterType = "Water",
                ReadingValue = 52m,
                Confidence = 0.92
            });

        var result = await sut.ScanMeterPhotoAsync(webpFile, "Water", previousValue: 40m, saveEvidence: false);

        result.EvidenceImageUrl.Should().BeNull();
        result.ValidationStatus.Should().Be("Valid");
        _fileStorageMock.Verify(s => s.UploadImageAsync(It.IsAny<IFormFile>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ScanMeterPhotoAsync_NotAMeterImage_SkipsCloudinaryAndReturnsErrorStatus()
    {
        var sut = CreateSut();
        var pngFile = CreateFormFile(ValidPngBytes, "desk.png", "image/png");

        _ocrMock.Setup(o => o.ExtractMeterReadingAsync(
                It.IsAny<byte[]>(),
                "image/png",
                "Electricity",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MeterOcrRawResult
            {
                IsMeterImage = false,
                DetectedMeterType = "Unknown",
                ReadingValue = null,
                Confidence = 0.15,
                Notes = "Không thấy công tơ"
            });

        var result = await sut.ScanMeterPhotoAsync(pngFile, "Electricity", previousValue: 100m, saveEvidence: true);

        result.IsMeterImage.Should().BeFalse();
        result.ValidationStatus.Should().Be("Error");
        result.EvidenceImageUrl.Should().BeNull();
        result.Warnings.Should().Contain(w => w.Contains("Không nhận diện được chỉ số"));
        _fileStorageMock.Verify(s => s.UploadImageAsync(It.IsAny<IFormFile>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ScanMeterPhotoAsync_MeterTypeMismatchAndLowConfidence_ReturnsWarnings()
    {
        var sut = CreateSut();
        var jpegFile = CreateFormFile(ValidJpegBytes);

        _ocrMock.Setup(o => o.ExtractMeterReadingAsync(
                It.IsAny<byte[]>(),
                "image/jpeg",
                "Electricity",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MeterOcrRawResult
            {
                IsMeterImage = true,
                DetectedMeterType = "Water",
                ReadingValue = 45m,
                Confidence = 0.48
            });

        _fileStorageMock.Setup(s => s.UploadImageAsync(It.IsAny<IFormFile>(), "meter-readings"))
            .ReturnsAsync(ValidCloudinaryUrl);

        var result = await sut.ScanMeterPhotoAsync(jpegFile, "Electricity", previousValue: null, saveEvidence: true);

        result.MeterTypeMismatch.Should().BeTrue();
        result.ConfidenceLevel.Should().Be("Low");
        result.ValidationStatus.Should().Be("Warning");
        result.Warnings.Should().Contain(w => w.Contains("đồng hồ Nước"));
        result.Warnings.Should().Contain(w => w.Contains("Độ tin cậy nhận diện thấp"));
    }

    [Fact]
    public async Task ScanMeterPhotoAsync_ReadingBelowPreviousOrSpike_ReturnsWarnings()
    {
        var sut = CreateSut();

        // Case 1: Below previous
        _ocrMock.Setup(o => o.ExtractMeterReadingAsync(It.IsAny<byte[]>(), "image/jpeg", "Electricity", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MeterOcrRawResult { IsMeterImage = true, DetectedMeterType = "Electricity", ReadingValue = 95m, Confidence = 0.9 });

        var belowResult = await sut.ScanMeterPhotoAsync(CreateFormFile(ValidJpegBytes), "Electricity", previousValue: 100m, saveEvidence: false);
        belowResult.ValidationStatus.Should().Be("Warning");
        belowResult.EstimatedConsumption.Should().Be(-5m);
        belowResult.Warnings.Should().Contain(w => w.Contains("nhỏ hơn chỉ số cũ"));

        // Case 2: Extreme spike (> 1500 kWh)
        _ocrMock.Setup(o => o.ExtractMeterReadingAsync(It.IsAny<byte[]>(), "image/jpeg", "Electricity", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MeterOcrRawResult { IsMeterImage = true, DetectedMeterType = "Electricity", ReadingValue = 2500m, Confidence = 0.9 });

        var spikeResult = await sut.ScanMeterPhotoAsync(CreateFormFile(ValidJpegBytes), "Electricity", previousValue: 100m, saveEvidence: false);
        spikeResult.ValidationStatus.Should().Be("Warning");
        spikeResult.EstimatedConsumption.Should().Be(2400m);
        spikeResult.Warnings.Should().Contain(w => w.Contains("tăng đột biến"));
    }

    [Fact]
    public async Task ConfirmManualReadingAsync_ValidCloudinaryUrl_PersistsEvidenceAndPhotoOcrSource()
    {
        var sut = CreateSut();
        const int guardUserId = 10;
        var dorm = new Dorm { Id = 1, Name = "Dom A", Code = "A" };
        var guard = new Guard { UserId = guardUserId, DormId = 1, Dorm = dorm };
        var room = new Room
        {
            Id = 101,
            RoomNumber = "A101L",
            Block = new Block { Id = 10, Code = "A1", DormId = 1, Dorm = dorm }
        };

        _ewRepoMock.Setup(r => r.GetBillingPeriodByIdAsync(1))
            .ReturnsAsync(new BillingPeriod { Id = 1, TermId = 1, Status = "Open", TargetMonth = 11, TargetYear = 2026 });
        _ewRepoMock.Setup(r => r.GetGuardByUserIdAsync(guardUserId)).ReturnsAsync(guard);
        _ewRepoMock.Setup(r => r.GetRoomsByBlockCodeAsync("A101")).ReturnsAsync(new List<Room> { room });
        _ewRepoMock.Setup(r => r.GetLatestReadingBeforePeriodAsync(101, "Electricity", 11, 2026))
            .ReturnsAsync(new MeterReading { CurrentValue = 100m });
        _ewRepoMock.Setup(r => r.GetReadingForPeriodAsync(1, 101, "Electricity")).ReturnsAsync((MeterReading?)null);

        MeterReading? addedReading = null;
        _ewRepoMock.Setup(r => r.AddMeterReadingAsync(It.IsAny<MeterReading>()))
            .Callback<MeterReading>(m => addedReading = m)
            .Returns(Task.CompletedTask);

        var result = await sut.ConfirmManualReadingAsync(guardUserId, new ManualConfirmRequestDto
        {
            BillingPeriodId = 1,
            BlockCode = "A101",
            ElectricityLeftNewValue = 160m,
            ElectricityLeftEvidenceUrl = ValidCloudinaryUrl,
            ElectricityLeftSource = "PhotoOCR"
        }, isManager: false);

        result.Saved.Should().Be(1);
        addedReading.Should().NotBeNull();
        addedReading!.EvidenceImageUrl.Should().Be(ValidCloudinaryUrl);
        addedReading.Source.Should().Be("PhotoOCR");
    }

    [Fact]
    public async Task ConfirmManualReadingAsync_ExternalNonCloudinaryUrl_ThrowsArgumentExceptionBeforeSaving()
    {
        var sut = CreateSut();
        const int guardUserId = 10;
        var dorm = new Dorm { Id = 1, Name = "Dom A", Code = "A" };
        var guard = new Guard { UserId = guardUserId, DormId = 1, Dorm = dorm };
        var room = new Room
        {
            Id = 101,
            RoomNumber = "A101L",
            Block = new Block { Id = 10, Code = "A1", DormId = 1, Dorm = dorm }
        };

        _ewRepoMock.Setup(r => r.GetBillingPeriodByIdAsync(1))
            .ReturnsAsync(new BillingPeriod { Id = 1, TermId = 1, Status = "Open", TargetMonth = 11, TargetYear = 2026 });
        _ewRepoMock.Setup(r => r.GetGuardByUserIdAsync(guardUserId)).ReturnsAsync(guard);
        _ewRepoMock.Setup(r => r.GetRoomsByBlockCodeAsync("A101")).ReturnsAsync(new List<Room> { room });

        var act = () => sut.ConfirmManualReadingAsync(guardUserId, new ManualConfirmRequestDto
        {
            BillingPeriodId = 1,
            BlockCode = "A101",
            ElectricityLeftNewValue = 160m,
            ElectricityLeftEvidenceUrl = "https://evil.example.com/meter-readings/tracker.png"
        }, isManager: false);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Đường dẫn ảnh minh chứng*");
        _ewRepoMock.Verify(r => r.AddMeterReadingAsync(It.IsAny<MeterReading>()), Times.Never);
        _ewRepoMock.Verify(r => r.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task ConfirmManualReadingAsync_NonGuardUser_ThrowsInvalidOperationException()
    {
        var sut = CreateSut();
        const int managerUserId = 99;
        var room = new Room
        {
            Id = 101,
            RoomNumber = "A101L",
            Block = new Block { Id = 10, Code = "A1", DormId = 1 }
        };

        _ewRepoMock.Setup(r => r.GetBillingPeriodByIdAsync(1))
            .ReturnsAsync(new BillingPeriod { Id = 1, TermId = 1, Status = "Open", TargetMonth = 11, TargetYear = 2026 });
        _ewRepoMock.Setup(r => r.GetRoomsByBlockCodeAsync("A101")).ReturnsAsync(new List<Room> { room });
        _ewRepoMock.Setup(r => r.GetGuardByUserIdAsync(managerUserId)).ReturnsAsync((Guard?)null);

        var act = () => sut.ConfirmManualReadingAsync(managerUserId, new ManualConfirmRequestDto
        {
            BillingPeriodId = 1,
            BlockCode = "A101",
            ElectricityLeftNewValue = 160m
        }, isManager: true);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*hồ sơ Bảo vệ*");
        _ewRepoMock.Verify(r => r.SaveChangesAsync(), Times.Never);
    }
}
