using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using FUD.DTOs.Booking;
using FUD.Models;
using FUD.Repositories.Interfaces.Booking;
using FUD.Services.Booking;
using Moq;
using Xunit;

namespace FUD.Tests.Booking;

/// <summary>
/// Unit tests cho nghiệp vụ Phê duyệt Đơn đặt phòng (Approve Booking) tại tầng Service.
/// Tuân thủ quy tắc AAA (Arrange - Act - Assert) và chuẩn kiểm thử chuyên nghiệp.
/// </summary>
public class ApproveBookingServiceTests
{
    private readonly Mock<IBookingRepository> _bookingRepoMock = new();

    private BookingService CreateSut() => new(_bookingRepoMock.Object);

    private static (BookingRequest Booking, User StudentUser, Student Student, User ManagerUser, Manager Manager, Term Term, Dorm Dorm, Block Block, Room Room, Bed Bed)
        CreateSampleEntities(int bookingId = 1, int studentId = 10, int managerId = 99, int bedId = 20, int termId = 5)
    {
        var studentUser = new User
        {
            Id = studentId,
            Code = "HE170001",
            FullName = "Nguyen Van A",
            Email = "anvhe170001@fpt.edu.vn",
            Gender = "Male",
            AccountStatus = "Active",
            CreatedAt = DateTime.UtcNow
        };
        var student = new Student { UserId = studentId, User = studentUser };

        var managerUser = new User
        {
            Id = managerId,
            Code = "MGR001",
            FullName = "Tran Quan Ly",
            Email = "manager@fpt.edu.vn",
            Gender = "Female",
            AccountStatus = "Active",
            CreatedAt = DateTime.UtcNow
        };
        var manager = new Manager { UserId = managerId, User = managerUser };

        var term = new Term
        {
            Id = termId,
            Name = "Fall 2026",
            AcademicYear = "2026-2027",
            StartDate = new DateOnly(2026, 9, 1),
            EndDate = new DateOnly(2026, 12, 31)
        };

        var dorm = new Dorm { Id = 1, Code = "DA", Name = "Dorm A", Status = "Active" };
        var block = new Block { Id = 1, DormId = 1, Code = "A1", Name = "Toa A1", GenderType = "Male", Status = "Active", Dorm = dorm };
        var room = new Room { Id = 1, BlockId = 1, RoomTypeId = 1, RoomNumber = "101", Status = "Active", Block = block };
        var bed = new Bed { Id = bedId, RoomId = 1, BedCode = "A1-101-1", PositionInRoom = "Tang 1 Trai", Status = "Holding", Room = room };

        var booking = new BookingRequest
        {
            Id = bookingId,
            StudentId = studentId,
            TermId = termId,
            BedId = bedId,
            BookingKind = "Booking",
            AgreementToRegulations = true,
            PaymentDeadline = DateTime.UtcNow.AddHours(24),
            Status = "Approved",
            CancelledBySystem = false,
            CreatedTime = DateTime.UtcNow.AddHours(-1),
            ProcessedById = managerId,
            ProcessedTime = DateTime.UtcNow,
            Student = student,
            ProcessedBy = manager,
            Term = term,
            Bed = bed
        };

        return (booking, studentUser, student, managerUser, manager, term, dorm, block, room, bed);
    }

    [Fact]
    public async Task ApproveAsync_WhenValidPendingBooking_CallsRepoAndReturnsMappedDto()
    {
        // Arrange
        const int bookingId = 100;
        const int managerId = 88;
        const decimal expectedPrice = 4200000m;
        var (sampleBooking, _, _, _, _, _, _, _, _, _) = CreateSampleEntities(bookingId: bookingId, managerId: managerId);

        _bookingRepoMock.Setup(r => r.ApproveBookingAsync(bookingId, managerId))
            .Returns(Task.CompletedTask);
        _bookingRepoMock.Setup(r => r.GetBookingRequestByIdAsync(bookingId))
            .ReturnsAsync(sampleBooking);
        _bookingRepoMock.Setup(r => r.GetBedPriceAsync(sampleBooking.BedId, sampleBooking.TermId))
            .ReturnsAsync(expectedPrice);

        var sut = CreateSut();

        // Act
        var result = await sut.ApproveAsync(bookingId, managerId);

        // Assert
        _bookingRepoMock.Verify(r => r.ApproveBookingAsync(bookingId, managerId), Times.Once);
        _bookingRepoMock.Verify(r => r.GetBookingRequestByIdAsync(bookingId), Times.Once);
        _bookingRepoMock.Verify(r => r.GetBedPriceAsync(sampleBooking.BedId, sampleBooking.TermId), Times.Once);

        result.Should().NotBeNull();
        result.Id.Should().Be(bookingId);
        result.StudentId.Should().Be(sampleBooking.StudentId);
        result.StudentCode.Should().Be(sampleBooking.Student.User.Code);
        result.StudentName.Should().Be(sampleBooking.Student.User.FullName);
        result.StudentGender.Should().Be(sampleBooking.Student.User.Gender);
        result.BedId.Should().Be(sampleBooking.BedId);
        result.BedCode.Should().Be(sampleBooking.Bed.BedCode);
        result.RoomNumber.Should().Be(sampleBooking.Bed.Room.RoomNumber);
        result.BlockName.Should().Be(sampleBooking.Bed.Room.Block.Name);
        result.DormName.Should().Be(sampleBooking.Bed.Room.Block.Dorm.Name);
        result.TermId.Should().Be(sampleBooking.TermId);
        result.TermName.Should().Be(sampleBooking.Term.Name);
        result.TermPrice.Should().Be(expectedPrice);
        result.Status.Should().Be("Approved");
        result.ProcessedByName.Should().Be(sampleBooking.ProcessedBy!.User.FullName);
        result.ProcessedTime.Should().Be(sampleBooking.ProcessedTime);
        result.RejectReason.Should().BeNull();
    }

    [Fact]
    public async Task ApproveAsync_WhenBedPriceIsNull_ReturnsDtoWithNullTermPrice()
    {
        // Arrange
        const int bookingId = 101;
        const int managerId = 88;
        var (sampleBooking, _, _, _, _, _, _, _, _, _) = CreateSampleEntities(bookingId: bookingId, managerId: managerId);

        _bookingRepoMock.Setup(r => r.ApproveBookingAsync(bookingId, managerId))
            .Returns(Task.CompletedTask);
        _bookingRepoMock.Setup(r => r.GetBookingRequestByIdAsync(bookingId))
            .ReturnsAsync(sampleBooking);
        _bookingRepoMock.Setup(r => r.GetBedPriceAsync(sampleBooking.BedId, sampleBooking.TermId))
            .ReturnsAsync((decimal?)null);

        var sut = CreateSut();

        // Act
        var result = await sut.ApproveAsync(bookingId, managerId);

        // Assert
        result.Should().NotBeNull();
        result.TermPrice.Should().BeNull();
        result.Status.Should().Be("Approved");
    }

    [Fact]
    public async Task ApproveAsync_WhenBookingNotFound_ThrowsKeyNotFoundException()
    {
        // Arrange
        const int notFoundBookingId = 999;
        const int managerId = 88;

        _bookingRepoMock.Setup(r => r.ApproveBookingAsync(notFoundBookingId, managerId))
            .ThrowsAsync(new KeyNotFoundException("Không tìm thấy đơn đặt phòng."));

        var sut = CreateSut();

        // Act
        Func<Task> act = async () => await sut.ApproveAsync(notFoundBookingId, managerId);

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("*Không tìm thấy đơn đặt phòng*");
        _bookingRepoMock.Verify(r => r.GetBookingRequestByIdAsync(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task ApproveAsync_WhenBookingAlreadyProcessed_ThrowsInvalidOperationException()
    {
        // Arrange
        const int bookingId = 102;
        const int managerId = 88;

        _bookingRepoMock.Setup(r => r.ApproveBookingAsync(bookingId, managerId))
            .ThrowsAsync(new InvalidOperationException("Đơn đặt phòng đang ở trạng thái 'Approved', không thể phê duyệt."));

        var sut = CreateSut();

        // Act
        Func<Task> act = async () => await sut.ApproveAsync(bookingId, managerId);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*không thể phê duyệt*");
        _bookingRepoMock.Verify(r => r.GetBookingRequestByIdAsync(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task ApproveAsync_WhenBedNotAvailableForTerm_ThrowsInvalidOperationException()
    {
        // Arrange
        const int bookingId = 103;
        const int managerId = 88;

        _bookingRepoMock.Setup(r => r.ApproveBookingAsync(bookingId, managerId))
            .ThrowsAsync(new InvalidOperationException("Giường này hiện không còn trống cho học kỳ này."));

        var sut = CreateSut();

        // Act
        Func<Task> act = async () => await sut.ApproveAsync(bookingId, managerId);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Giường này hiện không còn trống cho học kỳ này*");
        _bookingRepoMock.Verify(r => r.GetBookingRequestByIdAsync(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task CheckEligibilityAsync_WhenStudentAlreadyHasApprovedBooking_ReturnsIneligibleWithApprovedReason()
    {
        // Arrange
        const int studentId = 10;
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        var user = new User { Id = studentId, Gender = "Male", AccountStatus = "Active" };
        var period = new RegistrationPeriod { Id = 1, TermId = 5, PeriodType = "Booking", StartDate = today.AddDays(-2), EndDate = today.AddDays(5), ActiveFlag = true };
        var term = new Term { Id = 5, Name = "Fall 2026" };

        var (approvedBooking, _, _, _, _, _, _, _, _, _) = CreateSampleEntities(bookingId: 1, studentId: studentId, termId: 5);
        approvedBooking.Status = "Approved";

        _bookingRepoMock.Setup(r => r.GetUserByIdAsync(studentId)).ReturnsAsync(user);
        _bookingRepoMock.Setup(r => r.GetOpenBookingPeriodAsync(It.IsAny<DateOnly>()))
            .ReturnsAsync((period, term));
        _bookingRepoMock.Setup(r => r.GetStudentResidencyForTermAsync(studentId, term.Id))
            .ReturnsAsync((Residency?)null);
        _bookingRepoMock.Setup(r => r.GetStudentBookingForTermAsync(studentId, term.Id))
            .ReturnsAsync(approvedBooking);

        var sut = CreateSut();

        // Act
        var result = await sut.CheckEligibilityAsync(studentId);

        // Assert
        result.Eligible.Should().BeFalse();
        result.IneligibleReason.Should().Be("Đơn đặt phòng của bạn đã được phê duyệt.");
        result.CurrentBooking.Should().NotBeNull();
        result.CurrentBooking!.Status.Should().Be("Approved");
        result.CurrentBooking.RequestId.Should().Be(approvedBooking.Id);
    }

    [Fact]
    public async Task CreateBookingAsync_WhenStudentAlreadyHasApprovedBooking_ThrowsInvalidOperationException()
    {
        // Arrange
        const int studentId = 10;
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        var user = new User { Id = studentId, Gender = "Male", AccountStatus = "Active" };
        var period = new RegistrationPeriod { Id = 1, TermId = 5, PeriodType = "Booking", StartDate = today.AddDays(-2), EndDate = today.AddDays(5), ActiveFlag = true };
        var term = new Term { Id = 5, Name = "Fall 2026" };

        var (approvedBooking, _, _, _, _, _, _, _, _, _) = CreateSampleEntities(bookingId: 1, studentId: studentId, termId: 5);
        approvedBooking.Status = "Approved";

        _bookingRepoMock.Setup(r => r.GetOpenBookingPeriodAsync(It.IsAny<DateOnly>()))
            .ReturnsAsync((period, term));
        _bookingRepoMock.Setup(r => r.GetUserByIdAsync(studentId)).ReturnsAsync(user);
        _bookingRepoMock.Setup(r => r.GetStudentResidencyForTermAsync(studentId, term.Id))
            .ReturnsAsync((Residency?)null);
        _bookingRepoMock.Setup(r => r.GetStudentBookingForTermAsync(studentId, term.Id))
            .ReturnsAsync(approvedBooking);

        var sut = CreateSut();
        var dto = new CreateBookingRequestDto { BedId = 20, AgreementToRegulations = true };

        // Act
        Func<Task> act = async () => await sut.CreateBookingAsync(studentId, dto);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Bạn đã có đơn đặt phòng cho học kỳ này.");
        _bookingRepoMock.Verify(r => r.CreateBookingRequestAsync(It.IsAny<BookingRequest>()), Times.Never);
    }
}

