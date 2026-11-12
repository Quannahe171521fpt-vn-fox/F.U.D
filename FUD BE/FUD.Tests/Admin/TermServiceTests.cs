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
/// Unit tests cho TermService — tuân thủ quy tắc AAA.
/// ITermRepository được mock bằng Moq.
/// </summary>
public class TermServiceTests
{
    private readonly Mock<ITermRepository> _termRepoMock = new();

    private TermService CreateSut() => new(_termRepoMock.Object);

    // ─── Helpers ─────────────────────────────────────────────────────────────────

    private static Term BuildTerm(
        int id,
        DateOnly start,
        DateOnly end,
        PriceList? priceList = null,
        List<RegistrationPeriod>? periods = null)
    {
        return new Term
        {
            Id                  = id,
            Name                = $"HK{id}",
            AcademicYear        = "2025-2026",
            StartDate           = start,
            EndDate             = end,
            PriceList           = priceList,
            RegistrationPeriods = periods ?? new List<RegistrationPeriod>()
        };
    }

    private static RegistrationPeriod BuildPeriod(int id, int termId, string type, DateOnly start, DateOnly end, bool active = true)
    {
        return new RegistrationPeriod
        {
            Id         = id,
            TermId     = termId,
            PeriodType = type,
            StartDate  = start,
            EndDate    = end,
            ActiveFlag = active
        };
    }

    // ─── GetAllTermsAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAllTerms_WhenNoTerms_ReturnsEmptyList()
    {
        // Arrange
        _termRepoMock.Setup(r => r.GetAllTermsAsync()).ReturnsAsync(new List<Term>());

        var sut = CreateSut();

        // Act
        var result = await sut.GetAllTermsAsync();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAllTerms_WhenTermIsUpcoming_ReturnsUpcomingStatus()
    {
        // Arrange
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        var term  = BuildTerm(1, today.AddDays(10), today.AddDays(90));

        _termRepoMock.Setup(r => r.GetAllTermsAsync()).ReturnsAsync(new List<Term> { term });
        _termRepoMock.Setup(r => r.HasDependentDataAsync(1)).ReturnsAsync(false);
        // GetTermDetailAsync → GetTermByIdAsync cần setup
        _termRepoMock.Setup(r => r.GetTermByIdAsync(1)).ReturnsAsync(term);

        var sut = CreateSut();

        // Act
        var result = await sut.GetAllTermsAsync();

        // Assert
        result[0].TermStatus.Should().Be("Upcoming");
    }

    [Fact]
    public async Task GetAllTerms_WhenTermIsOngoing_ReturnsOngoingStatus()
    {
        // Arrange
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        var term  = BuildTerm(1, today.AddDays(-5), today.AddDays(85));

        _termRepoMock.Setup(r => r.GetAllTermsAsync()).ReturnsAsync(new List<Term> { term });
        _termRepoMock.Setup(r => r.HasDependentDataAsync(1)).ReturnsAsync(false);
        _termRepoMock.Setup(r => r.GetTermByIdAsync(1)).ReturnsAsync(term);

        var sut = CreateSut();

        // Act
        var result = await sut.GetAllTermsAsync();

        // Assert
        result[0].TermStatus.Should().Be("Ongoing");
    }

    [Fact]
    public async Task GetAllTerms_WhenTermHasEnded_ReturnsEndedStatus()
    {
        // Arrange
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        var term  = BuildTerm(1, today.AddDays(-90), today.AddDays(-1));

        _termRepoMock.Setup(r => r.GetAllTermsAsync()).ReturnsAsync(new List<Term> { term });
        _termRepoMock.Setup(r => r.HasDependentDataAsync(1)).ReturnsAsync(false);
        _termRepoMock.Setup(r => r.GetTermByIdAsync(1)).ReturnsAsync(term);

        var sut = CreateSut();

        // Act
        var result = await sut.GetAllTermsAsync();

        // Assert
        result[0].TermStatus.Should().Be("Ended");
    }

    // ─── GetTermDetailAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetTermDetail_WhenTermNotFound_ThrowsKeyNotFoundException()
    {
        // Arrange
        _termRepoMock.Setup(r => r.GetTermByIdAsync(99)).ReturnsAsync((Term?)null);

        var sut = CreateSut();

        // Act
        Func<Task> act = () => sut.GetTermDetailAsync(99);

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("*99*");
    }

    [Fact]
    public async Task GetTermDetail_WhenTermHasTwoPeriods_ReturnsAllPeriods()
    {
        // Arrange
        var today   = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        var keeping = BuildPeriod(1, 1, "Keeping", today.AddDays(1), today.AddDays(10));
        var booking = BuildPeriod(2, 1, "Booking", today.AddDays(11), today.AddDays(20));
        var term    = BuildTerm(1, today, today.AddDays(90),
            periods: new List<RegistrationPeriod> { keeping, booking });

        _termRepoMock.Setup(r => r.GetTermByIdAsync(1)).ReturnsAsync(term);
        _termRepoMock.Setup(r => r.HasDependentDataAsync(1)).ReturnsAsync(false);

        var sut = CreateSut();

        // Act
        var result = await sut.GetTermDetailAsync(1);

        // Assert
        result.AllPeriods.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetTermDetail_WhenNoPriceList_PriceListStatusIsNotCreated()
    {
        // Arrange
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        var term  = BuildTerm(1, today, today.AddDays(90), priceList: null);

        _termRepoMock.Setup(r => r.GetTermByIdAsync(1)).ReturnsAsync(term);
        _termRepoMock.Setup(r => r.HasDependentDataAsync(1)).ReturnsAsync(false);

        var sut = CreateSut();

        // Act
        var result = await sut.GetTermDetailAsync(1);

        // Assert
        result.PriceListStatus.Should().Be("NotCreated");
    }

    [Fact]
    public async Task GetTermDetail_WhenPriceListIsPublished_PriceListStatusIsPublished()
    {
        // Arrange
        var today     = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        var priceList = new PriceList { Status = "Published", PublishedTime = DateTime.UtcNow };
        var term      = BuildTerm(1, today, today.AddDays(90), priceList: priceList);

        _termRepoMock.Setup(r => r.GetTermByIdAsync(1)).ReturnsAsync(term);
        _termRepoMock.Setup(r => r.HasDependentDataAsync(1)).ReturnsAsync(false);

        var sut = CreateSut();

        // Act
        var result = await sut.GetTermDetailAsync(1);

        // Assert
        result.PriceListStatus.Should().Be("Published");
    }

    // ─── CreateTermAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateTerm_WhenEndDateNotAfterStartDate_ThrowsArgumentException()
    {
        // Arrange
        var dto = new CreateTermRequestDto
        {
            Name         = "HK1",
            AcademicYear = "2025-2026",
            StartDate    = new DateOnly(2026, 1, 1),
            EndDate      = new DateOnly(2026, 1, 1) // bằng nhau
        };

        var sut = CreateSut();

        // Act
        Func<Task> act = () => sut.CreateTermAsync(dto);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Ngày kết thúc*");
    }

    [Fact]
    public async Task CreateTerm_WhenTermAlreadyExists_ThrowsArgumentException()
    {
        // Arrange
        var dto = new CreateTermRequestDto
        {
            Name         = "HK1",
            AcademicYear = "2025-2026",
            StartDate    = new DateOnly(2026, 1, 1),
            EndDate      = new DateOnly(2026, 5, 31)
        };
        _termRepoMock.Setup(r => r.ExistsTermAsync("HK1", "2025-2026", null)).ReturnsAsync(true);

        var sut = CreateSut();

        // Act
        Func<Task> act = () => sut.CreateTermAsync(dto);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*đã tồn tại*");
    }

    [Fact]
    public async Task CreateTerm_WhenValid_CallsCreateAndReturnsDetail()
    {
        // Arrange
        var dto = new CreateTermRequestDto
        {
            Name         = "HK1",
            AcademicYear = "2025-2026",
            StartDate    = new DateOnly(2026, 1, 1),
            EndDate      = new DateOnly(2026, 5, 31)
        };
        var today   = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        var created = BuildTerm(1, dto.StartDate, dto.EndDate);

        _termRepoMock.Setup(r => r.ExistsTermAsync("HK1", "2025-2026", null)).ReturnsAsync(false);
        _termRepoMock.Setup(r => r.CreateTermAsync(It.IsAny<Term>())).ReturnsAsync(created);
        _termRepoMock.Setup(r => r.GetTermByIdAsync(1)).ReturnsAsync(created);
        _termRepoMock.Setup(r => r.HasDependentDataAsync(1)).ReturnsAsync(false);

        var sut = CreateSut();

        // Act
        var result = await sut.CreateTermAsync(dto);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("HK1");
        _termRepoMock.Verify(r => r.CreateTermAsync(It.IsAny<Term>()), Times.Once);
    }

    // ─── UpdateTermAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateTerm_WhenTermNotFound_ThrowsKeyNotFoundException()
    {
        // Arrange
        _termRepoMock.Setup(r => r.GetTermByIdAsync(99)).ReturnsAsync((Term?)null);

        var sut = CreateSut();

        // Act
        Func<Task> act = () => sut.UpdateTermAsync(99, new UpdateTermRequestDto
        {
            Name = "X", AcademicYear = "Y",
            StartDate = new DateOnly(2026,1,1), EndDate = new DateOnly(2026,5,31)
        });

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task UpdateTerm_WhenEndDateNotAfterStartDate_ThrowsArgumentException()
    {
        // Arrange
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        var term  = BuildTerm(1, today, today.AddDays(90));
        _termRepoMock.Setup(r => r.GetTermByIdAsync(1)).ReturnsAsync(term);

        var sut = CreateSut();

        // Act
        Func<Task> act = () => sut.UpdateTermAsync(1, new UpdateTermRequestDto
        {
            Name = "HK1", AcademicYear = "2025-2026",
            StartDate = new DateOnly(2026,1,1),
            EndDate   = new DateOnly(2026,1,1) // không hợp lệ
        });

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Ngày kết thúc*");
    }

    [Fact]
    public async Task UpdateTerm_WhenDuplicateName_ThrowsArgumentException()
    {
        // Arrange
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        var term  = BuildTerm(1, today, today.AddDays(90));
        _termRepoMock.Setup(r => r.GetTermByIdAsync(1)).ReturnsAsync(term);
        _termRepoMock.Setup(r => r.ExistsTermAsync("HK2", "2025-2026", 1)).ReturnsAsync(true);

        var sut = CreateSut();

        // Act
        Func<Task> act = () => sut.UpdateTermAsync(1, new UpdateTermRequestDto
        {
            Name = "HK2", AcademicYear = "2025-2026",
            StartDate = today, EndDate = today.AddDays(90)
        });

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*đã tồn tại*");
    }

    [Fact]
    public async Task UpdateTerm_WhenValid_CallsUpdateRepoAndReturnsDetail()
    {
        // Arrange
        var today   = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        var term    = BuildTerm(1, today, today.AddDays(90));
        var updated = BuildTerm(1, today, today.AddDays(120));
        updated.Name = "HK1-Updated";

        _termRepoMock.Setup(r => r.GetTermByIdAsync(1)).ReturnsAsync(term);
        _termRepoMock.Setup(r => r.ExistsTermAsync("HK1-Updated", "2025-2026", 1)).ReturnsAsync(false);
        _termRepoMock.Setup(r => r.UpdateTermAsync(It.IsAny<Term>())).Returns(Task.CompletedTask);
        _termRepoMock.Setup(r => r.HasDependentDataAsync(1)).ReturnsAsync(false);

        // GetTermDetailAsync → GetTermByIdAsync lần 2 trả updated
        _termRepoMock.SetupSequence(r => r.GetTermByIdAsync(1))
            .ReturnsAsync(term)
            .ReturnsAsync(updated);

        var sut = CreateSut();

        // Act
        var result = await sut.UpdateTermAsync(1, new UpdateTermRequestDto
        {
            Name = "HK1-Updated", AcademicYear = "2025-2026",
            StartDate = today, EndDate = today.AddDays(120)
        });

        // Assert
        _termRepoMock.Verify(r => r.UpdateTermAsync(It.IsAny<Term>()), Times.Once);
        result.Name.Should().Be("HK1-Updated");
    }

    // ─── DeleteTermAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteTerm_WhenTermNotFound_ThrowsKeyNotFoundException()
    {
        // Arrange
        _termRepoMock.Setup(r => r.GetTermByIdAsync(99)).ReturnsAsync((Term?)null);

        var sut = CreateSut();

        // Act
        Func<Task> act = () => sut.DeleteTermAsync(99);

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task DeleteTerm_WhenHasDependentData_ThrowsInvalidOperationException()
    {
        // Arrange
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        var term  = BuildTerm(1, today, today.AddDays(90));
        _termRepoMock.Setup(r => r.GetTermByIdAsync(1)).ReturnsAsync(term);
        _termRepoMock.Setup(r => r.HasDependentDataAsync(1)).ReturnsAsync(true);

        var sut = CreateSut();

        // Act
        Func<Task> act = () => sut.DeleteTermAsync(1);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Không thể xóa*");
    }

    [Fact]
    public async Task DeleteTerm_WhenNoDependentData_CallsDeleteOnRepo()
    {
        // Arrange
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        var term  = BuildTerm(1, today, today.AddDays(90));
        _termRepoMock.Setup(r => r.GetTermByIdAsync(1)).ReturnsAsync(term);
        _termRepoMock.Setup(r => r.HasDependentDataAsync(1)).ReturnsAsync(false);
        _termRepoMock.Setup(r => r.DeleteTermAsync(1)).Returns(Task.CompletedTask);

        var sut = CreateSut();

        // Act
        await sut.DeleteTermAsync(1);

        // Assert
        _termRepoMock.Verify(r => r.DeleteTermAsync(1), Times.Once);
    }

    // ─── SaveRegistrationScheduleAsync ───────────────────────────────────────────

    [Fact]
    public async Task SaveSchedule_WhenTermNotFound_ThrowsKeyNotFoundException()
    {
        // Arrange
        _termRepoMock.Setup(r => r.GetTermByIdAsync(99)).ReturnsAsync((Term?)null);

        var sut = CreateSut();

        // Act
        Func<Task> act = () => sut.SaveRegistrationScheduleAsync(99, new SaveRegistrationScheduleRequestDto
        {
            Periods = new List<PeriodConfigItemDto>()
        });

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task SaveSchedule_WhenPeriodEndBeforeStart_ThrowsArgumentException()
    {
        // Arrange
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        var term  = BuildTerm(1, today, today.AddDays(90));
        _termRepoMock.Setup(r => r.GetTermByIdAsync(1)).ReturnsAsync(term);

        var request = new SaveRegistrationScheduleRequestDto
        {
            Periods = new List<PeriodConfigItemDto>
            {
                new() { PeriodType = "Booking", StartDate = today.AddDays(10), EndDate = today.AddDays(10), ActiveFlag = false }
            }
        };

        var sut = CreateSut();

        // Act
        Func<Task> act = () => sut.SaveRegistrationScheduleAsync(1, request);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Ngày kết thúc*");
    }

    [Fact]
    public async Task SaveSchedule_WhenKeepingEndsAfterBookingStarts_ThrowsArgumentException()
    {
        // Arrange
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        var term  = BuildTerm(1, today, today.AddDays(90));
        _termRepoMock.Setup(r => r.GetTermByIdAsync(1)).ReturnsAsync(term);

        var request = new SaveRegistrationScheduleRequestDto
        {
            Periods = new List<PeriodConfigItemDto>
            {
                new() { PeriodType = "Keeping", StartDate = today.AddDays(1), EndDate = today.AddDays(15), ActiveFlag = false },
                new() { PeriodType = "Booking", StartDate = today.AddDays(10), EndDate = today.AddDays(20), ActiveFlag = false } // overlap
            }
        };

        var sut = CreateSut();

        // Act
        Func<Task> act = () => sut.SaveRegistrationScheduleAsync(1, request);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Giữ phòng cũ*phải kết thúc trước*");
    }

    [Fact]
    public async Task SaveSchedule_WhenActivatingWithoutPublishedPrice_ThrowsInvalidOperation()
    {
        // Arrange
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        var term  = BuildTerm(1, today, today.AddDays(90));
        _termRepoMock.Setup(r => r.GetTermByIdAsync(1)).ReturnsAsync(term);
        _termRepoMock.Setup(r => r.IsPriceListPublishedAsync(1)).ReturnsAsync(false);

        var request = new SaveRegistrationScheduleRequestDto
        {
            Periods = new List<PeriodConfigItemDto>
            {
                new() { PeriodType = "Booking", StartDate = today.AddDays(1), EndDate = today.AddDays(10), ActiveFlag = true }
            }
        };

        var sut = CreateSut();

        // Act
        Func<Task> act = () => sut.SaveRegistrationScheduleAsync(1, request);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*chưa có bảng giá*");
    }

    [Fact]
    public async Task SaveSchedule_WhenKeepingPeriodOverlapsAnotherTerm_ThrowsArgumentException()
    {
        // Arrange
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        var term  = BuildTerm(1, today, today.AddDays(90));
        _termRepoMock.Setup(r => r.GetTermByIdAsync(1)).ReturnsAsync(term);
        _termRepoMock.Setup(r => r.IsPriceListPublishedAsync(1)).ReturnsAsync(true);
        _termRepoMock.Setup(r => r.GetOverlappingPeriodsAsync("Keeping",
            It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), 1))
            .ReturnsAsync(new List<RegistrationPeriod> { new() { Id = 99 } });

        var request = new SaveRegistrationScheduleRequestDto
        {
            Periods = new List<PeriodConfigItemDto>
            {
                new() { PeriodType = "Keeping", StartDate = today.AddDays(1), EndDate = today.AddDays(10), ActiveFlag = true }
            }
        };

        var sut = CreateSut();

        // Act
        Func<Task> act = () => sut.SaveRegistrationScheduleAsync(1, request);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*bị trùng thời gian*");
    }

    [Fact]
    public async Task SaveSchedule_WhenValid_CallsSaveAndReturnsDetail()
    {
        // Arrange
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        var term  = BuildTerm(1, today, today.AddDays(90));
        _termRepoMock.Setup(r => r.GetTermByIdAsync(1)).ReturnsAsync(term);
        _termRepoMock.Setup(r => r.SaveRegistrationPeriodsAsync(1, It.IsAny<List<RegistrationPeriod>>())).Returns(Task.CompletedTask);
        _termRepoMock.Setup(r => r.HasDependentDataAsync(1)).ReturnsAsync(false);

        var request = new SaveRegistrationScheduleRequestDto
        {
            Periods = new List<PeriodConfigItemDto>
            {
                new() { PeriodType = "Booking", StartDate = today.AddDays(1), EndDate = today.AddDays(10), ActiveFlag = false }
            }
        };

        var sut = CreateSut();

        // Act
        await sut.SaveRegistrationScheduleAsync(1, request);

        // Assert
        _termRepoMock.Verify(r => r.SaveRegistrationPeriodsAsync(1, It.IsAny<List<RegistrationPeriod>>()), Times.Once);
    }

    // ─── TogglePeriodActiveAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task TogglePeriod_WhenActivatingWithoutPublishedPrice_ThrowsInvalidOperation()
    {
        // Arrange
        _termRepoMock.Setup(r => r.IsPriceListPublishedAsync(1)).ReturnsAsync(false);

        var sut = CreateSut();

        // Act
        Func<Task> act = () => sut.TogglePeriodActiveAsync(termId: 1, periodId: 1, active: true);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*chưa có bảng giá*");
    }

    [Fact]
    public async Task TogglePeriod_WhenActivatingKeepingWithOverlap_ThrowsArgumentException()
    {
        // Arrange
        var today  = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        var period = BuildPeriod(1, 1, "Keeping", today.AddDays(1), today.AddDays(10), active: false);

        _termRepoMock.Setup(r => r.IsPriceListPublishedAsync(1)).ReturnsAsync(true);
        _termRepoMock.Setup(r => r.GetPeriodsByTermIdAsync(1)).ReturnsAsync(new List<RegistrationPeriod> { period });
        _termRepoMock.Setup(r => r.GetOverlappingPeriodsAsync("Keeping",
            period.StartDate, period.EndDate, 1))
            .ReturnsAsync(new List<RegistrationPeriod> { new() { Id = 99 } });

        var sut = CreateSut();

        // Act
        Func<Task> act = () => sut.TogglePeriodActiveAsync(termId: 1, periodId: 1, active: true);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*bị trùng thời gian*");
    }

    [Fact]
    public async Task TogglePeriod_WhenDeactivating_SkipsChecksAndCallsRepo()
    {
        // Arrange
        _termRepoMock.Setup(r => r.TogglePeriodActiveAsync(1, false)).Returns(Task.CompletedTask);

        var sut = CreateSut();

        // Act
        await sut.TogglePeriodActiveAsync(termId: 1, periodId: 1, active: false);

        // Assert
        _termRepoMock.Verify(r => r.IsPriceListPublishedAsync(It.IsAny<int>()), Times.Never);
        _termRepoMock.Verify(r => r.TogglePeriodActiveAsync(1, false), Times.Once);
    }

    [Fact]
    public async Task TogglePeriod_WhenActivatingBookingPeriod_CallsRepoWithoutOverlapCheck()
    {
        // Arrange
        var today  = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        var period = BuildPeriod(2, 1, "Booking", today.AddDays(1), today.AddDays(10), active: false);

        _termRepoMock.Setup(r => r.IsPriceListPublishedAsync(1)).ReturnsAsync(true);
        _termRepoMock.Setup(r => r.GetPeriodsByTermIdAsync(1)).ReturnsAsync(new List<RegistrationPeriod> { period });
        _termRepoMock.Setup(r => r.TogglePeriodActiveAsync(2, true)).Returns(Task.CompletedTask);

        var sut = CreateSut();

        // Act
        await sut.TogglePeriodActiveAsync(termId: 1, periodId: 2, active: true);

        // Assert
        _termRepoMock.Verify(r => r.GetOverlappingPeriodsAsync(It.IsAny<string>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<int>()), Times.Never);
        _termRepoMock.Verify(r => r.TogglePeriodActiveAsync(2, true), Times.Once);
    }
}
