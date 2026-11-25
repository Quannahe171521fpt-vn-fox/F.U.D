using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using FUD.DTOs.Requests;
using FUD.Infrastructure.Email;
using FUD.Infrastructure.Storage;
using FUD.Models;
using FUD.Repositories.Interfaces.Requests;
using FUD.Repositories.Interfaces.Violations;
using FUD.Services.Interfaces;
using FUD.Services.Requests;
using Moq;
using Xunit;

namespace FUD.Tests.Requests;

public class CheckoutRequestServiceTests
{
    private readonly Mock<IRequestRepository> _requestRepoMock = new();
    private readonly Mock<IViolationRepository> _violationRepoMock = new();
    private readonly Mock<IFileStorage> _fileStorageMock = new();
    private readonly Mock<IEmailSender> _emailSenderMock = new();
    private readonly Mock<IInAppNotificationService> _notificationMock = new();

    private RequestService CreateSut() => new(
        _requestRepoMock.Object,
        _violationRepoMock.Object,
        _fileStorageMock.Object,
        _emailSenderMock.Object,
        _notificationMock.Object);

    private static DateOnly TodayVn => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));

    private static (User studentUser, Residency residency, Term term, Bed bed, Room room, Block block, Dorm dorm) BuildActiveStay(
        int studentId = 10,
        int daysUntilTermEnd = 60,
        int dormId = 1)
    {
        var dorm = new Dorm { Id = dormId, Code = $"D{dormId}", Name = $"Dorm {dormId}", Status = "Active" };
        var block = new Block { Id = 10, DormId = dormId, Code = "A1", Name = "Block A1", GenderType = "Male", Status = "Active", Dorm = dorm };
        var roomType = new RoomType { Id = 1, Name = "4 Beds", BedCapacity = 4 };
        var room = new Room { Id = 100, BlockId = 10, RoomTypeId = 1, RoomNumber = "A101", Status = "Active", Block = block, RoomType = roomType };
        var bed = new Bed { Id = 1000, RoomId = 100, BedCode = "A101-1", Status = "Occupied", Room = room };
        var term = new Term
        {
            Id = 2,
            Name = "Fall 2026",
            AcademicYear = "2026-2027",
            StartDate = TodayVn.AddDays(-30),
            EndDate = TodayVn.AddDays(daysUntilTermEnd),
            UtilityPrices = new List<UtilityPrice>
            {
                new() { MeterType = "Electricity", UnitPrice = 3000m },
                new() { MeterType = "Water", UnitPrice = 15000m }
            }
        };
        var residency = new Residency
        {
            Id = 500,
            StudentId = studentId,
            BedId = bed.Id,
            TermId = term.Id,
            Status = "Active",
            CheckInDate = TodayVn.AddDays(-20),
            Bed = bed,
            Term = term
        };
        bed.Residencies = new List<Residency> { residency };
        room.Beds = new List<Bed> { bed };

        var studentUser = new User
        {
            Id = studentId,
            Code = "SE170001",
            FullName = "Nguyen Van A",
            Email = "a@fpt.edu.vn",
            Gender = "Male",
            AccountStatus = "Active"
        };

        return (studentUser, residency, term, bed, room, block, dorm);
    }

    // ==================== GetCheckoutContextAsync ====================

    [Fact]
    public async Task GetCheckoutContextAsync_WhenActiveStayWithOverdueInvoices_ReturnsEligibleTrueWithOverdueFlag()
    {
        var sut = CreateSut();
        var (studentUser, residency, _, _, _, _, _) = BuildActiveStay(daysUntilTermEnd: 60);

        _requestRepoMock.Setup(r => r.GetUserByIdAsync(10)).ReturnsAsync(studentUser);
        _requestRepoMock.Setup(r => r.GetStudentActiveResidencyAsync(10)).ReturnsAsync(residency);
        _requestRepoMock.Setup(r => r.HasOverdueInvoicesAsync(10)).ReturnsAsync(true);
        _requestRepoMock.Setup(r => r.HasPendingCheckoutAsync(10, residency.TermId)).ReturnsAsync(false);

        var ctx = await sut.GetCheckoutContextAsync(10);

        ctx.Eligible.Should().BeTrue();
        ctx.HasOverdueInvoices.Should().BeTrue();
        ctx.CurrentResidency.Should().NotBeNull();
        ctx.CurrentResidency!.RoomNumber.Should().Be("A101");
    }

    [Fact]
    public async Task GetCheckoutContextAsync_WhenLessThan21DaysLeftInTerm_ReturnsEligibleFalse()
    {
        var sut = CreateSut();
        var (studentUser, residency, _, _, _, _, _) = BuildActiveStay(daysUntilTermEnd: 18);

        _requestRepoMock.Setup(r => r.GetUserByIdAsync(10)).ReturnsAsync(studentUser);
        _requestRepoMock.Setup(r => r.GetStudentActiveResidencyAsync(10)).ReturnsAsync(residency);
        _requestRepoMock.Setup(r => r.HasOverdueInvoicesAsync(10)).ReturnsAsync(false);
        _requestRepoMock.Setup(r => r.HasPendingCheckoutAsync(10, residency.TermId)).ReturnsAsync(false);

        var ctx = await sut.GetCheckoutContextAsync(10);

        ctx.Eligible.Should().BeFalse();
        ctx.IneligibleReason.Should().Contain("2 tuần");
    }

    // ==================== CreateRequestAsync (Checkout) ====================

    [Fact]
    public async Task CreateRequestAsync_ValidCheckoutRequest_CreatesRequestCheckoutAndLog()
    {
        var sut = CreateSut();
        var (studentUser, residency, _, _, _, _, _) = BuildActiveStay(daysUntilTermEnd: 60);

        _requestRepoMock.Setup(r => r.GetUserByIdAsync(10)).ReturnsAsync(studentUser);
        _requestRepoMock.Setup(r => r.GetStudentActiveResidencyAsync(10)).ReturnsAsync(residency);
        _requestRepoMock.Setup(r => r.HasPendingCheckoutAsync(10, residency.TermId)).ReturnsAsync(false);
        _requestRepoMock.Setup(r => r.CreateCheckoutRequestAsync(It.IsAny<Request>(), It.IsAny<CheckoutRequest>(), It.IsAny<RequestLog>()))
            .ReturnsAsync((Request req, CheckoutRequest co, RequestLog log) =>
            {
                req.Id = 91;
                co.RequestId = 91;
                return req;
            });

        var expectedDate = TodayVn.AddDays(10);
        _requestRepoMock.Setup(r => r.GetRequestByIdAsync(91)).ReturnsAsync(new Request
        {
            Id = 91,
            StudentId = 10,
            TermId = residency.TermId,
            RequestType = "Checkout",
            Status = "Pending",
            Reason = "Chuyển ra ở trọ ngoài",
            CreatedTime = DateTime.UtcNow,
            CheckoutRequest = new CheckoutRequest
            {
                RequestId = 91,
                ResidencyId = residency.Id,
                ExpectedCheckOutDate = expectedDate,
                Residency = residency
            }
        });

        var dto = new CreateRequestDto
        {
            RequestType = "Checkout",
            ExpectedCheckOutDate = expectedDate,
            Reason = "Chuyển ra ở trọ ngoài"
        };

        var result = await sut.CreateRequestAsync(10, dto);

        result.Id.Should().Be(91);
        result.RequestType.Should().Be("Checkout");
        result.Status.Should().Be("Pending");
        result.Checkout.Should().NotBeNull();
        result.Checkout!.ExpectedCheckOutDate.Should().Be(expectedDate);

        _requestRepoMock.Verify(r => r.CreateCheckoutRequestAsync(
            It.Is<Request>(q => q.RequestType == "Checkout" && q.Status == "Pending"),
            It.Is<CheckoutRequest>(c => c.ResidencyId == residency.Id && c.ExpectedCheckOutDate == expectedDate),
            It.Is<RequestLog>(l => l.ToStatus == "Pending")), Times.Once);
    }

    [Fact]
    public async Task CreateRequestAsync_CheckoutDateBefore7Days_ThrowsInvalidOperationException()
    {
        var sut = CreateSut();
        var (studentUser, residency, _, _, _, _, _) = BuildActiveStay(daysUntilTermEnd: 60);

        _requestRepoMock.Setup(r => r.GetUserByIdAsync(10)).ReturnsAsync(studentUser);
        _requestRepoMock.Setup(r => r.GetStudentActiveResidencyAsync(10)).ReturnsAsync(residency);
        _requestRepoMock.Setup(r => r.HasPendingCheckoutAsync(10, residency.TermId)).ReturnsAsync(false);

        var dto = new CreateRequestDto
        {
            RequestType = "Checkout",
            ExpectedCheckOutDate = TodayVn.AddDays(5),
            Reason = "Trả phòng gấp"
        };

        var act = () => sut.CreateRequestAsync(10, dto);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*sau ít nhất 1 tuần*");
    }

    [Fact]
    public async Task CreateRequestAsync_CheckoutDateWithinLast2WeeksOfTerm_ThrowsInvalidOperationException()
    {
        var sut = CreateSut();
        var (studentUser, residency, term, _, _, _, _) = BuildActiveStay(daysUntilTermEnd: 60);

        _requestRepoMock.Setup(r => r.GetUserByIdAsync(10)).ReturnsAsync(studentUser);
        _requestRepoMock.Setup(r => r.GetStudentActiveResidencyAsync(10)).ReturnsAsync(residency);
        _requestRepoMock.Setup(r => r.HasPendingCheckoutAsync(10, residency.TermId)).ReturnsAsync(false);

        var dto = new CreateRequestDto
        {
            RequestType = "Checkout",
            ExpectedCheckOutDate = term.EndDate.AddDays(-10),
            Reason = "Trả phòng sát cuối kỳ"
        };

        var act = () => sut.CreateRequestAsync(10, dto);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*trước ngày kết thúc học kỳ ít nhất 2 tuần*");
    }

    // ==================== ProcessRequestAsync (Step 1 Approve / Reject) ====================

    [Fact]
    public async Task ProcessRequestAsync_CheckoutApproveStep1_WhenGuardInOtherDorm_ThrowsInvalidOperationException()
    {
        var sut = CreateSut();
        var (studentUser, residency, term, _, _, _, _) = BuildActiveStay(dormId: 1);
        var req = new Request
        {
            Id = 92,
            StudentId = 10,
            TermId = term.Id,
            RequestType = "Checkout",
            Status = "Pending",
            Term = term,
            Student = new Student { UserId = 10, User = studentUser },
            CheckoutRequest = new CheckoutRequest
            {
                RequestId = 92,
                ResidencyId = residency.Id,
                ExpectedCheckOutDate = TodayVn.AddDays(10),
                Residency = residency
            }
        };

        _requestRepoMock.Setup(r => r.GetRequestByIdAsync(92)).ReturnsAsync(req);
        _requestRepoMock.Setup(r => r.GetGuardByUserIdAsync(202)).ReturnsAsync(new Guard { UserId = 202, DormId = 2 });

        var act = () => sut.ProcessRequestAsync(92, managerId: 5, new ProcessRequestDto
        {
            Decision = "Approved",
            AssignedGuardId = 202
        });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*tòa KTX phòng hiện tại*");
    }

    [Fact]
    public async Task ProcessRequestAsync_CheckoutApproveStep1_AssignsSameDormGuardAndNotifies()
    {
        var sut = CreateSut();
        var (studentUser, residency, term, _, _, _, _) = BuildActiveStay(dormId: 1);
        var guardUser = new User { Id = 201, Code = "GD01", FullName = "Tran Bao Ve", Phone = "0900000001" };
        var guard = new Guard { UserId = 201, DormId = 1, User = guardUser };
        var req = new Request
        {
            Id = 93,
            StudentId = 10,
            TermId = term.Id,
            RequestType = "Checkout",
            Status = "Pending",
            Term = term,
            Student = new Student { UserId = 10, User = studentUser },
            CheckoutRequest = new CheckoutRequest
            {
                RequestId = 93,
                ResidencyId = residency.Id,
                ExpectedCheckOutDate = TodayVn.AddDays(10),
                Residency = residency
            }
        };

        _requestRepoMock.Setup(r => r.GetRequestByIdAsync(93)).ReturnsAsync(req);
        _requestRepoMock.Setup(r => r.GetGuardsAsync(1)).ReturnsAsync(new List<Guard> { guard });

        var result = await sut.ProcessRequestAsync(93, managerId: 5, new ProcessRequestDto { Decision = "Approved" });

        result.Status.Should().Be("Approved");
        result.Checkout!.AssignedGuardId.Should().Be(201);
        _requestRepoMock.Verify(r => r.ApproveCheckoutStep1Async(
            req,
            req.CheckoutRequest!,
            It.Is<RequestLog>(l => l.ToStatus == "Approved"),
            It.Is<List<Notification>>(n => n.Count == 2 && n.Any(x => x.UserId == 201) && n.Any(x => x.UserId == 10))), Times.Once);
    }

    [Fact]
    public async Task ProcessRequestAsync_CheckoutRejectWithoutNote_ThrowsInvalidOperationException()
    {
        var sut = CreateSut();
        var (studentUser, residency, term, _, _, _, _) = BuildActiveStay();
        var req = new Request
        {
            Id = 94,
            StudentId = 10,
            TermId = term.Id,
            RequestType = "Checkout",
            Status = "Pending",
            Term = term,
            Student = new Student { UserId = 10, User = studentUser },
            CheckoutRequest = new CheckoutRequest
            {
                RequestId = 94,
                ResidencyId = residency.Id,
                ExpectedCheckOutDate = TodayVn.AddDays(10),
                Residency = residency
            }
        };

        _requestRepoMock.Setup(r => r.GetRequestByIdAsync(94)).ReturnsAsync(req);

        var act = () => sut.ProcessRequestAsync(94, managerId: 5, new ProcessRequestDto { Decision = "Rejected", Note = "  " });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*lý do từ chối*");
    }

    // ==================== SubmitCheckoutInspectionAsync ====================

    [Fact]
    public async Task SubmitCheckoutInspectionAsync_WhenBelongingsNotCleared_ThrowsInvalidOperationException()
    {
        var sut = CreateSut();
        var (studentUser, residency, term, _, _, _, _) = BuildActiveStay();
        var req = new Request
        {
            Id = 95,
            StudentId = 10,
            TermId = term.Id,
            RequestType = "Checkout",
            Status = "Approved",
            Term = term,
            Student = new Student { UserId = 10, User = studentUser },
            CheckoutRequest = new CheckoutRequest
            {
                RequestId = 95,
                ResidencyId = residency.Id,
                ExpectedCheckOutDate = TodayVn.AddDays(10),
                AssignedGuardId = 201,
                Residency = residency
            }
        };

        _requestRepoMock.Setup(r => r.GetRequestByIdAsync(95)).ReturnsAsync(req);

        var act = () => sut.SubmitCheckoutInspectionAsync(95, guardUserId: 201, new SubmitCheckoutInspectionDto
        {
            BelongingsCleared = false,
            CurrentElectricityReading = 120,
            CurrentWaterReading = 30
        });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*dọn sạch*đồ đạc cá nhân*");
    }

    [Fact]
    public async Task SubmitCheckoutInspectionAsync_ValidInput_CreatesCheckoutHandoverAndMeterReadings()
    {
        var sut = CreateSut();
        var (studentUser, residency, term, bed, room, _, _) = BuildActiveStay();
        var req = new Request
        {
            Id = 96,
            StudentId = 10,
            TermId = term.Id,
            RequestType = "Checkout",
            Status = "Approved",
            ProcessedById = 5,
            Term = term,
            Student = new Student { UserId = 10, User = studentUser },
            CheckoutRequest = new CheckoutRequest
            {
                RequestId = 96,
                ResidencyId = residency.Id,
                ExpectedCheckOutDate = TodayVn.AddDays(10),
                AssignedGuardId = 201,
                Residency = residency
            }
        };

        _requestRepoMock.Setup(r => r.GetRequestByIdAsync(96)).ReturnsAsync(req);
        _requestRepoMock.Setup(r => r.GetLatestRoomMeterReadingsAsync(room.Id)).ReturnsAsync(new Dictionary<string, decimal>
        {
            ["Electricity"] = 100m,
            ["Water"] = 20m
        });
        _requestRepoMock.Setup(r => r.GetRoomAndBedEquipmentsAsync(room.Id, bed.Id)).ReturnsAsync(new List<Equipment>
        {
            new() { Id = 1, RoomId = room.Id, BedId = bed.Id, AssetCode = "DESK-01", Condition = "Good" }
        });
        _requestRepoMock.Setup(r => r.GetUtilityPricesByTermAsync(term.Id)).ReturnsAsync(new Dictionary<string, decimal>
        {
            ["Electricity"] = 3000m,
            ["Water"] = 15000m
        });
        _requestRepoMock.Setup(r => r.GetActiveRoomResidentCountAsync(room.Id, term.Id)).ReturnsAsync(2);

        Handover? capturedHandover = null;
        List<MeterReading>? capturedReadings = null;
        _requestRepoMock.Setup(r => r.SubmitCheckoutInspectionAsync(
                It.IsAny<Request>(),
                It.IsAny<Handover>(),
                It.IsAny<List<HandoverItem>>(),
                It.IsAny<List<MeterReading>>(),
                It.IsAny<List<(int, string, string?)>>(),
                It.IsAny<RequestLog>(),
                It.IsAny<List<Notification>>()))
            .Callback<Request, Handover, List<HandoverItem>, List<MeterReading>, List<(int, string, string?)>, RequestLog, List<Notification>>(
                (_, h, _, mrs, _, _, _) =>
                {
                    capturedHandover = h;
                    capturedReadings = mrs;
                })
            .Returns(Task.CompletedTask);

        var result = await sut.SubmitCheckoutInspectionAsync(96, guardUserId: 201, new SubmitCheckoutInspectionDto
        {
            BelongingsCleared = true,
            CurrentElectricityReading = 120m,
            CurrentWaterReading = 24m,
            Items = new List<TransferInspectionItemInputDto>
            {
                new() { EquipmentId = 1, ItemCondition = "Damaged", Description = "Gãy ngăn kéo", CompensationAmount = 200000m }
            }
        });

        result.HandoverCompleted.Should().BeTrue();
        capturedHandover.Should().NotBeNull();
        capturedHandover!.HandoverType.Should().Be("Checkout");
        capturedHandover.ResidencyId.Should().BeNull();
        capturedHandover.BelongingsRemainingFlag.Should().BeFalse();
        capturedHandover.TotalCompensation.Should().Be(200000m);

        capturedReadings.Should().HaveCount(2);
        capturedReadings!.Should().OnlyContain(m => m.CheckoutRequestId == 96 && m.TransferRequestId == null);
    }

    // ==================== ProcessRequestAsync (Step 2 Completed) ====================

    [Fact]
    public async Task ProcessRequestAsync_CheckoutCompleteStep2_WithoutHandover_ThrowsInvalidOperationException()
    {
        var sut = CreateSut();
        var (studentUser, residency, term, _, _, _, _) = BuildActiveStay();
        var req = new Request
        {
            Id = 97,
            StudentId = 10,
            TermId = term.Id,
            RequestType = "Checkout",
            Status = "Approved",
            Term = term,
            Student = new Student { UserId = 10, User = studentUser },
            CheckoutRequest = new CheckoutRequest
            {
                RequestId = 97,
                ResidencyId = residency.Id,
                ExpectedCheckOutDate = TodayVn.AddDays(10),
                AssignedGuardId = 201,
                Residency = residency
            },
            Handover = null
        };

        _requestRepoMock.Setup(r => r.GetRequestByIdAsync(97)).ReturnsAsync(req);

        var act = () => sut.ProcessRequestAsync(97, managerId: 5, new ProcessRequestDto { Decision = "Completed" });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Chưa có biên bản kiểm tra trả phòng*");
    }

    [Fact]
    public async Task ProcessRequestAsync_CheckoutCompleteStep2_IssuesUtilityAndCompensationInvoicesAndCompletes()
    {
        var sut = CreateSut();
        var (studentUser, residency, term, bed, room, _, _) = BuildActiveStay();
        var handover = new Handover
        {
            Id = 700,
            HandoverType = "Checkout",
            RequestId = 98,
            RoomId = room.Id,
            GuardId = 201,
            Status = "Completed",
            TotalCompensation = 150000m,
            StudentAcceptedFlag = true,
            HandoverItems = new List<HandoverItem>
            {
                new()
                {
                    Id = 1,
                    EquipmentId = 1,
                    ItemCondition = "Damaged",
                    CompensationAmount = 150000m,
                    Equipment = new Equipment { Id = 1, AssetCode = "DESK-01", Catalog = new EquipmentCatalog { Name = "Bàn học" } }
                }
            }
        };

        var req = new Request
        {
            Id = 98,
            StudentId = 10,
            TermId = term.Id,
            RequestType = "Checkout",
            Status = "Approved",
            Term = term,
            Student = new Student { UserId = 10, User = studentUser },
            Handover = handover,
            CheckoutRequest = new CheckoutRequest
            {
                RequestId = 98,
                ResidencyId = residency.Id,
                ExpectedCheckOutDate = TodayVn.AddDays(10),
                AssignedGuardId = 201,
                Residency = residency,
                MeterReadings = new List<MeterReading>
                {
                    new() { Id = 1, MeterType = "Electricity", PreviousValue = 100m, CurrentValue = 110m, Consumption = 10m },
                    new() { Id = 2, MeterType = "Water", PreviousValue = 20m, CurrentValue = 22m, Consumption = 2m }
                }
            }
        };

        _requestRepoMock.Setup(r => r.GetRequestByIdAsync(98)).ReturnsAsync(req);

        List<Invoice>? capturedInvoices = null;
        _requestRepoMock.Setup(r => r.CompleteCheckoutAsync(
                It.IsAny<Request>(),
                It.IsAny<CheckoutRequest>(),
                residency.Id,
                bed.Id,
                It.IsAny<DateOnly>(),
                It.IsAny<List<Invoice>>(),
                700,
                5,
                It.IsAny<string?>(),
                It.IsAny<RequestLog>(),
                It.IsAny<Notification>(),
                It.IsAny<List<Notification>?>()))
            .Callback<Request, CheckoutRequest, int, int, DateOnly, List<Invoice>, int?, int, string?, RequestLog, Notification, List<Notification>?>(
                (_, _, _, _, _, invs, _, _, _, _, _, _) => capturedInvoices = invs)
            .Returns(Task.CompletedTask);

        var result = await sut.ProcessRequestAsync(98, managerId: 5, new ProcessRequestDto { Decision = "Completed" });

        result.Status.Should().Be("Completed");
        capturedInvoices.Should().NotBeNull();
        capturedInvoices!.Should().HaveCount(2);
        capturedInvoices.Should().ContainSingle(i => i.InvoiceType == "Utility" && i.HandoverId == 700 && i.Amount == 60000m);
        capturedInvoices.Should().ContainSingle(i => i.InvoiceType == "Compensation" && i.HandoverId == 700 && i.Amount == 150000m);
    }

    // ==================== Cross-blocking & Early Residency Active Checks ====================

    [Fact]
    public async Task GetCheckoutContextAsync_WhenPendingBedTransferExists_ReturnsEligibleFalse()
    {
        var sut = CreateSut();
        var (studentUser, residency, _, _, _, _, _) = BuildActiveStay();

        _requestRepoMock.Setup(r => r.GetUserByIdAsync(10)).ReturnsAsync(studentUser);
        _requestRepoMock.Setup(r => r.GetStudentActiveResidencyAsync(10)).ReturnsAsync(residency);
        _requestRepoMock.Setup(r => r.HasOverdueInvoicesAsync(10)).ReturnsAsync(false);
        _requestRepoMock.Setup(r => r.HasPendingCheckoutAsync(10, residency.TermId)).ReturnsAsync(false);
        _requestRepoMock.Setup(r => r.HasPendingBedTransferAsync(10, residency.TermId)).ReturnsAsync(true);

        var ctx = await sut.GetCheckoutContextAsync(10);

        ctx.Eligible.Should().BeFalse();
        ctx.IneligibleReason.Should().Contain("chuyển giường");
    }

    [Fact]
    public async Task CreateRequestAsync_CheckoutWhenPendingBedTransferExists_ThrowsInvalidOperationException()
    {
        var sut = CreateSut();
        var (studentUser, residency, _, _, _, _, _) = BuildActiveStay();

        _requestRepoMock.Setup(r => r.GetUserByIdAsync(10)).ReturnsAsync(studentUser);
        _requestRepoMock.Setup(r => r.GetStudentActiveResidencyAsync(10)).ReturnsAsync(residency);
        _requestRepoMock.Setup(r => r.HasPendingCheckoutAsync(10, residency.TermId)).ReturnsAsync(false);
        _requestRepoMock.Setup(r => r.HasPendingBedTransferAsync(10, residency.TermId)).ReturnsAsync(true);

        var act = () => sut.CreateRequestAsync(10, new CreateRequestDto
        {
            RequestType = "Checkout",
            ExpectedCheckOutDate = TodayVn.AddDays(10),
            Reason = "Xin trả phòng"
        });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*chuyển giường*");
    }

    [Fact]
    public async Task GetBedTransferContextAsync_WhenPendingCheckoutExists_ReturnsEligibleFalse()
    {
        var sut = CreateSut();
        var (studentUser, residency, _, _, _, _, _) = BuildActiveStay();

        _requestRepoMock.Setup(r => r.GetUserByIdAsync(10)).ReturnsAsync(studentUser);
        _requestRepoMock.Setup(r => r.GetStudentActiveResidencyAsync(10)).ReturnsAsync(residency);
        _requestRepoMock.Setup(r => r.GetPublishedRoomPricesAsync(residency.TermId))
            .ReturnsAsync(new Dictionary<(int, int), decimal> { [(1, 1)] = 3500000m });
        _requestRepoMock.Setup(r => r.HasOverdueInvoicesAsync(10)).ReturnsAsync(false);
        _requestRepoMock.Setup(r => r.HasPendingBedTransferAsync(10, residency.TermId)).ReturnsAsync(false);
        _requestRepoMock.Setup(r => r.HasPendingCheckoutAsync(10, residency.TermId)).ReturnsAsync(true);

        var ctx = await sut.GetBedTransferContextAsync(10);

        ctx.Eligible.Should().BeFalse();
        ctx.IneligibleReason.Should().Contain("trả phòng");
    }

    [Fact]
    public async Task CreateRequestAsync_BedTransferWhenPendingCheckoutExists_ThrowsInvalidOperationException()
    {
        var sut = CreateSut();
        var (studentUser, residency, _, _, _, _, _) = BuildActiveStay();

        _requestRepoMock.Setup(r => r.GetUserByIdAsync(10)).ReturnsAsync(studentUser);
        _requestRepoMock.Setup(r => r.GetStudentActiveResidencyAsync(10)).ReturnsAsync(residency);
        _requestRepoMock.Setup(r => r.HasOverdueInvoicesAsync(10)).ReturnsAsync(false);
        _requestRepoMock.Setup(r => r.HasPendingBedTransferAsync(10, residency.TermId)).ReturnsAsync(false);
        _requestRepoMock.Setup(r => r.HasPendingCheckoutAsync(10, residency.TermId)).ReturnsAsync(true);

        var act = () => sut.CreateRequestAsync(10, new CreateRequestDto
        {
            RequestType = "BedTransfer",
            ToBedId = 2000,
            Reason = "Xin đổi giường"
        });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*trả phòng*");
    }

    [Fact]
    public async Task ProcessRequestAsync_CheckoutApproveStep1_WhenResidencyNotActive_ThrowsWithActualStatus()
    {
        var sut = CreateSut();
        var (studentUser, residency, term, _, _, _, _) = BuildActiveStay();
        residency.Status = "Ended";

        var req = new Request
        {
            Id = 99,
            StudentId = 10,
            TermId = term.Id,
            RequestType = "Checkout",
            Status = "Pending",
            Term = term,
            Student = new Student { UserId = 10, User = studentUser },
            CheckoutRequest = new CheckoutRequest
            {
                RequestId = 99,
                ResidencyId = residency.Id,
                ExpectedCheckOutDate = TodayVn.AddDays(10),
                Residency = residency
            }
        };

        _requestRepoMock.Setup(r => r.GetRequestByIdAsync(99)).ReturnsAsync(req);

        var act = () => sut.ProcessRequestAsync(99, managerId: 5, new ProcessRequestDto { Decision = "Approved" });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*#500*Ended*");
    }

    [Fact]
    public async Task SubmitCheckoutInspectionAsync_WhenResidencyNotActive_ThrowsWithActualStatus()
    {
        var sut = CreateSut();
        var (studentUser, residency, term, _, _, _, _) = BuildActiveStay();
        residency.Status = "Ended";

        var req = new Request
        {
            Id = 100,
            StudentId = 10,
            TermId = term.Id,
            RequestType = "Checkout",
            Status = "Approved",
            Term = term,
            Student = new Student { UserId = 10, User = studentUser },
            CheckoutRequest = new CheckoutRequest
            {
                RequestId = 100,
                ResidencyId = residency.Id,
                ExpectedCheckOutDate = TodayVn.AddDays(10),
                AssignedGuardId = 201,
                Residency = residency
            }
        };

        _requestRepoMock.Setup(r => r.GetRequestByIdAsync(100)).ReturnsAsync(req);

        var act = () => sut.SubmitCheckoutInspectionAsync(100, guardUserId: 201, new SubmitCheckoutInspectionDto
        {
            BelongingsCleared = true,
            CurrentElectricityReading = 120m,
            CurrentWaterReading = 24m
        });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*#500*Ended*");
    }
}
