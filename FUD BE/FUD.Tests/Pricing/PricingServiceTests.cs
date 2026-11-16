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

namespace FUD.Tests.Pricing;

public class PricingServiceTests
{
    private readonly Mock<IPricingRepository> _pricingRepoMock = new();

    private PricingService CreateSut() => new(_pricingRepoMock.Object);

    [Fact]
    public async Task GetTermsLookupAsync_ReturnsTermsWithCorrectRegistrationStatus()
    {
        // Arrange
        var sut = CreateSut();
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));

        var termUpcoming = new Term
        {
            Id = 1,
            Name = "Fall 2026",
            AcademicYear = "2026-2027",
            StartDate = today.AddDays(30),
            EndDate = today.AddDays(120),
            RegistrationPeriods = new List<RegistrationPeriod>
            {
                new() { Id = 1, TermId = 1, PeriodType = "Keeping", StartDate = today.AddDays(5), EndDate = today.AddDays(10), ActiveFlag = true }
            }
        };

        var termOngoing = new Term
        {
            Id = 2,
            Name = "Summer 2026",
            AcademicYear = "2025-2026",
            StartDate = today.AddDays(-10),
            EndDate = today.AddDays(50),
            RegistrationPeriods = new List<RegistrationPeriod>
            {
                new() { Id = 2, TermId = 2, PeriodType = "Keeping", StartDate = today.AddDays(-5), EndDate = today.AddDays(5), ActiveFlag = true }
            }
        };

        var termEnded = new Term
        {
            Id = 3,
            Name = "Spring 2026",
            AcademicYear = "2025-2026",
            StartDate = today.AddDays(-100),
            EndDate = today.AddDays(-20),
            RegistrationPeriods = new List<RegistrationPeriod>
            {
                new() { Id = 3, TermId = 3, PeriodType = "Keeping", StartDate = today.AddDays(-90), EndDate = today.AddDays(-80), ActiveFlag = true }
            }
        };

        _pricingRepoMock.Setup(r => r.GetAllTermsAsync())
            .ReturnsAsync(new List<Term> { termUpcoming, termOngoing, termEnded });

        // Act
        var result = await sut.GetTermsLookupAsync();

        // Assert
        result.Should().HaveCount(3);

        var lookup1 = result.First(t => t.Id == 1);
        lookup1.IsRegistrationStarted.Should().BeFalse();
        lookup1.RegistrationStatus.Should().Be("Chưa bắt đầu");

        var lookup2 = result.First(t => t.Id == 2);
        lookup2.IsRegistrationStarted.Should().BeTrue();
        lookup2.RegistrationStatus.Should().Be("Đang mở đăng ký");

        var lookup3 = result.First(t => t.Id == 3);
        lookup3.IsRegistrationStarted.Should().BeTrue();
        lookup3.RegistrationStatus.Should().Be("Đã kết thúc");
    }

    [Fact]
    public async Task GetPricingByTermAsync_TermExists_ReturnsPricingMatrixWithPreviousPrices()
    {
        // Arrange
        var sut = CreateSut();
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));

        var termPrev = new Term
        {
            Id = 1,
            Name = "Spring 2026",
            StartDate = today.AddDays(-100),
            EndDate = today.AddDays(-20),
            RegistrationPeriods = new List<RegistrationPeriod>()
        };

        var termCurrent = new Term
        {
            Id = 2,
            Name = "Summer 2026",
            AcademicYear = "2025-2026",
            StartDate = today.AddDays(10),
            EndDate = today.AddDays(100),
            RegistrationPeriods = new List<RegistrationPeriod>
            {
                new() { Id = 1, TermId = 2, PeriodType = "NewRegistration", StartDate = today.AddDays(5), EndDate = today.AddDays(10), ActiveFlag = true }
            }
        };

        var dormA = new Dorm { Id = 1, Name = "Dorm A", Code = "A" };
        var dorms = new List<Dorm> { dormA };

        var rt4 = new RoomType { Id = 10, Name = "4 Beds", BedCapacity = 4 };
        var roomTypes = new List<RoomType> { rt4 };

        var prevPriceList = new PriceList
        {
            Id = 101,
            TermId = 1,
            Status = "Published",
            PriceItems = new List<PriceItem>
            {
                new() { DormId = 1, RoomTypeId = 10, UnitPrice = 3500000 }
            }
        };

        var currentPriceList = new PriceList
        {
            Id = 102,
            TermId = 2,
            Status = "Draft",
            PriceItems = new List<PriceItem>
            {
                new() { DormId = 1, RoomTypeId = 10, UnitPrice = 3800000 }
            }
        };

        _pricingRepoMock.Setup(r => r.GetTermByIdAsync(2)).ReturnsAsync(termCurrent);
        _pricingRepoMock.Setup(r => r.GetAllTermsAsync()).ReturnsAsync(new List<Term> { termPrev, termCurrent });
        _pricingRepoMock.Setup(r => r.GetPriceListByTermIdAsync(1)).ReturnsAsync(prevPriceList);
        _pricingRepoMock.Setup(r => r.GetPriceListByTermIdAsync(2)).ReturnsAsync(currentPriceList);
        _pricingRepoMock.Setup(r => r.GetAllDormsAsync()).ReturnsAsync(dorms);
        _pricingRepoMock.Setup(r => r.GetAllRoomTypesAsync()).ReturnsAsync(roomTypes);
        _pricingRepoMock.Setup(r => r.GetUtilityPricesByTermIdAsync(2)).ReturnsAsync(new List<UtilityPriceDto>());

        // Act
        var result = await sut.GetPricingByTermAsync(2);

        // Assert
        result.Should().NotBeNull();
        result.TermId.Should().Be(2);
        result.TermName.Should().Be("Summer 2026");
        result.Status.Should().Be("Draft");
        result.TermStatus.Should().Be("Upcoming");
        result.IsRegistrationStarted.Should().BeFalse();
        result.Items.Should().HaveCount(1);

        var item = result.Items.First();
        item.DormId.Should().Be(1);
        item.RoomTypeId.Should().Be(10);
        item.PreviousSemesterPrice.Should().Be(3500000);
        item.CurrentPrice.Should().Be(3800000);
    }

    [Fact]
    public async Task GetPricingByTermAsync_TermNotFound_ThrowsKeyNotFoundException()
    {
        // Arrange
        var sut = CreateSut();
        _pricingRepoMock.Setup(r => r.GetTermByIdAsync(999)).ReturnsAsync((Term?)null);

        // Act
        var act = async () => await sut.GetPricingByTermAsync(999);

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("*Không tìm thấy học kỳ*");
    }

    [Fact]
    public async Task SavePricingAsync_ValidInputBeforeRegistrationStarts_SavesPriceListAndReturnsUpdatedPricing()
    {
        // Arrange
        var sut = CreateSut();
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));

        var term = new Term
        {
            Id = 1,
            Name = "Fall 2026",
            StartDate = today.AddDays(20),
            EndDate = today.AddDays(100),
            RegistrationPeriods = new List<RegistrationPeriod>
            {
                new() { Id = 1, TermId = 1, PeriodType = "Keeping", StartDate = today.AddDays(5), EndDate = today.AddDays(10), ActiveFlag = true }
            }
        };

        var request = new SavePricingRequestDto
        {
            TermId = 1,
            Items = new List<UpdatePricingItemInputDto>
            {
                new() { DormId = 1, RoomTypeId = 10, CurrentPrice = 4000000 }
            }
        };

        _pricingRepoMock.Setup(r => r.GetTermByIdAsync(1)).ReturnsAsync(term);
        _pricingRepoMock.Setup(r => r.GetPriceListByTermIdAsync(1)).ReturnsAsync((PriceList?)null);
        _pricingRepoMock.Setup(r => r.CreatePriceListAsync(It.IsAny<PriceList>()))
            .ReturnsAsync((PriceList pl) => { pl.Id = 55; return pl; });
        _pricingRepoMock.Setup(r => r.UpdatePriceItemsAsync(55, It.IsAny<List<PriceItem>>()))
            .Returns(Task.CompletedTask);

        _pricingRepoMock.Setup(r => r.GetAllTermsAsync()).ReturnsAsync(new List<Term> { term });
        _pricingRepoMock.Setup(r => r.GetAllDormsAsync()).ReturnsAsync(new List<Dorm> { new() { Id = 1, Name = "Dorm A", Code = "A" } });
        _pricingRepoMock.Setup(r => r.GetAllRoomTypesAsync()).ReturnsAsync(new List<RoomType> { new() { Id = 10, Name = "4 Beds", BedCapacity = 4 } });
        _pricingRepoMock.Setup(r => r.GetUtilityPricesByTermIdAsync(1)).ReturnsAsync(new List<UtilityPriceDto>());

        // Act
        var result = await sut.SavePricingAsync(managerUserId: 2, request);

        // Assert
        result.Should().NotBeNull();
        result.TermId.Should().Be(1);

        _pricingRepoMock.Verify(r => r.CreatePriceListAsync(It.Is<PriceList>(pl =>
            pl.TermId == 1 &&
            pl.Status == "Published" &&
            pl.CreatedById == 2)), Times.Once);

        _pricingRepoMock.Verify(r => r.UpdatePriceItemsAsync(55, It.Is<List<PriceItem>>(items =>
            items.Count == 1 &&
            items[0].UnitPrice == 4000000)), Times.Once);
    }

    [Fact]
    public async Task SavePricingAsync_WhenRegistrationAlreadyStarted_ThrowsInvalidOperationException()
    {
        // Arrange
        var sut = CreateSut();
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));

        var term = new Term
        {
            Id = 2,
            Name = "Summer 2026",
            StartDate = today.AddDays(-5),
            EndDate = today.AddDays(50),
            RegistrationPeriods = new List<RegistrationPeriod>
            {
                new() { Id = 1, TermId = 2, PeriodType = "Keeping", StartDate = today.AddDays(-2), EndDate = today.AddDays(5), ActiveFlag = true }
            }
        };

        var request = new SavePricingRequestDto
        {
            TermId = 2,
            Items = new List<UpdatePricingItemInputDto>
            {
                new() { DormId = 1, RoomTypeId = 10, CurrentPrice = 4200000 }
            }
        };

        _pricingRepoMock.Setup(r => r.GetTermByIdAsync(2)).ReturnsAsync(term);

        // Act
        var act = async () => await sut.SavePricingAsync(managerUserId: 2, request);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("TERM_NOT_EDITABLE");

        _pricingRepoMock.Verify(r => r.CreatePriceListAsync(It.IsAny<PriceList>()), Times.Never);
        _pricingRepoMock.Verify(r => r.UpdatePriceItemsAsync(It.IsAny<int>(), It.IsAny<List<PriceItem>>()), Times.Never);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-500000)]
    public async Task SavePricingAsync_WhenPriceIsZeroOrNegative_ThrowsArgumentException(decimal invalidPrice)
    {
        // Arrange
        var sut = CreateSut();
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));

        var term = new Term
        {
            Id = 3,
            Name = "Fall 2026",
            StartDate = today.AddDays(20),
            EndDate = today.AddDays(100),
            RegistrationPeriods = new List<RegistrationPeriod>
            {
                new() { Id = 1, TermId = 3, PeriodType = "Keeping", StartDate = today.AddDays(5), EndDate = today.AddDays(10), ActiveFlag = true }
            }
        };

        var request = new SavePricingRequestDto
        {
            TermId = 3,
            Items = new List<UpdatePricingItemInputDto>
            {
                new() { DormId = 1, RoomTypeId = 10, CurrentPrice = invalidPrice }
            }
        };

        _pricingRepoMock.Setup(r => r.GetTermByIdAsync(3)).ReturnsAsync(term);

        // Act
        var act = async () => await sut.SavePricingAsync(managerUserId: 2, request);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Giá phải là số lớn hơn 0*");
    }

    [Fact]
    public async Task SaveUtilityPricesAsync_ValidPricesBeforeRegistrationStarts_SavesAndReturnsPricing()
    {
        // Arrange
        var sut = CreateSut();
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));

        var term = new Term
        {
            Id = 1,
            Name = "Fall 2026",
            StartDate = today.AddDays(20),
            EndDate = today.AddDays(100),
            RegistrationPeriods = new List<RegistrationPeriod>
            {
                new() { Id = 1, TermId = 1, PeriodType = "Keeping", StartDate = today.AddDays(5), EndDate = today.AddDays(10), ActiveFlag = true }
            }
        };

        var request = new SaveUtilityPricesRequestDto
        {
            TermId = 1,
            Items = new List<UtilityPriceItemInputDto>
            {
                new() { MeterType = "Electricity", UnitPrice = 3000 },
                new() { MeterType = "Water", UnitPrice = 15000 }
            }
        };

        _pricingRepoMock.Setup(r => r.GetTermByIdAsync(1)).ReturnsAsync(term);
        _pricingRepoMock.Setup(r => r.SaveUtilityPricesAsync(1, It.IsAny<List<UtilityPrice>>())).Returns(Task.CompletedTask);

        _pricingRepoMock.Setup(r => r.GetAllTermsAsync()).ReturnsAsync(new List<Term> { term });
        _pricingRepoMock.Setup(r => r.GetPriceListByTermIdAsync(1)).ReturnsAsync((PriceList?)null);
        _pricingRepoMock.Setup(r => r.GetAllDormsAsync()).ReturnsAsync(new List<Dorm>());
        _pricingRepoMock.Setup(r => r.GetAllRoomTypesAsync()).ReturnsAsync(new List<RoomType>());
        _pricingRepoMock.Setup(r => r.GetUtilityPricesByTermIdAsync(1)).ReturnsAsync(new List<UtilityPriceDto>
        {
            new() { MeterType = "Electricity", UnitPrice = 3000 },
            new() { MeterType = "Water", UnitPrice = 15000 }
        });

        // Act
        var result = await sut.SaveUtilityPricesAsync(managerUserId: 2, request);

        // Assert
        result.Should().NotBeNull();
        result.UtilityPrices.Should().HaveCount(2);

        _pricingRepoMock.Verify(r => r.SaveUtilityPricesAsync(1, It.Is<List<UtilityPrice>>(list =>
            list.Count == 2 &&
            list.Any(u => u.MeterType == "Electricity" && u.UnitPrice == 3000) &&
            list.Any(u => u.MeterType == "Water" && u.UnitPrice == 15000))), Times.Once);
    }

    [Fact]
    public async Task SaveUtilityPricesAsync_WhenUnitPriceZeroOrNegative_ThrowsArgumentException()
    {
        // Arrange
        var sut = CreateSut();
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));

        var term = new Term
        {
            Id = 1,
            Name = "Fall 2026",
            StartDate = today.AddDays(20),
            EndDate = today.AddDays(100),
            RegistrationPeriods = new List<RegistrationPeriod>()
        };

        var request = new SaveUtilityPricesRequestDto
        {
            TermId = 1,
            Items = new List<UtilityPriceItemInputDto>
            {
                new() { MeterType = "Electricity", UnitPrice = -500 }
            }
        };

        _pricingRepoMock.Setup(r => r.GetTermByIdAsync(1)).ReturnsAsync(term);

        // Act
        var act = async () => await sut.SaveUtilityPricesAsync(managerUserId: 2, request);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*phải lớn hơn 0*");
    }
}
