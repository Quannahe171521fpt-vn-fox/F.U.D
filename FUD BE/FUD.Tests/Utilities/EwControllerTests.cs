using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using FUD.Common;
using FUD.Controllers.Utilities;
using FUD.DTOs.Utilities;
using FUD.Services.Interfaces.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace FUD.Tests.Utilities;

/// <summary>
/// Unit tests cho EwController (REST API Điện – Nước) - Tầng Controller.
/// Kiểm tra ánh xạ HTTP status codes, kiểm thực file .xlsx (magic bytes, giới hạn 5MB) và xác thực Claims.
/// </summary>
public class EwControllerTests
{
    private static readonly byte[] ValidXlsxZipHeader = { 0x50, 0x4B, 0x03, 0x04, 0x14, 0x00, 0x06, 0x00 };

    private readonly Mock<IEwService> _ewServiceMock = new();

    private EwController CreateSut(int? userId = 10, string? role = "Guard")
    {
        var claims = new List<Claim>();
        if (userId.HasValue)
            claims.Add(new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString()));
        if (!string.IsNullOrWhiteSpace(role))
            claims.Add(new Claim(ClaimTypes.Role, role));

        var identity = claims.Count > 0 ? new ClaimsIdentity(claims, "TestAuth") : new ClaimsIdentity();
        var controller = new EwController(_ewServiceMock.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(identity)
                }
            }
        };
        return controller;
    }

    private static IFormFile CreateFormFile(byte[] bytes, string fileName, long? reportedLength = null)
    {
        if (reportedLength.HasValue)
        {
            var mock = new Mock<IFormFile>();
            mock.SetupGet(f => f.FileName).Returns(fileName);
            mock.SetupGet(f => f.Length).Returns(reportedLength.Value);
            mock.Setup(f => f.OpenReadStream()).Returns(() => new MemoryStream(bytes));
            return mock.Object;
        }

        return new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", fileName);
    }

    #region Usages, BillingPeriods, Progress & BlockForm Tests

    [Fact]
    public async Task GetUsages_WhenAuthorizedGuard_Returns200OkWithApiResponse()
    {
        // Arrange
        var data = new object[] { new { Block = "A101" } };
        _ewServiceMock.Setup(s => s.GetUsagesAsync("A101", "Electricity", 11, 2026, 1, 2, null, 10, false))
            .ReturnsAsync(data);
        var sut = CreateSut(userId: 10, role: "Guard");

        // Act
        var result = await sut.GetUsages("A101", "Electricity", 11, 2026, 1, 2);

        // Assert
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var body = ok.Value.Should().BeOfType<ApiResponse<IEnumerable<object>>>().Subject;
        body.Success.Should().BeTrue();
        body.Data.Should().BeSameAs(data);
    }

    [Fact]
    public async Task GetUsages_WhenServiceThrowsUnauthorizedAccess_Returns403Forbidden()
    {
        // Arrange
        _ewServiceMock.Setup(s => s.GetUsagesAsync(null, null, null, null, null, null, null, 10, false))
            .ThrowsAsync(new UnauthorizedAccessException("Chưa phân công KTX"));
        var sut = CreateSut(userId: 10, role: "Guard");

        // Act
        var result = await sut.GetUsages(null, null, null, null, null, null);

        // Assert
        var obj = result.Should().BeOfType<ObjectResult>().Subject;
        obj.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task GetBlockProgress_WhenPeriodNotFound_Returns404NotFound()
    {
        // Arrange
        _ewServiceMock.Setup(s => s.GetBlockProgressAsync(99, 1, true))
            .ThrowsAsync(new KeyNotFoundException("Không tìm thấy kỳ"));
        var sut = CreateSut(userId: 1, role: "Manager");

        // Act
        var result = await sut.GetBlockProgress(99);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetBlockFormData_WhenBlockCodeIsBlank_Returns400BadRequest(string? blankBlockCode)
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var result = await sut.GetBlockFormData(blankBlockCode, billingPeriodId: 1);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
        _ewServiceMock.Verify(s => s.GetBlockFormDataAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<bool>()), Times.Never);
    }

    #endregion

    #region ScanMeterPhoto, ConfirmManualReading & UpdateReading Tests

    [Fact]
    public async Task ScanMeterPhoto_WhenOcrTimesOut_Returns504GatewayTimeout()
    {
        // Arrange
        var file = CreateFormFile(new byte[] { 0xFF, 0xD8, 0xFF }, "meter.jpg");
        _ewServiceMock.Setup(s => s.ScanMeterPhotoAsync(file, "Electricity", 100m, true, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException("Hết thời gian chờ AI"));
        var sut = CreateSut();

        // Act
        var result = await sut.ScanMeterPhoto(file, "Electricity", 100m, true);

        // Assert
        var obj = result.Should().BeOfType<ObjectResult>().Subject;
        obj.StatusCode.Should().Be(StatusCodes.Status504GatewayTimeout);
    }

    [Theory]
    [InlineData("Lỗi giới hạn 429 từ Gemini", StatusCodes.Status429TooManyRequests)]
    [InlineData("Dịch vụ AI chưa cấu hình", StatusCodes.Status503ServiceUnavailable)]
    public async Task ScanMeterPhoto_WhenInvalidOperationThrown_MapsRateLimit429OrServiceUnavailable503(
        string exceptionMessage,
        int expectedStatusCode)
    {
        // Arrange
        var file = CreateFormFile(new byte[] { 0xFF, 0xD8, 0xFF }, "meter.jpg");
        _ewServiceMock.Setup(s => s.ScanMeterPhotoAsync(file, null, null, true, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException(exceptionMessage));
        var sut = CreateSut();

        // Act
        var result = await sut.ScanMeterPhoto(file, null, null, true);

        // Assert
        var obj = result.Should().BeOfType<ObjectResult>().Subject;
        obj.StatusCode.Should().Be(expectedStatusCode);
    }

    [Fact]
    public async Task ConfirmManualReading_WhenUserClaimMissing_Returns403Forbidden()
    {
        // Arrange
        var sut = CreateSut(userId: null, role: null);

        // Act
        var result = await sut.ConfirmManualReading(new ManualConfirmRequestDto { BillingPeriodId = 1, BlockCode = "A101" });

        // Assert
        var obj = result.Should().BeOfType<ObjectResult>().Subject;
        obj.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task ConfirmManualReading_WhenManagerAlertedTrue_Returns200OkWithAnomalyWarningMessage()
    {
        // Arrange
        var req = new ManualConfirmRequestDto { BillingPeriodId = 1, BlockCode = "A101", ElectricityLeftNewValue = 2000m };
        _ewServiceMock.Setup(s => s.ConfirmManualReadingAsync(10, req, false))
            .ReturnsAsync(new ManualConfirmResponseDto { Saved = 1, Flagged = 1, ManagerAlerted = true });
        var sut = CreateSut(userId: 10, role: "Guard");

        // Act
        var result = await sut.ConfirmManualReading(req);

        // Assert
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var body = ok.Value.Should().BeOfType<ApiResponse<ManualConfirmResponseDto>>().Subject;
        body.Success.Should().BeTrue();
        body.Message.Should().Contain("bất thường đã gửi cảnh báo");
    }

    [Fact]
    public async Task UpdateReading_WhenValidRequest_Returns200Ok()
    {
        // Arrange
        _ewServiceMock.Setup(s => s.UpdateReadingAsync(15, 250m, "Đã kiểm tra", 1, true)).ReturnsAsync(true);
        var sut = CreateSut(userId: 1, role: "Manager");

        // Act
        var result = await sut.UpdateReading(15, new UpdateReadingRequestDto { CurrentValue = 250m, Note = "Đã kiểm tra" });

        // Assert
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeOfType<ApiResponse>().Which.Success.Should().BeTrue();
    }

    #endregion

    #region Excel Import & Template Validation Tests

    [Fact]
    public async Task PreviewExcelImport_WhenExtensionIsNotXlsx_Returns400BadRequest()
    {
        // Arrange
        var csvFile = CreateFormFile(ValidXlsxZipHeader, "readings.csv");
        var sut = CreateSut();

        // Act
        var result = await sut.PreviewExcelImport(csvFile, billingPeriodId: 1);

        // Assert
        var bad = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        bad.Value.Should().BeOfType<ApiResponse>().Which.Message.Should().Contain(".xlsx");
    }

    [Fact]
    public async Task PreviewExcelImport_WhenFileExceeds5MbLimit_Returns400BadRequest()
    {
        // Arrange
        var oversized = CreateFormFile(ValidXlsxZipHeader, "huge.xlsx", reportedLength: 5L * 1024 * 1024 + 1);
        var sut = CreateSut();

        // Act
        var result = await sut.PreviewExcelImport(oversized, billingPeriodId: 1);

        // Assert
        var bad = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        bad.Value.Should().BeOfType<ApiResponse>().Which.Message.Should().Contain("5MB");
    }

    [Fact]
    public async Task PreviewExcelImport_WhenSpoofedNonZipMagicBytes_Returns400BadRequest()
    {
        // Arrange
        var fakeXlsx = CreateFormFile(new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31 }, "fake.xlsx");
        var sut = CreateSut();

        // Act
        var result = await sut.PreviewExcelImport(fakeXlsx, billingPeriodId: 1);

        // Assert
        var bad = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        bad.Value.Should().BeOfType<ApiResponse>().Which.Message.Should().Contain("không phải định dạng Excel");
    }

    [Fact]
    public async Task PreviewExcelImport_WhenValidXlsxStream_CallsServiceAndReturns200Ok()
    {
        // Arrange
        var validFile = CreateFormFile(ValidXlsxZipHeader, "valid.xlsx");
        var expected = new ExcelPreviewResponseDto { TotalRows = 4, ValidRows = 4 };
        _ewServiceMock.Setup(s => s.PreviewExcelImportAsync(It.IsAny<Stream>(), 1, 10, false))
            .ReturnsAsync(expected);
        var sut = CreateSut(userId: 10, role: "Guard");

        // Act
        var result = await sut.PreviewExcelImport(validFile, billingPeriodId: 1);

        // Assert
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeOfType<ApiResponse<ExcelPreviewResponseDto>>().Which.Data.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task DownloadTemplate_WhenAuthorized_ReturnsXlsxFileResult()
    {
        // Arrange
        var bytes = new byte[] { 0x50, 0x4B, 0x03, 0x04 };
        _ewServiceMock.Setup(s => s.GenerateExcelTemplateAsync(1, "A", "ALL", 1, true)).ReturnsAsync(bytes);
        var sut = CreateSut(userId: 1, role: "Manager");

        // Act
        var result = await sut.DownloadTemplate(1, "A", "ALL");

        // Assert
        var fileResult = result.Should().BeOfType<FileContentResult>().Subject;
        fileResult.FileDownloadName.Should().Be("Ew_Template.xlsx");
        fileResult.ContentType.Should().Be("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        fileResult.FileContents.Should().Equal(bytes);
    }

    #endregion

    #region Anomalies, Surcharges & Student Endpoints Tests

    [Fact]
    public async Task ResolveAnomaly_WhenAnomalyNotFound_Returns404NotFound()
    {
        // Arrange
        var req = new ResolveAnomalyRequestDto { Action = "Confirm" };
        _ewServiceMock.Setup(s => s.ResolveAnomalyAsync(404, req, 1))
            .ThrowsAsync(new KeyNotFoundException("Không tìm thấy"));
        var sut = CreateSut(userId: 1, role: "Manager");

        // Act
        var result = await sut.ResolveAnomaly(404, req);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task CalculatePeriodSurcharges_WhenSucceeded_Returns200OkWithSummary()
    {
        // Arrange
        var calcDto = new BillingCalculationResultDto { BillingPeriodId = 1, RoomsCalculated = 5, InvoicesCreated = 15, Message = "Đã tính xong" };
        _ewServiceMock.Setup(s => s.CalculatePeriodSurchargesAsync(1)).ReturnsAsync(calcDto);
        var sut = CreateSut(userId: 1, role: "Manager");

        // Act
        var result = await sut.CalculatePeriodSurcharges(1);

        // Assert
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var body = ok.Value.Should().BeOfType<ApiResponse<BillingCalculationResultDto>>().Subject;
        body.Data!.RoomsCalculated.Should().Be(5);
    }

    [Fact]
    public async Task GetMyUsage_WhenStudentHasNoActiveResidency_Returns200OkWithNullDataAndFriendlyMessage()
    {
        // Arrange
        _ewServiceMock.Setup(s => s.GetStudentEwReportAsync(50, 1)).ReturnsAsync((StudentEwReportDto?)null);
        var sut = CreateSut(userId: 50, role: "Student");

        // Act
        var result = await sut.GetMyUsage(termId: 1);

        // Assert
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var body = ok.Value.Should().BeOfType<ApiResponse<StudentEwReportDto?>>().Subject;
        body.Success.Should().BeTrue();
        body.Data.Should().BeNull();
        body.Message.Should().Contain("chưa có hồ sơ lưu trú");
    }

    [Fact]
    public async Task GetMyTerms_WhenUserClaimMissing_Returns401Unauthorized()
    {
        // Arrange
        var sut = CreateSut(userId: null, role: null);

        // Act
        var result = await sut.GetMyTerms();

        // Assert
        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    #endregion
}
