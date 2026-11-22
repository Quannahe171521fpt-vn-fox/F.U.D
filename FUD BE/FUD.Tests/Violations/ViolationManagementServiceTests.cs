using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using FUD.DTOs.Violations;
using FUD.Infrastructure.Email;
using FUD.Infrastructure.Storage;
using FUD.Models;
using FUD.Repositories.Interfaces.Violations;
using FUD.Services.Violations;
using Moq;
using Xunit;

namespace FUD.Tests.Violations;

public class ViolationManagementServiceTests
{
    private readonly Mock<IViolationRepository> _violationRepoMock = new();
    private readonly Mock<IFileStorage> _fileStorageMock = new();
    private readonly Mock<IEmailSender> _emailSenderMock = new();

    private ViolationService CreateSut() => new(
        _violationRepoMock.Object,
        _fileStorageMock.Object,
        _emailSenderMock.Object);

    [Fact]
    public async Task GetViolationsAsync_ManagerQueriesAllViolations_AutoFinalizesExpiredViolationsWithoutComplaints()
    {
        // Arrange
        var sut = CreateSut();
        var now = DateTime.UtcNow;

        var vActive = new Violation
        {
            Id = 1,
            Code = "VIO-001",
            Status = "Recorded",
            InitialLevel = "Cảnh cáo",
            RequestDeadline = now.AddHours(12),
            ComplaintRequests = new List<ComplaintRequest>()
        };

        var vExpired = new Violation
        {
            Id = 2,
            Code = "VIO-002",
            Status = "Recorded",
            InitialLevel = "Đình chỉ nội trú",
            RequestDeadline = now.AddHours(-2), // Overdue
            ComplaintRequests = new List<ComplaintRequest>(),
            StudentId = 20,
            EvaluatedById = 5
        };

        var studentUser = new User
        {
            Id = 20,
            AccountStatus = "Active"
        };

        _violationRepoMock.Setup(r => r.GetViolationsAsync(null, null, null, null))
            .ReturnsAsync(new List<Violation> { vActive, vExpired });
        _violationRepoMock.Setup(r => r.GetUserByIdAsync(20)).ReturnsAsync(studentUser);
        _violationRepoMock.Setup(r => r.UpdateViolationAsync(It.IsAny<Violation>())).Returns(Task.CompletedTask);
        _violationRepoMock.Setup(r => r.UpdateUserAsync(It.IsAny<User>())).Returns(Task.CompletedTask);

        // Act
        var result = await sut.GetViolationsAsync();

        // Assert
        result.Should().HaveCount(2);

        // vExpired should be auto finalized
        vExpired.Status.Should().Be("Finalized");
        vExpired.FinalLevel.Should().Be("Đình chỉ nội trú");
        vExpired.FinalizedTime.Should().NotBeNull();
        _violationRepoMock.Verify(r => r.UpdateViolationAsync(vExpired), Times.Once);

        // Student should be suspended because punishment contains "Đình chỉ"
        studentUser.AccountStatus.Should().Be("Suspended");
        studentUser.SuspendedById.Should().Be(5);
        _violationRepoMock.Verify(r => r.UpdateUserAsync(studentUser), Times.Once);
    }

    [Fact]
    public async Task GetViolationsAsync_WhenGuardHasNoDormAssigned_ReturnsEmptyList()
    {
        // Arrange
        var sut = CreateSut();
        var guardUserId = 12;

        _violationRepoMock.Setup(r => r.GetGuardDormIdAsync(guardUserId)).ReturnsAsync((int?)null);

        // Act
        var result = await sut.GetViolationsAsync(guardUserId: guardUserId);

        // Assert
        result.Should().BeEmpty();
        _violationRepoMock.Verify(r => r.GetViolationsAsync(It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<string?>(), It.IsAny<int?>()), Times.Never);
    }

    [Fact]
    public async Task GetViolationByIdAsync_WhenViolationExists_ReturnsViolationWithPriorCount()
    {
        // Arrange
        var sut = CreateSut();
        var violation = new Violation
        {
            Id = 10,
            Code = "VIO-10",
            StudentId = 50,
            Status = "Recorded",
            Description = "Nấu ăn trong phòng",
            ViolationEvidences = new List<ViolationEvidence>()
        };

        _violationRepoMock.Setup(r => r.GetViolationByIdAsync(10)).ReturnsAsync(violation);
        _violationRepoMock.Setup(r => r.GetStudentPriorViolationsCountAsync(50, 10)).ReturnsAsync(2);

        // Act
        var result = await sut.GetViolationByIdAsync(10);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(10);
        result.Code.Should().Be("VIO-10");
        result.PriorViolationsCount.Should().Be(2);
    }

    [Fact]
    public async Task GetViolationByIdAsync_WhenViolationNotFound_ReturnsNull()
    {
        // Arrange
        var sut = CreateSut();
        _violationRepoMock.Setup(r => r.GetViolationByIdAsync(999)).ReturnsAsync((Violation?)null);

        // Act
        var result = await sut.GetViolationByIdAsync(999);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task EvaluateViolationAsync_ValidLevel_SetsInitialLevelAnd24hDeadlineAndSendsEmail()
    {
        // Arrange
        var sut = CreateSut();
        var studentUser = new User { Id = 101, FullName = "Nguyen Van A", Email = "se101@fpt.edu.vn" };
        var student = new Student { UserId = 101, User = studentUser };
        var violationType = new ViolationType { Id = 1, Name = "Sử dụng thiết bị điện công suất lớn" };

        var violation = new Violation
        {
            Id = 15,
            Code = "VIO-15",
            StudentId = 101,
            Student = student,
            ViolationType = violationType,
            Status = "Recorded",
            OccurrenceTime = DateTime.UtcNow.AddDays(-1),
            ViolationEvidences = new List<ViolationEvidence>()
        };

        _violationRepoMock.Setup(r => r.GetViolationByIdAsync(15)).ReturnsAsync(violation);
        _violationRepoMock.Setup(r => r.UpdateViolationAsync(It.IsAny<Violation>())).Returns(Task.CompletedTask);

        var request = new EvaluateViolationRequestDto
        {
            InitialLevel = "Cảnh cáo cấp độ 1",
            ManagerNote = "Tái phạm sẽ đình chỉ"
        };

        // Act
        var result = await sut.EvaluateViolationAsync(15, managerUserId: 2, request);

        // Assert
        result.Should().NotBeNull();
        violation.EvaluatedById.Should().Be(2);
        violation.InitialLevel.Should().Be("Cảnh cáo cấp độ 1");
        violation.ManagerNote.Should().Be("Tái phạm sẽ đình chỉ");
        violation.RequestDeadline.Should().NotBeNull();
        violation.RequestDeadline!.Value.Should().BeAfter(DateTime.UtcNow.AddHours(23));

        _violationRepoMock.Verify(r => r.UpdateViolationAsync(violation), Times.Once);
        _emailSenderMock.Verify(e => e.SendEmailAsync(
            "se101@fpt.edu.vn",
            It.Is<string>(s => s.Contains("VIO-15")),
            It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task EvaluateViolationAsync_DismissedAction_SetsStatusDismissedAndNotifiesStudent()
    {
        // Arrange
        var sut = CreateSut();
        var studentUser = new User { Id = 102, FullName = "Tran Van B", Email = "se102@fpt.edu.vn" };
        var student = new Student { UserId = 102, User = studentUser };

        var violation = new Violation
        {
            Id = 16,
            Code = "VIO-16",
            StudentId = 102,
            Student = student,
            Status = "Recorded",
            ViolationEvidences = new List<ViolationEvidence>()
        };

        _violationRepoMock.Setup(r => r.GetViolationByIdAsync(16)).ReturnsAsync(violation);
        _violationRepoMock.Setup(r => r.UpdateViolationAsync(It.IsAny<Violation>())).Returns(Task.CompletedTask);

        var request = new EvaluateViolationRequestDto
        {
            InitialLevel = "Bác bỏ",
            ManagerNote = "Hình ảnh minh chứng không rõ mặt sinh viên"
        };

        // Act
        var result = await sut.EvaluateViolationAsync(16, managerUserId: 2, request);

        // Assert
        result.Should().NotBeNull();
        violation.Status.Should().Be("Dismissed");
        violation.ManagerNote.Should().Be("Hình ảnh minh chứng không rõ mặt sinh viên");
        violation.FinalizedTime.Should().NotBeNull();

        _violationRepoMock.Verify(r => r.UpdateViolationAsync(violation), Times.Once);
        _emailSenderMock.Verify(e => e.SendEmailAsync(
            "se102@fpt.edu.vn",
            It.Is<string>(s => s.Contains("bác bỏ biên bản")),
            It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task EvaluateViolationAsync_DismissedWithoutManagerNote_ThrowsArgumentException()
    {
        // Arrange
        var sut = CreateSut();
        var violation = new Violation { Id = 17, Status = "Recorded" };
        _violationRepoMock.Setup(r => r.GetViolationByIdAsync(17)).ReturnsAsync(violation);

        var request = new EvaluateViolationRequestDto
        {
            InitialLevel = "Bác bỏ",
            ManagerNote = "   " // Empty note
        };

        // Act
        var act = async () => await sut.EvaluateViolationAsync(17, managerUserId: 2, request);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Vui lòng nhập lý do bác bỏ*");
        _violationRepoMock.Verify(r => r.UpdateViolationAsync(It.IsAny<Violation>()), Times.Never);
    }

    [Fact]
    public async Task EvaluateViolationAsync_WhenViolationNotFound_ThrowsArgumentException()
    {
        // Arrange
        var sut = CreateSut();
        _violationRepoMock.Setup(r => r.GetViolationByIdAsync(999)).ReturnsAsync((Violation?)null);

        var request = new EvaluateViolationRequestDto { InitialLevel = "Cảnh cáo" };

        // Act
        var act = async () => await sut.EvaluateViolationAsync(999, managerUserId: 2, request);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Không tìm thấy hồ sơ vi phạm*");
    }

    [Fact]
    public async Task UnsuspendStudentAsync_WhenStudentExists_RestoresActiveStatusAndSendsEmail()
    {
        // Arrange
        var sut = CreateSut();
        var studentUser = new User
        {
            Id = 300,
            FullName = "Nguyen Suspended",
            Email = "suspended@fpt.edu.vn",
            AccountStatus = "Suspended",
            SuspendedAt = DateTime.UtcNow.AddDays(-10),
            SuspensionReason = "Đình chỉ nội trú do vi phạm"
        };

        _violationRepoMock.Setup(r => r.GetUserByIdAsync(300)).ReturnsAsync(studentUser);
        _violationRepoMock.Setup(r => r.UpdateUserAsync(studentUser)).Returns(Task.CompletedTask);

        // Act
        await sut.UnsuspendStudentAsync(300, managerUserId: 2, note: "Đã hoàn thành kiểm điểm");

        // Assert
        studentUser.AccountStatus.Should().Be("Active");
        studentUser.SuspendedAt.Should().BeNull();
        studentUser.SuspensionReason.Should().BeNull();

        _violationRepoMock.Verify(r => r.UpdateUserAsync(studentUser), Times.Once);
        _emailSenderMock.Verify(e => e.SendEmailAsync(
            "suspended@fpt.edu.vn",
            It.Is<string>(s => s.Contains("Mở khóa quyền nội trú")),
            It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task UnsuspendStudentAsync_WhenStudentNotFound_ThrowsArgumentException()
    {
        // Arrange
        var sut = CreateSut();
        _violationRepoMock.Setup(r => r.GetUserByIdAsync(404)).ReturnsAsync((User?)null);

        // Act
        var act = async () => await sut.UnsuspendStudentAsync(404, managerUserId: 2);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Không tìm thấy thông tin sinh viên*");
    }

    [Fact]
    public async Task GetViolationByIdAsync_WhenStudentRequestsOtherStudentViolation_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var sut = CreateSut();
        var violation = new Violation
        {
            Id = 50,
            Code = "VIO-50",
            StudentId = 100,
            Status = "Recorded",
            ViolationEvidences = new List<ViolationEvidence>()
        };
        _violationRepoMock.Setup(r => r.GetViolationByIdAsync(50)).ReturnsAsync(violation);

        // Act
        var act = () => sut.GetViolationByIdAsync(50, callerUserId: 200, isManagerOrAdmin: false, isGuard: false);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task GetViolationByIdAsync_WhenGuardRequestsViolationInOtherDorm_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var sut = CreateSut();
        var violation = new Violation
        {
            Id = 51,
            Code = "VIO-51",
            StudentId = 100,
            Room = new Room { Id = 10, Block = new Block { Id = 2, DormId = 2 } },
            Status = "Recorded",
            ViolationEvidences = new List<ViolationEvidence>()
        };
        _violationRepoMock.Setup(r => r.GetViolationByIdAsync(51)).ReturnsAsync(violation);
        _violationRepoMock.Setup(r => r.GetGuardDormIdAsync(77)).ReturnsAsync(1);

        // Act
        var act = () => sut.GetViolationByIdAsync(51, callerUserId: 77, isManagerOrAdmin: false, isGuard: true);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }
}
