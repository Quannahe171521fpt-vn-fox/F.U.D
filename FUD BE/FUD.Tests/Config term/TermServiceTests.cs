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

namespace FUD.Tests.ConfigTerm;

public class TermServiceTests
{
    private readonly Mock<ITermRepository> _termRepoMock;
    private readonly TermService _termService;

    public TermServiceTests()
    {
        _termRepoMock = new Mock<ITermRepository>();
        _termService = new TermService(_termRepoMock.Object);
    }

    [Fact]
    public async Task GetAllTermsAsync_CalculatesStatusesAndMappingsCorrectly()
    {
        // Arrange
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        var publishedTime = DateTime.UtcNow.AddDays(-5);

        var terms = new List<Term>
        {
            // Term 1: Upcoming, PriceList Published, Keeping Active
            new Term
            {
                Id = 1,
                Name = "Fall 2026",
                AcademicYear = "2026-2027",
                StartDate = today.AddDays(10),
                EndDate = today.AddDays(100),
                PriceList = new PriceList { Id = 1, Status = "Published", PublishedTime = publishedTime },
                RegistrationPeriods = new List<RegistrationPeriod>
                {
                    new RegistrationPeriod
                    {
                        Id = 1,
                        TermId = 1,
                        PeriodType = "Keeping",
                        StartDate = today.AddDays(-2),
                        EndDate = today.AddDays(5),
                        ActiveFlag = true
                    }
                }
            },
            // Term 2: Ongoing, PriceList Draft
            new Term
            {
                Id = 2,
                Name = "Summer 2026",
                AcademicYear = "2025-2026",
                StartDate = today.AddDays(-10),
                EndDate = today.AddDays(20),
                PriceList = new PriceList { Id = 2, Status = "Draft" },
                RegistrationPeriods = new List<RegistrationPeriod>()
            },
            // Term 3: Ended, No PriceList
            new Term
            {
                Id = 3,
                Name = "Spring 2026",
                AcademicYear = "2025-2026",
                StartDate = today.AddDays(-120),
                EndDate = today.AddDays(-30),
                PriceList = null,
                RegistrationPeriods = new List<RegistrationPeriod>()
            }
        };

        _termRepoMock.Setup(r => r.GetAllTermsAsync()).ReturnsAsync(terms);
        _termRepoMock.Setup(r => r.HasDependentDataAsync(1)).ReturnsAsync(false);
        _termRepoMock.Setup(r => r.HasDependentDataAsync(2)).ReturnsAsync(true);
        _termRepoMock.Setup(r => r.HasDependentDataAsync(3)).ReturnsAsync(false);

        // Act
        var result = await _termService.GetAllTermsAsync();

        // Assert
        result.Should().HaveCount(3);

        // Term 1 asserts
        var t1 = result.First(t => t.Id == 1);
        t1.TermStatus.Should().Be("Upcoming");
        t1.PriceListStatus.Should().Be("Published");
        t1.IsPricePublished.Should().BeTrue();
        t1.PriceListPublishedTime.Should().Be(publishedTime);
        t1.HasDependentData.Should().BeFalse();
        t1.KeepingPeriod.Should().NotBeNull();
        t1.KeepingPeriod!.StatusText.Should().Be("Đang mở");
        t1.DisplayName.Should().Be("Fall 2026 (2026-2027)");

        // Term 2 asserts
        var t2 = result.First(t => t.Id == 2);
        t2.TermStatus.Should().Be("Ongoing");
        t2.PriceListStatus.Should().Be("Draft");
        t2.IsPricePublished.Should().BeFalse();
        t2.HasDependentData.Should().BeTrue();

        // Term 3 asserts
        var t3 = result.First(t => t.Id == 3);
        t3.TermStatus.Should().Be("Ended");
        t3.PriceListStatus.Should().Be("NotCreated");
    }

    [Fact]
    public async Task GetTermDetailAsync_NonExistingId_ThrowsKeyNotFoundException()
    {
        // Arrange
        _termRepoMock.Setup(r => r.GetTermByIdAsync(999)).ReturnsAsync((Term?)null);

        // Act
        var act = () => _termService.GetTermDetailAsync(999);

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("*999*");
    }

    [Fact]
    public async Task GetTermDetailAsync_ExistingId_ReturnsCompleteDetailDto()
    {
        // Arrange
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        var term = new Term
        {
            Id = 1,
            Name = "Spring 2026",
            AcademicYear = "2025-2026",
            StartDate = today.AddDays(-10),
            EndDate = today.AddDays(30),
            PriceList = new PriceList { Id = 1, Status = "Published" },
            RegistrationPeriods = new List<RegistrationPeriod>
            {
                new RegistrationPeriod { Id = 1, TermId = 1, PeriodType = "Keeping", StartDate = today.AddDays(-5), EndDate = today.AddDays(5), ActiveFlag = true },
                new RegistrationPeriod { Id = 2, TermId = 1, PeriodType = "Booking", StartDate = today.AddDays(6), EndDate = today.AddDays(15), ActiveFlag = false }
            }
        };

        _termRepoMock.Setup(r => r.GetTermByIdAsync(1)).ReturnsAsync(term);
        _termRepoMock.Setup(r => r.HasDependentDataAsync(1)).ReturnsAsync(false);

        // Act
        var detail = await _termService.GetTermDetailAsync(1);

        // Assert
        detail.Should().NotBeNull();
        detail.Id.Should().Be(1);
        detail.Name.Should().Be("Spring 2026");
        detail.TermStatus.Should().Be("Ongoing");
        detail.AllPeriods.Should().HaveCount(2);
        detail.KeepingPeriod.Should().NotBeNull();
        detail.BookingPeriod.Should().NotBeNull();
        detail.BookingPeriod!.StatusText.Should().Be("Đã tắt");
    }

    [Fact]
    public async Task CreateTermAsync_EndDateLessThanOrEqualToStartDate_ThrowsArgumentException()
    {
        // Arrange
        var request = new CreateTermRequestDto
        {
            Name = "Summer 2026",
            AcademicYear = "2025-2026",
            StartDate = new DateOnly(2026, 6, 1),
            EndDate = new DateOnly(2026, 6, 1) // Bằng ngày bắt đầu
        };

        // Act
        var act = () => _termService.CreateTermAsync(request);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("Ngày kết thúc học kỳ phải lớn hơn ngày bắt đầu.");
    }

    [Fact]
    public async Task CreateTermAsync_DuplicateTermNameAndYear_ThrowsArgumentException()
    {
        // Arrange
        var request = new CreateTermRequestDto
        {
            Name = "  Spring 2026  ",
            AcademicYear = "  2025-2026  ",
            StartDate = new DateOnly(2026, 1, 1),
            EndDate = new DateOnly(2026, 4, 30)
        };

        _termRepoMock.Setup(r => r.ExistsTermAsync("Spring 2026", "2025-2026", null))
            .ReturnsAsync(true);

        // Act
        var act = () => _termService.CreateTermAsync(request);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Học kỳ 'Spring 2026' cho năm học '2025-2026' đã tồn tại.*");
    }

    [Fact]
    public async Task CreateTermAsync_ValidInput_CreatesAndReturnsTermDetail()
    {
        // Arrange
        var request = new CreateTermRequestDto
        {
            Name = "Fall 2026",
            AcademicYear = "2026-2027",
            StartDate = new DateOnly(2026, 9, 1),
            EndDate = new DateOnly(2026, 12, 31)
        };

        var createdTerm = new Term
        {
            Id = 10,
            Name = "Fall 2026",
            AcademicYear = "2026-2027",
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            RegistrationPeriods = new List<RegistrationPeriod>()
        };

        _termRepoMock.Setup(r => r.ExistsTermAsync(request.Name, request.AcademicYear, null))
            .ReturnsAsync(false);
        _termRepoMock.Setup(r => r.CreateTermAsync(It.IsAny<Term>()))
            .ReturnsAsync(createdTerm);
        _termRepoMock.Setup(r => r.GetTermByIdAsync(10))
            .ReturnsAsync(createdTerm);
        _termRepoMock.Setup(r => r.HasDependentDataAsync(10))
            .ReturnsAsync(false);

        // Act
        var result = await _termService.CreateTermAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(10);
        result.Name.Should().Be("Fall 2026");
        _termRepoMock.Verify(r => r.CreateTermAsync(It.Is<Term>(t => t.Name == "Fall 2026" && t.AcademicYear == "2026-2027")), Times.Once);
    }

    [Fact]
    public async Task UpdateTermAsync_NonExistingTerm_ThrowsKeyNotFoundException()
    {
        // Arrange
        _termRepoMock.Setup(r => r.GetTermByIdAsync(999)).ReturnsAsync((Term?)null);
        var request = new UpdateTermRequestDto
        {
            Name = "Updated",
            AcademicYear = "2026",
            StartDate = new DateOnly(2026, 1, 1),
            EndDate = new DateOnly(2026, 4, 30)
        };

        // Act
        var act = () => _termService.UpdateTermAsync(999, request);

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("*999*");
    }

    [Fact]
    public async Task UpdateTermAsync_EndDateLessThanOrEqualToStartDate_ThrowsArgumentException()
    {
        // Arrange
        var existingTerm = new Term { Id = 1, Name = "T1", AcademicYear = "2026", StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 4, 30) };
        _termRepoMock.Setup(r => r.GetTermByIdAsync(1)).ReturnsAsync(existingTerm);

        var request = new UpdateTermRequestDto
        {
            Name = "T1",
            AcademicYear = "2026",
            StartDate = new DateOnly(2026, 5, 1),
            EndDate = new DateOnly(2026, 4, 30) // EndDate nhỏ hơn StartDate
        };

        // Act
        var act = () => _termService.UpdateTermAsync(1, request);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("Ngày kết thúc học kỳ phải lớn hơn ngày bắt đầu.");
    }

    [Fact]
    public async Task UpdateTermAsync_DuplicateWithOtherTerm_ThrowsArgumentException()
    {
        // Arrange
        var existingTerm = new Term { Id = 1, Name = "Old Name", AcademicYear = "2025-2026", StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 4, 30) };
        _termRepoMock.Setup(r => r.GetTermByIdAsync(1)).ReturnsAsync(existingTerm);
        _termRepoMock.Setup(r => r.ExistsTermAsync("Spring 2026", "2025-2026", 1)).ReturnsAsync(true);

        var request = new UpdateTermRequestDto
        {
            Name = "Spring 2026",
            AcademicYear = "2025-2026",
            StartDate = new DateOnly(2026, 1, 1),
            EndDate = new DateOnly(2026, 4, 30)
        };

        // Act
        var act = () => _termService.UpdateTermAsync(1, request);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Học kỳ 'Spring 2026' cho năm học '2025-2026' đã tồn tại.*");
    }

    [Fact]
    public async Task UpdateTermAsync_ValidInput_UpdatesAndReturnsDetail()
    {
        // Arrange
        var existingTerm = new Term
        {
            Id = 1,
            Name = "Spring 2026",
            AcademicYear = "2025-2026",
            StartDate = new DateOnly(2026, 1, 1),
            EndDate = new DateOnly(2026, 4, 30),
            RegistrationPeriods = new List<RegistrationPeriod>()
        };

        var request = new UpdateTermRequestDto
        {
            Name = "Spring 2026 Updated",
            AcademicYear = "2025-2026",
            StartDate = new DateOnly(2026, 1, 5),
            EndDate = new DateOnly(2026, 5, 10)
        };

        _termRepoMock.Setup(r => r.GetTermByIdAsync(1)).ReturnsAsync(existingTerm);
        _termRepoMock.Setup(r => r.ExistsTermAsync("Spring 2026 Updated", "2025-2026", 1)).ReturnsAsync(false);
        _termRepoMock.Setup(r => r.UpdateTermAsync(existingTerm)).Returns(Task.CompletedTask);
        _termRepoMock.Setup(r => r.HasDependentDataAsync(1)).ReturnsAsync(false);

        // Act
        var result = await _termService.UpdateTermAsync(1, request);

        // Assert
        result.Should().NotBeNull();
        existingTerm.Name.Should().Be("Spring 2026 Updated");
        existingTerm.StartDate.Should().Be(new DateOnly(2026, 1, 5));
        existingTerm.EndDate.Should().Be(new DateOnly(2026, 5, 10));
        _termRepoMock.Verify(r => r.UpdateTermAsync(existingTerm), Times.Once);
    }

    [Fact]
    public async Task DeleteTermAsync_NonExistingTerm_ThrowsKeyNotFoundException()
    {
        // Arrange
        _termRepoMock.Setup(r => r.GetTermByIdAsync(999)).ReturnsAsync((Term?)null);

        // Act
        var act = () => _termService.DeleteTermAsync(999);

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("*999*");
    }

    [Fact]
    public async Task DeleteTermAsync_WithDependentData_ThrowsInvalidOperationException()
    {
        // Arrange
        var existingTerm = new Term { Id = 1, Name = "T1", AcademicYear = "2026", StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 4, 30) };
        _termRepoMock.Setup(r => r.GetTermByIdAsync(1)).ReturnsAsync(existingTerm);
        _termRepoMock.Setup(r => r.HasDependentDataAsync(1)).ReturnsAsync(true);

        // Act
        var act = () => _termService.DeleteTermAsync(1);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Không thể xóa học kỳ vì đã có dữ liệu sinh viên đăng ký, cư trú hoặc hóa đơn liên quan.*");
        _termRepoMock.Verify(r => r.DeleteTermAsync(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task DeleteTermAsync_WithoutDependentData_CallsRepoDelete()
    {
        // Arrange
        var existingTerm = new Term { Id = 1, Name = "T1", AcademicYear = "2026", StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 4, 30) };
        _termRepoMock.Setup(r => r.GetTermByIdAsync(1)).ReturnsAsync(existingTerm);
        _termRepoMock.Setup(r => r.HasDependentDataAsync(1)).ReturnsAsync(false);

        // Act
        await _termService.DeleteTermAsync(1);

        // Assert
        _termRepoMock.Verify(r => r.DeleteTermAsync(1), Times.Once);
    }

    [Fact]
    public async Task SaveRegistrationScheduleAsync_NonExistingTerm_ThrowsKeyNotFoundException()
    {
        // Arrange
        _termRepoMock.Setup(r => r.GetTermByIdAsync(999)).ReturnsAsync((Term?)null);
        var request = new SaveRegistrationScheduleRequestDto { TermId = 999 };

        // Act
        var act = () => _termService.SaveRegistrationScheduleAsync(999, request);

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("*999*");
    }

    [Fact]
    public async Task SaveRegistrationScheduleAsync_EndDateBeforeStartDate_ThrowsArgumentException()
    {
        // Arrange
        var term = new Term { Id = 1, Name = "T1", AcademicYear = "2026", StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 4, 30) };
        _termRepoMock.Setup(r => r.GetTermByIdAsync(1)).ReturnsAsync(term);

        var request = new SaveRegistrationScheduleRequestDto
        {
            TermId = 1,
            Periods = new List<PeriodConfigItemDto>
            {
                new PeriodConfigItemDto
                {
                    PeriodType = "Keeping",
                    StartDate = new DateOnly(2026, 1, 10),
                    EndDate = new DateOnly(2026, 1, 5) // EndDate <= StartDate
                }
            }
        };

        // Act
        var act = () => _termService.SaveRegistrationScheduleAsync(1, request);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Ngày kết thúc của đợt Giữ phòng cũ phải lớn hơn ngày bắt đầu.*");
    }

    [Fact]
    public async Task SaveRegistrationScheduleAsync_KeepingEndDateAfterBookingStartDate_ThrowsArgumentException()
    {
        // Arrange
        var term = new Term { Id = 1, Name = "T1", AcademicYear = "2026", StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 4, 30) };
        _termRepoMock.Setup(r => r.GetTermByIdAsync(1)).ReturnsAsync(term);

        var request = new SaveRegistrationScheduleRequestDto
        {
            TermId = 1,
            Periods = new List<PeriodConfigItemDto>
            {
                new PeriodConfigItemDto
                {
                    PeriodType = "Keeping",
                    StartDate = new DateOnly(2026, 1, 1),
                    EndDate = new DateOnly(2026, 1, 15) // Kết thúc ngày 15
                },
                new PeriodConfigItemDto
                {
                    PeriodType = "Booking",
                    StartDate = new DateOnly(2026, 1, 10), // Bắt đầu ngày 10 (trước ngày 15)
                    EndDate = new DateOnly(2026, 1, 20)
                }
            }
        };

        // Act
        var act = () => _termService.SaveRegistrationScheduleAsync(1, request);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("Giai đoạn Giữ phòng cũ (Keeping) phải kết thúc trước hoặc cùng ngày khi giai đoạn Đăng ký phòng mới (Booking) bắt đầu.");
    }

    [Fact]
    public async Task SaveRegistrationScheduleAsync_ActiveFlagTrueWithoutPublishedPrice_ThrowsInvalidOperationException()
    {
        // Arrange
        var term = new Term { Id = 1, Name = "T1", AcademicYear = "2026", StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 4, 30) };
        _termRepoMock.Setup(r => r.GetTermByIdAsync(1)).ReturnsAsync(term);
        _termRepoMock.Setup(r => r.IsPriceListPublishedAsync(1)).ReturnsAsync(false);

        var request = new SaveRegistrationScheduleRequestDto
        {
            TermId = 1,
            Periods = new List<PeriodConfigItemDto>
            {
                new PeriodConfigItemDto
                {
                    PeriodType = "Keeping",
                    StartDate = new DateOnly(2026, 1, 1),
                    EndDate = new DateOnly(2026, 1, 10),
                    ActiveFlag = true // Kích hoạt khi chưa có bảng giá Published
                }
            }
        };

        // Act
        var act = () => _termService.SaveRegistrationScheduleAsync(1, request);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*chưa có bảng giá ở trạng thái Published*");
    }

    [Fact]
    public async Task SaveRegistrationScheduleAsync_KeepingOverlapsOtherTerm_ThrowsArgumentException()
    {
        // Arrange
        var term = new Term { Id = 1, Name = "T1", AcademicYear = "2026", StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 4, 30) };
        _termRepoMock.Setup(r => r.GetTermByIdAsync(1)).ReturnsAsync(term);
        _termRepoMock.Setup(r => r.IsPriceListPublishedAsync(1)).ReturnsAsync(true);
        _termRepoMock.Setup(r => r.GetOverlappingPeriodsAsync("Keeping", new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 10), 1))
            .ReturnsAsync(new List<RegistrationPeriod> { new RegistrationPeriod { Id = 99, TermId = 2 } });

        var request = new SaveRegistrationScheduleRequestDto
        {
            TermId = 1,
            Periods = new List<PeriodConfigItemDto>
            {
                new PeriodConfigItemDto
                {
                    PeriodType = "Keeping",
                    StartDate = new DateOnly(2026, 1, 1),
                    EndDate = new DateOnly(2026, 1, 10),
                    ActiveFlag = true
                }
            }
        };

        // Act
        var act = () => _termService.SaveRegistrationScheduleAsync(1, request);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Kỳ giữ phòng (Keeping) bị trùng thời gian với một kỳ giữ phòng của học kỳ khác.*");
    }

    [Fact]
    public async Task SaveRegistrationScheduleAsync_ValidSchedule_SavesSuccessfully()
    {
        // Arrange
        var term = new Term
        {
            Id = 1,
            Name = "Spring 2026",
            AcademicYear = "2025-2026",
            StartDate = new DateOnly(2026, 1, 1),
            EndDate = new DateOnly(2026, 4, 30),
            RegistrationPeriods = new List<RegistrationPeriod>()
        };

        _termRepoMock.Setup(r => r.GetTermByIdAsync(1)).ReturnsAsync(term);
        _termRepoMock.Setup(r => r.IsPriceListPublishedAsync(1)).ReturnsAsync(true);
        _termRepoMock.Setup(r => r.GetOverlappingPeriodsAsync("Keeping", It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), 1))
            .ReturnsAsync(new List<RegistrationPeriod>());
        _termRepoMock.Setup(r => r.HasDependentDataAsync(1)).ReturnsAsync(false);

        var request = new SaveRegistrationScheduleRequestDto
        {
            TermId = 1,
            Periods = new List<PeriodConfigItemDto>
            {
                new PeriodConfigItemDto { PeriodType = "Keeping", StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 1, 5), ActiveFlag = true },
                new PeriodConfigItemDto { PeriodType = "Booking", StartDate = new DateOnly(2026, 1, 6), EndDate = new DateOnly(2026, 1, 15), ActiveFlag = false }
            }
        };

        // Act
        var result = await _termService.SaveRegistrationScheduleAsync(1, request);

        // Assert
        result.Should().NotBeNull();
        _termRepoMock.Verify(r => r.SaveRegistrationPeriodsAsync(1, It.Is<List<RegistrationPeriod>>(p => p.Count == 2)), Times.Once);
    }

    [Fact]
    public async Task TogglePeriodActiveAsync_ActiveTrueWithoutPublishedPrice_ThrowsInvalidOperationException()
    {
        // Arrange
        _termRepoMock.Setup(r => r.IsPriceListPublishedAsync(1)).ReturnsAsync(false);

        // Act
        var act = () => _termService.TogglePeriodActiveAsync(1, 10, active: true);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*chưa có bảng giá ở trạng thái Published*");
    }

    [Fact]
    public async Task TogglePeriodActiveAsync_ActiveTrueKeepingOverlapsOtherTerm_ThrowsArgumentException()
    {
        // Arrange
        _termRepoMock.Setup(r => r.IsPriceListPublishedAsync(1)).ReturnsAsync(true);
        var keepingPeriod = new RegistrationPeriod
        {
            Id = 10,
            TermId = 1,
            PeriodType = "Keeping",
            StartDate = new DateOnly(2026, 1, 1),
            EndDate = new DateOnly(2026, 1, 5)
        };
        _termRepoMock.Setup(r => r.GetPeriodsByTermIdAsync(1))
            .ReturnsAsync(new List<RegistrationPeriod> { keepingPeriod });
        _termRepoMock.Setup(r => r.GetOverlappingPeriodsAsync("Keeping", keepingPeriod.StartDate, keepingPeriod.EndDate, 1))
            .ReturnsAsync(new List<RegistrationPeriod> { new RegistrationPeriod { Id = 99, TermId = 2 } });

        // Act
        var act = () => _termService.TogglePeriodActiveAsync(1, 10, active: true);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Kỳ giữ phòng (Keeping) bị trùng thời gian với một kỳ giữ phòng của học kỳ khác.*");
    }

    [Fact]
    public async Task TogglePeriodActiveAsync_ValidRequest_CallsRepoToggle()
    {
        // Arrange
        _termRepoMock.Setup(r => r.IsPriceListPublishedAsync(1)).ReturnsAsync(true);
        var bookingPeriod = new RegistrationPeriod
        {
            Id = 20,
            TermId = 1,
            PeriodType = "Booking",
            StartDate = new DateOnly(2026, 1, 6),
            EndDate = new DateOnly(2026, 1, 15)
        };
        _termRepoMock.Setup(r => r.GetPeriodsByTermIdAsync(1))
            .ReturnsAsync(new List<RegistrationPeriod> { bookingPeriod });

        // Act
        await _termService.TogglePeriodActiveAsync(1, 20, active: true);

        // Assert
        _termRepoMock.Verify(r => r.TogglePeriodActiveAsync(20, true), Times.Once);
    }
}
