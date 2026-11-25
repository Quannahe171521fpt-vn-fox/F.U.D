using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ClosedXML.Excel;
using FluentAssertions;
using FUD.Common;
using FUD.DTOs.Utilities;
using FUD.Infrastructure.Excel;
using FUD.Models;
using FUD.Repositories.Interfaces.Utilities;
using FUD.Services.Interfaces;
using FUD.Services.Utilities;
using Moq;
using Xunit;

namespace FUD.Tests.Utilities;

/// <summary>
/// Unit tests toàn diện cho EwService (Quản lý Điện – Nước) - Tầng Service.
/// Tuân thủ cấu trúc AAA (Arrange - Act - Assert), cách ly hoàn toàn dependency bằng Moq.
/// </summary>
public class EwServiceTests
{
    private readonly Mock<IEwRepository> _ewRepoMock = new();
    private readonly Mock<IInAppNotificationService> _notificationMock = new();

    private EwService CreateSut(bool withNotification = true) =>
        new(
            _ewRepoMock.Object,
            withNotification ? _notificationMock.Object : null);

    private static BillingPeriod CreatePeriod(
        int id = 1,
        int termId = 1,
        int month = 11,
        int year = 2026,
        string status = "Open") => new()
    {
        Id = id,
        TermId = termId,
        TargetMonth = month,
        TargetYear = year,
        Status = status,
        Term = new Term
        {
            Id = termId,
            Name = "Fall",
            AcademicYear = "2026",
            StartDate = new DateOnly(2026, 9, 1),
            EndDate = new DateOnly(2026, 12, 31)
        }
    };

    private static (Dorm Dorm, Block Block, Room RoomL, Room RoomR, Guard Guard) CreateStandardBlockSetup(
        int dormId = 1,
        string dormCode = "A",
        string blockCode = "A101",
        int guardUserId = 10)
    {
        var dorm = new Dorm { Id = dormId, Code = dormCode, Name = $"Dom {dormCode}", Status = "Active" };
        var block = new Block { Id = 10, DormId = dormId, Code = blockCode, Name = $"Block {blockCode}", Status = "Active", Dorm = dorm };
        var roomL = new Room { Id = 101, BlockId = 10, RoomNumber = $"{blockCode}L", Status = "Active", Block = block };
        var roomR = new Room { Id = 102, BlockId = 10, RoomNumber = $"{blockCode}R", Status = "Active", Block = block };
        var guard = new Guard { UserId = guardUserId, DormId = dormId, Dorm = dorm };
        return (dorm, block, roomL, roomR, guard);
    }

    private static MemoryStream BuildExcelStream(params (string Dorm, string Block, string Type, string Date, object? L, object? R)[] rows)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Sheet1");
        ws.Cell(1, 1).Value = "Dorm";
        ws.Cell(1, 2).Value = "Block";
        ws.Cell(1, 3).Value = "Type";
        ws.Cell(1, 4).Value = "Date";
        ws.Cell(1, 5).Value = "L";
        ws.Cell(1, 6).Value = "R";
        ws.Cell(1, 7).Value = "Term";

        int rowIdx = 2;
        foreach (var r in rows)
        {
            ws.Cell(rowIdx, 1).Value = r.Dorm;
            ws.Cell(rowIdx, 2).Value = r.Block;
            ws.Cell(rowIdx, 3).Value = r.Type;
            ws.Cell(rowIdx, 4).Value = r.Date;
            if (r.L is decimal ld) ws.Cell(rowIdx, 5).Value = ld;
            else if (r.L is string ls) ws.Cell(rowIdx, 5).Value = ls;
            if (r.R is decimal rd) ws.Cell(rowIdx, 6).Value = rd;
            else if (r.R is string rs) ws.Cell(rowIdx, 6).Value = rs;
            ws.Cell(rowIdx, 7).Value = 1;
            rowIdx++;
        }

        var ms = new MemoryStream();
        wb.SaveAs(ms);
        ms.Position = 0;
        return ms;
    }

    #region CheckReadingAnomaly Boundary & Partitioning Tests

    [Theory]
    [InlineData("Electricity", 100, 150, 0)]        // Normal: 50 kWh
    [InlineData("Electricity", 0, 1200, 0)]         // Exact upper boundary: 1200 - 200 = 1000 (not > 1000)
    [InlineData("Electricity", 100, 500, 800)]      // Cumulative exact boundary: 800 + 400 = 1200
    [InlineData("Water", 10, 18, 0)]                // Normal: 8 m3
    [InlineData("Water", 0, 62, 0)]                 // Exact upper boundary: 62 - 12 = 50 (not > 50)
    [InlineData("Water", 20, 42, 40)]               // Cumulative exact boundary: 40 + 22 = 62
    public void CheckReadingAnomaly_WhenReadingWithinOrAtBoundary_ReturnsNotFlagged(
        string meterType,
        decimal oldReading,
        decimal newReading,
        decimal prevTermConsumption)
    {
        // Arrange & Act
        var (isFlagged, reason) = EwService.CheckReadingAnomaly(meterType, oldReading, newReading, prevTermConsumption);

        // Assert
        isFlagged.Should().BeFalse();
        reason.Should().BeNull();
    }

    [Theory]
    [InlineData("Electricity", 200, 199.9, 0, "nhỏ hơn chỉ số cũ")]
    [InlineData("Water", 50, 50, 0, "Mức tiêu thụ bằng 0")]
    [InlineData("Electricity", 0, 1200.01, 0, "vượt định mức")]
    [InlineData("Electricity", 100, 501, 800, "vượt định mức")]
    [InlineData("Water", 0, 62.01, 0, "vượt định mức")]
    [InlineData("Water", 10, 33, 40, "rò rỉ đường ống nước")]
    public void CheckReadingAnomaly_WhenReadingViolatesThreshold_ReturnsFlaggedWithReason(
        string meterType,
        decimal oldReading,
        decimal newReading,
        decimal prevTermConsumption,
        string expectedReasonSubstring)
    {
        // Arrange & Act
        var (isFlagged, reason) = EwService.CheckReadingAnomaly(meterType, oldReading, newReading, prevTermConsumption);

        // Assert
        isFlagged.Should().BeTrue();
        reason.Should().Contain(expectedReasonSubstring);
    }

    #endregion

    #region GetUsagesAsync & GetBillingPeriodsAsync Tests

    [Fact]
    public async Task GetUsagesAsync_WhenCallerIsManager_ForwardsFiltersWithoutOverridingDormId()
    {
        // Arrange
        var expected = new object[] { new { Block = "A101" } };
        _ewRepoMock.Setup(r => r.GetGroupedUsagesAsync("A101", "Electricity", 11, 2026, 1, 2, 5))
            .ReturnsAsync(expected);
        var sut = CreateSut();

        // Act
        var result = await sut.GetUsagesAsync("A101", "Electricity", 11, 2026, termId: 1, billingPeriodId: 2, dormId: 5, userId: 99, isManager: true);

        // Assert
        result.Should().BeSameAs(expected);
        _ewRepoMock.Verify(r => r.GetGuardDormIdAsync(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task GetBillingPeriodsAsync_WhenPeriodsExist_MapsLookupDtosWithFormattedNames()
    {
        // Arrange
        var periods = new List<BillingPeriod>
        {
            CreatePeriod(id: 1, termId: 2, month: 11, year: 2026, status: "Open"),
            new() { Id = 2, TermId = 2, TargetMonth = 10, TargetYear = 2026, Status = "Calculated", Term = null! }
        };
        _ewRepoMock.Setup(r => r.GetAllBillingPeriodsAsync()).ReturnsAsync(periods);
        var sut = CreateSut();

        // Act
        var result = await sut.GetBillingPeriodsAsync();

        // Assert
        result.Should().HaveCount(2);
        result[0].DisplayName.Should().Be("Tháng 11/2026");
        result[0].TermName.Should().Be("Fall (2026)");
        result[1].DisplayName.Should().Be("Tháng 10/2026");
        result[1].TermName.Should().BeEmpty();
    }

    #endregion

    #region GetBlockProgressAsync & GetBlockFormDataAsync Tests

    [Fact]
    public async Task GetBlockProgressAsync_WhenPeriodDoesNotExist_ThrowsKeyNotFoundException()
    {
        // Arrange
        _ewRepoMock.Setup(r => r.GetBillingPeriodByIdAsync(999)).ReturnsAsync((BillingPeriod?)null);
        var sut = CreateSut();

        // Act
        var act = () => sut.GetBlockProgressAsync(999, userId: 1, isManager: true);

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("*999*");
    }

    [Fact]
    public async Task GetBlockProgressAsync_WhenBlocksHaveCompleteAndIncompleteReadings_ComputesProgressAccurately()
    {
        // Arrange
        var period = CreatePeriod(id: 1, month: 11, year: 2026);
        var (_, block1, room1L, room1R, _) = CreateStandardBlockSetup(blockCode: "A101");
        var block2 = new Block { Id = 20, DormId = 1, Code = "A102", Name = "Block A102", Dorm = block1.Dorm };
        var room2L = new Room { Id = 201, BlockId = 20, RoomNumber = "A102L", Status = "Active", Block = block2 };

        var readings = new List<MeterReading>
        {
            new() { RoomId = 101, MeterType = "Electricity", CurrentValue = 120, Status = "Recorded", RecordedTime = new DateTime(2026, 11, 20) },
            new() { RoomId = 101, MeterType = "Water", CurrentValue = 15, Status = "Recorded", RecordedTime = new DateTime(2026, 11, 20) },
            new() { RoomId = 102, MeterType = "Electricity", CurrentValue = 130, Status = "Recorded", RecordedTime = new DateTime(2026, 11, 20) },
            new() { RoomId = 102, MeterType = "Water", CurrentValue = 18, Status = "Recorded", RecordedTime = new DateTime(2026, 11, 20) },
            new() { RoomId = 201, MeterType = "Electricity", CurrentValue = 90, Status = "Recorded", RecordedTime = new DateTime(2026, 11, 20) }
        };

        _ewRepoMock.Setup(r => r.GetBillingPeriodByIdAsync(1)).ReturnsAsync(period);
        _ewRepoMock.Setup(r => r.GetAllActiveRoomsWithBlockAsync()).ReturnsAsync(new List<Room> { room1L, room1R, room2L });
        _ewRepoMock.Setup(r => r.GetReadingsByPeriodAsync(1)).ReturnsAsync(readings);
        var sut = CreateSut();

        // Act
        var result = await sut.GetBlockProgressAsync(1, userId: 1, isManager: true);

        // Assert
        result.TotalBlocks.Should().Be(2);
        result.CompletedBlocks.Should().Be(1);
        result.MissingBlocks.Should().Be(1);
        result.Blocks.First(b => b.BlockCode == "A101").WaterRight!.HasReading.Should().BeTrue();
        result.Blocks.First(b => b.BlockCode == "A102").WaterLeft!.HasReading.Should().BeFalse();
    }

    [Fact]
    public async Task GetBlockFormDataAsync_WhenNoRoomsMatchBlockCode_ThrowsKeyNotFoundException()
    {
        // Arrange
        _ewRepoMock.Setup(r => r.GetBillingPeriodByIdAsync(1)).ReturnsAsync(CreatePeriod());
        _ewRepoMock.Setup(r => r.GetRoomsByBlockCodeAsync("Z999")).ReturnsAsync(new List<Room>());
        var sut = CreateSut();

        // Act
        var act = () => sut.GetBlockFormDataAsync("Z999", 1, userId: 1, isManager: true);

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("*Z999*");
    }

    [Fact]
    public async Task GetBlockFormDataAsync_WhenLeftAndRightRoomsHaveHistory_PopulatesOldAndCurrentReadings()
    {
        // Arrange
        var period = CreatePeriod(id: 1, month: 11, year: 2026);
        var (_, _, roomL, roomR, _) = CreateStandardBlockSetup();

        _ewRepoMock.Setup(r => r.GetBillingPeriodByIdAsync(1)).ReturnsAsync(period);
        _ewRepoMock.Setup(r => r.GetRoomsByBlockCodeAsync("A101")).ReturnsAsync(new List<Room> { roomL, roomR });
        _ewRepoMock.Setup(r => r.GetLatestReadingBeforePeriodAsync(101, "Electricity", 11, 2026))
            .ReturnsAsync(new MeterReading { CurrentValue = 250m });
        _ewRepoMock.Setup(r => r.GetReadingForPeriodAsync(1, 101, "Electricity"))
            .ReturnsAsync(new MeterReading { CurrentValue = 310m, Source = "PhotoOCR", EvidenceImageUrl = "https://res.cloudinary.com/demo/image/upload/meter-readings/a.jpg" });
        var sut = CreateSut();

        // Act
        var result = await sut.GetBlockFormDataAsync("A101", 1, userId: 1, isManager: true);

        // Assert
        result.BlockCode.Should().Be("A101");
        result.ElectricityLeft!.OldReading.Should().Be(250m);
        result.ElectricityLeft.AlreadyRecorded.Should().BeTrue();
        result.ElectricityLeft.ExistingCurrentValue.Should().Be(310m);
        result.ElectricityLeft.ExistingSource.Should().Be("PhotoOCR");
        result.ElectricityRight!.RoomId.Should().Be(102);
        result.ElectricityRight.AlreadyRecorded.Should().BeFalse();
    }

    #endregion

    #region ConfirmManualReadingAsync Tests

    [Fact]
    public async Task ConfirmManualReadingAsync_WhenPeriodStatusIsClosed_ThrowsInvalidOperationException()
    {
        // Arrange
        _ewRepoMock.Setup(r => r.GetBillingPeriodByIdAsync(1))
            .ReturnsAsync(CreatePeriod(status: "Calculated"));
        var sut = CreateSut();

        // Act
        var act = () => sut.ConfirmManualReadingAsync(10, new ManualConfirmRequestDto { BillingPeriodId = 1, BlockCode = "A101" });

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*hiện không mở*");
    }

    [Fact]
    public async Task ConfirmManualReadingAsync_WhenNegativeValueAndImportOnlyValidFalse_ThrowsArgumentException()
    {
        // Arrange
        var period = CreatePeriod();
        var (_, _, roomL, _, guard) = CreateStandardBlockSetup();
        _ewRepoMock.Setup(r => r.GetBillingPeriodByIdAsync(1)).ReturnsAsync(period);
        _ewRepoMock.Setup(r => r.GetRoomsByBlockCodeAsync("A101")).ReturnsAsync(new List<Room> { roomL });
        _ewRepoMock.Setup(r => r.GetGuardByUserIdAsync(10)).ReturnsAsync(guard);
        var sut = CreateSut();

        var dto = new ManualConfirmRequestDto
        {
            BillingPeriodId = 1,
            BlockCode = "A101",
            ElectricityLeftNewValue = -5m,
            ImportOnlyValid = false
        };

        // Act
        var act = () => sut.ConfirmManualReadingAsync(10, dto);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*không được âm*");
    }

    [Fact]
    public async Task ConfirmManualReadingAsync_WhenNegativeValueAndImportOnlyValidTrue_SkipsNegativeSlot()
    {
        // Arrange
        var period = CreatePeriod();
        var (_, _, roomL, _, guard) = CreateStandardBlockSetup();
        _ewRepoMock.Setup(r => r.GetBillingPeriodByIdAsync(1)).ReturnsAsync(period);
        _ewRepoMock.Setup(r => r.GetRoomsByBlockCodeAsync("A101")).ReturnsAsync(new List<Room> { roomL });
        _ewRepoMock.Setup(r => r.GetGuardByUserIdAsync(10)).ReturnsAsync(guard);
        var sut = CreateSut();

        var dto = new ManualConfirmRequestDto
        {
            BillingPeriodId = 1,
            BlockCode = "A101",
            ElectricityLeftNewValue = -10m,
            ImportOnlyValid = true
        };

        // Act
        var result = await sut.ConfirmManualReadingAsync(10, dto);

        // Assert
        result.Saved.Should().Be(0);
        result.Skipped.Should().Be(1);
        result.Results.Single().Status.Should().Be("Skipped");
    }

    [Fact]
    public async Task ConfirmManualReadingAsync_WhenPhotoOcrSourceWithoutEvidenceImage_ThrowsArgumentException()
    {
        // Arrange
        var period = CreatePeriod();
        var (_, _, roomL, _, guard) = CreateStandardBlockSetup();
        _ewRepoMock.Setup(r => r.GetBillingPeriodByIdAsync(1)).ReturnsAsync(period);
        _ewRepoMock.Setup(r => r.GetRoomsByBlockCodeAsync("A101")).ReturnsAsync(new List<Room> { roomL });
        _ewRepoMock.Setup(r => r.GetGuardByUserIdAsync(10)).ReturnsAsync(guard);
        var sut = CreateSut();

        var dto = new ManualConfirmRequestDto
        {
            BillingPeriodId = 1,
            BlockCode = "A101",
            ElectricityLeftNewValue = 150m,
            ElectricityLeftSource = "PhotoOCR",
            ElectricityLeftEvidenceUrl = null
        };

        // Act
        var act = () => sut.ConfirmManualReadingAsync(10, dto);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*PhotoOCR*");
    }

    [Fact]
    public async Task ConfirmManualReadingAsync_WhenNormalReadingProducesSurcharge_NotifiesCheckedInStudents()
    {
        // Arrange
        var period = CreatePeriod(id: 1, month: 11, year: 2026);
        var (_, _, roomL, _, guard) = CreateStandardBlockSetup();
        _ewRepoMock.Setup(r => r.GetBillingPeriodByIdAsync(1)).ReturnsAsync(period);
        _ewRepoMock.Setup(r => r.GetRoomsByBlockCodeAsync("A101")).ReturnsAsync(new List<Room> { roomL });
        _ewRepoMock.Setup(r => r.GetGuardByUserIdAsync(10)).ReturnsAsync(guard);
        _ewRepoMock.Setup(r => r.GetLatestReadingBeforePeriodAsync(101, "Electricity", 11, 2026))
            .ReturnsAsync(new MeterReading { CurrentValue = 100m });
        _ewRepoMock.Setup(r => r.CalculateAndPublishRoomSurchargeAsync(1, 101))
            .ReturnsAsync(new RoomSurchargeCalculationResult
            {
                RoomId = 101,
                RoomNumber = "A101L",
                Calculated = true,
                TotalCost = 100000m,
                NumberOfResidents = 4,
                SharePerStudent = 25000m,
                InvoicesCreated = 4,
                NotifiedStudentIds = new List<int> { 201, 202, 203, 204 }
            });
        var sut = CreateSut();

        var dto = new ManualConfirmRequestDto
        {
            BillingPeriodId = 1,
            BlockCode = "A101",
            ElectricityLeftNewValue = 340m
        };

        // Act
        var result = await sut.ConfirmManualReadingAsync(10, dto);

        // Assert
        result.Saved.Should().Be(1);
        result.Flagged.Should().Be(0);
        _notificationMock.Verify(
            n => n.NotifyUsersAsync(
                It.Is<IEnumerable<int>>(ids => ids.Count() == 4),
                It.Is<string>(t => t.Contains("Thông báo công nợ điện nước")),
                It.Is<string?>(c => c != null && c.Contains("25,000") || c != null && c.Contains("25.000")),
                "Utility",
                "1",
                false),
            Times.Once);
    }

    [Fact]
    public async Task ConfirmManualReadingAsync_WhenReadingIsAnomalous_CreatesUsageAnomalyAndAlertsManagerRole()
    {
        // Arrange
        var period = CreatePeriod(id: 1, month: 11, year: 2026);
        var (_, _, roomL, _, guard) = CreateStandardBlockSetup();
        _ewRepoMock.Setup(r => r.GetBillingPeriodByIdAsync(1)).ReturnsAsync(period);
        _ewRepoMock.Setup(r => r.GetRoomsByBlockCodeAsync("A101")).ReturnsAsync(new List<Room> { roomL });
        _ewRepoMock.Setup(r => r.GetGuardByUserIdAsync(10)).ReturnsAsync(guard);
        _ewRepoMock.Setup(r => r.GetLatestReadingBeforePeriodAsync(101, "Electricity", 11, 2026))
            .ReturnsAsync(new MeterReading { CurrentValue = 200m });
        _ewRepoMock.Setup(r => r.CalculateAndPublishRoomSurchargeAsync(1, 101))
            .ReturnsAsync(new RoomSurchargeCalculationResult { RoomId = 101, Calculated = false, HasFlaggedReadings = true });
        var sut = CreateSut();

        var dto = new ManualConfirmRequestDto
        {
            BillingPeriodId = 1,
            BlockCode = "A101",
            ElectricityLeftNewValue = 180m // Smaller than 200 -> Flagged
        };

        // Act
        var result = await sut.ConfirmManualReadingAsync(10, dto);

        // Assert
        result.Saved.Should().Be(1);
        result.Flagged.Should().Be(1);
        result.ManagerAlerted.Should().BeTrue();
        _ewRepoMock.Verify(r => r.AddUsageAnomalyAsync(It.Is<UsageAnomaly>(a => a.Status == "Flagged")), Times.Once);
        _notificationMock.Verify(
            n => n.NotifyRolesAsync(
                It.Is<IEnumerable<string>>(roles => roles.Contains(AccountConstants.Roles.Manager)),
                It.Is<string>(t => t.Contains("Cảnh báo chỉ số")),
                It.IsAny<string?>(),
                "Utility",
                "1"),
            Times.Once);
    }

    #endregion

    #region UpdateReadingAsync Tests

    [Fact]
    public async Task UpdateReadingAsync_WhenReadingDoesNotExist_ThrowsKeyNotFoundException()
    {
        // Arrange
        _ewRepoMock.Setup(r => r.GetReadingByIdAsync(404)).ReturnsAsync((MeterReading?)null);
        var sut = CreateSut();

        // Act
        var act = () => sut.UpdateReadingAsync(404, 150m, "Note", userId: 1, isManager: true);

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task UpdateReadingAsync_WhenCurrentValueIsNegative_ThrowsArgumentException()
    {
        // Arrange
        _ewRepoMock.Setup(r => r.GetReadingByIdAsync(10)).ReturnsAsync(new MeterReading { Id = 10, PreviousValue = 50m });
        var sut = CreateSut();

        // Act
        var act = () => sut.UpdateReadingAsync(10, -1m, null, userId: 1, isManager: true);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*số âm*");
    }

    [Fact]
    public async Task UpdateReadingAsync_WhenNewValueBelowPrevious_FlagsReadingAndAddsAnomaly()
    {
        // Arrange
        var reading = new MeterReading { Id = 10, BillingPeriodId = 1, RoomId = 101, PreviousValue = 100m, CurrentValue = 120m };
        _ewRepoMock.Setup(r => r.GetReadingByIdAsync(10)).ReturnsAsync(reading);
        var sut = CreateSut();

        // Act
        var result = await sut.UpdateReadingAsync(10, 80m, "Đọc nhầm", userId: 1, isManager: true);

        // Assert
        result.Should().BeTrue();
        reading.Status.Should().Be("Flagged");
        _ewRepoMock.Verify(r => r.AddUsageAnomalyAsync(It.Is<UsageAnomaly>(a => a.ReadingId == 10 && a.Status == "Flagged")), Times.Once);
        _ewRepoMock.Verify(r => r.CalculateAndPublishRoomSurchargeAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task UpdateReadingAsync_WhenManagerCorrectsFlaggedReading_ResolvesOpenAnomaliesAndRecalculatesSurcharge()
    {
        // Arrange
        var period = CreatePeriod(id: 1);
        var openAnomaly = new UsageAnomaly { Id = 5, ReadingId = 10, Status = "Flagged" };
        var reading = new MeterReading { Id = 10, BillingPeriodId = 1, RoomId = 101, PreviousValue = 100m, CurrentValue = 90m, Status = "Flagged" };

        _ewRepoMock.Setup(r => r.GetReadingByIdAsync(10)).ReturnsAsync(reading);
        _ewRepoMock.Setup(r => r.GetValidManagerIdAsync(1)).ReturnsAsync(1);
        _ewRepoMock.Setup(r => r.GetActiveAnomaliesByReadingIdAsync(10)).ReturnsAsync(new List<UsageAnomaly> { openAnomaly });
        _ewRepoMock.Setup(r => r.GetBillingPeriodByIdAsync(1)).ReturnsAsync(period);
        _ewRepoMock.Setup(r => r.CalculateAndPublishRoomSurchargeAsync(1, 101))
            .ReturnsAsync(new RoomSurchargeCalculationResult { RoomId = 101, Calculated = true });
        var sut = CreateSut();

        // Act
        var result = await sut.UpdateReadingAsync(10, 145m, "Đã kiểm tra lại", userId: 1, isManager: true);

        // Assert
        result.Should().BeTrue();
        reading.Status.Should().Be("Resolved");
        openAnomaly.Status.Should().Be("Resolved");
        openAnomaly.Resolution.Should().Be("Corrected");
        openAnomaly.ResolvedById.Should().Be(1);
        _ewRepoMock.Verify(r => r.CalculateAndPublishRoomSurchargeAsync(1, 101), Times.Once);
    }

    #endregion

    #region Excel Preview, Confirm, Batches & Template Tests

    [Fact]
    public async Task PreviewExcelImportAsync_WhenMixedValidWarningAndUnknownBlockRows_ReturnsAccurateBreakdown()
    {
        // Arrange
        var period = CreatePeriod(id: 1, month: 11, year: 2026);
        var (_, _, roomL, roomR, _) = CreateStandardBlockSetup(blockCode: "A101");
        using var stream = BuildExcelStream(
            ("A", "A101", "E", "30/11/2026", 150m, 160m),  // Valid
            ("A", "A101", "W", "30/11/2026", 0m, 15m),     // Left consumption = 0 -> Warning
            ("A", "Z999", "E", "30/11/2026", 100m, 100m)); // Unknown block -> Error

        _ewRepoMock.Setup(r => r.GetBillingPeriodByIdAsync(1)).ReturnsAsync(period);
        _ewRepoMock.Setup(r => r.GetReadingsByPeriodAsync(1)).ReturnsAsync(new List<MeterReading>());
        _ewRepoMock.Setup(r => r.GetRoomsByBlockCodeAsync("A101")).ReturnsAsync(new List<Room> { roomL, roomR });
        _ewRepoMock.Setup(r => r.GetRoomsByBlockCodeAsync("Z999")).ReturnsAsync(new List<Room>());
        var sut = CreateSut();

        // Act
        var preview = await sut.PreviewExcelImportAsync(stream, 1, userId: 1, isManager: true);

        // Assert
        preview.TotalRows.Should().Be(3);
        preview.ValidRows.Should().Be(1);
        preview.WarningRows.Should().Be(1);
        preview.ErrorRows.Should().Be(1);
        preview.Rows[1].Status.Should().Be("Warning");
        preview.Rows[2].Status.Should().Be("Error");
    }

    [Fact]
    public async Task ConfirmExcelImportAsync_WhenErrorRowPresentAndImportOnlyValidFalse_ThrowsArgumentException()
    {
        // Arrange
        var period = CreatePeriod(id: 1, status: "Open");
        _ewRepoMock.Setup(r => r.GetBillingPeriodByIdAsync(1)).ReturnsAsync(period);
        _ewRepoMock.Setup(r => r.GetAllBlocksAsync()).ReturnsAsync(new List<Block> { new() { Id = 1, Code = "A101" } });
        var sut = CreateSut();

        var request = new ExcelConfirmImportRequestDto
        {
            BillingPeriodId = 1,
            FileName = "test.xlsx",
            ImportOnlyValid = false,
            Rows = new List<ExcelPreviewRowDto>
            {
                new() { RowIndex = 2, BlockCode = "Z999", Status = "Error", ErrorMessage = "Không tìm thấy block" }
            }
        };

        // Act
        var act = () => sut.ConfirmExcelImportAsync(1, request, isManager: true);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Dòng 2 có lỗi*");
    }

    [Fact]
    public async Task ConfirmExcelImportAsync_WhenValidAndWarningRows_SavesReadingsAlertsManagerAndCalculatesSurcharges()
    {
        // Arrange
        var period = CreatePeriod(id: 1, status: "Open");
        _ewRepoMock.Setup(r => r.GetBillingPeriodByIdAsync(1)).ReturnsAsync(period);
        _ewRepoMock.Setup(r => r.GetAllBlocksAsync()).ReturnsAsync(new List<Block> { new() { Id = 10, Code = "A101" } });
        _ewRepoMock.Setup(r => r.CalculateAndPublishRoomSurchargeAsync(1, It.IsAny<int>()))
            .ReturnsAsync(new RoomSurchargeCalculationResult { Calculated = true });
        var sut = CreateSut();

        var request = new ExcelConfirmImportRequestDto
        {
            BillingPeriodId = 1,
            FileName = "import_t11.xlsx",
            ImportOnlyValid = true,
            Rows = new List<ExcelPreviewRowDto>
            {
                new()
                {
                    RowIndex = 2,
                    BlockCode = "A101",
                    MeterType = "Electricity",
                    LeftRoomId = 101,
                    LeftRoomNumber = "A101L",
                    LeftOldReading = 100m,
                    LeftValue = 160m,   // Normal
                    RightRoomId = 102,
                    RightRoomNumber = "A101R",
                    RightOldReading = 200m,
                    RightValue = 180m,  // Anomalous (< old)
                    Status = "Warning"
                }
            }
        };

        // Act
        var result = await sut.ConfirmExcelImportAsync(10, request, isManager: true);

        // Assert
        result.SavedReadings.Should().Be(2);
        result.FlaggedReadings.Should().Be(1);
        result.ManagerAlerted.Should().BeTrue();
        _ewRepoMock.Verify(r => r.AddImportBatchAsync(It.Is<ImportBatch>(b => b.FileName == "import_t11.xlsx")), Times.Once);
        _notificationMock.Verify(
            n => n.NotifyRolesAsync(
                It.IsAny<IEnumerable<string>>(),
                It.Is<string>(t => t.Contains("Cảnh báo chỉ số")),
                It.Is<string?>(c => c != null && c.Contains("import_t11.xlsx")),
                "Utility",
                "1"),
            Times.Once);
    }

    [Fact]
    public async Task GenerateExcelTemplateAsync_WhenManagerSpecifiesDormAndElectricityOnly_ReturnsFilteredTemplateBytes()
    {
        // Arrange
        var period = CreatePeriod(id: 1, termId: 3, month: 11, year: 2026);
        var dormA = new Dorm { Id = 1, Code = "DOM-A", Name = "Dom A" };
        var dormB = new Dorm { Id = 2, Code = "DOM-B", Name = "Dom B" };
        var blocks = new List<Block>
        {
            new() { Id = 1, DormId = 1, Code = "A101", Dorm = dormA },
            new() { Id = 2, DormId = 2, Code = "B101", Dorm = dormB }
        };

        _ewRepoMock.Setup(r => r.GetBillingPeriodByIdAsync(1)).ReturnsAsync(period);
        _ewRepoMock.Setup(r => r.GetAllBlocksAsync()).ReturnsAsync(blocks);
        var sut = CreateSut();

        // Act
        var bytes = await sut.GenerateExcelTemplateAsync(1, dormCode: "A", meterType: "Electricity", userId: 1, isManager: true);

        // Assert
        bytes.Should().NotBeEmpty();
        using var wb = new XLWorkbook(new MemoryStream(bytes));
        var ws = wb.Worksheet(1);
        ws.RowsUsed().Count().Should().Be(2); // Header + 1 row (A101, E)
        ws.Cell(2, 1).GetString().Should().Be("A");
        ws.Cell(2, 2).GetString().Should().Be("A101");
        ws.Cell(2, 3).GetString().Should().Be("E");
        ws.Cell(2, 4).GetString().Should().Be("30/11/2026");
    }

    #endregion

    #region Anomalies & Surcharge Calculation Tests

    [Fact]
    public async Task ResolveAnomalyAsync_WhenAnomalyDoesNotExist_ThrowsKeyNotFoundException()
    {
        // Arrange
        _ewRepoMock.Setup(r => r.GetAnomalyByIdAsync(999)).ReturnsAsync((UsageAnomaly?)null);
        var sut = CreateSut();

        // Act
        var act = () => sut.ResolveAnomalyAsync(999, new ResolveAnomalyRequestDto { Action = "Confirm" }, managerUserId: 1);

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task ResolveAnomalyAsync_WhenConfirmActionOnReadingSmallerThanPrevious_ThrowsArgumentException()
    {
        // Arrange
        var anomaly = new UsageAnomaly
        {
            Id = 5,
            ReadingId = 10,
            Status = "Flagged",
            Reading = new MeterReading { Id = 10, PreviousValue = 200m, CurrentValue = 150m }
        };
        _ewRepoMock.Setup(r => r.GetAnomalyByIdAsync(5)).ReturnsAsync(anomaly);
        var sut = CreateSut();

        // Act
        var act = () => sut.ResolveAnomalyAsync(5, new ResolveAnomalyRequestDto { Action = "Confirm" }, managerUserId: 1);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*nhỏ hơn chỉ số cũ*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData(-10.0)]
    [InlineData(90.0)]
    public async Task ResolveAnomalyAsync_WhenAdjustActionHasInvalidValue_ThrowsArgumentException(double? invalidAdjustedVal)
    {
        // Arrange
        var anomaly = new UsageAnomaly
        {
            Id = 5,
            ReadingId = 10,
            Status = "Flagged",
            Reading = new MeterReading { Id = 10, PreviousValue = 100m, CurrentValue = 1500m }
        };
        _ewRepoMock.Setup(r => r.GetAnomalyByIdAsync(5)).ReturnsAsync(anomaly);
        var sut = CreateSut();

        var req = new ResolveAnomalyRequestDto
        {
            Action = "Adjust",
            AdjustedValue = invalidAdjustedVal.HasValue ? (decimal)invalidAdjustedVal.Value : null
        };

        // Act
        var act = () => sut.ResolveAnomalyAsync(5, req, managerUserId: 1);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ResolveAnomalyAsync_WhenAdjustActionIsValid_MarksCorrectedUpdatesReadingAndTriggersSurcharge()
    {
        // Arrange
        var period = CreatePeriod(id: 1, month: 11, year: 2026);
        var reading = new MeterReading
        {
            Id = 10,
            BillingPeriodId = 1,
            BillingPeriod = period,
            RoomId = 101,
            Room = new Room { Id = 101, RoomNumber = "A101L" },
            PreviousValue = 100m,
            CurrentValue = 2500m,
            Status = "Flagged"
        };
        var anomaly = new UsageAnomaly { Id = 5, ReadingId = 10, Status = "Flagged", Reading = reading };

        _ewRepoMock.Setup(r => r.GetAnomalyByIdAsync(5)).ReturnsAsync(anomaly);
        _ewRepoMock.Setup(r => r.GetValidManagerIdAsync(1)).ReturnsAsync(1);
        _ewRepoMock.Setup(r => r.GetActiveAnomaliesByReadingIdAsync(10)).ReturnsAsync(new List<UsageAnomaly> { anomaly });
        _ewRepoMock.Setup(r => r.CalculateAndPublishRoomSurchargeAsync(1, 101))
            .ReturnsAsync(new RoomSurchargeCalculationResult
            {
                RoomId = 101,
                RoomNumber = "A101L",
                Calculated = true,
                TotalCost = 50000m,
                NumberOfResidents = 2,
                SharePerStudent = 25000m,
                NotifiedStudentIds = new List<int> { 301, 302 }
            });
        var sut = CreateSut();

        // Act
        var result = await sut.ResolveAnomalyAsync(
            5,
            new ResolveAnomalyRequestDto { Action = "Adjust", AdjustedValue = 250m, ManagerNote = "Đọc dư số 0" },
            managerUserId: 1);

        // Assert
        result.Status.Should().Be("Resolved");
        result.Resolution.Should().Be("Corrected");
        result.FinalReadingValue.Should().Be(250m);
        result.FinalConsumption.Should().Be(150m);
        result.SurchargeCalculated.Should().BeTrue();
        result.DebtNotificationsSent.Should().Be(2);
    }

    [Fact]
    public async Task CalculatePeriodSurchargesAsync_WhenPeriodContainsNormalAndFlaggedRooms_SummarizesTotals()
    {
        // Arrange
        var period = CreatePeriod(id: 1);
        var readings = new List<MeterReading>
        {
            new() { RoomId = 101, MeterType = "Electricity" },
            new() { RoomId = 101, MeterType = "Water" },
            new() { RoomId = 102, MeterType = "Electricity" }
        };

        _ewRepoMock.Setup(r => r.GetBillingPeriodByIdAsync(1)).ReturnsAsync(period);
        _ewRepoMock.Setup(r => r.GetReadingsByPeriodAsync(1)).ReturnsAsync(readings);
        _ewRepoMock.Setup(r => r.CalculateAndPublishRoomSurchargeAsync(1, 101))
            .ReturnsAsync(new RoomSurchargeCalculationResult
            {
                RoomId = 101,
                RoomNumber = "A101L",
                Calculated = true,
                TotalCost = 60000m,
                NumberOfResidents = 3,
                SharePerStudent = 20000m,
                InvoicesCreated = 3,
                NotifiedStudentIds = new List<int> { 1, 2, 3 }
            });
        _ewRepoMock.Setup(r => r.CalculateAndPublishRoomSurchargeAsync(1, 102))
            .ReturnsAsync(new RoomSurchargeCalculationResult
            {
                RoomId = 102,
                RoomNumber = "A101R",
                Calculated = false,
                HasFlaggedReadings = true
            });
        var sut = CreateSut();

        // Act
        var summary = await sut.CalculatePeriodSurchargesAsync(1);

        // Assert
        summary.RoomsCalculated.Should().Be(1);
        summary.RoomsSkippedFlagged.Should().Be(1);
        summary.InvoicesCreated.Should().Be(3);
        summary.NotificationsSent.Should().Be(3);
        summary.Message.Should().Contain("bỏ qua 1 phòng");
    }

    [Fact]
    public async Task GetStudentTermsAndReportAsync_WhenCalled_DelegatesToRepository()
    {
        // Arrange
        var terms = new List<StudentTermOptionDto> { new() { TermId = 1, TermName = "Fall", IsCurrent = true } };
        var report = new StudentEwReportDto { TermId = 1, RoomNumber = "A101" };
        _ewRepoMock.Setup(r => r.GetStudentTermsAsync(50)).ReturnsAsync(terms);
        _ewRepoMock.Setup(r => r.GetStudentEwReportAsync(50, 1)).ReturnsAsync(report);
        var sut = CreateSut();

        // Act
        var actualTerms = await sut.GetStudentTermsAsync(50);
        var actualReport = await sut.GetStudentEwReportAsync(50, 1);

        // Assert
        actualTerms.Should().BeSameAs(terms);
        actualReport.Should().BeSameAs(report);
    }

    #endregion
}
