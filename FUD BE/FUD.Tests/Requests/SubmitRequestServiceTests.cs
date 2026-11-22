using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using FUD.DTOs.Requests;
using FUD.Infrastructure.Email;
using FUD.Infrastructure.Storage;
using FUD.Models;
using FUD.Repositories.Interfaces.Requests;
using FUD.Repositories.Interfaces.Violations;
using FUD.Services.Requests;
using Microsoft.AspNetCore.Http;
using Moq;
using Xunit;

namespace FUD.Tests.Requests;

public class SubmitRequestServiceTests
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

    private static IFormFile CreateMockFormFile(string fileName = "evidence.jpg", string content = "fake image content")
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        var stream = new MemoryStream(bytes);
        return new FormFile(stream, 0, bytes.Length, "Evidences", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/jpeg"
        };
    }

    [Fact]
    public async Task CreateRequestAsync_WhenStudentSuspended_ThrowsInvalidOperationException()
    {
        // Arrange
        var sut = CreateSut();
        var studentId = 10;
        var suspendedUser = new User
        {
            Id = 10,
            AccountStatus = "Suspended",
            SuspensionReason = "Kỷ luật nội trú"
        };
        _requestRepoMock.Setup(r => r.GetUserByIdAsync(studentId)).ReturnsAsync(suspendedUser);

        var dto = new CreateRequestDto
        {
            RequestType = "Complaint",
            Content = "Khiếu nại quyết định kỷ luật"
        };

        // Act
        var act = async () => await sut.CreateRequestAsync(studentId, dto);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*ĐÌNH CHỈ NỘI TRÚ*");
        _requestRepoMock.Verify(r => r.CreateRequestAsync(It.IsAny<Request>()), Times.Never);
    }

    [Fact]
    public async Task CreateRequestAsync_ComplaintRequestWithEvidences_SavesRequestAndComplaintDetails()
    {
        // Arrange
        var sut = CreateSut();
        var studentId = 11;
        var activeUser = new User { Id = 11, AccountStatus = "Active" };
        var activeTerm = new Term { Id = 5, Name = "Fall 2026" };

        _requestRepoMock.Setup(r => r.GetUserByIdAsync(studentId)).ReturnsAsync(activeUser);
        _requestRepoMock.Setup(r => r.GetCurrentActiveTermAsync()).ReturnsAsync(activeTerm);
        _violationRepoMock.Setup(r => r.GetViolationByIdAsync(100))
            .ReturnsAsync(new Violation { Id = 100, StudentId = studentId });

        var file = CreateMockFormFile("complaint.jpg");
        var dto = new CreateRequestDto
        {
            RequestType = "Complaint",
            TargetType = "Violation",
            TargetViolationId = 100,
            Content = "Tôi không có mặt tại phòng vào thời điểm xảy ra vụ việc.",
            Evidences = new List<IFormFile> { file }
        };

        var uploadedUrls = new List<string> { "https://cloudinary.com/complaint/evidence1.jpg" };
        _fileStorageMock.Setup(f => f.UploadImagesAsync(dto.Evidences, "complaint-evidences"))
            .ReturnsAsync(uploadedUrls);

        _requestRepoMock.Setup(r => r.CreateRequestAsync(It.IsAny<Request>()))
            .ReturnsAsync((Request req) => { req.Id = 77; return req; });
        _requestRepoMock.Setup(r => r.AddComplaintRequestAsync(It.IsAny<ComplaintRequest>(), It.IsAny<List<ComplaintEvidence>>()))
            .ReturnsAsync((ComplaintRequest cr, IEnumerable<ComplaintEvidence> evs) => cr);

        var reloadedRequest = new Request
        {
            Id = 77,
            StudentId = 11,
            TermId = 5,
            RequestType = "Complaint",
            Status = "Pending",
            Reason = dto.Content,
            CreatedTime = DateTime.UtcNow
        };
        _requestRepoMock.Setup(r => r.GetRequestByIdAsync(77)).ReturnsAsync(reloadedRequest);

        // Act
        var result = await sut.CreateRequestAsync(studentId, dto);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(77);
        result.RequestType.Should().Be("Complaint");
        result.Status.Should().Be("Pending");

        _requestRepoMock.Verify(r => r.CreateRequestAsync(It.Is<Request>(req =>
            req.StudentId == 11 &&
            req.TermId == 5 &&
            req.RequestType == "Complaint" &&
            req.Status == "Pending")), Times.Once);

        _fileStorageMock.Verify(f => f.UploadImagesAsync(dto.Evidences, "complaint-evidences"), Times.Once);

        _requestRepoMock.Verify(r => r.AddComplaintRequestAsync(
            It.Is<ComplaintRequest>(c => c.RequestId == 77 && c.ViolationId == 100 && c.TargetType == "Violation"),
            It.Is<List<ComplaintEvidence>>(evs => evs.Count == 1 && evs[0].FileUrl == uploadedUrls[0])),
            Times.Once);
    }

    [Fact]
    public async Task CreateRequestAsync_ValidBedTransferRequest_CreatesBedTransferAndUpdatesBedStatus()
    {
        // Arrange
        var sut = CreateSut();
        var studentId = 12;
        var activeUser = new User { Id = 12, Gender = "Male", AccountStatus = "Active" };

        var dorm = new Dorm { Id = 1, Name = "Dorm A", Status = "Active" };
        var block = new Block { Id = 1, Name = "Block A1", GenderType = "Male", Status = "Active", Dorm = dorm };
        var room = new Room { Id = 1, RoomNumber = "101", Status = "Active", Block = block };
        var currentBed = new Bed { Id = 10, BedCode = "101-1", Room = room };

        var term = new Term
        {
            Id = 2,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-10)),
            EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(80))
        };

        var residency = new Residency
        {
            Id = 50,
            StudentId = 12,
            TermId = 2,
            BedId = 10,
            Bed = currentBed,
            Term = term
        };

        var toRoom = new Room { Id = 2, RoomNumber = "102", Status = "Active", Block = block };
        var toBed = new Bed
        {
            Id = 20,
            BedCode = "102-1",
            Status = "Free",
            Room = toRoom
        };

        _requestRepoMock.Setup(r => r.GetUserByIdAsync(studentId)).ReturnsAsync(activeUser);
        _requestRepoMock.Setup(r => r.GetStudentActiveResidencyAsync(studentId)).ReturnsAsync(residency);
        _requestRepoMock.Setup(r => r.HasOverdueInvoicesAsync(studentId)).ReturnsAsync(false);
        _requestRepoMock.Setup(r => r.HasPendingBedTransferAsync(studentId, 2)).ReturnsAsync(false);
        _requestRepoMock.Setup(r => r.GetBedWithLocationByIdAsync(20)).ReturnsAsync(toBed);
        _requestRepoMock.Setup(r => r.IsBedAvailableForTransferAsync(20, 2)).ReturnsAsync(true);

        _requestRepoMock.Setup(r => r.CreateBedTransferRequestAsync(It.IsAny<Request>(), It.IsAny<BedTransferRequest>(), It.IsAny<RequestLog>()))
            .ReturnsAsync((Request req, BedTransferRequest bt, RequestLog log) => { req.Id = 88; return req; });

        var reloaded = new Request
        {
            Id = 88,
            StudentId = 12,
            TermId = 2,
            RequestType = "BedTransfer",
            Status = "Pending",
            Reason = "Chuyển gần cửa sổ",
            CreatedTime = DateTime.UtcNow
        };
        _requestRepoMock.Setup(r => r.GetRequestByIdAsync(88)).ReturnsAsync(reloaded);

        var dto = new CreateRequestDto
        {
            RequestType = "BedTransfer",
            ToBedId = 20,
            Reason = "Chuyển gần cửa sổ",
            TransferDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2))
        };

        // Act
        var result = await sut.CreateRequestAsync(studentId, dto);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(88);
        result.RequestType.Should().Be("BedTransfer");

        toBed.Status.Should().Be("Pending Transfer");
        _requestRepoMock.Verify(r => r.CreateBedTransferRequestAsync(
            It.Is<Request>(req => req.StudentId == 12 && req.RequestType == "BedTransfer"),
            It.Is<BedTransferRequest>(bt => bt.FromBedId == 10 && bt.ToBedId == 20),
            It.Is<RequestLog>(log => log.ToStatus == "Pending")), Times.Once);
    }

    [Fact]
    public async Task CreateRequestAsync_BedTransferWithNoTargetBed_ThrowsInvalidOperationException()
    {
        // Arrange
        var sut = CreateSut();
        var studentId = 14;
        var activeUser = new User { Id = 14, AccountStatus = "Active" };
        _requestRepoMock.Setup(r => r.GetUserByIdAsync(studentId)).ReturnsAsync(activeUser);

        var dto = new CreateRequestDto
        {
            RequestType = "BedTransfer",
            ToBedId = null,
            Reason = "Không chọn giường"
        };

        // Act
        var act = async () => await sut.CreateRequestAsync(studentId, dto);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Vui lòng chọn giường đích*");
    }

    [Fact]
    public async Task CreateRequestAsync_BedTransferWhenStudentHasOverdueInvoices_ThrowsInvalidOperationException()
    {
        // Arrange
        var sut = CreateSut();
        var studentId = 15;
        var activeUser = new User { Id = 15, AccountStatus = "Active" };
        var residency = new Residency { Id = 1, StudentId = 15, TermId = 1, BedId = 5 };

        _requestRepoMock.Setup(r => r.GetUserByIdAsync(studentId)).ReturnsAsync(activeUser);
        _requestRepoMock.Setup(r => r.GetStudentActiveResidencyAsync(studentId)).ReturnsAsync(residency);
        _requestRepoMock.Setup(r => r.HasOverdueInvoicesAsync(studentId)).ReturnsAsync(true);

        var dto = new CreateRequestDto
        {
            RequestType = "BedTransfer",
            ToBedId = 9,
            Reason = "Đổi giường"
        };

        // Act
        var act = async () => await sut.CreateRequestAsync(studentId, dto);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*hóa đơn quá hạn*");
    }

    [Fact]
    public async Task CreateRequestAsync_ComplaintForAnotherStudentViolation_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var sut = CreateSut();
        var studentId = 16;
        var otherStudentId = 99;
        var activeUser = new User { Id = studentId, AccountStatus = "Active" };
        _requestRepoMock.Setup(r => r.GetUserByIdAsync(studentId)).ReturnsAsync(activeUser);
        _violationRepoMock.Setup(r => r.GetViolationByIdAsync(500))
            .ReturnsAsync(new Violation { Id = 500, StudentId = otherStudentId });

        var dto = new CreateRequestDto
        {
            RequestType = "Complaint",
            TargetType = "Violation",
            TargetViolationId = 500,
            Content = "Khiếu nại biên bản của sinh viên khác",
            Evidences = new List<IFormFile> { CreateMockFormFile("idor.jpg") }
        };

        // Act
        var act = async () => await sut.CreateRequestAsync(studentId, dto);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("Bạn chỉ được khiếu nại biên bản vi phạm của chính mình.");
        _requestRepoMock.Verify(r => r.CreateRequestAsync(It.IsAny<Request>()), Times.Never);
        _fileStorageMock.Verify(
            f => f.UploadImagesAsync(It.IsAny<IEnumerable<IFormFile>>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateRequestAsync_ComplaintForMissingViolation_ThrowsArgumentException()
    {
        // Arrange
        var sut = CreateSut();
        var studentId = 17;
        var activeUser = new User { Id = studentId, AccountStatus = "Active" };
        _requestRepoMock.Setup(r => r.GetUserByIdAsync(studentId)).ReturnsAsync(activeUser);
        _violationRepoMock.Setup(r => r.GetViolationByIdAsync(999))
            .ReturnsAsync((Violation?)null);

        var dto = new CreateRequestDto
        {
            RequestType = "Complaint",
            TargetType = "Violation",
            TargetViolationId = 999,
            Content = "Khiếu nại biên bản không tồn tại",
            Evidences = new List<IFormFile> { CreateMockFormFile("missing.jpg") }
        };

        // Act
        var act = async () => await sut.CreateRequestAsync(studentId, dto);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("Biên bản vi phạm không tồn tại.");
        _requestRepoMock.Verify(r => r.CreateRequestAsync(It.IsAny<Request>()), Times.Never);
        _fileStorageMock.Verify(
            f => f.UploadImagesAsync(It.IsAny<IEnumerable<IFormFile>>(), It.IsAny<string>()),
            Times.Never);
    }
}
