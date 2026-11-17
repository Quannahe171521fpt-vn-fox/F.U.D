using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using FUD.Models;
using FUD.Repositories.Booking;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FUD.Tests.Booking;

/// <summary>
/// Unit tests cho nghiệp vụ Phê duyệt Đơn đặt phòng (Approve Booking) tại tầng Repository.
/// Tuân thủ quy tắc AAA (Arrange - Act - Assert) với EF Core InMemory Database.
/// </summary>
public class ApproveBookingRepositoryTests
{
    private static FudContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<FudContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new FudContext(options);
    }

    private static async Task<(Term Term, Dorm Dorm, Block Block, Room Room, Bed Bed, User StudentUser, Student Student, User ManagerUser, BookingRequest Booking)>
        SeedBasicBookingGraphAsync(FudContext context, string bookingStatus = "Pending", string bedStatus = "Free")
    {
        var term = new Term
        {
            Id = 1,
            Name = "Fall 2026",
            AcademicYear = "2026-2027",
            StartDate = new DateOnly(2026, 9, 1),
            EndDate = new DateOnly(2026, 12, 31)
        };

        var dorm = new Dorm { Id = 1, CampusId = 1, Code = "DA", Name = "Dorm A", Status = "Active" };
        var block = new Block { Id = 1, DormId = 1, Code = "A1", Name = "Block A1", GenderType = "Male", Status = "Active", Dorm = dorm };
        var roomType = new RoomType { Id = 1, Name = "Phòng 4 giường", BedCapacity = 4 };
        var room = new Room { Id = 1, BlockId = 1, RoomTypeId = 1, RoomNumber = "101", FloorLevel = 1, Status = "Active", Block = block, RoomType = roomType };
        var bed = new Bed { Id = 10, RoomId = 1, BedCode = "A1-101-1", PositionInRoom = "Dưới 1", Status = bedStatus, Room = room };

        var studentUser = new User
        {
            Id = 100,
            Code = "HE170001",
            FullName = "Nguyen Van A",
            Email = "anvhe170001@fpt.edu.vn",
            Gender = "Male",
            AccountStatus = "Active",
            CreatedAt = DateTime.UtcNow
        };
        var student = new Student { UserId = 100, User = studentUser, Major = "SE", Cohort = "K17" };

        var managerUser = new User
        {
            Id = 200,
            Code = "MGR001",
            FullName = "Tran Quan Ly",
            Email = "manager@fpt.edu.vn",
            Gender = "Female",
            AccountStatus = "Active",
            CreatedAt = DateTime.UtcNow
        };

        var booking = new BookingRequest
        {
            Id = 1,
            StudentId = 100,
            TermId = 1,
            BedId = 10,
            BookingKind = "Booking",
            AgreementToRegulations = true,
            PaymentDeadline = DateTime.UtcNow.AddHours(24),
            Status = bookingStatus,
            CancelledBySystem = false,
            CreatedTime = DateTime.UtcNow.AddHours(-2),
            Bed = bed,
            Term = term,
            Student = student
        };

        await context.Terms.AddAsync(term);
        await context.Dorms.AddAsync(dorm);
        await context.Blocks.AddAsync(block);
        await context.RoomTypes.AddAsync(roomType);
        await context.Rooms.AddAsync(room);
        await context.Beds.AddAsync(bed);
        await context.Users.AddRangeAsync(studentUser, managerUser);
        await context.Students.AddAsync(student);
        await context.BookingRequests.AddAsync(booking);
        await context.SaveChangesAsync();

        return (term, dorm, block, room, bed, studentUser, student, managerUser, booking);
    }

    [Fact]
    public async Task ApproveBookingAsync_WhenValidPendingBooking_UpdatesStatusToApprovedAndSetsProcessedInfo()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var (_, _, _, _, _, _, _, managerUser, booking) = await SeedBasicBookingGraphAsync(context);
        var sut = new BookingRepository(context);

        // Act
        await sut.ApproveBookingAsync(booking.Id, managerUser.Id);

        // Assert
        var updatedBooking = await context.BookingRequests.FindAsync(booking.Id);
        updatedBooking.Should().NotBeNull();
        updatedBooking!.Status.Should().Be("Approved");
        updatedBooking.ProcessedById.Should().Be(managerUser.Id);
        updatedBooking.ProcessedTime.Should().BeCloseTo(DateTime.UtcNow.AddHours(7), TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task ApproveBookingAsync_WhenValidPendingBooking_ChangesBedStatusToHolding()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var (_, _, _, _, bed, _, _, managerUser, booking) = await SeedBasicBookingGraphAsync(context, bedStatus: "Free");
        var sut = new BookingRepository(context);

        // Act
        await sut.ApproveBookingAsync(booking.Id, managerUser.Id);

        // Assert
        var updatedBed = await context.Beds.FindAsync(bed.Id);
        updatedBed.Should().NotBeNull();
        updatedBed!.Status.Should().Be("Holding");
    }

    [Fact]
    public async Task ApproveBookingAsync_WhenValidPendingBooking_CreatesResidencyWithStatusReserved()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var (term, _, _, _, bed, _, student, managerUser, booking) = await SeedBasicBookingGraphAsync(context);
        var sut = new BookingRepository(context);

        // Act
        await sut.ApproveBookingAsync(booking.Id, managerUser.Id);

        // Assert
        var residency = await context.Residencies.FirstOrDefaultAsync(r => r.SourceBookingId == booking.Id);
        residency.Should().NotBeNull();
        residency!.StudentId.Should().Be(student.UserId);
        residency.BedId.Should().Be(bed.Id);
        residency.TermId.Should().Be(term.Id);
        residency.RegistrationType.Should().Be("Booking");
        residency.Status.Should().Be("Reserved");
        residency.CheckInDate.Should().BeNull();
        residency.PlannedCheckOutDate.Should().Be(term.EndDate);
    }

    [Fact]
    public async Task ApproveBookingAsync_WhenManagerNotInManagersTable_AutoCreatesManagerRecord()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var (_, _, _, _, _, _, _, managerUser, booking) = await SeedBasicBookingGraphAsync(context);
        var sut = new BookingRepository(context);

        var managerExistsBefore = await context.Managers.AnyAsync(m => m.UserId == managerUser.Id);
        managerExistsBefore.Should().BeFalse();

        // Act
        await sut.ApproveBookingAsync(booking.Id, managerUser.Id);

        // Assert
        var managerExistsAfter = await context.Managers.AnyAsync(m => m.UserId == managerUser.Id);
        managerExistsAfter.Should().BeTrue();
    }

    [Fact]
    public async Task ApproveBookingAsync_WhenBookingDoesNotExist_ThrowsKeyNotFoundException()
    {
        // Arrange
        const int nonExistentBookingId = 9999;
        const int managerId = 200;
        using var context = CreateInMemoryContext();
        var sut = new BookingRepository(context);

        // Act
        Func<Task> act = async () => await sut.ApproveBookingAsync(nonExistentBookingId, managerId);

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("Không tìm thấy đơn đặt phòng.");
    }

    [Fact]
    public async Task ApproveBookingAsync_WhenBookingStatusIsAlreadyApproved_ThrowsInvalidOperationException()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var (_, _, _, _, _, _, _, managerUser, booking) = await SeedBasicBookingGraphAsync(context, bookingStatus: "Approved");
        var sut = new BookingRepository(context);

        // Act
        Func<Task> act = async () => await sut.ApproveBookingAsync(booking.Id, managerUser.Id);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*đang ở trạng thái 'Approved', không thể phê duyệt*");
    }

    [Fact]
    public async Task ApproveBookingAsync_WhenBookingStatusIsRejected_ThrowsInvalidOperationException()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var (_, _, _, _, _, _, _, managerUser, booking) = await SeedBasicBookingGraphAsync(context, bookingStatus: "Rejected");
        var sut = new BookingRepository(context);

        // Act
        Func<Task> act = async () => await sut.ApproveBookingAsync(booking.Id, managerUser.Id);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*đang ở trạng thái 'Rejected', không thể phê duyệt*");
    }

    [Fact]
    public async Task ApproveBookingAsync_WhenBedOccupiedByActiveResidencyInSameTerm_ThrowsInvalidOperationException()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var (term, _, _, _, bed, _, _, managerUser, booking) = await SeedBasicBookingGraphAsync(context);

        var otherStudentUser = new User
        {
            Id = 101,
            Code = "HE170002",
            FullName = "Tran Van B",
            Email = "b@fpt.edu.vn",
            Gender = "Male",
            AccountStatus = "Active",
            CreatedAt = DateTime.UtcNow
        };
        var otherStudent = new Student { UserId = 101, User = otherStudentUser };
        var activeResidency = new Residency
        {
            Id = 50,
            StudentId = 101,
            BedId = bed.Id,
            TermId = term.Id,
            Status = "Active",
            CheckInDate = new DateOnly(2026, 9, 1)
        };
        await context.Users.AddAsync(otherStudentUser);
        await context.Students.AddAsync(otherStudent);
        await context.Residencies.AddAsync(activeResidency);
        await context.SaveChangesAsync();

        var sut = new BookingRepository(context);

        // Act
        Func<Task> act = async () => await sut.ApproveBookingAsync(booking.Id, managerUser.Id);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Giường này hiện không còn trống cho học kỳ này.");
    }

    [Fact]
    public async Task ApproveBookingAsync_WhenBedOccupiedByReservedResidencyInSameTerm_ThrowsInvalidOperationException()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var (term, _, _, _, bed, _, _, managerUser, booking) = await SeedBasicBookingGraphAsync(context);

        var reservedResidency = new Residency
        {
            Id = 51,
            StudentId = 101,
            BedId = bed.Id,
            TermId = term.Id,
            Status = "Reserved"
        };
        await context.Residencies.AddAsync(reservedResidency);
        await context.SaveChangesAsync();

        var sut = new BookingRepository(context);

        // Act
        Func<Task> act = async () => await sut.ApproveBookingAsync(booking.Id, managerUser.Id);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Giường này hiện không còn trống cho học kỳ này.");
    }

    [Fact]
    public async Task ApproveBookingAsync_WhenBedHasAnotherApprovedBookingInSameTerm_ThrowsInvalidOperationException()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var (term, _, _, _, bed, _, _, managerUser, booking) = await SeedBasicBookingGraphAsync(context);

        var anotherApprovedBooking = new BookingRequest
        {
            Id = 2,
            StudentId = 102,
            TermId = term.Id,
            BedId = bed.Id,
            BookingKind = "Booking",
            Status = "Approved",
            AgreementToRegulations = true,
            PaymentDeadline = DateTime.UtcNow.AddHours(24),
            CreatedTime = DateTime.UtcNow.AddHours(-1)
        };
        await context.BookingRequests.AddAsync(anotherApprovedBooking);
        await context.SaveChangesAsync();

        var sut = new BookingRepository(context);

        // Act
        Func<Task> act = async () => await sut.ApproveBookingAsync(booking.Id, managerUser.Id);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Giường này hiện không còn trống cho học kỳ này.");
    }

    [Fact]
    public async Task ApproveBookingAsync_WhenBedHasAnotherPendingBookingInSameTerm_ThrowsInvalidOperationException()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var (term, _, _, _, bed, _, _, managerUser, booking) = await SeedBasicBookingGraphAsync(context);

        var anotherPendingBooking = new BookingRequest
        {
            Id = 3,
            StudentId = 103,
            TermId = term.Id,
            BedId = bed.Id,
            BookingKind = "Booking",
            Status = "Pending",
            AgreementToRegulations = true,
            PaymentDeadline = DateTime.UtcNow.AddHours(24),
            CreatedTime = DateTime.UtcNow.AddHours(-1)
        };
        await context.BookingRequests.AddAsync(anotherPendingBooking);
        await context.SaveChangesAsync();

        var sut = new BookingRepository(context);

        // Act
        Func<Task> act = async () => await sut.ApproveBookingAsync(booking.Id, managerUser.Id);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Giường này hiện không còn trống cho học kỳ này.");
    }

    [Fact]
    public async Task ApproveBookingAsync_WhenBedIsUnderMaintenance_ThrowsInvalidOperationException()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var (_, _, _, _, bed, _, _, managerUser, booking) = await SeedBasicBookingGraphAsync(context, bedStatus: "Maintenance");
        var sut = new BookingRepository(context);

        // Act
        Func<Task> act = async () => await sut.ApproveBookingAsync(booking.Id, managerUser.Id);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Giường này hiện không còn trống cho học kỳ này.");
    }

    [Fact]
    public async Task ApproveBookingAsync_WhenBedOccupiedInDifferentTerm_ApprovesSuccessfully()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var (_, _, _, _, bed, _, _, managerUser, booking) = await SeedBasicBookingGraphAsync(context);

        var otherTerm = new Term
        {
            Id = 99,
            Name = "Spring 2027",
            AcademicYear = "2026-2027",
            StartDate = new DateOnly(2027, 1, 1),
            EndDate = new DateOnly(2027, 4, 30)
        };
        var otherTermResidency = new Residency
        {
            Id = 999,
            StudentId = 105,
            BedId = bed.Id,
            TermId = otherTerm.Id,
            Status = "Active"
        };
        await context.Terms.AddAsync(otherTerm);
        await context.Residencies.AddAsync(otherTermResidency);
        await context.SaveChangesAsync();

        var sut = new BookingRepository(context);

        // Act
        await sut.ApproveBookingAsync(booking.Id, managerUser.Id);

        // Assert
        var approvedBooking = await context.BookingRequests.FindAsync(booking.Id);
        approvedBooking!.Status.Should().Be("Approved");
    }

    [Fact]
    public async Task ApproveBookingAsync_ExcludesCurrentBookingItselfFromCollisionCheck()
    {
        // Arrange: Đơn đặt phòng hiện tại đang là Pending và giữ giường này.
        // Khi phê duyệt, thuật toán kiểm tra không tự xung đột với chính đơn này.
        using var context = CreateInMemoryContext();
        var (_, _, _, _, _, _, _, managerUser, booking) = await SeedBasicBookingGraphAsync(context, bookingStatus: "Pending");
        var sut = new BookingRepository(context);

        // Act
        var act = async () => await sut.ApproveBookingAsync(booking.Id, managerUser.Id);

        // Assert
        await act.Should().NotThrowAsync();
        var updated = await context.BookingRequests.FindAsync(booking.Id);
        updated!.Status.Should().Be("Approved");
    }
}

