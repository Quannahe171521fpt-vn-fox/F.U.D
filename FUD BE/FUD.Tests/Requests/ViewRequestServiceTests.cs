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
using FUD.Services.Requests;
using Moq;
using Xunit;

namespace FUD.Tests.Requests;

public class ViewRequestServiceTests
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

    [Fact]
    public async Task GetAllRequestsAsync_ManagerFiltersByTypeAndStatus_ReturnsMappedRequestList()
    {
        // Arrange
        var sut = CreateSut();
        var studentUser = new User { Id = 10, Code = "SE160010", FullName = "Nguyen Van A" };
        var student = new Student { UserId = 10, User = studentUser };

        var requests = new List<Request>
        {
            new()
            {
                Id = 1,
                StudentId = 10,
                Student = student,
                RequestType = "BedTransfer",
                Status = "Pending",
                Reason = "Muốn chuyển sang tầng thấp hơn",
                CreatedTime = DateTime.UtcNow.AddHours(-5)
            },
            new()
            {
                Id = 2,
                StudentId = 10,
                Student = student,
                RequestType = "BedTransfer",
                Status = "Approved",
                Reason = "Cùng phòng bạn thân",
                CreatedTime = DateTime.UtcNow.AddDays(-1)
            }
        };

        _requestRepoMock.Setup(r => r.GetAllRequestsAsync("BedTransfer", "Pending"))
            .ReturnsAsync(new List<Request> { requests[0] });

        // Act
        var result = await sut.GetAllRequestsAsync("BedTransfer", "Pending");

        // Assert
        result.Should().HaveCount(1);
        var item = result.First();
        item.Id.Should().Be(1);
        item.RequestType.Should().Be("BedTransfer");
        item.Status.Should().Be("Pending");
        item.StudentName.Should().Be("Nguyen Van A");
        item.StudentCode.Should().Be("SE160010");
    }

    [Fact]
    public async Task GetStudentRequestsAsync_StudentQueriesTheirOwnRequests_ReturnsCorrectList()
    {
        // Arrange
        var sut = CreateSut();
        var studentId = 25;
        var studentUser = new User { Id = 25, Code = "SE160025", FullName = "Le Thi C" };
        var student = new Student { UserId = 25, User = studentUser };

        var requests = new List<Request>
        {
            new()
            {
                Id = 5,
                StudentId = studentId,
                Student = student,
                RequestType = "Complaint",
                Status = "Pending",
                Reason = "Kháng cáo biên bản vi phạm",
                CreatedTime = DateTime.UtcNow.AddDays(-2)
            },
            new()
            {
                Id = 6,
                StudentId = studentId,
                Student = student,
                RequestType = "Inspection",
                Status = "Approved",
                Reason = "Kiểm tra cơ sở vật chất phòng",
                CreatedTime = DateTime.UtcNow.AddDays(-10)
            }
        };

        _requestRepoMock.Setup(r => r.GetStudentRequestsAsync(studentId))
            .ReturnsAsync(requests);

        // Act
        var result = await sut.GetStudentRequestsAsync(studentId);

        // Assert
        result.Should().HaveCount(2);
        result.Select(r => r.Id).Should().Contain(new[] { 5, 6 });
        result.All(r => r.StudentId == studentId).Should().BeTrue();
    }

    [Fact]
    public async Task GetBedTransferContextAsync_EligibleStudent_ReturnsResidencyAndAvailableBeds()
    {
        // Arrange
        var sut = CreateSut();
        var studentId = 30;
        var studentUser = new User
        {
            Id = 30,
            Code = "SE160030",
            Gender = "Male",
            AccountStatus = "Active"
        };

        var dorm = new Dorm { Id = 1, Name = "Dorm A", Code = "A" };
        var block = new Block { Id = 1, DormId = 1, Name = "Block A1", Code = "A1", GenderType = "Male", Dorm = dorm };
        var roomType = new RoomType { Id = 1, Name = "Phòng 4 giường", BedCapacity = 4 };
        var room = new Room { Id = 1, BlockId = 1, RoomNumber = "101", FloorLevel = 1, RoomTypeId = 1, Block = block, RoomType = roomType };
        var currentBed = new Bed { Id = 100, RoomId = 1, BedCode = "101-1", PositionInRoom = "Dưới trái", Room = room };

        var term = new Term { Id = 1, Name = "Fall 2026", AcademicYear = "2026-2027", StartDate = new DateOnly(2026, 9, 1), EndDate = new DateOnly(2026, 12, 31) };

        var activeResidency = new Residency
        {
            Id = 50,
            StudentId = studentId,
            TermId = 1,
            BedId = 100,
            Bed = currentBed,
            Term = term,
            CheckInDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-15))
        };

        var targetBed = new Bed
        {
            Id = 102,
            BedCode = "102-1",
            PositionInRoom = "Dưới phải",
            RoomId = 2,
            Room = new Room { Id = 2, BlockId = 1, RoomNumber = "102", FloorLevel = 1, RoomTypeId = 1, Block = block, RoomType = roomType }
        };

        _requestRepoMock.Setup(r => r.GetUserByIdAsync(studentId)).ReturnsAsync(studentUser);
        _requestRepoMock.Setup(r => r.GetStudentActiveResidencyAsync(studentId)).ReturnsAsync(activeResidency);
        _requestRepoMock.Setup(r => r.GetPublishedRoomPricesAsync(1)).ReturnsAsync(new Dictionary<(int, int), decimal> { { (1, 1), 3500000 } });
        _requestRepoMock.Setup(r => r.HasOverdueInvoicesAsync(studentId)).ReturnsAsync(false);
        _requestRepoMock.Setup(r => r.HasPendingBedTransferAsync(studentId, 1)).ReturnsAsync(false);
        _requestRepoMock.Setup(r => r.GetAvailableBedsForTransferAsync("Male", 100, 1))
            .ReturnsAsync(new List<Bed> { targetBed });

        // Act
        var result = await sut.GetBedTransferContextAsync(studentId);

        // Assert
        result.Should().NotBeNull();
        result.Eligible.Should().BeTrue();
        result.CurrentResidency.Should().NotBeNull();
        result.CurrentResidency!.BedCode.Should().Be("101-1");
        result.CurrentResidency.RoomNumber.Should().Be("101");
        result.AvailableBeds.Should().HaveCount(1);
        result.AvailableBeds.First().BedCode.Should().Be("102-1");
    }

    [Fact]
    public async Task GetBedTransferContextAsync_SuspendedStudent_ReturnsIneligibleWithReason()
    {
        // Arrange
        var sut = CreateSut();
        var studentId = 35;
        var suspendedUser = new User
        {
            Id = 35,
            AccountStatus = "Suspended",
            SuspensionReason = "Đình chỉ do vi phạm quy chế"
        };
        _requestRepoMock.Setup(r => r.GetUserByIdAsync(studentId)).ReturnsAsync(suspendedUser);

        // Act
        var result = await sut.GetBedTransferContextAsync(studentId);

        // Assert
        result.Should().NotBeNull();
        result.Eligible.Should().BeFalse();
        result.IneligibleReason.Should().Contain("bị đình chỉ nội trú");
    }

    [Fact]
    public async Task GetBedTransferContextAsync_NoActiveResidency_ReturnsIneligible()
    {
        // Arrange
        var sut = CreateSut();
        var studentId = 36;
        var activeUser = new User { Id = 36, AccountStatus = "Active" };
        _requestRepoMock.Setup(r => r.GetUserByIdAsync(studentId)).ReturnsAsync(activeUser);
        _requestRepoMock.Setup(r => r.GetStudentActiveResidencyAsync(studentId)).ReturnsAsync((Residency?)null);

        // Act
        var result = await sut.GetBedTransferContextAsync(studentId);

        // Assert
        result.Should().NotBeNull();
        result.Eligible.Should().BeFalse();
        result.IneligibleReason.Should().Contain("chưa có đợt lưu trú nào đang hoạt động");
    }
}
