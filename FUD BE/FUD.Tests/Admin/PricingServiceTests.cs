using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using FUD.DTOs.Pricing;
using FUD.Models;
using FUD.Repositories.Interfaces;
using FUD.Services;
using Moq;
using Xunit;

namespace FUD.Tests.Admin;

/// <summary>
/// Unit tests cho PricingService — tuân thủ quy tắc AAA.
/// IPricingRepository được mock bằng Moq.
/// </summary>
public class PricingServiceTests
{
    private readonly Mock<IPricingRepository> _pricingRepoMock = new();

    private PricingService CreateSut() => new(_pricingRepoMock.Object);

    // ─── Helpers ────────────────────────────────────────────────────────────────

    /// <summary>Tạo Term mẫu với RegistrationPeriods tùy chỉnh.</summary>
    private static Term BuildTerm(
        int id,
        DateOnly start,
        DateOnly end,
        List<RegistrationPeriod>? periods = null)
    {
        return new Term
        {
            Id                   = id,
            Name                 = $"HK{id}",
            AcademicYear         = "2025-2026",
            StartDate            = start,
            EndDate              = end,
            RegistrationPeriods  = periods ?? new List<RegistrationPeriod>()
        };
    }

    private static RegistrationPeriod ActivePeriod(DateOnly start, DateOnly end) =>
        new() { StartDate = start, EndDate = end, ActiveFlag = true };

    // ─── GetTermsLookupAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetTermsLookup_WhenNoTerms_ReturnsEmptyList()
    {
        // Arrange
        _pricingRepoMock.Setup(r => r.GetAllTermsAsync()).ReturnsAsync(new List<Term>());

        var sut = CreateSut();

        // Act
        var result = await sut.GetTermsLookupAsync();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetTermsLookup_WhenTermHasNoActivePeriods_ReturnsNotStartedStatus()
    {
        // Arrange
        var term = BuildTerm(1,
            DateOnly.FromDateTime(DateTime.Today.AddDays(-30)),
            DateOnly.FromDateTime(DateTime.Today.AddDays(30)));
        // Không có RegistrationPeriods
        _pricingRepoMock.Setup(r => r.GetAllTermsAsync()).ReturnsAsync(new List<Term> { term });

        var sut = CreateSut();

        // Act
        var result = await sut.GetTermsLookupAsync();

        // Assert
        result.Should().HaveCount(1);
        result[0].IsRegistrationStarted.Should().BeFalse();
        result[0].RegistrationStatus.Should().Be("Chưa bắt đầu");
    }

    [Fact]
    public async Task GetTermsLookup_WhenTodayIsWithinActivePeriod_ReturnsOpenStatus()
    {
        // Arrange
        // Giả lập "hôm nay" (giờ VN) nằm trong khoảng period
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        var period = ActivePeriod(today.AddDays(-5), today.AddDays(5));
        var term   = BuildTerm(1,
            today.AddDays(-10),
            today.AddDays(60),
            new List<RegistrationPeriod> { period });

        _pricingRepoMock.Setup(r => r.GetAllTermsAsync()).ReturnsAsync(new List<Term> { term });

        var sut = CreateSut();

        // Act
        var result = await sut.GetTermsLookupAsync();

        // Assert
        result[0].IsRegistrationStarted.Should().BeTrue();
        result[0].RegistrationStatus.Should().Be("Đang mở đăng ký");
    }

    [Fact]
    public async Task GetTermsLookup_WhenPeriodHasEnded_ReturnsEndedStatus()
    {
        // Arrange
        var today  = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        var period = ActivePeriod(today.AddDays(-20), today.AddDays(-5)); // đã kết thúc
        var term   = BuildTerm(1, today.AddDays(-30), today.AddDays(60),
            new List<RegistrationPeriod> { period });

        _pricingRepoMock.Setup(r => r.GetAllTermsAsync()).ReturnsAsync(new List<Term> { term });

        var sut = CreateSut();

        // Act
        var result = await sut.GetTermsLookupAsync();

        // Assert
        result[0].IsRegistrationStarted.Should().BeTrue();
        result[0].RegistrationStatus.Should().Be("Đã kết thúc");
    }

    // ─── GetPricingByTermAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task GetPricingByTerm_WhenTermNotFound_ThrowsKeyNotFoundException()
    {
        // Arrange
        _pricingRepoMock.Setup(r => r.GetTermByIdAsync(99)).ReturnsAsync((Term?)null);

        var sut = CreateSut();

        // Act
        Func<Task> act = () => sut.GetPricingByTermAsync(99);

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("*99*");
    }

    [Fact]
    public async Task GetPricingByTerm_WhenNoPriceList_CurrentPriceIsZero()
    {
        // Arrange
        var term = BuildTerm(1,
            DateOnly.FromDateTime(DateTime.Today),
            DateOnly.FromDateTime(DateTime.Today.AddDays(90)));
        var dorm     = new Dorm     { Id = 1, Name = "KTX A", Code = "A" };
        var roomType = new RoomType { Id = 1, Name = "4 người", BedCapacity = 4 };

        _pricingRepoMock.Setup(r => r.GetTermByIdAsync(1)).ReturnsAsync(term);
        _pricingRepoMock.Setup(r => r.GetAllTermsAsync()).ReturnsAsync(new List<Term> { term });
        _pricingRepoMock.Setup(r => r.GetPriceListByTermIdAsync(1)).ReturnsAsync((PriceList?)null);
        _pricingRepoMock.Setup(r => r.GetAllDormsAsync()).ReturnsAsync(new List<Dorm> { dorm });
        _pricingRepoMock.Setup(r => r.GetAllRoomTypesAsync()).ReturnsAsync(new List<RoomType> { roomType });

        var sut = CreateSut();

        // Act
        var result = await sut.GetPricingByTermAsync(1);

        // Assert
        result.Items.Should().HaveCount(1);
        result.Items[0].CurrentPrice.Should().Be(0);
    }

    [Fact]
    public async Task GetPricingByTerm_WhenOnlyPrevPriceExists_CurrentPriceUsesPrevPrice()
    {
        // Arrange
        var today   = DateOnly.FromDateTime(DateTime.Today);
        var prevTerm = BuildTerm(0, today.AddDays(-180), today.AddDays(-1));
        var currTerm = BuildTerm(1, today, today.AddDays(90));

        var dorm     = new Dorm     { Id = 1, Name = "KTX A", Code = "A" };
        var roomType = new RoomType { Id = 1, Name = "4 người", BedCapacity = 4 };

        var prevPriceList = new PriceList
        {
            Id     = 10,
            Status = "Published",
            PriceItems = new List<PriceItem>
            {
                new() { DormId = 1, RoomTypeId = 1, UnitPrice = 2_000_000m }
            }
        };

        _pricingRepoMock.Setup(r => r.GetTermByIdAsync(1)).ReturnsAsync(currTerm);
        _pricingRepoMock.Setup(r => r.GetAllTermsAsync()).ReturnsAsync(new List<Term> { prevTerm, currTerm });
        _pricingRepoMock.Setup(r => r.GetPriceListByTermIdAsync(0)).ReturnsAsync(prevPriceList);
        _pricingRepoMock.Setup(r => r.GetPriceListByTermIdAsync(1)).ReturnsAsync((PriceList?)null);
        _pricingRepoMock.Setup(r => r.GetAllDormsAsync()).ReturnsAsync(new List<Dorm> { dorm });
        _pricingRepoMock.Setup(r => r.GetAllRoomTypesAsync()).ReturnsAsync(new List<RoomType> { roomType });

        var sut = CreateSut();

        // Act
        var result = await sut.GetPricingByTermAsync(1);

        // Assert
        result.Items[0].CurrentPrice.Should().Be(2_000_000m);
        result.Items[0].PreviousSemesterPrice.Should().Be(2_000_000m);
    }

    [Fact]
    public async Task GetPricingByTerm_WhenCurrentPriceExists_CurrentPriceOverridesPrev()
    {
        // Arrange
        var today    = DateOnly.FromDateTime(DateTime.Today);
        var prevTerm = BuildTerm(0, today.AddDays(-180), today.AddDays(-1));
        var currTerm = BuildTerm(1, today, today.AddDays(90));

        var dorm     = new Dorm     { Id = 1, Name = "KTX A", Code = "A" };
        var roomType = new RoomType { Id = 1, Name = "4 người", BedCapacity = 4 };

        var prevPriceList = new PriceList
        {
            Id = 10, Status = "Published",
            PriceItems = new List<PriceItem> { new() { DormId = 1, RoomTypeId = 1, UnitPrice = 2_000_000m } }
        };
        var currPriceList = new PriceList
        {
            Id = 11, Status = "Published",
            PriceItems = new List<PriceItem> { new() { DormId = 1, RoomTypeId = 1, UnitPrice = 2_500_000m } }
        };

        _pricingRepoMock.Setup(r => r.GetTermByIdAsync(1)).ReturnsAsync(currTerm);
        _pricingRepoMock.Setup(r => r.GetAllTermsAsync()).ReturnsAsync(new List<Term> { prevTerm, currTerm });
        _pricingRepoMock.Setup(r => r.GetPriceListByTermIdAsync(0)).ReturnsAsync(prevPriceList);
        _pricingRepoMock.Setup(r => r.GetPriceListByTermIdAsync(1)).ReturnsAsync(currPriceList);
        _pricingRepoMock.Setup(r => r.GetAllDormsAsync()).ReturnsAsync(new List<Dorm> { dorm });
        _pricingRepoMock.Setup(r => r.GetAllRoomTypesAsync()).ReturnsAsync(new List<RoomType> { roomType });

        var sut = CreateSut();

        // Act
        var result = await sut.GetPricingByTermAsync(1);

        // Assert
        result.Items[0].CurrentPrice.Should().Be(2_500_000m);
        result.Items[0].PreviousSemesterPrice.Should().Be(2_000_000m);
    }

    // ─── SavePricingAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task SavePricing_WhenTermNotFound_ThrowsKeyNotFoundException()
    {
        // Arrange
        _pricingRepoMock.Setup(r => r.GetTermByIdAsync(99)).ReturnsAsync((Term?)null);

        var sut = CreateSut();

        // Act
        Func<Task> act = () => sut.SavePricingAsync(1, new SavePricingRequestDto { TermId = 99 });

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("*99*");
    }

    [Fact]
    public async Task SavePricing_WhenPriceIsZero_ThrowsArgumentException()
    {
        // Arrange
        var term = BuildTerm(1,
            DateOnly.FromDateTime(DateTime.Today),
            DateOnly.FromDateTime(DateTime.Today.AddDays(90)));

        _pricingRepoMock.Setup(r => r.GetTermByIdAsync(1)).ReturnsAsync(term);

        var request = new SavePricingRequestDto
        {
            TermId = 1,
            Items  = new List<UpdatePricingItemInputDto> { new() { DormId = 1, RoomTypeId = 1, CurrentPrice = 0 } }
        };

        var sut = CreateSut();

        // Act
        Func<Task> act = () => sut.SavePricingAsync(managerUserId: 1, request);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Giá không hợp lệ*");
    }

    [Fact]
    public async Task SavePricing_WhenRegistrationStartedAndNotConfirmed_ThrowsInvalidOperation()
    {
        // Arrange
        var today  = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        var period = ActivePeriod(today.AddDays(-5), today.AddDays(5));
        var term   = BuildTerm(1, today.AddDays(-10), today.AddDays(90),
            new List<RegistrationPeriod> { period });

        _pricingRepoMock.Setup(r => r.GetTermByIdAsync(1)).ReturnsAsync(term);

        var request = new SavePricingRequestDto
        {
            TermId                      = 1,
            ConfirmApplyToNewBookingsOnly = false,
            Items = new List<UpdatePricingItemInputDto> { new() { DormId = 1, RoomTypeId = 1, CurrentPrice = 1_000_000 } }
        };

        var sut = CreateSut();

        // Act
        Func<Task> act = () => sut.SavePricingAsync(managerUserId: 1, request);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*REGISTRATION_ALREADY_STARTED*");
    }

    [Fact]
    public async Task SavePricing_WhenNoPriceListExists_CreatesNewPriceList()
    {
        // Arrange
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        var term  = BuildTerm(1, today, today.AddDays(90));

        _pricingRepoMock.Setup(r => r.GetTermByIdAsync(1)).ReturnsAsync(term);
        _pricingRepoMock.Setup(r => r.GetPriceListByTermIdAsync(1)).ReturnsAsync((PriceList?)null);

        var newPriceList = new PriceList { Id = 1, Status = "Published", PriceItems = new List<PriceItem>() };
        _pricingRepoMock.Setup(r => r.CreatePriceListAsync(It.IsAny<PriceList>())).ReturnsAsync(newPriceList);
        _pricingRepoMock.Setup(r => r.UpdatePriceItemsAsync(It.IsAny<int>(), It.IsAny<List<PriceItem>>())).Returns(Task.CompletedTask);

        // GetPricingByTermAsync được gọi nội bộ
        _pricingRepoMock.Setup(r => r.GetAllTermsAsync()).ReturnsAsync(new List<Term> { term });
        _pricingRepoMock.Setup(r => r.GetAllDormsAsync()).ReturnsAsync(new List<Dorm>());
        _pricingRepoMock.Setup(r => r.GetAllRoomTypesAsync()).ReturnsAsync(new List<RoomType>());

        var request = new SavePricingRequestDto
        {
            TermId = 1,
            Items  = new List<UpdatePricingItemInputDto> { new() { DormId = 1, RoomTypeId = 1, CurrentPrice = 1_000_000 } }
        };

        var sut = CreateSut();

        // Act
        await sut.SavePricingAsync(managerUserId: 1, request);

        // Assert
        _pricingRepoMock.Verify(r => r.CreatePriceListAsync(It.IsAny<PriceList>()), Times.Once);
    }

    [Fact]
    public async Task SavePricing_WhenPriceListExists_UpdatesWithoutCreating()
    {
        // Arrange
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        var term  = BuildTerm(1, today, today.AddDays(90));
        var existingPriceList = new PriceList { Id = 5, Status = "Draft", PriceItems = new List<PriceItem>() };

        _pricingRepoMock.Setup(r => r.GetTermByIdAsync(1)).ReturnsAsync(term);
        _pricingRepoMock.Setup(r => r.GetPriceListByTermIdAsync(1)).ReturnsAsync(existingPriceList);
        _pricingRepoMock.Setup(r => r.UpdatePriceItemsAsync(It.IsAny<int>(), It.IsAny<List<PriceItem>>())).Returns(Task.CompletedTask);

        _pricingRepoMock.Setup(r => r.GetAllTermsAsync()).ReturnsAsync(new List<Term> { term });
        _pricingRepoMock.Setup(r => r.GetAllDormsAsync()).ReturnsAsync(new List<Dorm>());
        _pricingRepoMock.Setup(r => r.GetAllRoomTypesAsync()).ReturnsAsync(new List<RoomType>());

        var request = new SavePricingRequestDto
        {
            TermId = 1,
            Items  = new List<UpdatePricingItemInputDto> { new() { DormId = 1, RoomTypeId = 1, CurrentPrice = 2_000_000 } }
        };

        var sut = CreateSut();

        // Act
        await sut.SavePricingAsync(managerUserId: 1, request);

        // Assert
        _pricingRepoMock.Verify(r => r.CreatePriceListAsync(It.IsAny<PriceList>()), Times.Never);
        _pricingRepoMock.Verify(r => r.UpdatePriceItemsAsync(5, It.IsAny<List<PriceItem>>()), Times.Once);
    }
}

