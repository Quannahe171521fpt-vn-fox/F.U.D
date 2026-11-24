using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using FluentAssertions;
using FUD.Controllers.Checkin;
using FUD.DTOs.Checkin;
using FUD.Models;
using FUD.Services.Checkin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace FUD.Tests.Checkin;

public class CheckinAuthorizationTests
{
    private static FudContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<FudContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new FudContext(options);
    }

    [Theory]
    [InlineData(nameof(CheckinController.GetPendingStudents), "Guard,Manager,Admin")]
    [InlineData(nameof(CheckinController.GetRoomEquipments), "Guard,Manager,Admin")]
    [InlineData(nameof(CheckinController.CompleteCheckin), "Guard")]
    [InlineData(nameof(CheckinController.GetHistory), "Guard,Manager,Admin")]
    [InlineData(nameof(CheckinController.GetMyCheckins), "Student")]
    [InlineData(nameof(CheckinController.GetById), "Student,Guard,Manager,Admin")]
    public void CheckinController_Action_HasExpectedAuthorizeRoles(string actionName, string expectedRoles)
    {
        var method = typeof(CheckinController).GetMethod(
            actionName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);

        method.Should().NotBeNull($"action {actionName} must exist on CheckinController");

        var authorizeAttributes = method!.GetCustomAttributes<AuthorizeAttribute>(inherit: false).ToList();
        authorizeAttributes.Should().ContainSingle(
            $"action {actionName} must declare an explicit [Authorize(Roles = ...)] attribute");
        authorizeAttributes[0].Roles.Should().Be(expectedRoles);
    }

    [Fact]
    public void CheckinController_AllPublicActions_DeclareExplicitRoles()
    {
        var publicActions = typeof(CheckinController)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Where(m => !m.IsSpecialName && m.GetCustomAttribute<NonActionAttribute>() == null)
            .ToList();

        publicActions.Should().HaveCount(6);

        foreach (var action in publicActions)
        {
            var authorize = action.GetCustomAttribute<AuthorizeAttribute>(inherit: false);
            authorize.Should().NotBeNull($"action {action.Name} must declare [Authorize(Roles = ...)]");
            authorize!.Roles.Should().NotBeNullOrWhiteSpace($"action {action.Name} must specify Roles");
        }
    }

    [Fact]
    public async Task CompleteCheckinAsync_WhenCallerNotInGuardsTable_ThrowsUnauthorizedAndDoesNotInsertGuard()
    {
        // Arrange
        await using var context = CreateInMemoryContext();
        var sut = new CheckinService(context);
        var dto = new CreateCheckinHandoverDto
        {
            StudentId = 10,
            RoomId = 1,
            BedId = 1,
            ResidencyId = 1
        };

        // Act
        var act = () => sut.CompleteCheckinAsync(guardUserId: 999, dto);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        (await context.Guards.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task CompleteCheckinAsync_WhenRoomOutsideGuardDorm_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        await using var context = CreateInMemoryContext();
        context.Dorms.AddRange(
            new Dorm { Id = 1, Code = "A", Name = "Dom A", Status = "Active" },
            new Dorm { Id = 2, Code = "B", Name = "Dom B", Status = "Active" });
        context.Blocks.Add(new Block { Id = 20, DormId = 2, Code = "B1", Name = "Block B1", GenderType = "Male", Status = "Active" });
        context.Rooms.Add(new Room { Id = 200, BlockId = 20, RoomTypeId = 1, RoomNumber = "B101", Status = "Active" });
        context.Beds.Add(new Bed { Id = 2000, RoomId = 200, BedCode = "B101-1", Status = "Reserved" });
        context.Guards.Add(new Guard { UserId = 50, DormId = 1 });
        context.Residencies.Add(new Residency
        {
            Id = 500,
            StudentId = 10,
            BedId = 2000,
            TermId = 1,
            Status = "Reserved"
        });
        await context.SaveChangesAsync();

        var sut = new CheckinService(context);
        var dto = new CreateCheckinHandoverDto
        {
            StudentId = 10,
            RoomId = 200,
            BedId = 2000,
            ResidencyId = 500
        };

        // Act
        var act = () => sut.CompleteCheckinAsync(guardUserId: 50, dto);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        (await context.Handovers.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task GetCheckinByIdAsync_WhenStudentRequestsOtherStudentHandover_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        await using var context = CreateInMemoryContext();
        context.Dorms.Add(new Dorm { Id = 1, Code = "A", Name = "Dom A", Status = "Active" });
        context.Blocks.Add(new Block { Id = 10, DormId = 1, Code = "A1", Name = "Block A1", GenderType = "Male", Status = "Active" });
        context.Rooms.Add(new Room { Id = 100, BlockId = 10, RoomTypeId = 1, RoomNumber = "A101", Status = "Active" });
        context.Beds.Add(new Bed { Id = 1000, RoomId = 100, BedCode = "A101-1", Status = "Occupied" });
        context.Terms.Add(new Term { Id = 1, Name = "Fall 2026", AcademicYear = "2026-2027", StartDate = new DateOnly(2026, 9, 1), EndDate = new DateOnly(2026, 12, 31) });
        context.Users.AddRange(
            new User { Id = 10, Code = "SE001", FullName = "Student A", Email = "a@fpt.edu.vn", Gender = "Male", AccountStatus = "Active" },
            new User { Id = 50, Code = "GD001", FullName = "Guard A", Email = "g@fpt.edu.vn", Gender = "Male", AccountStatus = "Active" });
        context.Students.Add(new Student { UserId = 10 });
        context.Guards.Add(new Guard { UserId = 50, DormId = 1 });
        context.Residencies.Add(new Residency { Id = 1, StudentId = 10, BedId = 1000, TermId = 1, Status = "Active" });
        context.Handovers.Add(new Handover
        {
            Id = 77,
            HandoverType = "Checkin",
            ResidencyId = 1,
            RoomId = 100,
            GuardId = 50,
            Status = "Completed",
            CreatedTime = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var sut = new CheckinService(context);

        // Act
        var act = () => sut.GetCheckinByIdAsync(77, callerUserId: 99, isManagerOrAdmin: false, isGuard: false);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task GetCheckinByIdAsync_WhenGuardRequestsOtherDormHandover_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        await using var context = CreateInMemoryContext();
        context.Dorms.AddRange(
            new Dorm { Id = 1, Code = "A", Name = "Dom A", Status = "Active" },
            new Dorm { Id = 2, Code = "B", Name = "Dom B", Status = "Active" });
        context.Blocks.Add(new Block { Id = 20, DormId = 2, Code = "B1", Name = "Block B1", GenderType = "Male", Status = "Active" });
        context.Rooms.Add(new Room { Id = 200, BlockId = 20, RoomTypeId = 1, RoomNumber = "B101", Status = "Active" });
        context.Beds.Add(new Bed { Id = 2000, RoomId = 200, BedCode = "B101-1", Status = "Occupied" });
        context.Terms.Add(new Term { Id = 1, Name = "Fall 2026", AcademicYear = "2026-2027", StartDate = new DateOnly(2026, 9, 1), EndDate = new DateOnly(2026, 12, 31) });
        context.Users.AddRange(
            new User { Id = 10, Code = "SE001", FullName = "Student B", Email = "b@fpt.edu.vn", Gender = "Male", AccountStatus = "Active" },
            new User { Id = 50, Code = "GD001", FullName = "Guard A", Email = "ga@fpt.edu.vn", Gender = "Male", AccountStatus = "Active" },
            new User { Id = 51, Code = "GD002", FullName = "Guard B", Email = "gb@fpt.edu.vn", Gender = "Male", AccountStatus = "Active" });
        context.Students.Add(new Student { UserId = 10 });
        context.Guards.AddRange(
            new Guard { UserId = 50, DormId = 1 },
            new Guard { UserId = 51, DormId = 2 });
        context.Residencies.Add(new Residency { Id = 1, StudentId = 10, BedId = 2000, TermId = 1, Status = "Active" });
        context.Handovers.Add(new Handover
        {
            Id = 88,
            HandoverType = "Checkin",
            ResidencyId = 1,
            RoomId = 200,
            GuardId = 51,
            Status = "Completed",
            CreatedTime = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var sut = new CheckinService(context);

        // Act
        var act = () => sut.GetCheckinByIdAsync(88, callerUserId: 50, isManagerOrAdmin: false, isGuard: true);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task GetPendingStudentsAsync_WhenGuardHasNoDorm_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        await using var context = CreateInMemoryContext();
        var sut = new CheckinService(context);

        // Act
        var act = () => sut.GetPendingStudentsAsync(guardUserId: 777);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task CompleteCheckinAsync_WhenStudentHasUnfinishedCheckoutOrTransferRequest_ThrowsInvalidOperationException()
    {
        // Arrange
        await using var context = CreateInMemoryContext();
        context.Dorms.Add(new Dorm { Id = 1, Code = "A", Name = "Dom A", Status = "Active" });
        context.Blocks.Add(new Block { Id = 10, DormId = 1, Code = "A1", Name = "Block A1", GenderType = "Male", Status = "Active" });
        context.Rooms.Add(new Room { Id = 100, BlockId = 10, RoomTypeId = 1, RoomNumber = "A101", Status = "Active" });
        context.Beds.Add(new Bed { Id = 1000, RoomId = 100, BedCode = "A101-1", Status = "Reserved" });
        context.Terms.Add(new Term { Id = 1, Name = "Fall 2026", AcademicYear = "2026-2027", StartDate = new DateOnly(2026, 9, 1), EndDate = new DateOnly(2026, 12, 31) });
        context.Users.AddRange(
            new User { Id = 10, Code = "SE001", FullName = "Student A", Email = "a@fpt.edu.vn", Gender = "Male", AccountStatus = "Active" },
            new User { Id = 50, Code = "GD001", FullName = "Guard A", Email = "g@fpt.edu.vn", Gender = "Male", AccountStatus = "Active" });
        context.Students.Add(new Student { UserId = 10 });
        context.Guards.Add(new Guard { UserId = 50, DormId = 1 });
        context.Residencies.Add(new Residency { Id = 500, StudentId = 10, BedId = 1000, TermId = 1, Status = "Reserved" });
        context.Requests.Add(new Request
        {
            Id = 88,
            StudentId = 10,
            TermId = 1,
            RequestType = "BedTransfer",
            Status = "Approved",
            CreatedTime = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var sut = new CheckinService(context);
        var dto = new CreateCheckinHandoverDto
        {
            StudentId = 10,
            RoomId = 100,
            BedId = 1000,
            ResidencyId = 500
        };

        // Act
        var act = () => sut.CompleteCheckinAsync(guardUserId: 50, dto);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Check-out*");
        (await context.Handovers.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task CompleteCheckinAsync_WhenStudentStillHasOtherActiveResidency_ThrowsInvalidOperationException()
    {
        // Arrange
        await using var context = CreateInMemoryContext();
        context.Dorms.AddRange(
            new Dorm { Id = 1, Code = "A", Name = "Dom A", Status = "Active" },
            new Dorm { Id = 2, Code = "B", Name = "Dom B", Status = "Active" });
        context.Blocks.AddRange(
            new Block { Id = 10, DormId = 1, Code = "A1", Name = "Block A1", GenderType = "Male", Status = "Active" },
            new Block { Id = 20, DormId = 2, Code = "B1", Name = "Block B1", GenderType = "Male", Status = "Active" });
        context.Rooms.AddRange(
            new Room { Id = 100, BlockId = 10, RoomTypeId = 1, RoomNumber = "A101", Status = "Active" },
            new Room { Id = 200, BlockId = 20, RoomTypeId = 1, RoomNumber = "B202", Status = "Active" });
        context.Beds.AddRange(
            new Bed { Id = 1000, RoomId = 100, BedCode = "A101-1", Status = "Occupied" },
            new Bed { Id = 2000, RoomId = 200, BedCode = "B202-1", Status = "Reserved" });
        context.Terms.AddRange(
            new Term { Id = 1, Name = "Fall 2026", AcademicYear = "2026-2027", StartDate = new DateOnly(2026, 9, 1), EndDate = new DateOnly(2026, 12, 31) },
            new Term { Id = 2, Name = "Spring 2027", AcademicYear = "2026-2027", StartDate = new DateOnly(2027, 1, 1), EndDate = new DateOnly(2027, 4, 30) });
        context.Users.AddRange(
            new User { Id = 10, Code = "SE001", FullName = "Student A", Email = "a@fpt.edu.vn", Gender = "Male", AccountStatus = "Active" },
            new User { Id = 51, Code = "GD002", FullName = "Guard B", Email = "gb@fpt.edu.vn", Gender = "Male", AccountStatus = "Active" });
        context.Students.Add(new Student { UserId = 10 });
        context.Guards.Add(new Guard { UserId = 51, DormId = 2 });
        context.Residencies.AddRange(
            new Residency { Id = 500, StudentId = 10, BedId = 1000, TermId = 1, Status = "Active" },
            new Residency { Id = 501, StudentId = 10, BedId = 2000, TermId = 2, Status = "Reserved", RegistrationType = "Booking" });
        await context.SaveChangesAsync();

        var sut = new CheckinService(context);
        var dto = new CreateCheckinHandoverDto
        {
            StudentId = 10,
            RoomId = 200,
            BedId = 2000,
            ResidencyId = 501
        };

        // Act
        var act = () => sut.CompleteCheckinAsync(guardUserId: 51, dto);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Check-out phòng cũ*");
        (await context.Handovers.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task CompleteCheckinAsync_WhenKeepingSameBedAcrossTerms_DoesNotBlockCheckin()
    {
        // Arrange
        await using var context = CreateInMemoryContext();
        context.Dorms.Add(new Dorm { Id = 1, Code = "A", Name = "Dom A", Status = "Active" });
        context.Blocks.Add(new Block { Id = 10, DormId = 1, Code = "A1", Name = "Block A1", GenderType = "Male", Status = "Active" });
        context.Rooms.Add(new Room { Id = 100, BlockId = 10, RoomTypeId = 1, RoomNumber = "A101", Status = "Active" });
        context.Beds.Add(new Bed { Id = 1000, RoomId = 100, BedCode = "A101-1", Status = "Occupied" });
        context.Terms.AddRange(
            new Term { Id = 1, Name = "Fall 2026", AcademicYear = "2026-2027", StartDate = new DateOnly(2026, 9, 1), EndDate = new DateOnly(2026, 12, 31) },
            new Term { Id = 2, Name = "Spring 2027", AcademicYear = "2026-2027", StartDate = new DateOnly(2027, 1, 1), EndDate = new DateOnly(2027, 4, 30) });
        context.Users.AddRange(
            new User { Id = 10, Code = "SE001", FullName = "Student A", Email = "a@fpt.edu.vn", Gender = "Male", AccountStatus = "Active" },
            new User { Id = 50, Code = "GD001", FullName = "Guard A", Email = "ga@fpt.edu.vn", Gender = "Male", AccountStatus = "Active" });
        context.Students.Add(new Student { UserId = 10 });
        context.Guards.Add(new Guard { UserId = 50, DormId = 1 });
        context.Residencies.AddRange(
            new Residency { Id = 500, StudentId = 10, BedId = 1000, TermId = 1, Status = "Active" },
            new Residency { Id = 501, StudentId = 10, BedId = 1000, TermId = 2, Status = "Reserved", RegistrationType = "Keeping" });
        await context.SaveChangesAsync();

        var sut = new CheckinService(context);
        var dto = new CreateCheckinHandoverDto
        {
            StudentId = 10,
            RoomId = 100,
            BedId = 1000,
            ResidencyId = 501
        };

        // Act
        var result = await sut.CompleteCheckinAsync(guardUserId: 50, dto);

        // Assert
        result.Should().NotBeNull();
        result.ResidencyId.Should().Be(501);
        result.Status.Should().Be("Completed");
    }

    [Fact]
    public async Task CompleteCheckinAsync_WhenTransferHandoverCompletedAndOldResidencyEnded_Succeeds()
    {
        // Arrange
        await using var context = CreateInMemoryContext();
        context.Dorms.AddRange(
            new Dorm { Id = 1, Code = "A", Name = "Dom A", Status = "Active" },
            new Dorm { Id = 2, Code = "B", Name = "Dom B", Status = "Active" });
        context.Blocks.AddRange(
            new Block { Id = 10, DormId = 1, Code = "A1", Name = "Block A1", GenderType = "Male", Status = "Active" },
            new Block { Id = 20, DormId = 2, Code = "B1", Name = "Block B1", GenderType = "Male", Status = "Active" });
        context.Rooms.AddRange(
            new Room { Id = 100, BlockId = 10, RoomTypeId = 1, RoomNumber = "A101", Status = "Active" },
            new Room { Id = 200, BlockId = 20, RoomTypeId = 1, RoomNumber = "B202", Status = "Active" });
        context.Beds.AddRange(
            new Bed { Id = 1000, RoomId = 100, BedCode = "A101-1", Status = "Free" },
            new Bed { Id = 2000, RoomId = 200, BedCode = "B202-1", Status = "Occupied" });
        context.Terms.Add(new Term { Id = 1, Name = "Fall 2026", AcademicYear = "2026-2027", StartDate = new DateOnly(2026, 9, 1), EndDate = new DateOnly(2026, 12, 31) });
        context.Users.AddRange(
            new User { Id = 10, Code = "SE001", FullName = "Student A", Email = "a@fpt.edu.vn", Gender = "Male", AccountStatus = "Active" },
            new User { Id = 50, Code = "GD001", FullName = "Guard A", Email = "ga@fpt.edu.vn", Gender = "Male", AccountStatus = "Active" },
            new User { Id = 51, Code = "GD002", FullName = "Guard B", Email = "gb@fpt.edu.vn", Gender = "Male", AccountStatus = "Active" });
        context.Students.Add(new Student { UserId = 10 });
        context.Guards.AddRange(
            new Guard { UserId = 50, DormId = 1 },
            new Guard { UserId = 51, DormId = 2 });
        context.Residencies.AddRange(
            new Residency { Id = 500, StudentId = 10, BedId = 1000, TermId = 1, Status = "Ended" },
            new Residency { Id = 501, StudentId = 10, BedId = 2000, TermId = 1, Status = "Active", RegistrationType = "Transfer" });
        context.Requests.Add(new Request
        {
            Id = 90,
            StudentId = 10,
            TermId = 1,
            RequestType = "BedTransfer",
            Status = "Completed",
            CreatedTime = DateTime.UtcNow
        });
        context.BedTransferRequests.Add(new BedTransferRequest
        {
            RequestId = 90,
            FromResidencyId = 500,
            NewResidencyId = 501,
            FromBedId = 1000,
            ToBedId = 2000,
            TransferReason = "Chuyển phòng"
        });
        context.Handovers.Add(new Handover
        {
            Id = 300,
            HandoverType = "BedTransfer",
            RequestId = 90,
            RoomId = 100,
            GuardId = 50,
            Status = "Completed",
            StudentAcceptedFlag = true,
            StudentAcceptedTime = DateTime.UtcNow,
            CreatedTime = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var sut = new CheckinService(context);
        var dto = new CreateCheckinHandoverDto
        {
            StudentId = 10,
            RoomId = 200,
            BedId = 2000,
            ResidencyId = 501
        };

        // Act
        var result = await sut.CompleteCheckinAsync(guardUserId: 51, dto);

        // Assert
        result.Should().NotBeNull();
        result.ResidencyId.Should().Be(501);
        result.Status.Should().Be("Completed");
    }

    [Fact]
    public async Task GetPendingStudentsAsync_MarksStudentBlockedWhenPendingCheckoutExists()
    {
        // Arrange
        await using var context = CreateInMemoryContext();
        context.Dorms.Add(new Dorm { Id = 1, Code = "A", Name = "Dom A", Status = "Active" });
        context.Blocks.Add(new Block { Id = 10, DormId = 1, Code = "A1", Name = "Block A1", GenderType = "Male", Status = "Active" });
        context.Rooms.Add(new Room { Id = 100, BlockId = 10, RoomTypeId = 1, RoomNumber = "A101", Status = "Active" });
        context.Beds.Add(new Bed { Id = 1000, RoomId = 100, BedCode = "A101-1", Status = "Reserved" });
        context.Terms.Add(new Term { Id = 1, Name = "Fall 2026", AcademicYear = "2026-2027", StartDate = new DateOnly(2026, 9, 1), EndDate = new DateOnly(2026, 12, 31) });
        context.Users.AddRange(
            new User { Id = 10, Code = "SE001", FullName = "Student A", Email = "a@fpt.edu.vn", Gender = "Male", AccountStatus = "Active" },
            new User { Id = 50, Code = "GD001", FullName = "Guard A", Email = "ga@fpt.edu.vn", Gender = "Male", AccountStatus = "Active" });
        context.Students.Add(new Student { UserId = 10 });
        context.Guards.Add(new Guard { UserId = 50, DormId = 1 });
        context.Residencies.Add(new Residency { Id = 500, StudentId = 10, BedId = 1000, TermId = 1, Status = "Reserved" });
        context.Requests.Add(new Request
        {
            Id = 91,
            StudentId = 10,
            TermId = 1,
            RequestType = "Checkout",
            Status = "Approved",
            CreatedTime = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var sut = new CheckinService(context);

        // Act
        var pending = await sut.GetPendingStudentsAsync(guardUserId: 50);

        // Assert
        pending.Should().ContainSingle();
        pending[0].CanCheckin.Should().BeFalse();
        pending[0].CheckinBlockedReason.Should().Contain("Check-out");
    }
}
