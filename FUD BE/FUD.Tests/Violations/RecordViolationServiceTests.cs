using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using FUD.DTOs.Violations;
using FUD.Infrastructure.Email;
using FUD.Infrastructure.Storage;
using FUD.Models;
using FUD.Repositories.Interfaces.Violations;
using FUD.Services.Violations;
using Microsoft.AspNetCore.Http;
using Moq;
using Xunit;

namespace FUD.Tests.Violations;

public class RecordViolationServiceTests
{
    private readonly Mock<IViolationRepository> _violationRepoMock = new();
    private readonly Mock<IFileStorage> _fileStorageMock = new();
    private readonly Mock<IEmailSender> _emailSenderMock = new();

    private ViolationService CreateSut() => new(
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
    public async Task GetViolationTypesAsync_ReturnsMappedViolationTypeDtos()
    {
        // Arrange
        var sut = CreateSut();
        var types = new List<ViolationType>
        {
            new() { Id = 1, Name = "Hút thuốc trong phòng" },
            new() { Id = 2, Name = "Sử dụng thiết bị điện công suất lớn" }
        };
        _violationRepoMock.Setup(r => r.GetViolationTypesAsync()).ReturnsAsync(types);

        // Act
        var result = await sut.GetViolationTypesAsync();

        // Assert
        result.Should().HaveCount(2);
        result[0].Id.Should().Be(1);
        result[0].Name.Should().Be("Hút thuốc trong phòng");
        result[1].Id.Should().Be(2);
        result[1].Name.Should().Be("Sử dụng thiết bị điện công suất lớn");
    }

    [Fact]
    public async Task GetStudentsByRoomAsync_RoomWithActiveResidencies_ReturnsRoomStudentDtos()
    {
        // Arrange
        var sut = CreateSut();
        var user1 = new User { Id = 10, Code = "SE160001", FullName = "Nguyen Van A" };
        var student1 = new Student { UserId = 10, User = user1 };
        var bed1 = new Bed { Id = 1, BedCode = "101-A" };

        var user2 = new User { Id = 11, Code = "SE160002", FullName = "Tran Thi B" };
        var student2 = new Student { UserId = 11, User = user2 };
        var bed2 = new Bed { Id = 2, BedCode = "101-B" };

        var residencies = new List<Residency>
        {
            new() { Id = 101, StudentId = 10, Student = student1, Bed = bed1 },
            new() { Id = 102, StudentId = 11, Student = student2, Bed = bed2 }
        };

        _violationRepoMock.Setup(r => r.GetActiveResidenciesByRoomIdAsync(1)).ReturnsAsync(residencies);

        // Act
        var result = await sut.GetStudentsByRoomAsync(1);

        // Assert
        result.Should().HaveCount(2);
        result[0].StudentId.Should().Be(10);
        result[0].StudentCode.Should().Be("SE160001");
        result[0].FullName.Should().Be("Nguyen Van A");
        result[0].BedCode.Should().Be("101-A");
        result[0].ResidencyId.Should().Be(101);

        result[1].StudentId.Should().Be(11);
        result[1].StudentCode.Should().Be("SE160002");
    }

    [Fact]
    public async Task SearchStudentByCodeAsync_WhenStudentExists_ReturnsStudentDetails()
    {
        // Arrange
        var sut = CreateSut();
        var user = new User { Id = 20, Code = "SE161234", FullName = "Le Van C" };
        var student = new Student { UserId = 20, User = user };
        var room = new Room { Id = 5, RoomNumber = "202" };
        var bed = new Bed { Id = 50, BedCode = "202-1", Room = room };
        var residency = new Residency { Id = 500, StudentId = 20, Bed = bed };

        _violationRepoMock.Setup(r => r.GetStudentByCodeAsync("SE161234")).ReturnsAsync(student);
        _violationRepoMock.Setup(r => r.GetCurrentResidencyByStudentIdAsync(20)).ReturnsAsync(residency);

        // Act
        var result = await sut.SearchStudentByCodeAsync("  SE161234  ");

        // Assert
        result.Should().NotBeNull();
        result!.StudentId.Should().Be(20);
        result.StudentCode.Should().Be("SE161234");
        result.FullName.Should().Be("Le Van C");
        result.BedCode.Should().Be("202-1");
        result.StatusDescription.Should().Contain("Phòng 202 (Giường 202-1)");
    }

    [Fact]
    public async Task SearchStudentByCodeAsync_WhenStudentNotFound_ReturnsNull()
    {
        // Arrange
        var sut = CreateSut();
        _violationRepoMock.Setup(r => r.GetStudentByCodeAsync("UNKNOWN")).ReturnsAsync((Student?)null);

        // Act
        var result = await sut.SearchStudentByCodeAsync("UNKNOWN");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task RecordViolationsAsync_ValidRequestWithMultipleStudents_CreatesViolationsAndUploadsEvidences()
    {
        // Arrange
        var sut = CreateSut();
        var guardUserId = 99;
        var roomId = 10;
        var occurrenceTime = DateTime.UtcNow.AddHours(-1);

        var mockFile1 = CreateMockFormFile("img1.jpg");
        var mockFile2 = CreateMockFormFile("img2.jpg");
        var evidences = new List<IFormFile> { mockFile1, mockFile2 };

        var request = new CreateViolationRequestDto
        {
            RoomId = roomId,
            StudentIds = new List<int> { 101, 102 },
            ViolationTypeId = 3,
            OccurrenceTime = occurrenceTime,
            Description = "Nấu ăn trong phòng ký túc xá bằng bếp từ",
            Evidences = evidences
        };

        var uploadedUrls = new List<string>
        {
            "https://cloudinary.com/violation/img1.jpg",
            "https://cloudinary.com/violation/img2.jpg"
        };

        _violationRepoMock.Setup(r => r.RoomExistsAsync(roomId)).ReturnsAsync(true);
        _fileStorageMock.Setup(f => f.UploadImagesAsync(evidences, "violations")).ReturnsAsync(uploadedUrls);

        var residency1 = new Residency { Id = 1001, StudentId = 101 };
        var residency2 = new Residency { Id = 1002, StudentId = 102 };
        _violationRepoMock.Setup(r => r.GetCurrentResidencyByStudentIdAsync(101)).ReturnsAsync(residency1);
        _violationRepoMock.Setup(r => r.GetCurrentResidencyByStudentIdAsync(102)).ReturnsAsync(residency2);

        _violationRepoMock.SetupSequence(r => r.GenerateViolationCodeAsync())
            .ReturnsAsync("VIO-2026-0001")
            .ReturnsAsync("VIO-2026-0002");

        List<Violation>? capturedViolations = null;
        _violationRepoMock.Setup(r => r.AddViolationsAsync(It.IsAny<IEnumerable<Violation>>()))
            .Callback<IEnumerable<Violation>>(v => capturedViolations = v.ToList())
            .Returns(Task.CompletedTask);

        // Act
        var result = await sut.RecordViolationsAsync(request, guardUserId);

        // Assert
        result.Should().HaveCount(2);

        _fileStorageMock.Verify(f => f.UploadImagesAsync(evidences, "violations"), Times.Once);
        _violationRepoMock.Verify(r => r.AddViolationsAsync(It.IsAny<IEnumerable<Violation>>()), Times.Once);

        capturedViolations.Should().NotBeNull();
        capturedViolations!.Should().HaveCount(2);

        var v1 = capturedViolations.First(v => v.StudentId == 101);
        v1.Code.Should().Be("VIO-2026-0001");
        v1.RoomId.Should().Be(roomId);
        v1.ResidencyId.Should().Be(1001);
        v1.ViolationTypeId.Should().Be(3);
        v1.RecordedById.Should().Be(guardUserId);
        v1.Status.Should().Be("Recorded");
        v1.Description.Should().Be("Nấu ăn trong phòng ký túc xá bằng bếp từ");
        v1.ViolationEvidences.Should().HaveCount(2);
        v1.ViolationEvidences.Select(e => e.FileUrl).Should().BeEquivalentTo(uploadedUrls);

        var v2 = capturedViolations.First(v => v.StudentId == 102);
        v2.Code.Should().Be("VIO-2026-0002");
        v2.ResidencyId.Should().Be(1002);
    }

    [Fact]
    public async Task RecordViolationsAsync_EmptyStudentIds_ThrowsArgumentException()
    {
        // Arrange
        var sut = CreateSut();
        var request = new CreateViolationRequestDto
        {
            RoomId = 1,
            StudentIds = new List<int>(),
            Description = "Test description",
            Evidences = new List<IFormFile> { CreateMockFormFile() }
        };

        // Act
        var act = async () => await sut.RecordViolationsAsync(request, guardUserId: 1);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Phải chọn ít nhất 1 sinh viên vi phạm*");
    }

    [Fact]
    public async Task RecordViolationsAsync_RoomDoesNotExist_ThrowsArgumentException()
    {
        // Arrange
        var sut = CreateSut();
        var request = new CreateViolationRequestDto
        {
            RoomId = 999,
            StudentIds = new List<int> { 10 },
            Description = "Test description",
            Evidences = new List<IFormFile> { CreateMockFormFile() }
        };
        _violationRepoMock.Setup(r => r.RoomExistsAsync(999)).ReturnsAsync(false);

        // Act
        var act = async () => await sut.RecordViolationsAsync(request, guardUserId: 1);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Phòng không tồn tại*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RecordViolationsAsync_EmptyDescription_ThrowsArgumentException(string? invalidDesc)
    {
        // Arrange
        var sut = CreateSut();
        var request = new CreateViolationRequestDto
        {
            RoomId = 1,
            StudentIds = new List<int> { 10 },
            Description = invalidDesc!,
            Evidences = new List<IFormFile> { CreateMockFormFile() }
        };
        _violationRepoMock.Setup(r => r.RoomExistsAsync(1)).ReturnsAsync(true);

        // Act
        var act = async () => await sut.RecordViolationsAsync(request, guardUserId: 1);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Mô tả vi phạm không được để trống*");
    }

    [Fact]
    public async Task RecordViolationsAsync_EmptyEvidences_ThrowsArgumentException()
    {
        // Arrange
        var sut = CreateSut();
        var request = new CreateViolationRequestDto
        {
            RoomId = 1,
            StudentIds = new List<int> { 10 },
            Description = "Vi phạm có mô tả hợp lệ",
            Evidences = new List<IFormFile>()
        };
        _violationRepoMock.Setup(r => r.RoomExistsAsync(1)).ReturnsAsync(true);

        // Act
        var act = async () => await sut.RecordViolationsAsync(request, guardUserId: 1);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Yêu cầu tối thiểu 1 ảnh minh chứng*");
    }

    [Fact]
    public async Task RecordViolationsAsync_UploadFails_ThrowsInvalidOperationException()
    {
        // Arrange
        var sut = CreateSut();
        var mockFile = CreateMockFormFile();
        var request = new CreateViolationRequestDto
        {
            RoomId = 1,
            StudentIds = new List<int> { 10 },
            Description = "Vi phạm hợp lệ",
            Evidences = new List<IFormFile> { mockFile }
        };
        _violationRepoMock.Setup(r => r.RoomExistsAsync(1)).ReturnsAsync(true);
        _fileStorageMock.Setup(f => f.UploadImagesAsync(It.IsAny<List<IFormFile>>(), "violations"))
            .ReturnsAsync(new List<string>()); // Empty list indicates upload failure

        // Act
        var act = async () => await sut.RecordViolationsAsync(request, guardUserId: 1);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Không thể tải lên ảnh minh chứng*");
        _violationRepoMock.Verify(r => r.AddViolationsAsync(It.IsAny<IEnumerable<Violation>>()), Times.Never);
    }
}
