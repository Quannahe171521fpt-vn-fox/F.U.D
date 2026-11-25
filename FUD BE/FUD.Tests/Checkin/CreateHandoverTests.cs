using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using FluentAssertions;
using FUD.Common;
using FUD.Controllers.Checkin;
using FUD.DTOs.Checkin;
using FUD.Models;
using FUD.Services.Checkin;
using FUD.Services.Interfaces;
using FUD.Services.Interfaces.Checkin;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using Xunit;

namespace FUD.Tests.Checkin;

public class CreateHandoverTests
{
    private static FudContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<FudContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new FudContext(options);
    }

    private static async Task SeedDormRoomAndUsersAsync(FudContext context)
    {
        context.Dorms.AddRange(
            new Dorm { Id = 1, Code = "A", Name = "Dom A", Status = "Active" },
            new Dorm { Id = 2, Code = "B", Name = "Dom B", Status = "Active" });
        context.Blocks.AddRange(
            new Block { Id = 10, DormId = 1, Code = "A1", Name = "Block A1", GenderType = "Male", Status = "Active" },
            new Block { Id = 20, DormId = 2, Code = "B1", Name = "Block B1", GenderType = "Male", Status = "Active" });
        context.Rooms.AddRange(
            new Room { Id = 100, BlockId = 10, RoomTypeId = 1, RoomNumber = "A101", Status = "Active" },
            new Room { Id = 200, BlockId = 20, RoomTypeId = 1, RoomNumber = "B201", Status = "Active" });
        context.Beds.AddRange(
            new Bed { Id = 1000, RoomId = 100, BedCode = "A101-1", Status = "Reserved" },
            new Bed { Id = 1001, RoomId = 100, BedCode = "A101-2", Status = "Free" });
        context.Terms.Add(new Term
        {
            Id = 1,
            Name = "Fall 2026",
            AcademicYear = "2026-2027",
            StartDate = new DateOnly(2026, 9, 1),
            EndDate = new DateOnly(2026, 12, 31)
        });
        context.Users.AddRange(
            new User { Id = 10, Code = "SE001", FullName = "Nguyen Van A", Email = "a@fpt.edu.vn", Gender = "Male", AccountStatus = "Active" },
            new User { Id = 50, Code = "GD001", FullName = "Tran Bao Ve", Email = "g@fpt.edu.vn", Gender = "Male", AccountStatus = "Active" });
        context.Students.Add(new Student { UserId = 10 });
        context.Guards.Add(new Guard { UserId = 50, DormId = 1 });
        context.EquipmentCatalogs.AddRange(
            new EquipmentCatalog { Id = 1, Category = "Điện lạnh", Name = "Điều hòa" },
            new EquipmentCatalog { Id = 2, Category = "Nội thất", Name = "Bàn học" });
        await context.SaveChangesAsync();
    }

    private static CheckinController CreateController(Mock<ICheckinService> serviceMock, params Claim[] claims)
    {
        var identity = claims.Length > 0 ? new ClaimsIdentity(claims, "TestAuth") : new ClaimsIdentity();
        return new CheckinController(serviceMock.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(identity)
                }
            }
        };
    }

    // ==================== SERVICE: GetRoomEquipmentsAsync ====================

    [Fact]
    public async Task GetRoomEquipmentsAsync_ReturnsCommonAndTargetBedEquipments_ExcludesOtherBedsAndLiquidated()
    {
        await using var context = CreateInMemoryContext();
        await SeedDormRoomAndUsersAsync(context);

        context.Equipments.AddRange(
            new Equipment { Id = 1, RoomId = 100, BedId = null, CatalogId = 1, AssetCode = "AC-01", Condition = "Good" },
            new Equipment { Id = 2, RoomId = 100, BedId = 1000, CatalogId = 2, AssetCode = "DESK-01", Condition = "Good" },
            new Equipment { Id = 3, RoomId = 100, BedId = 1001, CatalogId = 2, AssetCode = "DESK-02", Condition = "Good" },
            new Equipment { Id = 4, RoomId = 100, BedId = null, CatalogId = 1, AssetCode = "AC-OLD", Condition = "Liquidated" });
        await context.SaveChangesAsync();

        var sut = new CheckinService(context);
        var result = await sut.GetRoomEquipmentsAsync(roomId: 100, bedId: 1000, guardUserId: 50);

        result.Should().HaveCount(2);
        result.Select(e => e.AssetCode).Should().BeEquivalentTo(new[] { "AC-01", "DESK-01" });
    }

    [Fact]
    public async Task GetRoomEquipmentsAsync_MapsBrokenAndUnderRepairToDamagedWithDefaultNote()
    {
        await using var context = CreateInMemoryContext();
        await SeedDormRoomAndUsersAsync(context);

        context.Equipments.AddRange(
            new Equipment { Id = 1, RoomId = 100, BedId = null, CatalogId = 1, AssetCode = "AC-01", Condition = "Broken", Note = null },
            new Equipment { Id = 2, RoomId = 100, BedId = 1000, CatalogId = 2, AssetCode = "DESK-01", Condition = "Under repair", Note = "" });
        await context.SaveChangesAsync();

        var sut = new CheckinService(context);
        var result = await sut.GetRoomEquipmentsAsync(roomId: 100, bedId: 1000, guardUserId: 50);

        result.Should().HaveCount(2);
        var broken = result.Single(e => e.AssetCode == "AC-01");
        var repairing = result.Single(e => e.AssetCode == "DESK-01");
        broken.CurrentCondition.Should().Be("Damaged");
        broken.Note.Should().Be("Hư hỏng");
        repairing.CurrentCondition.Should().Be("Damaged");
        repairing.Note.Should().Be("Đang sửa chữa");
    }

    [Fact]
    public async Task GetRoomEquipmentsAsync_WhenGuardRequestsRoomInOtherDorm_ThrowsUnauthorizedAccessException()
    {
        await using var context = CreateInMemoryContext();
        await SeedDormRoomAndUsersAsync(context);

        var sut = new CheckinService(context);
        var act = () => sut.GetRoomEquipmentsAsync(roomId: 200, bedId: null, guardUserId: 50);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*khu KTX mình phụ trách*");
    }

    // ==================== SERVICE: CompleteCheckinAsync (Create Handover) ====================

    [Theory]
    [InlineData(0, 100, 1000)]
    [InlineData(10, 0, 1000)]
    [InlineData(10, 100, 0)]
    public async Task CompleteCheckinAsync_WhenInvalidIds_ThrowsArgumentException(int studentId, int roomId, int bedId)
    {
        await using var context = CreateInMemoryContext();
        var sut = new CheckinService(context);
        var dto = new CreateCheckinHandoverDto
        {
            StudentId = studentId,
            RoomId = roomId,
            BedId = bedId
        };

        var act = () => sut.CompleteCheckinAsync(guardUserId: 50, dto);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*không hợp lệ*");
    }

    [Fact]
    public async Task CompleteCheckinAsync_WhenNoResidencyAndNoBooking_ThrowsArgumentException()
    {
        await using var context = CreateInMemoryContext();
        await SeedDormRoomAndUsersAsync(context);

        var sut = new CheckinService(context);
        var dto = new CreateCheckinHandoverDto
        {
            StudentId = 10,
            RoomId = 100,
            BedId = 1000,
            ResidencyId = null,
            BookingRequestId = null
        };

        var act = () => sut.CompleteCheckinAsync(guardUserId: 50, dto);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*chờ Check-in*");
    }

    [Fact]
    public async Task CompleteCheckinAsync_WhenBookingBelongsToAnotherStudent_ThrowsArgumentException()
    {
        await using var context = CreateInMemoryContext();
        await SeedDormRoomAndUsersAsync(context);

        context.BookingRequests.Add(new BookingRequest
        {
            Id = 200,
            StudentId = 99,
            TermId = 1,
            BedId = 1000,
            BookingKind = "Booking",
            Status = "Approved",
            PaymentDeadline = DateTime.UtcNow.AddDays(3),
            CreatedTime = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var sut = new CheckinService(context);
        var dto = new CreateCheckinHandoverDto
        {
            StudentId = 10,
            RoomId = 100,
            BedId = 1000,
            BookingRequestId = 200
        };

        var act = () => sut.CompleteCheckinAsync(guardUserId: 50, dto);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Không tìm thấy đơn đặt phòng hợp lệ*");
    }

    [Fact]
    public async Task CompleteCheckinAsync_WhenHandoverAlreadyExists_ThrowsInvalidOperationException()
    {
        await using var context = CreateInMemoryContext();
        await SeedDormRoomAndUsersAsync(context);

        context.Residencies.Add(new Residency { Id = 500, StudentId = 10, BedId = 1000, TermId = 1, Status = "Active" });
        context.Handovers.Add(new Handover
        {
            Id = 300,
            HandoverType = "Checkin",
            ResidencyId = 500,
            RoomId = 100,
            GuardId = 50,
            Status = "Completed",
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

        var act = () => sut.CompleteCheckinAsync(guardUserId: 50, dto);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*đã được lập biên bản bàn giao*");
    }

    [Fact]
    public async Task CompleteCheckinAsync_ValidResidencyWithEquipments_CreatesHandoverItemsAuditLogAndUpdatesStatus()
    {
        await using var context = CreateInMemoryContext();
        await SeedDormRoomAndUsersAsync(context);

        context.Residencies.Add(new Residency { Id = 500, StudentId = 10, BedId = 1000, TermId = 1, Status = "Reserved", RegistrationType = "Booking" });
        context.Equipments.AddRange(
            new Equipment { Id = 1, RoomId = 100, BedId = null, CatalogId = 1, AssetCode = "AC-01", Condition = "Good" },
            new Equipment { Id = 2, RoomId = 100, BedId = 1000, CatalogId = 2, AssetCode = "DESK-01", Condition = "Good" },
            new Equipment { Id = 3, RoomId = 100, BedId = 1001, CatalogId = 2, AssetCode = "DESK-02", Condition = "Good" });
        await context.SaveChangesAsync();

        var sut = new CheckinService(context);
        var dto = new CreateCheckinHandoverDto
        {
            StudentId = 10,
            RoomId = 100,
            BedId = 1000,
            ResidencyId = 500,
            StudentTitle = "Trưởng phòng",
            OtherItems = "1 chìa khóa phòng",
            ElectricityL = "1200",
            WaterL = "45",
            Items = new List<CheckinItemInputDto>
            {
                new() { EquipmentId = 1, ItemCondition = "Good", Note = "Hoạt động tốt" },
                new() { EquipmentId = 2, ItemCondition = "Good", Note = "Mới" }
            }
        };

        var result = await sut.CompleteCheckinAsync(guardUserId: 50, dto);

        result.Should().NotBeNull();
        result.Status.Should().Be("Completed");
        result.WorkflowStage.Should().Be("PendingManagerCheckRecord");
        result.StudentAcceptedFlag.Should().BeTrue();
        result.HasDamageOrMissingFlag.Should().BeFalse();
        result.StudentTitle.Should().Be("Trưởng phòng");
        result.OtherItems.Should().Be("1 chìa khóa phòng");
        result.ElectricityL.Should().Be("1200");
        result.WaterL.Should().Be("45");
        result.Items.Should().HaveCount(2);

        var residency = await context.Residencies.FindAsync(500);
        var bed = await context.Beds.FindAsync(1000);
        residency!.Status.Should().Be("Active");
        residency.CheckInDate.Should().NotBeNull();
        bed!.Status.Should().Be("Occupied");

        var audit = await context.AuditLogs.SingleAsync();
        audit.ActionType.Should().Be("CHECKIN_HANDOVER");
        audit.UserId.Should().Be(50);
    }

    [Fact]
    public async Task CompleteCheckinAsync_WhenItemMarkedDamaged_UpdatesEquipmentToBrokenAndSetsDamageFlag()
    {
        await using var context = CreateInMemoryContext();
        await SeedDormRoomAndUsersAsync(context);

        context.Residencies.Add(new Residency { Id = 500, StudentId = 10, BedId = 1000, TermId = 1, Status = "Reserved" });
        context.Equipments.Add(new Equipment { Id = 2, RoomId = 100, BedId = 1000, CatalogId = 2, AssetCode = "DESK-01", Condition = "Good" });
        await context.SaveChangesAsync();

        var sut = new CheckinService(context);
        var dto = new CreateCheckinHandoverDto
        {
            StudentId = 10,
            RoomId = 100,
            BedId = 1000,
            ResidencyId = 500,
            Items = new List<CheckinItemInputDto>
            {
                new() { EquipmentId = 2, ItemCondition = "Damaged", Note = "Trầy xước mặt bàn" }
            }
        };

        var result = await sut.CompleteCheckinAsync(guardUserId: 50, dto);

        result.Status.Should().Be("Draft");
        result.WorkflowStage.Should().Be("PendingManagerTroubleshooting");
        result.HasDamageOrMissingFlag.Should().BeTrue();
        result.Items.Should().ContainSingle(i => i.EquipmentId == 2 && i.ItemCondition == "Damaged");
        (await context.Equipments.FindAsync(2))!.Condition.Should().Be("Broken");
        (await context.Residencies.FindAsync(500))!.Status.Should().Be("Reserved");
        (await context.Beds.FindAsync(1000))!.Status.Should().Be("Reserved");
    }

    [Fact]
    public async Task CompleteCheckinAsync_WithUnknownItemCondition_NormalizesConditionToGood()
    {
        await using var context = CreateInMemoryContext();
        await SeedDormRoomAndUsersAsync(context);

        context.Residencies.Add(new Residency { Id = 500, StudentId = 10, BedId = 1000, TermId = 1, Status = "Reserved" });
        context.Equipments.Add(new Equipment { Id = 2, RoomId = 100, BedId = 1000, CatalogId = 2, AssetCode = "DESK-01", Condition = "Good" });
        await context.SaveChangesAsync();

        var sut = new CheckinService(context);
        var dto = new CreateCheckinHandoverDto
        {
            StudentId = 10,
            RoomId = 100,
            BedId = 1000,
            ResidencyId = 500,
            Items = new List<CheckinItemInputDto>
            {
                new() { EquipmentId = 2, ItemCondition = "InvalidCondition" }
            }
        };

        var result = await sut.CompleteCheckinAsync(guardUserId: 50, dto);

        result.HasDamageOrMissingFlag.Should().BeFalse();
        result.Items.Should().ContainSingle(i => i.EquipmentId == 2 && i.ItemCondition == "Good");
    }

    [Fact]
    public async Task CompleteCheckinAsync_FromApprovedBookingWithoutResidency_CreatesResidencyMarksBookingCompletedAndCreatesHandover()
    {
        await using var context = CreateInMemoryContext();
        await SeedDormRoomAndUsersAsync(context);

        context.BookingRequests.Add(new BookingRequest
        {
            Id = 200,
            StudentId = 10,
            TermId = 1,
            BedId = 1000,
            BookingKind = "Booking",
            Status = "Approved",
            PaymentDeadline = DateTime.UtcNow.AddDays(3),
            CreatedTime = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var sut = new CheckinService(context);
        var dto = new CreateCheckinHandoverDto
        {
            StudentId = 10,
            RoomId = 100,
            BedId = 1000,
            BookingRequestId = 200
        };

        var result = await sut.CompleteCheckinAsync(guardUserId: 50, dto);

        result.Should().NotBeNull();
        result.Status.Should().Be("Completed");
        (await context.Residencies.CountAsync()).Should().Be(1);
        (await context.BookingRequests.FindAsync(200))!.Status.Should().Be("Completed");
    }

    [Fact]
    public async Task CompleteCheckinAsync_WhenNotificationServiceProvided_NotifiesStudent()
    {
        await using var context = CreateInMemoryContext();
        await SeedDormRoomAndUsersAsync(context);

        context.Residencies.Add(new Residency { Id = 500, StudentId = 10, BedId = 1000, TermId = 1, Status = "Reserved" });
        await context.SaveChangesAsync();

        var notiMock = new Mock<IInAppNotificationService>();
        var sut = new CheckinService(context, notiMock.Object);
        var dto = new CreateCheckinHandoverDto
        {
            StudentId = 10,
            RoomId = 100,
            BedId = 1000,
            ResidencyId = 500
        };

        var result = await sut.CompleteCheckinAsync(guardUserId: 50, dto);

        notiMock.Verify(
            n => n.NotifyUserAsync(
                10,
                It.Is<string>(t => t.Contains("Check-in")),
                It.IsAny<string?>(),
                "Checkin",
                result.Id.ToString(),
                false),
            Times.Once);
    }

    // ==================== SERVICE: Troubleshooting, GuardConfirm & ManagerCheck ====================

    [Fact]
    public async Task TroubleshootByManagerAsync_WhenDraft_TransitionsToPendingGuardConfirmAndNotifiesGuard()
    {
        await using var context = CreateInMemoryContext();
        await SeedDormRoomAndUsersAsync(context);

        context.Users.Add(new User { Id = 5, Code = "MG001", FullName = "Le Quan Ly", Email = "m@fpt.edu.vn", Gender = "Male", AccountStatus = "Active" });
        context.Residencies.Add(new Residency { Id = 500, StudentId = 10, BedId = 1000, TermId = 1, Status = "Reserved" });
        context.Equipments.Add(new Equipment { Id = 2, RoomId = 100, BedId = 1000, CatalogId = 2, AssetCode = "DESK-01", Condition = "Broken" });
        context.Handovers.Add(new Handover
        {
            Id = 300,
            HandoverType = "Checkin",
            ResidencyId = 500,
            RoomId = 100,
            GuardId = 50,
            Status = "Draft",
            HasDamageOrMissingFlag = true,
            StudentAcceptedFlag = false,
            CreatedTime = DateTime.UtcNow,
            HandoverItems = new List<HandoverItem>
            {
                new() { EquipmentId = 2, ItemCondition = "Damaged", Note = "Gãy chân bàn", CompensationAmount = 0 }
            }
        });
        await context.SaveChangesAsync();

        var notiMock = new Mock<IInAppNotificationService>();
        var sut = new CheckinService(context, notiMock.Object);

        var result = await sut.TroubleshootByManagerAsync(managerUserId: 5, handoverId: 300, new ManagerTroubleshootCheckinDto
        {
            Note = "Đã thay bàn mới",
            RepairDamagedEquipments = true
        });

        result.Status.Should().Be("Pending Acceptance");
        result.WorkflowStage.Should().Be("PendingGuardConfirm");
        result.Items.Should().ContainSingle(i => i.EquipmentId == 2 && i.ItemCondition == "Good");
        (await context.Equipments.FindAsync(2))!.Condition.Should().Be("Good");

        notiMock.Verify(
            n => n.NotifyUserAsync(
                50,
                It.Is<string>(t => t.Contains("Check-in")),
                It.IsAny<string?>(),
                "Checkin",
                "300",
                false),
            Times.Once);
    }

    [Fact]
    public async Task GuardConfirmCheckinAsync_WhenDraft_ThrowsInvalidOperationException()
    {
        await using var context = CreateInMemoryContext();
        await SeedDormRoomAndUsersAsync(context);

        context.Residencies.Add(new Residency { Id = 500, StudentId = 10, BedId = 1000, TermId = 1, Status = "Reserved" });
        context.Handovers.Add(new Handover
        {
            Id = 301,
            HandoverType = "Checkin",
            ResidencyId = 500,
            RoomId = 100,
            GuardId = 50,
            Status = "Draft",
            HasDamageOrMissingFlag = true,
            StudentAcceptedFlag = false,
            CreatedTime = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var sut = new CheckinService(context);
        var act = () => sut.GuardConfirmCheckinAsync(guardUserId: 50, handoverId: 301);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Troubleshooting*");
    }

    [Fact]
    public async Task GuardConfirmCheckinAsync_WhenPendingAcceptanceWithoutStudentSign_CompletesHandoverAndOccupiesBed()
    {
        await using var context = CreateInMemoryContext();
        await SeedDormRoomAndUsersAsync(context);

        context.BookingRequests.Add(new BookingRequest
        {
            Id = 200,
            StudentId = 10,
            TermId = 1,
            BedId = 1000,
            BookingKind = "Booking",
            Status = "Approved",
            PaymentDeadline = DateTime.UtcNow.AddDays(3),
            CreatedTime = DateTime.UtcNow
        });
        context.Residencies.Add(new Residency
        {
            Id = 500,
            StudentId = 10,
            BedId = 1000,
            TermId = 1,
            SourceBookingId = 200,
            Status = "Reserved"
        });
        context.Handovers.Add(new Handover
        {
            Id = 302,
            HandoverType = "Checkin",
            ResidencyId = 500,
            RoomId = 100,
            GuardId = 50,
            Status = "Pending Acceptance",
            StudentAcceptedFlag = false,
            CreatedTime = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var sut = new CheckinService(context);
        var result = await sut.GuardConfirmCheckinAsync(guardUserId: 50, handoverId: 302);

        result.Status.Should().Be("Completed");
        result.WorkflowStage.Should().Be("PendingManagerCheckRecord");
        result.StudentAcceptedFlag.Should().BeTrue();
        result.CompletedTime.Should().NotBeNull();
        (await context.Residencies.FindAsync(500))!.Status.Should().Be("Active");
        (await context.Beds.FindAsync(1000))!.Status.Should().Be("Occupied");
        (await context.BookingRequests.FindAsync(200))!.Status.Should().Be("Completed");
    }

    [Fact]
    public async Task ManagerCheckRecordAsync_WhenCompleted_MarksWorkflowStageCompleted()
    {
        await using var context = CreateInMemoryContext();
        await SeedDormRoomAndUsersAsync(context);

        context.Users.Add(new User { Id = 5, Code = "MG001", FullName = "Le Quan Ly", Email = "m@fpt.edu.vn", Gender = "Male", AccountStatus = "Active" });
        context.Residencies.Add(new Residency { Id = 500, StudentId = 10, BedId = 1000, TermId = 1, Status = "Active" });
        context.Handovers.Add(new Handover
        {
            Id = 303,
            HandoverType = "Checkin",
            ResidencyId = 500,
            RoomId = 100,
            GuardId = 50,
            Status = "Completed",
            StudentAcceptedFlag = true,
            CreatedTime = DateTime.UtcNow,
            CompletedTime = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var sut = new CheckinService(context);
        var result = await sut.ManagerCheckRecordAsync(managerUserId: 5, handoverId: 303, new ManagerCheckRecordDto { Note = "Hồ sơ hợp lệ" });

        result.Status.Should().Be("Completed");
        result.WorkflowStage.Should().Be("Completed");
        result.ManagerReviewedTime.Should().NotBeNull();
    }

    // ==================== CONTROLLER: CompleteCheckin & GetRoomEquipments ====================

    [Fact]
    public async Task CompleteCheckin_ValidRequest_Returns200OkWithHandoverAndMessage()
    {
        var serviceMock = new Mock<ICheckinService>();
        var dto = new CreateCheckinHandoverDto { StudentId = 10, RoomId = 100, BedId = 1000, ResidencyId = 500 };
        var response = new CheckinHandoverResponseDto { Id = 301, ResidencyId = 500, Status = "Completed" };

        serviceMock.Setup(s => s.CompleteCheckinAsync(50, dto)).ReturnsAsync(response);
        var controller = CreateController(serviceMock, new Claim(ClaimTypes.NameIdentifier, "50"), new Claim(ClaimTypes.Role, "Guard"));

        var result = await controller.CompleteCheckin(dto);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.StatusCode.Should().Be(StatusCodes.Status200OK);
        var api = ok.Value.Should().BeOfType<ApiResponse<CheckinHandoverResponseDto>>().Subject;
        api.Success.Should().BeTrue();
        api.Message.Should().Contain("bàn giao tài sản Check-in");
        api.Data!.Id.Should().Be(301);
    }

    [Fact]
    public async Task CompleteCheckin_WhenMissingGuardClaim_Returns401Unauthorized()
    {
        var serviceMock = new Mock<ICheckinService>();
        var controller = CreateController(serviceMock);

        var result = await controller.CompleteCheckin(new CreateCheckinHandoverDto());

        var unauthorized = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        unauthorized.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        serviceMock.Verify(s => s.CompleteCheckinAsync(It.IsAny<int>(), It.IsAny<CreateCheckinHandoverDto>()), Times.Never);
    }

    [Fact]
    public async Task CompleteCheckin_WhenServiceThrowsUnauthorizedAccessException_Returns403Forbidden()
    {
        var serviceMock = new Mock<ICheckinService>();
        var dto = new CreateCheckinHandoverDto { StudentId = 10, RoomId = 200, BedId = 2000 };
        serviceMock.Setup(s => s.CompleteCheckinAsync(50, dto))
            .ThrowsAsync(new UnauthorizedAccessException("Ngoài khu KTX phụ trách."));

        var controller = CreateController(serviceMock, new Claim(ClaimTypes.NameIdentifier, "50"));

        var result = await controller.CompleteCheckin(dto);

        var obj = result.Should().BeOfType<ObjectResult>().Subject;
        obj.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task CompleteCheckin_WhenServiceThrowsArgumentException_Returns400BadRequest()
    {
        var serviceMock = new Mock<ICheckinService>();
        var dto = new CreateCheckinHandoverDto();
        serviceMock.Setup(s => s.CompleteCheckinAsync(50, dto))
            .ThrowsAsync(new ArgumentException("Thông tin không hợp lệ."));

        var controller = CreateController(serviceMock, new Claim(ClaimTypes.NameIdentifier, "50"));

        var result = await controller.CompleteCheckin(dto);

        var badReq = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badReq.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task CompleteCheckin_WhenServiceThrowsInvalidOperationException_Returns409Conflict()
    {
        var serviceMock = new Mock<ICheckinService>();
        var dto = new CreateCheckinHandoverDto { StudentId = 10, RoomId = 100, BedId = 1000, ResidencyId = 500 };
        serviceMock.Setup(s => s.CompleteCheckinAsync(50, dto))
            .ThrowsAsync(new InvalidOperationException("Sinh viên này đã được lập biên bản bàn giao Check-in trước đó."));

        var controller = CreateController(serviceMock, new Claim(ClaimTypes.NameIdentifier, "50"));

        var result = await controller.CompleteCheckin(dto);

        var conflict = result.Should().BeOfType<ConflictObjectResult>().Subject;
        conflict.StatusCode.Should().Be(StatusCodes.Status409Conflict);
    }

    [Fact]
    public async Task CompleteCheckin_WhenServiceThrowsKeyNotFoundException_Returns404NotFound()
    {
        var serviceMock = new Mock<ICheckinService>();
        var dto = new CreateCheckinHandoverDto { StudentId = 10, RoomId = 100, BedId = 1000 };
        serviceMock.Setup(s => s.CompleteCheckinAsync(50, dto))
            .ThrowsAsync(new KeyNotFoundException("Không tìm thấy dữ liệu."));

        var controller = CreateController(serviceMock, new Claim(ClaimTypes.NameIdentifier, "50"));

        var result = await controller.CompleteCheckin(dto);

        var notFound = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFound.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task GetRoomEquipments_AsGuardOnly_PassesGuardUserIdAndReturns200Ok()
    {
        var serviceMock = new Mock<ICheckinService>();
        var list = new List<RoomEquipmentCheckinDto> { new() { EquipmentId = 1, AssetCode = "AC-01" } };
        serviceMock.Setup(s => s.GetRoomEquipmentsAsync(100, 1000, 50)).ReturnsAsync(list);

        var controller = CreateController(
            serviceMock,
            new Claim(ClaimTypes.NameIdentifier, "50"),
            new Claim(ClaimTypes.Role, "Guard"));

        var result = await controller.GetRoomEquipments(roomId: 100, bedId: 1000);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.StatusCode.Should().Be(StatusCodes.Status200OK);
        serviceMock.Verify(s => s.GetRoomEquipmentsAsync(100, 1000, 50), Times.Once);
    }

    [Fact]
    public async Task GetRoomEquipments_AsManager_PassesNullGuardUserIdAndReturns200Ok()
    {
        var serviceMock = new Mock<ICheckinService>();
        serviceMock.Setup(s => s.GetRoomEquipmentsAsync(100, null, null))
            .ReturnsAsync(new List<RoomEquipmentCheckinDto>());

        var controller = CreateController(
            serviceMock,
            new Claim(ClaimTypes.NameIdentifier, "5"),
            new Claim(ClaimTypes.Role, "Manager"));

        var result = await controller.GetRoomEquipments(roomId: 100, bedId: null);

        result.Should().BeOfType<OkObjectResult>();
        serviceMock.Verify(s => s.GetRoomEquipmentsAsync(100, null, null), Times.Once);
    }
}
