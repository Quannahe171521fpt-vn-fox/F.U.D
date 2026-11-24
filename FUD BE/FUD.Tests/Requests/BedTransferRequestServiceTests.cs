using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using FUD.DTOs.Requests;
using FUD.Infrastructure.Email;
using FUD.Infrastructure.Storage;
using FUD.Models;
using FUD.Repositories.Interfaces.Requests;
using FUD.Repositories.Interfaces.Violations;
using FUD.Services.Requests;
using Moq;
using Xunit;

namespace FUD.Tests.Requests;

public class BedTransferRequestServiceTests
{
    private readonly Mock<IRequestRepository> _requestRepoMock = new();
    private readonly Mock<IViolationRepository> _violationRepoMock = new();
    private readonly Mock<IFileStorage> _fileStorageMock = new();
    private readonly Mock<IEmailSender> _emailSenderMock = new();

    private RequestService CreateSut() => new(
        _requestRepoMock.Object,
        _violationRepoMock.Object,
        _fileStorageMock.Object,
        _emailSenderMock.Object);

    private static User CreateStudentUser(int id = 10, string gender = "Male", string accountStatus = "Active") => new()
    {
        Id = id,
        Code = $"SE{id}",
        FullName = "Nguyen Van A",
        Email = $"se{id}@fpt.edu.vn",
        Gender = gender,
        AccountStatus = accountStatus,
        CreatedAt = DateTime.UtcNow
    };

    private static Residency CreateActiveResidency(int studentId = 10, int bedId = 1, string gender = "Male")
    {
        var dorm = new Dorm { Id = 1, CampusId = 1, Code = "A", Name = "Dom A", Status = "Active" };
        var block = new Block { Id = 1, DormId = 1, Code = "101", Name = "Block 101", GenderType = gender, Status = "Active", Dorm = dorm };
        var roomType = new RoomType { Id = 1, Name = "Phòng 4 giường", BedCapacity = 4 };
        var room = new Room { Id = 1, BlockId = 1, RoomTypeId = 1, RoomNumber = "101L", FloorLevel = 1, Status = "Active", Block = block, RoomType = roomType };
        var bed = new Bed { Id = bedId, RoomId = 1, BedCode = "101L-1", PositionInRoom = "Bottom Left", Status = "Occupied", Room = room };
        var term = new Term
        {
            Id = 1,
            Name = "Fall",
            AcademicYear = "2026-2027",
            StartDate = new DateOnly(2026, 9, 1),
            EndDate = new DateOnly(2026, 12, 31)
        };

        return new Residency
        {
            Id = 100,
            StudentId = studentId,
            BedId = bedId,
            TermId = 1,
            Status = "Active",
            CheckInDate = new DateOnly(2026, 9, 1),
            Bed = bed,
            Term = term
        };
    }

    private static Bed CreateTargetBed(int bedId = 2, string gender = "Male", string bedStatus = "Free")
    {
        var dorm = new Dorm { Id = 1, CampusId = 1, Code = "A", Name = "Dom A", Status = "Active" };
        var block = new Block { Id = 2, DormId = 1, Code = "201", Name = "Block 201", GenderType = gender, Status = "Active", Dorm = dorm };
        var roomType = new RoomType { Id = 2, Name = "Phòng 6 giường", BedCapacity = 6 };
        var room = new Room { Id = 2, BlockId = 2, RoomTypeId = 2, RoomNumber = "201L", FloorLevel = 2, Status = "Active", Block = block, RoomType = roomType };
        return new Bed { Id = bedId, RoomId = 2, BedCode = "201L-1", PositionInRoom = "Bottom Left", Status = bedStatus, Room = room };
    }

    [Fact]
    public async Task GetBedTransferContextAsync_WhenNoActiveResidency_ReturnsIneligible()
    {
        var student = CreateStudentUser();
        _requestRepoMock.Setup(r => r.GetUserByIdAsync(student.Id)).ReturnsAsync(student);
        _requestRepoMock.Setup(r => r.GetStudentActiveResidencyAsync(student.Id)).ReturnsAsync((Residency?)null);

        var sut = CreateSut();
        var result = await sut.GetBedTransferContextAsync(student.Id);

        result.Eligible.Should().BeFalse();
        result.IneligibleReason.Should().Contain("Active");
        result.CurrentResidency.Should().BeNull();
    }

    [Fact]
    public async Task GetBedTransferContextAsync_WhenHasOverdueInvoice_ReturnsIneligible()
    {
        var student = CreateStudentUser();
        var residency = CreateActiveResidency(student.Id);
        _requestRepoMock.Setup(r => r.GetUserByIdAsync(student.Id)).ReturnsAsync(student);
        _requestRepoMock.Setup(r => r.GetStudentActiveResidencyAsync(student.Id)).ReturnsAsync(residency);
        _requestRepoMock.Setup(r => r.GetPublishedRoomPricesAsync(residency.TermId))
            .ReturnsAsync(new Dictionary<(int, int), decimal> { { (1, 1), 4000000m } });
        _requestRepoMock.Setup(r => r.HasOverdueInvoicesAsync(student.Id)).ReturnsAsync(true);

        var sut = CreateSut();
        var result = await sut.GetBedTransferContextAsync(student.Id);

        result.Eligible.Should().BeFalse();
        result.IneligibleReason.Should().Contain("quá hạn");
        result.CurrentResidency.Should().NotBeNull();
    }

    [Fact]
    public async Task GetBedTransferContextAsync_WhenHasPendingBedTransfer_ReturnsIneligible()
    {
        var student = CreateStudentUser();
        var residency = CreateActiveResidency(student.Id);
        _requestRepoMock.Setup(r => r.GetUserByIdAsync(student.Id)).ReturnsAsync(student);
        _requestRepoMock.Setup(r => r.GetStudentActiveResidencyAsync(student.Id)).ReturnsAsync(residency);
        _requestRepoMock.Setup(r => r.GetPublishedRoomPricesAsync(residency.TermId))
            .ReturnsAsync(new Dictionary<(int, int), decimal> { { (1, 1), 4000000m } });
        _requestRepoMock.Setup(r => r.HasOverdueInvoicesAsync(student.Id)).ReturnsAsync(false);
        _requestRepoMock.Setup(r => r.HasPendingBedTransferAsync(student.Id, residency.TermId)).ReturnsAsync(true);

        var sut = CreateSut();
        var result = await sut.GetBedTransferContextAsync(student.Id);

        result.Eligible.Should().BeFalse();
        result.IneligibleReason.Should().Contain("chờ xét duyệt");
    }

    [Fact]
    public async Task GetBedTransferContextAsync_WhenEligible_ReturnsCurrentStayAndAvailableBeds()
    {
        var student = CreateStudentUser();
        var residency = CreateActiveResidency(student.Id);
        var targetBed = CreateTargetBed(2, "Male", "Free");
        _requestRepoMock.Setup(r => r.GetUserByIdAsync(student.Id)).ReturnsAsync(student);
        _requestRepoMock.Setup(r => r.GetStudentActiveResidencyAsync(student.Id)).ReturnsAsync(residency);
        _requestRepoMock.Setup(r => r.GetPublishedRoomPricesAsync(residency.TermId))
            .ReturnsAsync(new Dictionary<(int, int), decimal>
            {
                { (1, 1), 4000000m },
                { (1, 2), 3000000m }
            });
        _requestRepoMock.Setup(r => r.HasOverdueInvoicesAsync(student.Id)).ReturnsAsync(false);
        _requestRepoMock.Setup(r => r.HasPendingBedTransferAsync(student.Id, residency.TermId)).ReturnsAsync(false);
        _requestRepoMock.Setup(r => r.GetAvailableBedsForTransferAsync("Male", residency.BedId, residency.TermId))
            .ReturnsAsync(new List<Bed> { targetBed });

        var sut = CreateSut();
        var result = await sut.GetBedTransferContextAsync(student.Id);

        result.Eligible.Should().BeTrue();
        result.CurrentResidency.Should().NotBeNull();
        result.CurrentResidency!.BedCode.Should().Be("101L-1");
        result.CurrentResidency.CurrentTermPrice.Should().Be(4000000m);
        result.AvailableBeds.Should().HaveCount(1);
        result.AvailableBeds[0].BedCode.Should().Be("201L-1");
        result.AvailableBeds[0].TermPrice.Should().Be(3000000m);
    }

    [Fact]
    public async Task CreateRequestAsync_BedTransfer_WhenToBedSameAsFromBed_ThrowsInvalidOperationException()
    {
        var student = CreateStudentUser();
        var residency = CreateActiveResidency(student.Id, bedId: 1);
        var sameBed = CreateTargetBed(bedId: 1, gender: "Male", bedStatus: "Free");

        _requestRepoMock.Setup(r => r.GetUserByIdAsync(student.Id)).ReturnsAsync(student);
        _requestRepoMock.Setup(r => r.GetStudentActiveResidencyAsync(student.Id)).ReturnsAsync(residency);
        _requestRepoMock.Setup(r => r.HasOverdueInvoicesAsync(student.Id)).ReturnsAsync(false);
        _requestRepoMock.Setup(r => r.HasPendingBedTransferAsync(student.Id, residency.TermId)).ReturnsAsync(false);
        _requestRepoMock.Setup(r => r.GetBedWithLocationByIdAsync(1)).ReturnsAsync(sameBed);

        var sut = CreateSut();
        var act = async () => await sut.CreateRequestAsync(student.Id, new CreateRequestDto
        {
            RequestType = "BedTransfer",
            ToBedId = 1,
            Reason = "Muốn đổi giường"
        });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*khác với giường bạn đang ở*");
    }

    [Fact]
    public async Task CreateRequestAsync_BedTransfer_WhenGenderMismatch_ThrowsInvalidOperationException()
    {
        var student = CreateStudentUser(gender: "Male");
        var residency = CreateActiveResidency(student.Id, bedId: 1, gender: "Male");
        var femaleBed = CreateTargetBed(bedId: 3, gender: "Female", bedStatus: "Free");

        _requestRepoMock.Setup(r => r.GetUserByIdAsync(student.Id)).ReturnsAsync(student);
        _requestRepoMock.Setup(r => r.GetStudentActiveResidencyAsync(student.Id)).ReturnsAsync(residency);
        _requestRepoMock.Setup(r => r.HasOverdueInvoicesAsync(student.Id)).ReturnsAsync(false);
        _requestRepoMock.Setup(r => r.HasPendingBedTransferAsync(student.Id, residency.TermId)).ReturnsAsync(false);
        _requestRepoMock.Setup(r => r.GetBedWithLocationByIdAsync(3)).ReturnsAsync(femaleBed);

        var sut = CreateSut();
        var act = async () => await sut.CreateRequestAsync(student.Id, new CreateRequestDto
        {
            RequestType = "BedTransfer",
            ToBedId = 3,
            Reason = "Chuyển sang phòng mới"
        });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Giới tính sinh viên không khớp*");
    }

    [Fact]
    public async Task CreateRequestAsync_BedTransfer_WhenValid_CreatesRequestAndBedTransferRecord()
    {
        var student = CreateStudentUser(gender: "Male");
        var residency = CreateActiveResidency(student.Id, bedId: 1, gender: "Male");
        var targetBed = CreateTargetBed(bedId: 2, gender: "Male", bedStatus: "Free");

        _requestRepoMock.Setup(r => r.GetUserByIdAsync(student.Id)).ReturnsAsync(student);
        _requestRepoMock.Setup(r => r.GetStudentActiveResidencyAsync(student.Id)).ReturnsAsync(residency);
        _requestRepoMock.Setup(r => r.HasOverdueInvoicesAsync(student.Id)).ReturnsAsync(false);
        _requestRepoMock.Setup(r => r.HasPendingBedTransferAsync(student.Id, residency.TermId)).ReturnsAsync(false);
        _requestRepoMock.Setup(r => r.GetBedWithLocationByIdAsync(2)).ReturnsAsync(targetBed);
        _requestRepoMock.Setup(r => r.IsBedAvailableForTransferAsync(2, residency.TermId)).ReturnsAsync(true);

        Request? capturedRequest = null;
        BedTransferRequest? capturedTransfer = null;
        RequestLog? capturedLog = null;

        _requestRepoMock.Setup(r => r.CreateBedTransferRequestAsync(
                It.IsAny<Request>(),
                It.IsAny<BedTransferRequest>(),
                It.IsAny<RequestLog>()))
            .Callback<Request, BedTransferRequest, RequestLog>((req, tr, log) =>
            {
                req.Id = 55;
                tr.RequestId = 55;
                tr.FromBed = residency.Bed;
                tr.ToBed = targetBed;
                req.Student = new Student { UserId = student.Id, User = student };
                req.BedTransferRequest = tr;
                capturedRequest = req;
                capturedTransfer = tr;
                capturedLog = log;
            })
            .ReturnsAsync((Request req, BedTransferRequest _, RequestLog _) => req);

        _requestRepoMock.Setup(r => r.GetRequestByIdAsync(55))
            .ReturnsAsync(() => capturedRequest);

        var sut = CreateSut();
        var todayVn = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        residency.Term.EndDate = todayVn.AddDays(30);
        var validTransferDate = todayVn.AddDays(7);
        var result = await sut.CreateRequestAsync(student.Id, new CreateRequestDto
        {
            RequestType = "BedTransfer",
            ToBedId = 2,
            TransferDate = validTransferDate,
            Reason = "Chuyển sang ở gần bạn cùng lớp"
        });

        capturedRequest.Should().NotBeNull();
        capturedTransfer.Should().NotBeNull();
        capturedLog.Should().NotBeNull();
        capturedTransfer!.FromBedId.Should().Be(1);
        capturedTransfer.ToBedId.Should().Be(2);
        capturedTransfer.FromResidencyId.Should().Be(residency.Id);
        capturedLog!.ToStatus.Should().Be("Pending");

        result.Id.Should().Be(55);
        result.RequestType.Should().Be("BedTransfer");
        result.Status.Should().Be("Pending");
        result.BedTransfer.Should().NotBeNull();
        result.BedTransfer!.FromBedCode.Should().Be("101L-1");
        result.BedTransfer.ToBedCode.Should().Be("201L-1");
        result.BedTransfer.TransferDate.Should().Be(validTransferDate);
        targetBed.Status.Should().Be("Pending Transfer");
    }

    [Fact]
    public async Task CreateRequestAsync_BedTransfer_WhenTransferDateLessThan7Days_ThrowsInvalidOperationException()
    {
        var student = CreateStudentUser(gender: "Male");
        var residency = CreateActiveResidency(student.Id, bedId: 1, gender: "Male");
        var todayVn = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        residency.Term.EndDate = todayVn.AddDays(30);
        var targetBed = CreateTargetBed(bedId: 2, gender: "Male", bedStatus: "Free");

        _requestRepoMock.Setup(r => r.GetUserByIdAsync(student.Id)).ReturnsAsync(student);
        _requestRepoMock.Setup(r => r.GetStudentActiveResidencyAsync(student.Id)).ReturnsAsync(residency);
        _requestRepoMock.Setup(r => r.HasOverdueInvoicesAsync(student.Id)).ReturnsAsync(false);
        _requestRepoMock.Setup(r => r.HasPendingBedTransferAsync(student.Id, residency.TermId)).ReturnsAsync(false);
        _requestRepoMock.Setup(r => r.GetBedWithLocationByIdAsync(2)).ReturnsAsync(targetBed);
        _requestRepoMock.Setup(r => r.IsBedAvailableForTransferAsync(2, residency.TermId)).ReturnsAsync(true);

        var sut = CreateSut();
        var act = async () => await sut.CreateRequestAsync(student.Id, new CreateRequestDto
        {
            RequestType = "BedTransfer",
            ToBedId = 2,
            TransferDate = todayVn.AddDays(6),
            Reason = "Chuyển sớm hơn 7 ngày"
        });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*ít nhất 1 tuần*");
    }

    [Fact]
    public async Task CreateRequestAsync_BedTransfer_WhenLessThan7DaysLeftInTermAndDateProvided_ThrowsInvalidOperationException()
    {
        var student = CreateStudentUser(gender: "Male");
        var residency = CreateActiveResidency(student.Id, bedId: 1, gender: "Male");
        var todayVn = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        residency.Term.EndDate = todayVn.AddDays(5);
        var targetBed = CreateTargetBed(bedId: 2, gender: "Male", bedStatus: "Free");

        _requestRepoMock.Setup(r => r.GetUserByIdAsync(student.Id)).ReturnsAsync(student);
        _requestRepoMock.Setup(r => r.GetStudentActiveResidencyAsync(student.Id)).ReturnsAsync(residency);
        _requestRepoMock.Setup(r => r.HasOverdueInvoicesAsync(student.Id)).ReturnsAsync(false);
        _requestRepoMock.Setup(r => r.HasPendingBedTransferAsync(student.Id, residency.TermId)).ReturnsAsync(false);
        _requestRepoMock.Setup(r => r.GetBedWithLocationByIdAsync(2)).ReturnsAsync(targetBed);
        _requestRepoMock.Setup(r => r.IsBedAvailableForTransferAsync(2, residency.TermId)).ReturnsAsync(true);

        var sut = CreateSut();
        var act = async () => await sut.CreateRequestAsync(student.Id, new CreateRequestDto
        {
            RequestType = "BedTransfer",
            ToBedId = 2,
            TransferDate = todayVn.AddDays(7),
            Reason = "Cuối học kỳ còn dưới 7 ngày"
        });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*ít hơn 1 tuần*");
    }

    [Fact]
    public async Task CreateRequestAsync_BedTransfer_WhenTransferDateOmitted_Succeeds()
    {
        var student = CreateStudentUser(gender: "Male");
        var residency = CreateActiveResidency(student.Id, bedId: 1, gender: "Male");
        var targetBed = CreateTargetBed(bedId: 2, gender: "Male", bedStatus: "Free");

        _requestRepoMock.Setup(r => r.GetUserByIdAsync(student.Id)).ReturnsAsync(student);
        _requestRepoMock.Setup(r => r.GetStudentActiveResidencyAsync(student.Id)).ReturnsAsync(residency);
        _requestRepoMock.Setup(r => r.HasOverdueInvoicesAsync(student.Id)).ReturnsAsync(false);
        _requestRepoMock.Setup(r => r.HasPendingBedTransferAsync(student.Id, residency.TermId)).ReturnsAsync(false);
        _requestRepoMock.Setup(r => r.GetBedWithLocationByIdAsync(2)).ReturnsAsync(targetBed);
        _requestRepoMock.Setup(r => r.IsBedAvailableForTransferAsync(2, residency.TermId)).ReturnsAsync(true);

        Request? capturedRequest = null;
        _requestRepoMock.Setup(r => r.CreateBedTransferRequestAsync(
                It.IsAny<Request>(),
                It.IsAny<BedTransferRequest>(),
                It.IsAny<RequestLog>()))
            .Callback<Request, BedTransferRequest, RequestLog>((req, tr, _) =>
            {
                req.Id = 56;
                tr.RequestId = 56;
                tr.FromBed = residency.Bed;
                tr.ToBed = targetBed;
                req.Student = new Student { UserId = student.Id, User = student };
                req.BedTransferRequest = tr;
                capturedRequest = req;
            })
            .ReturnsAsync((Request req, BedTransferRequest _, RequestLog _) => req);

        _requestRepoMock.Setup(r => r.GetRequestByIdAsync(56))
            .ReturnsAsync(() => capturedRequest);

        var sut = CreateSut();
        var result = await sut.CreateRequestAsync(student.Id, new CreateRequestDto
        {
            RequestType = "BedTransfer",
            ToBedId = 2,
            TransferDate = null,
            Reason = "Không nhập ngày dự kiến (tùy chọn)"
        });

        result.Id.Should().Be(56);
        result.BedTransfer!.TransferDate.Should().BeNull();
    }

    private static Request CreateBedTransferRequestGraph(string status = "Pending")
    {
        var student = CreateStudentUser(id: 10, gender: "Male");
        var residency = CreateActiveResidency(studentId: 10, bedId: 1, gender: "Male");
        var toBed = CreateTargetBed(bedId: 2, gender: "Male", bedStatus: "Pending Transfer");

        residency.Bed.Room.Beds.Add(residency.Bed);
        residency.Bed.Residencies.Add(residency);
        residency.Term.UtilityPrices.Add(new UtilityPrice { Id = 1, TermId = 1, MeterType = "Electricity", UnitPrice = 3000m });
        residency.Term.UtilityPrices.Add(new UtilityPrice { Id = 2, TermId = 1, MeterType = "Water", UnitPrice = 15000m });

        var bt = new BedTransferRequest
        {
            RequestId = 77,
            FromResidencyId = residency.Id,
            FromResidency = residency,
            FromBedId = residency.BedId,
            FromBed = residency.Bed,
            ToBedId = toBed.Id,
            ToBed = toBed,
            TransferReason = "Muốn chuyển sang phòng mới",
            TransferDate = new DateOnly(2026, 11, 1)
        };

        return new Request
        {
            Id = 77,
            StudentId = student.Id,
            TermId = residency.TermId,
            Term = residency.Term,
            Student = new Student { UserId = student.Id, User = student },
            RequestType = "BedTransfer",
            Status = status,
            Reason = bt.TransferReason,
            CreatedTime = DateTime.UtcNow,
            BedTransferRequest = bt
        };
    }

    [Fact]
    public async Task ProcessRequestAsync_BedTransfer_RejectWithoutNote_ThrowsInvalidOperationException()
    {
        var req = CreateBedTransferRequestGraph("Pending");
        _requestRepoMock.Setup(r => r.GetRequestByIdAsync(77)).ReturnsAsync(req);

        var sut = CreateSut();
        var act = async () => await sut.ProcessRequestAsync(77, 99, new ProcessRequestDto
        {
            Decision = "Rejected",
            Note = "   "
        });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*lý do từ chối*");
    }

    [Fact]
    public async Task ProcessRequestAsync_BedTransfer_RejectWithReason_UpdatesStatusAndNotifiesStudent()
    {
        var req = CreateBedTransferRequestGraph("Pending");
        _requestRepoMock.Setup(r => r.GetRequestByIdAsync(77)).ReturnsAsync(req);

        RequestLog? capturedLog = null;
        Notification? capturedNoti = null;
        _requestRepoMock.Setup(r => r.RejectBedTransferAsync(
                It.IsAny<Request>(),
                It.IsAny<RequestLog>(),
                It.IsAny<Notification>()))
            .Callback<Request, RequestLog, Notification>((_, log, noti) =>
            {
                capturedLog = log;
                capturedNoti = noti;
            })
            .Returns(Task.CompletedTask);

        var sut = CreateSut();
        var result = await sut.ProcessRequestAsync(77, 99, new ProcessRequestDto
        {
            Decision = "Rejected",
            Note = "Giường đích đang bảo trì đột xuất"
        });

        result.Status.Should().Be("Rejected");
        result.RejectReason.Should().Be("Giường đích đang bảo trì đột xuất");
        capturedLog.Should().NotBeNull();
        capturedLog!.ToStatus.Should().Be("Rejected");
        capturedNoti.Should().NotBeNull();
        capturedNoti!.UserId.Should().Be(req.StudentId);
        _emailSenderMock.Verify(e => e.SendEmailAsync(req.Student.User.Email, It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task ProcessRequestAsync_BedTransfer_ApproveStep1WithoutGuard_ThrowsInvalidOperationException()
    {
        var req = CreateBedTransferRequestGraph("Pending");
        _requestRepoMock.Setup(r => r.GetRequestByIdAsync(77)).ReturnsAsync(req);
        _requestRepoMock.Setup(r => r.GetGuardsAsync(1)).ReturnsAsync(new List<Guard>());

        var sut = CreateSut();
        var act = async () => await sut.ProcessRequestAsync(77, 99, new ProcessRequestDto
        {
            Decision = "Approved",
            AssignedGuardId = null
        });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Bảo vệ*");
    }

    [Fact]
    public async Task ProcessRequestAsync_BedTransfer_ApproveStep1_WhenGuardFromDifferentDorm_ThrowsInvalidOperationException()
    {
        var req = CreateBedTransferRequestGraph("Pending");
        var otherDormGuard = new Guard
        {
            UserId = 202,
            DormId = 2,
            User = new User { Id = 202, Code = "GRD02", FullName = "Guard Dom B", Gender = "Male", AccountStatus = "Active" }
        };
        _requestRepoMock.Setup(r => r.GetRequestByIdAsync(77)).ReturnsAsync(req);
        _requestRepoMock.Setup(r => r.GetGuardByUserIdAsync(202)).ReturnsAsync(otherDormGuard);

        var sut = CreateSut();
        var act = async () => await sut.ProcessRequestAsync(77, 99, new ProcessRequestDto
        {
            Decision = "Approved",
            AssignedGuardId = 202
        });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*tòa KTX phòng cũ*");
    }

    [Fact]
    public async Task ProcessRequestAsync_BedTransfer_ApproveStep1_WhenGuardIdOmitted_AutoAssignsOldDormGuard()
    {
        var req = CreateBedTransferRequestGraph("Pending");
        var guardUser = new User { Id = 201, Code = "GRD01", FullName = "Tran Bao Ve", Email = "grd01@fpt.edu.vn", Phone = "0901234567", Gender = "Male", AccountStatus = "Active" };
        var oldDormGuard = new Guard { UserId = 201, DormId = 1, User = guardUser };

        _requestRepoMock.Setup(r => r.GetRequestByIdAsync(77)).ReturnsAsync(req);
        _requestRepoMock.Setup(r => r.GetGuardsAsync(1)).ReturnsAsync(new List<Guard> { oldDormGuard });
        _requestRepoMock.Setup(r => r.GetPublishedRoomPricesAsync(1))
            .ReturnsAsync(new Dictionary<(int, int), decimal>
            {
                { (1, 1), 4000000m },
                { (1, 2), 5210000m }
            });
        _requestRepoMock.Setup(r => r.GetStudentPaidRoomAmountAsync(10, 1)).ReturnsAsync(4000000m);
        _requestRepoMock.Setup(r => r.ApproveBedTransferStep1Async(
                It.IsAny<Request>(),
                It.IsAny<BedTransferRequest>(),
                It.IsAny<RequestLog>(),
                It.IsAny<List<Notification>>()))
            .Callback<Request, BedTransferRequest, RequestLog, List<Notification>>((_, bt, _, _) =>
            {
                bt.AssignedGuard = oldDormGuard;
            })
            .Returns(Task.CompletedTask);

        var sut = CreateSut();
        var result = await sut.ProcessRequestAsync(77, 99, new ProcessRequestDto
        {
            Decision = "Approved",
            AssignedGuardId = null
        });

        result.Status.Should().Be("Approved");
        result.BedTransfer!.AssignedGuardId.Should().Be(201);
    }

    [Fact]
    public async Task ProcessRequestAsync_BedTransfer_ApproveStep1_AssignsGuardAndCalculatesProrate()
    {
        var req = CreateBedTransferRequestGraph("Pending");
        var guardUser = new User { Id = 201, Code = "GRD01", FullName = "Tran Bao Ve", Email = "grd01@fpt.edu.vn", Phone = "0901234567", Gender = "Male", AccountStatus = "Active" };
        var guard = new Guard { UserId = 201, DormId = 1, User = guardUser };

        _requestRepoMock.Setup(r => r.GetRequestByIdAsync(77)).ReturnsAsync(req);
        _requestRepoMock.Setup(r => r.GetGuardByUserIdAsync(201)).ReturnsAsync(guard);
        _requestRepoMock.Setup(r => r.GetPublishedRoomPricesAsync(1))
            .ReturnsAsync(new Dictionary<(int, int), decimal>
            {
                { (1, 1), 4000000m },
                { (1, 2), 5210000m }
            });
        _requestRepoMock.Setup(r => r.GetStudentPaidRoomAmountAsync(10, 1)).ReturnsAsync(4000000m);

        List<Notification>? capturedNotis = null;
        RequestLog? capturedLog = null;
        _requestRepoMock.Setup(r => r.ApproveBedTransferStep1Async(
                It.IsAny<Request>(),
                It.IsAny<BedTransferRequest>(),
                It.IsAny<RequestLog>(),
                It.IsAny<List<Notification>>()))
            .Callback<Request, BedTransferRequest, RequestLog, List<Notification>>((_, bt, log, notis) =>
            {
                bt.AssignedGuard = guard;
                capturedLog = log;
                capturedNotis = notis;
            })
            .Returns(Task.CompletedTask);

        var sut = CreateSut();
        var transferDate = new DateOnly(2026, 11, 1);
        var result = await sut.ProcessRequestAsync(77, 99, new ProcessRequestDto
        {
            Decision = "Approved",
            AssignedGuardId = 201,
            TransferDate = transferDate,
            Note = "Đã phân công bảo vệ kiểm tra trước khi chuyển"
        });

        result.Status.Should().Be("Approved");
        result.BedTransfer.Should().NotBeNull();
        result.BedTransfer!.AssignedGuardId.Should().Be(201);
        result.BedTransfer.AssignedGuardName.Should().Be("Tran Bao Ve");
        result.BedTransfer.DaysStayed.Should().Be(61);
        result.BedTransfer.DaysRemaining.Should().Be(60);
        result.BedTransfer.AmountAlreadyPaid.Should().Be(4000000m);
        result.BedTransfer.PriceDifference.Should().BeGreaterThan(0m);
        capturedLog!.ToStatus.Should().Be("Approved");
        capturedNotis.Should().HaveCount(2);
        capturedNotis!.Should().Contain(n => n.UserId == req.StudentId);
        capturedNotis.Should().Contain(n => n.UserId == 201);
    }

    [Fact]
    public async Task SubmitTransferInspectionAsync_WhenReadingLessThanPrevious_ThrowsInvalidOperationException()
    {
        var req = CreateBedTransferRequestGraph("Approved");
        req.BedTransferRequest!.AssignedGuardId = 201;

        _requestRepoMock.Setup(r => r.GetRequestByIdAsync(77)).ReturnsAsync(req);
        _requestRepoMock.Setup(r => r.GetLatestRoomMeterReadingsAsync(1))
            .ReturnsAsync(new Dictionary<string, decimal> { ["Electricity"] = 1200m, ["Water"] = 45m });

        var sut = CreateSut();
        var act = async () => await sut.SubmitTransferInspectionAsync(77, 201, new SubmitTransferInspectionDto
        {
            CurrentElectricityReading = 1150m,
            CurrentWaterReading = 50m,
            Items = new List<TransferInspectionItemInputDto>()
        });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Chỉ số điện hiện tại*");
    }

    [Fact]
    public async Task SubmitTransferInspectionAsync_WhenValid_CreatesThreePartyHandoverAndMeterReadings()
    {
        var req = CreateBedTransferRequestGraph("Approved");
        req.ProcessedById = 99;
        var guardUser = new User { Id = 201, Code = "GRD01", FullName = "Tran Bao Ve", Gender = "Male", AccountStatus = "Active" };
        req.BedTransferRequest!.AssignedGuardId = 201;
        req.BedTransferRequest.AssignedGuard = new Guard { UserId = 201, DormId = 1, User = guardUser };

        var catalog = new EquipmentCatalog { Id = 1, Category = "Nội thất", Name = "Bàn học gỗ" };
        var equip = new Equipment { Id = 501, RoomId = 1, BedId = 1, CatalogId = 1, AssetCode = "EQ-501", Condition = "Good", Catalog = catalog };

        _requestRepoMock.Setup(r => r.GetRequestByIdAsync(77)).ReturnsAsync(req);
        _requestRepoMock.Setup(r => r.GetLatestRoomMeterReadingsAsync(1))
            .ReturnsAsync(new Dictionary<string, decimal> { ["Electricity"] = 1000m, ["Water"] = 40m });
        _requestRepoMock.Setup(r => r.GetRoomAndBedEquipmentsAsync(1, 1))
            .ReturnsAsync(new List<Equipment> { equip });
        _requestRepoMock.Setup(r => r.GetUtilityPricesByTermAsync(1))
            .ReturnsAsync(new Dictionary<string, decimal> { ["Electricity"] = 3000m, ["Water"] = 15000m });
        _requestRepoMock.Setup(r => r.GetActiveRoomResidentCountAsync(1, 1)).ReturnsAsync(2);

        Handover? capturedHandover = null;
        List<HandoverItem>? capturedItems = null;
        List<MeterReading>? capturedMeters = null;
        RequestLog? capturedLog = null;
        _requestRepoMock.Setup(r => r.SubmitTransferInspectionAsync(
                It.IsAny<Request>(),
                It.IsAny<Handover>(),
                It.IsAny<List<HandoverItem>>(),
                It.IsAny<List<MeterReading>>(),
                It.IsAny<List<(int EquipmentId, string Condition, string? Note)>>(),
                It.IsAny<RequestLog>(),
                It.IsAny<List<Notification>>()))
            .Callback<Request, Handover, List<HandoverItem>, List<MeterReading>, List<(int, string, string?)>, RequestLog, List<Notification>>(
                (_, h, items, meters, _, log, _) =>
                {
                    h.Id = 300;
                    capturedHandover = h;
                    capturedItems = items;
                    capturedMeters = meters;
                    capturedLog = log;
                })
            .Returns(Task.CompletedTask);

        var sut = CreateSut();
        var result = await sut.SubmitTransferInspectionAsync(77, 201, new SubmitTransferInspectionDto
        {
            CurrentElectricityReading = 1050m,
            CurrentWaterReading = 44m,
            Note = "Gãy móc áo cạnh bàn học",
            Items = new List<TransferInspectionItemInputDto>
            {
                new() { EquipmentId = 501, ItemCondition = "Damaged", Description = "Gãy góc bàn", CompensationAmount = 120000m }
            }
        });

        capturedHandover.Should().NotBeNull();
        capturedHandover!.HandoverType.Should().Be("BedTransfer");
        capturedHandover.RequestId.Should().Be(77);
        capturedHandover.ResidencyId.Should().BeNull();
        capturedHandover.StudentAcceptedFlag.Should().BeTrue();
        capturedHandover.StudentAcceptedTime.Should().NotBeNull();
        capturedHandover.TotalCompensation.Should().Be(120000m);
        capturedItems.Should().HaveCount(1);
        capturedMeters.Should().HaveCount(2);
        capturedLog.Should().NotBeNull();
        capturedLog!.RequestId.Should().Be(77);
        result.HandoverCompleted.Should().BeTrue();
        result.Handover!.TotalCompensation.Should().Be(120000m);
    }

    [Fact]
    public async Task ProcessRequestAsync_BedTransfer_CompleteStep2WithoutHandover_ThrowsInvalidOperationException()
    {
        var req = CreateBedTransferRequestGraph("Approved");
        req.Handover = null;
        _requestRepoMock.Setup(r => r.GetRequestByIdAsync(77)).ReturnsAsync(req);

        var sut = CreateSut();
        var act = async () => await sut.ProcessRequestAsync(77, 99, new ProcessRequestDto
        {
            Decision = "Completed"
        });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*biên bản kiểm tra*");
    }

    [Fact]
    public async Task ProcessRequestAsync_BedTransfer_CompleteStep2_IssuesInvoicesAndUpdatesResidency()
    {
        var req = CreateBedTransferRequestGraph("Approved");
        var bt = req.BedTransferRequest!;
        bt.OldBedDailyRate = 30000m;
        bt.NewBedDailyRate = 40000m;
        bt.DaysStayed = 61;
        bt.DaysRemaining = 60;
        bt.AmountAlreadyPaid = 3630000m; // TotalAmount = 1,830,000 + 2,400,000 = 4,230,000 -> Diff = +600,000
        bt.MeterReadings.Add(new MeterReading { Id = 1, RoomId = 1, TransferRequestId = 77, MeterType = "Electricity", PreviousValue = 100m, CurrentValue = 120m, Source = "Handover", Status = "Recorded", RecordedTime = DateTime.UtcNow });

        var catalog = new EquipmentCatalog { Id = 1, Category = "Nội thất", Name = "Bàn học gỗ" };
        var equip = new Equipment { Id = 501, RoomId = 1, CatalogId = 1, AssetCode = "EQ-501", Condition = "Broken", Catalog = catalog };
        req.Handover = new Handover
        {
            Id = 300,
            HandoverType = "BedTransfer",
            RequestId = 77,
            RoomId = 1,
            GuardId = 201,
            HasDamageOrMissingFlag = true,
            TotalCompensation = 120000m,
            Status = "Completed",
            StudentAcceptedFlag = true,
            StudentAcceptedTime = DateTime.UtcNow,
            HandoverItems = new List<HandoverItem>
            {
                new() { Id = 1, HandoverId = 300, EquipmentId = 501, Equipment = equip, ItemCondition = "Damaged", CompensationAmount = 120000m }
            }
        };

        _requestRepoMock.Setup(r => r.GetRequestByIdAsync(77)).ReturnsAsync(req);

        Residency? capturedNewResidency = null;
        List<Invoice>? capturedInvoices = null;
        Notification? capturedNoti = null;
        List<Notification>? capturedInvoiceNotis = null;
        _requestRepoMock.Setup(r => r.CompleteBedTransferAsync(
                It.IsAny<Request>(),
                It.IsAny<BedTransferRequest>(),
                100,
                1,
                2,
                It.IsAny<DateOnly>(),
                It.IsAny<Residency>(),
                It.IsAny<List<Invoice>>(),
                300,
                99,
                It.IsAny<string?>(),
                It.IsAny<RequestLog>(),
                It.IsAny<Notification>(),
                It.IsAny<List<Notification>?>()))
            .Callback<Request, BedTransferRequest, int, int, int, DateOnly, Residency, List<Invoice>, int?, int, string?, RequestLog, Notification, List<Notification>?>(
                (_, _, _, _, _, _, newRes, invoices, _, _, _, _, noti, invNotis) =>
                {
                    capturedNewResidency = newRes;
                    capturedInvoices = invoices;
                    capturedNoti = noti;
                    capturedInvoiceNotis = invNotis;
                })
            .Returns(Task.CompletedTask);

        var sut = CreateSut();
        var result = await sut.ProcessRequestAsync(77, 99, new ProcessRequestDto
        {
            Decision = "Completed",
            Note = "Đã xuất hóa đơn và hoàn tất chuyển giường"
        });

        result.Status.Should().Be("Completed");
        capturedNewResidency.Should().NotBeNull();
        capturedNewResidency!.RegistrationType.Should().Be("Transfer");
        capturedNewResidency.Status.Should().Be("Active");
        capturedNewResidency.BedId.Should().Be(2);

        capturedInvoices.Should().NotBeNull();
        capturedInvoices.Should().HaveCount(2);
        capturedInvoices!.Should().Contain(i => i.InvoiceType == "BedTransfer" && i.TransferRequestId == 77 && i.HandoverId == null && i.Amount == 660000m);
        capturedInvoices.Should().Contain(i => i.InvoiceType == "Compensation" && i.HandoverId == 300 && i.TransferRequestId == null && i.Amount == 120000m);
        capturedNoti.Should().NotBeNull();
        capturedInvoiceNotis.Should().NotBeNull();
        capturedInvoiceNotis.Should().HaveCount(2);
        capturedInvoiceNotis!.Should().OnlyContain(n => n.UserId == req.StudentId && n.ReferenceType == "Invoice" && n.Content != null && n.Content.Contains("Thanh toán hóa đơn"));
        _emailSenderMock.Verify(e => e.SendEmailAsync(req.Student.User.Email, It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task ProcessRequestAsync_BedTransfer_CompleteStep2_WhenNoDebt_CreatesNoInvoiceNotifications()
    {
        var req = CreateBedTransferRequestGraph("Approved");
        var bt = req.BedTransferRequest!;
        bt.OldBedDailyRate = 40000m;
        bt.NewBedDailyRate = 30000m;
        bt.DaysStayed = 61;
        bt.DaysRemaining = 60;
        bt.AmountAlreadyPaid = 4840000m; // Prorate âm (hoàn/cấn trừ), không phát sinh hóa đơn
        req.Handover = new Handover
        {
            Id = 305,
            HandoverType = "BedTransfer",
            RequestId = 77,
            RoomId = 1,
            GuardId = 201,
            HasDamageOrMissingFlag = false,
            TotalCompensation = 0m,
            Status = "Completed",
            StudentAcceptedFlag = true,
            StudentAcceptedTime = DateTime.UtcNow
        };

        _requestRepoMock.Setup(r => r.GetRequestByIdAsync(77)).ReturnsAsync(req);

        List<Invoice>? capturedInvoices = null;
        List<Notification>? capturedInvoiceNotis = null;
        _requestRepoMock.Setup(r => r.CompleteBedTransferAsync(
                It.IsAny<Request>(),
                It.IsAny<BedTransferRequest>(),
                100,
                1,
                2,
                It.IsAny<DateOnly>(),
                It.IsAny<Residency>(),
                It.IsAny<List<Invoice>>(),
                305,
                99,
                It.IsAny<string?>(),
                It.IsAny<RequestLog>(),
                It.IsAny<Notification>(),
                It.IsAny<List<Notification>?>()))
            .Callback<Request, BedTransferRequest, int, int, int, DateOnly, Residency, List<Invoice>, int?, int, string?, RequestLog, Notification, List<Notification>?>(
                (_, _, _, _, _, _, _, invoices, _, _, _, _, _, invNotis) =>
                {
                    capturedInvoices = invoices;
                    capturedInvoiceNotis = invNotis;
                })
            .Returns(Task.CompletedTask);

        var sut = CreateSut();
        var result = await sut.ProcessRequestAsync(77, 99, new ProcessRequestDto { Decision = "Completed" });

        result.Status.Should().Be("Completed");
        capturedInvoices.Should().BeEmpty();
        capturedInvoiceNotis.Should().BeEmpty();
    }

    [Fact]
    public async Task SubmitTransferInspectionAsync_WithValidEvidenceUrls_SavesAndReturnsEvidenceImageUrls()
    {
        const string elecUrl = "https://res.cloudinary.com/demo-cloud/image/upload/v1/meter-readings/elec-transfer.jpg";
        const string waterUrl = "https://res.cloudinary.com/demo-cloud/image/upload/v1/meter-readings/water-transfer.jpg";

        var req = CreateBedTransferRequestGraph("Approved");
        req.ProcessedById = 99;
        var guardUser = new User { Id = 201, Code = "GRD01", FullName = "Tran Bao Ve", Gender = "Male", AccountStatus = "Active" };
        req.BedTransferRequest!.AssignedGuardId = 201;
        req.BedTransferRequest.AssignedGuard = new Guard { UserId = 201, DormId = 1, User = guardUser };

        _requestRepoMock.Setup(r => r.GetRequestByIdAsync(77)).ReturnsAsync(req);
        _requestRepoMock.Setup(r => r.GetLatestRoomMeterReadingsAsync(1))
            .ReturnsAsync(new Dictionary<string, decimal> { ["Electricity"] = 1000m, ["Water"] = 40m });
        _requestRepoMock.Setup(r => r.GetRoomAndBedEquipmentsAsync(1, 1)).ReturnsAsync(new List<Equipment>());
        _requestRepoMock.Setup(r => r.GetUtilityPricesByTermAsync(1))
            .ReturnsAsync(new Dictionary<string, decimal> { ["Electricity"] = 3000m, ["Water"] = 15000m });
        _requestRepoMock.Setup(r => r.GetActiveRoomResidentCountAsync(1, 1)).ReturnsAsync(2);

        List<MeterReading>? capturedMeters = null;
        _requestRepoMock.Setup(r => r.SubmitTransferInspectionAsync(
                req,
                It.IsAny<Handover>(),
                It.IsAny<List<HandoverItem>>(),
                It.IsAny<List<MeterReading>>(),
                It.IsAny<List<(int, string, string?)>>(),
                It.IsAny<RequestLog>(),
                It.IsAny<List<Notification>>()))
            .Callback<Request, Handover, List<HandoverItem>, List<MeterReading>, List<(int, string, string?)>, RequestLog, List<Notification>>(
                (_, h, _, meters, _, _, _) =>
                {
                    h.Id = 301;
                    req.Handover = h;
                    req.BedTransferRequest!.MeterReadings = meters;
                    capturedMeters = meters;
                })
            .Returns(Task.CompletedTask);

        var sut = CreateSut();
        var result = await sut.SubmitTransferInspectionAsync(77, 201, new SubmitTransferInspectionDto
        {
            CurrentElectricityReading = 1060m,
            CurrentWaterReading = 45m,
            ElectricityEvidenceImageUrl = elecUrl,
            WaterEvidenceImageUrl = waterUrl,
            Items = new List<TransferInspectionItemInputDto>()
        });

        capturedMeters.Should().NotBeNull();
        capturedMeters!.Should().ContainSingle(m => m.MeterType == "Electricity" && m.EvidenceImageUrl == elecUrl && m.Source == "Handover");
        capturedMeters.Should().ContainSingle(m => m.MeterType == "Water" && m.EvidenceImageUrl == waterUrl && m.Source == "Handover");
        result.MeterReadings.Should().ContainSingle(m => m.MeterType == "Electricity" && m.EvidenceImageUrl == elecUrl);
        result.MeterReadings.Should().ContainSingle(m => m.MeterType == "Water" && m.EvidenceImageUrl == waterUrl);
    }

    [Fact]
    public async Task SubmitTransferInspectionAsync_WithInvalidEvidenceUrl_ThrowsInvalidOperationException()
    {
        var req = CreateBedTransferRequestGraph("Approved");
        req.BedTransferRequest!.AssignedGuardId = 201;

        _requestRepoMock.Setup(r => r.GetRequestByIdAsync(77)).ReturnsAsync(req);
        _requestRepoMock.Setup(r => r.GetLatestRoomMeterReadingsAsync(1))
            .ReturnsAsync(new Dictionary<string, decimal> { ["Electricity"] = 1000m, ["Water"] = 40m });

        var sut = CreateSut();
        var act = () => sut.SubmitTransferInspectionAsync(77, 201, new SubmitTransferInspectionDto
        {
            CurrentElectricityReading = 1060m,
            CurrentWaterReading = 45m,
            ElectricityEvidenceImageUrl = "javascript:alert(1)",
            Items = new List<TransferInspectionItemInputDto>()
        });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Đường dẫn ảnh minh chứng*");
    }
}
