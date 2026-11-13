using System;
using System.Threading.Tasks;
using FluentAssertions;
using FUD.DTOs.Keeping;
using FUD.Models;
using FUD.Repositories.Interfaces.Keeping;
using FUD.Services.Keeping;
using Moq;
using Xunit;

namespace FUD.Tests.Keeping;

/// <summary>
/// Unit tests cho KeepingEligibilityService (UC02) — tuân thủ quy tắc AAA (Arrange / Act / Assert).
/// </summary>
public class KeepingEligibilityServiceTests
{
    private readonly Mock<IKeepingRepository> _keepingRepoMock = new();

    private KeepingEligibilityService CreateSut() => new(_keepingRepoMock.Object);

    private static Residency CreateSampleResidency(int studentId = 10, int bedId = 100, int currentTermId = 1)
    {
        var dorm = new Dorm { Id = 1, Name = "Dorm A", Code = "DA", Status = "Active" };
        var block = new Block { Id = 1, DormId = 1, Name = "Block A1", Code = "A1", GenderType = "Male", Status = "Active", Dorm = dorm };
        var room = new Room { Id = 1, BlockId = 1, RoomNumber = "101", Status = "Active", Block = block };
        var bed = new Bed { Id = bedId, RoomId = 1, BedCode = "A1-101-1", Status = "Occupied", Room = room };
        var currentTerm = new Term
        {
            Id = currentTermId,
            Name = "Summer 2026",
            AcademicYear = "2025-2026",
            StartDate = new DateOnly(2026, 5, 1),
            EndDate = new DateOnly(2026, 8, 31)
        };

        return new Residency
        {
            Id = 500,
            StudentId = studentId,
            BedId = bedId,
            TermId = currentTermId,
            Status = "Active",
            Bed = bed,
            Term = currentTerm
        };
    }

    private static (RegistrationPeriod Period, Term NextTerm, Term CurrentTerm) CreateSampleOpenPeriod(Term currentTerm, int nextTermId = 2)
    {
        var nextTerm = new Term
        {
            Id = nextTermId,
            Name = "Fall 2026",
            AcademicYear = "2026-2027",
            StartDate = new DateOnly(2026, 9, 1),
            EndDate = new DateOnly(2026, 12, 31)
        };

        var period = new RegistrationPeriod
        {
            Id = 50,
            TermId = nextTermId,
            PeriodType = "Keeping",
            StartDate = new DateOnly(2026, 8, 1),
            EndDate = new DateOnly(2026, 8, 15),
            ActiveFlag = true,
            Term = nextTerm
        };

        return (period, nextTerm, currentTerm);
    }

    [Fact]
    public async Task CheckEligibilityAsync_WhenNotCurrentResident_ReturnsIneligibleNotResident()
    {
        // Arrange
        const int studentId = 10;
        _keepingRepoMock.Setup(r => r.GetActiveResidencyAsync(studentId))
            .ReturnsAsync((Residency?)null);

        var sut = CreateSut();

        // Act
        var result = await sut.CheckEligibilityAsync(studentId);

        // Assert
        result.Eligible.Should().BeFalse();
        result.ReasonCode.Should().Be(KeepingReasonCodes.NotResident);
        result.CurrentStay.Should().BeNull();
        result.NextTerm.Should().BeNull();
    }

    [Fact]
    public async Task CheckEligibilityAsync_WhenKeepingPeriodNotOpen_ReturnsIneligiblePeriodNotOpen()
    {
        // Arrange
        const int studentId = 10;
        var residency = CreateSampleResidency(studentId);
        _keepingRepoMock.Setup(r => r.GetActiveResidencyAsync(studentId))
            .ReturnsAsync(residency);
        _keepingRepoMock.Setup(r => r.GetOpenKeepingPeriodAsync(studentId, It.IsAny<DateOnly>()))
            .ReturnsAsync((null, null, residency.Term));

        var sut = CreateSut();

        // Act
        var result = await sut.CheckEligibilityAsync(studentId);

        // Assert
        result.Eligible.Should().BeFalse();
        result.ReasonCode.Should().Be(KeepingReasonCodes.PeriodNotOpen);
        result.CurrentStay.Should().NotBeNull();
        result.CurrentStay!.BedCode.Should().Be("A1-101-1");
        result.NextTerm.Should().BeNull();
    }

    [Fact]
    public async Task CheckEligibilityAsync_WhenPriceListNotPublished_ReturnsIneligiblePriceNotPublished()
    {
        // Arrange
        const int studentId = 10;
        var residency = CreateSampleResidency(studentId);
        var (period, nextTerm, currentTerm) = CreateSampleOpenPeriod(residency.Term);

        _keepingRepoMock.Setup(r => r.GetActiveResidencyAsync(studentId)).ReturnsAsync(residency);
        _keepingRepoMock.Setup(r => r.GetOpenKeepingPeriodAsync(studentId, It.IsAny<DateOnly>()))
            .ReturnsAsync((period, nextTerm, currentTerm));
        _keepingRepoMock.Setup(r => r.GetKeepingBedPriceAsync(residency.BedId, nextTerm.Id))
            .ReturnsAsync(1_200_000m);
        _keepingRepoMock.Setup(r => r.GetKeepingMonthsAsync(residency.BedId, nextTerm.Id))
            .ReturnsAsync(4);
        _keepingRepoMock.Setup(r => r.IsPriceListPublishedAsync(nextTerm.Id))
            .ReturnsAsync(false);

        var sut = CreateSut();

        // Act
        var result = await sut.CheckEligibilityAsync(studentId);

        // Assert
        result.Eligible.Should().BeFalse();
        result.ReasonCode.Should().Be(KeepingReasonCodes.PriceNotPublished);
        result.CurrentStay.Should().NotBeNull();
        result.NextTerm.Should().NotBeNull();
        result.NextTerm!.Id.Should().Be(nextTerm.Id);
        result.Fee.Should().NotBeNull();
        result.Fee!.Amount.Should().Be(1_200_000m);
        result.Period.Should().NotBeNull();
    }

    [Fact]
    public async Task CheckEligibilityAsync_WhenHasOverdueInvoice_ReturnsIneligibleHasOverdueInvoice()
    {
        // Arrange
        const int studentId = 10;
        var residency = CreateSampleResidency(studentId);
        var (period, nextTerm, currentTerm) = CreateSampleOpenPeriod(residency.Term);

        _keepingRepoMock.Setup(r => r.GetActiveResidencyAsync(studentId)).ReturnsAsync(residency);
        _keepingRepoMock.Setup(r => r.GetOpenKeepingPeriodAsync(studentId, It.IsAny<DateOnly>()))
            .ReturnsAsync((period, nextTerm, currentTerm));
        _keepingRepoMock.Setup(r => r.GetKeepingBedPriceAsync(residency.BedId, nextTerm.Id)).ReturnsAsync(1_200_000m);
        _keepingRepoMock.Setup(r => r.GetKeepingMonthsAsync(residency.BedId, nextTerm.Id)).ReturnsAsync(4);
        _keepingRepoMock.Setup(r => r.IsPriceListPublishedAsync(nextTerm.Id)).ReturnsAsync(true);
        _keepingRepoMock.Setup(r => r.HasOverdueInvoiceAsync(studentId)).ReturnsAsync(true);

        var sut = CreateSut();

        // Act
        var result = await sut.CheckEligibilityAsync(studentId);

        // Assert
        result.Eligible.Should().BeFalse();
        result.ReasonCode.Should().Be(KeepingReasonCodes.HasOverdueInvoice);
    }

    [Fact]
    public async Task CheckEligibilityAsync_WhenHasUnpaidInvoice_ReturnsIneligibleHasUnpaidInvoice()
    {
        // Arrange
        const int studentId = 10;
        var residency = CreateSampleResidency(studentId);
        var (period, nextTerm, currentTerm) = CreateSampleOpenPeriod(residency.Term);

        _keepingRepoMock.Setup(r => r.GetActiveResidencyAsync(studentId)).ReturnsAsync(residency);
        _keepingRepoMock.Setup(r => r.GetOpenKeepingPeriodAsync(studentId, It.IsAny<DateOnly>()))
            .ReturnsAsync((period, nextTerm, currentTerm));
        _keepingRepoMock.Setup(r => r.GetKeepingBedPriceAsync(residency.BedId, nextTerm.Id)).ReturnsAsync(1_200_000m);
        _keepingRepoMock.Setup(r => r.GetKeepingMonthsAsync(residency.BedId, nextTerm.Id)).ReturnsAsync(4);
        _keepingRepoMock.Setup(r => r.IsPriceListPublishedAsync(nextTerm.Id)).ReturnsAsync(true);
        _keepingRepoMock.Setup(r => r.HasOverdueInvoiceAsync(studentId)).ReturnsAsync(false);
        _keepingRepoMock.Setup(r => r.HasUnpaidInvoiceAsync(studentId)).ReturnsAsync(true);

        var sut = CreateSut();

        // Act
        var result = await sut.CheckEligibilityAsync(studentId);

        // Assert
        result.Eligible.Should().BeFalse();
        result.ReasonCode.Should().Be(KeepingReasonCodes.HasUnpaidInvoice);
    }

    [Fact]
    public async Task CheckEligibilityAsync_WhenAlreadyKept_ReturnsIneligibleAlreadyKept()
    {
        // Arrange
        const int studentId = 10;
        var residency = CreateSampleResidency(studentId);
        var (period, nextTerm, currentTerm) = CreateSampleOpenPeriod(residency.Term);

        _keepingRepoMock.Setup(r => r.GetActiveResidencyAsync(studentId)).ReturnsAsync(residency);
        _keepingRepoMock.Setup(r => r.GetOpenKeepingPeriodAsync(studentId, It.IsAny<DateOnly>()))
            .ReturnsAsync((period, nextTerm, currentTerm));
        _keepingRepoMock.Setup(r => r.GetKeepingBedPriceAsync(residency.BedId, nextTerm.Id)).ReturnsAsync(1_200_000m);
        _keepingRepoMock.Setup(r => r.GetKeepingMonthsAsync(residency.BedId, nextTerm.Id)).ReturnsAsync(4);
        _keepingRepoMock.Setup(r => r.IsPriceListPublishedAsync(nextTerm.Id)).ReturnsAsync(true);
        _keepingRepoMock.Setup(r => r.HasOverdueInvoiceAsync(studentId)).ReturnsAsync(false);
        _keepingRepoMock.Setup(r => r.HasUnpaidInvoiceAsync(studentId)).ReturnsAsync(false);
        _keepingRepoMock.Setup(r => r.HasCompletedKeepingRequestAsync(studentId, nextTerm.Id)).ReturnsAsync(true);

        var sut = CreateSut();

        // Act
        var result = await sut.CheckEligibilityAsync(studentId);

        // Assert
        result.Eligible.Should().BeFalse();
        result.ReasonCode.Should().Be(KeepingReasonCodes.AlreadyKept);
    }

    [Fact]
    public async Task CheckEligibilityAsync_WhenAllConditionsMet_ReturnsEligibleWithFullDetails()
    {
        // Arrange
        const int studentId = 10;
        var residency = CreateSampleResidency(studentId);
        var (period, nextTerm, currentTerm) = CreateSampleOpenPeriod(residency.Term);

        _keepingRepoMock.Setup(r => r.GetActiveResidencyAsync(studentId)).ReturnsAsync(residency);
        _keepingRepoMock.Setup(r => r.GetOpenKeepingPeriodAsync(studentId, It.IsAny<DateOnly>()))
            .ReturnsAsync((period, nextTerm, currentTerm));
        _keepingRepoMock.Setup(r => r.GetKeepingBedPriceAsync(residency.BedId, nextTerm.Id)).ReturnsAsync(1_500_000m);
        _keepingRepoMock.Setup(r => r.GetKeepingMonthsAsync(residency.BedId, nextTerm.Id)).ReturnsAsync(4);
        _keepingRepoMock.Setup(r => r.IsPriceListPublishedAsync(nextTerm.Id)).ReturnsAsync(true);
        _keepingRepoMock.Setup(r => r.HasOverdueInvoiceAsync(studentId)).ReturnsAsync(false);
        _keepingRepoMock.Setup(r => r.HasUnpaidInvoiceAsync(studentId)).ReturnsAsync(false);
        _keepingRepoMock.Setup(r => r.HasCompletedKeepingRequestAsync(studentId, nextTerm.Id)).ReturnsAsync(false);

        var sut = CreateSut();

        // Act
        var result = await sut.CheckEligibilityAsync(studentId);

        // Assert
        result.Eligible.Should().BeTrue();
        result.ReasonCode.Should().BeNull();
        result.Message.Should().BeNull();
        result.CurrentStay.Should().NotBeNull();
        result.CurrentStay!.DormName.Should().Be("Dorm A");
        result.CurrentStay.BlockName.Should().Be("Block A1");
        result.CurrentStay.RoomNumber.Should().Be("101");
        result.CurrentStay.BedCode.Should().Be("A1-101-1");
        result.NextTerm.Should().NotBeNull();
        result.NextTerm!.Name.Should().Be("Fall 2026");
        result.Fee.Should().NotBeNull();
        result.Fee!.Amount.Should().Be(1_500_000m);
        result.Fee.Months.Should().Be(4);
        result.Fee.Currency.Should().Be("VND");
        result.Period.Should().NotBeNull();
        result.Period!.Start.Should().Be(period.StartDate);
        result.Period.End.Should().Be(period.EndDate);
    }

    [Fact]
    public async Task CheckEligibilityAsync_WhenBedPriceIsNull_DefaultsFeeAmountToZero()
    {
        // Arrange
        const int studentId = 10;
        var residency = CreateSampleResidency(studentId);
        var (period, nextTerm, currentTerm) = CreateSampleOpenPeriod(residency.Term);

        _keepingRepoMock.Setup(r => r.GetActiveResidencyAsync(studentId)).ReturnsAsync(residency);
        _keepingRepoMock.Setup(r => r.GetOpenKeepingPeriodAsync(studentId, It.IsAny<DateOnly>()))
            .ReturnsAsync((period, nextTerm, currentTerm));
        _keepingRepoMock.Setup(r => r.GetKeepingBedPriceAsync(residency.BedId, nextTerm.Id)).ReturnsAsync((decimal?)null);
        _keepingRepoMock.Setup(r => r.GetKeepingMonthsAsync(residency.BedId, nextTerm.Id)).ReturnsAsync(1);
        _keepingRepoMock.Setup(r => r.IsPriceListPublishedAsync(nextTerm.Id)).ReturnsAsync(true);
        _keepingRepoMock.Setup(r => r.HasOverdueInvoiceAsync(studentId)).ReturnsAsync(false);
        _keepingRepoMock.Setup(r => r.HasUnpaidInvoiceAsync(studentId)).ReturnsAsync(false);
        _keepingRepoMock.Setup(r => r.HasCompletedKeepingRequestAsync(studentId, nextTerm.Id)).ReturnsAsync(false);

        var sut = CreateSut();

        // Act
        var result = await sut.CheckEligibilityAsync(studentId);

        // Assert
        result.Eligible.Should().BeTrue();
        result.Fee.Should().NotBeNull();
        result.Fee!.Amount.Should().Be(0m);
    }
}

