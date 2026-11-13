using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using FUD.DTOs.Keeping;
using FUD.Infrastructure.Email;
using FUD.Models;
using FUD.Repositories.Interfaces;
using FUD.Repositories.Interfaces.Keeping;
using FUD.Services.Interfaces.Keeping;
using FUD.Services.Keeping;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace FUD.Tests.Keeping;

/// <summary>
/// Unit tests cho KeepingService (UC02) — tuân thủ quy tắc AAA (Arrange / Act / Assert).
/// Kết hợp EF Core InMemory Database (cho FudContext) và Moq cho các interface phụ thuộc.
/// </summary>
public class KeepingServiceTests
{
    private readonly Mock<IKeepingRepository> _keepingRepoMock = new();
    private readonly Mock<IKeepingEligibilityService> _eligibilityServiceMock = new();
    private readonly Mock<INotificationService> _notificationServiceMock = new();
    private readonly Mock<ILogger<KeepingService>> _loggerMock = new();

    private static FudContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<FudContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new FudContext(options);
    }

    private KeepingService CreateSut(FudContext context) => new(
        _keepingRepoMock.Object,
        _eligibilityServiceMock.Object,
        _notificationServiceMock.Object,
        context,
        _loggerMock.Object);

    private static Mock<IAccountTransaction> CreateMockTransaction()
    {
        var tx = new Mock<IAccountTransaction>();
        tx.Setup(t => t.CommitAsync()).Returns(Task.CompletedTask);
        tx.Setup(t => t.RollbackAsync()).Returns(Task.CompletedTask);
        tx.Setup(t => t.DisposeAsync()).Returns(ValueTask.CompletedTask);
        return tx;
    }

    private static (Dorm Dorm, Block Block, Room Room, Bed Bed, Term CurrentTerm, Term NextTerm, Residency ActiveResidency, User StudentUser)
        CreateSampleEntities(int studentId = 100, string studentGender = "Male", string blockGender = "Male")
    {
        var dorm = new Dorm { Id = 1, CampusId = 1, Code = "DA", Name = "Dorm A", Status = "Active" };
        var block = new Block { Id = 1, DormId = 1, Code = "A1", Name = "Block A1", GenderType = blockGender, Status = "Active", Dorm = dorm };
        var room = new Room { Id = 1, BlockId = 1, RoomTypeId = 1, RoomNumber = "101", Status = "Active", Block = block };
        var bed = new Bed { Id = 10, RoomId = 1, BedCode = "A1-101-1", Status = "Occupied", Room = room };

        var currentTerm = new Term
        {
            Id = 1,
            Name = "Summer 2026",
            AcademicYear = "2025-2026",
            StartDate = new DateOnly(2026, 5, 1),
            EndDate = new DateOnly(2026, 8, 31)
        };

        var nextTerm = new Term
        {
            Id = 2,
            Name = "Fall 2026",
            AcademicYear = "2026-2027",
            StartDate = new DateOnly(2026, 9, 1),
            EndDate = new DateOnly(2026, 12, 31)
        };

        var activeResidency = new Residency
        {
            Id = 50,
            StudentId = studentId,
            BedId = bed.Id,
            TermId = currentTerm.Id,
            Status = "Active",
            Bed = bed,
            Term = currentTerm
        };

        var studentUser = new User
        {
            Id = studentId,
            Code = $"HE{studentId}",
            FullName = $"Student {studentId}",
            Email = $"student{studentId}@fpt.edu.vn",
            Gender = studentGender,
            AccountStatus = "Active",
            CreatedAt = DateTime.UtcNow
        };

        return (dorm, block, room, bed, currentTerm, nextTerm, activeResidency, studentUser);
    }

    private static KeepingEligibilityResponseDto CreateEligibleResponse(int nextTermId = 2, string nextTermName = "Fall 2026")
    {
        return new KeepingEligibilityResponseDto
        {
            Eligible = true,
            NextTerm = new NextTermDto
            {
                Id = nextTermId,
                Name = nextTermName,
                AcademicYear = "2026-2027",
                StartDate = new DateOnly(2026, 9, 1),
                EndDate = new DateOnly(2026, 12, 31)
            }
        };
    }

    [Fact]
    public async Task CreateAsync_WhenNotAgreedToRegulations_ThrowsArgumentException()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var sut = CreateSut(context);
        var dto = new CreateKeepingRequestDto { AgreedToRegulations = false };

        // Act
        Func<Task> act = async () => await sut.CreateAsync(101, dto);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*KEEP_400*");
    }

    [Fact]
    public async Task CreateAsync_WhenIneligibleAndNotAlreadyKept_ThrowsKeepingNotEligibleException()
    {
        // Arrange
        const int studentId = 102;
        using var context = CreateInMemoryContext();
        _eligibilityServiceMock.Setup(s => s.CheckEligibilityAsync(studentId))
            .ReturnsAsync(new KeepingEligibilityResponseDto
            {
                Eligible = false,
                ReasonCode = KeepingReasonCodes.HasUnpaidInvoice,
                Message = "Bạn đang có hóa đơn chưa thanh toán."
            });

        var sut = CreateSut(context);
        var dto = new CreateKeepingRequestDto { AgreedToRegulations = true };

        // Act
        Func<Task> act = async () => await sut.CreateAsync(studentId, dto);

        // Assert
        var ex = await act.Should().ThrowAsync<KeepingNotEligibleException>();
        ex.Which.ReasonCode.Should().Be(KeepingReasonCodes.HasUnpaidInvoice);
    }

    [Fact]
    public async Task CreateAsync_WhenIneligibleBecauseAlreadyKept_ReturnsExistingRequestIdempotently()
    {
        // Arrange
        const int studentId = 103;
        using var context = CreateInMemoryContext();
        var (_, _, _, bed, _, nextTerm, _, _) = CreateSampleEntities(studentId);

        var existingRequest = new Request
        {
            Id = 77,
            StudentId = studentId,
            TermId = nextTerm.Id,
            RequestType = KeepingConstants.RequestType,
            Status = KeepingConstants.StatusCompleted,
            Term = nextTerm,
            Residencies = new List<Residency>
            {
                new() { Id = 88, StudentId = studentId, BedId = bed.Id, TermId = nextTerm.Id, Status = KeepingConstants.ResidencyUpcoming, Bed = bed }
            }
        };

        _eligibilityServiceMock.Setup(s => s.CheckEligibilityAsync(studentId))
            .ReturnsAsync(new KeepingEligibilityResponseDto
            {
                Eligible = false,
                ReasonCode = KeepingReasonCodes.AlreadyKept,
                Message = "Đã giữ phòng.",
                NextTerm = new NextTermDto { Id = nextTerm.Id, Name = nextTerm.Name }
            });
        _keepingRepoMock.Setup(r => r.GetCompletedKeepingRequestAsync(studentId, nextTerm.Id))
            .ReturnsAsync(existingRequest);
        _keepingRepoMock.Setup(r => r.GetKeepingBedPriceAsync(bed.Id, nextTerm.Id))
            .ReturnsAsync(1_200_000m);

        var sut = CreateSut(context);
        var dto = new CreateKeepingRequestDto { AgreedToRegulations = true };

        // Act
        var result = await sut.CreateAsync(studentId, dto);

        // Assert
        result.RequestId.Should().Be(77);
        result.Status.Should().Be(KeepingConstants.StatusCompleted);
        result.FeeAmount.Should().Be(1_200_000m);
        context.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateAsync_WhenEligibleButCompletedRequestAlreadyExists_ReturnsExistingRequestIdempotently()
    {
        // Arrange
        const int studentId = 104;
        using var context = CreateInMemoryContext();
        var (_, _, _, bed, _, nextTerm, _, _) = CreateSampleEntities(studentId);

        var existingRequest = new Request
        {
            Id = 80,
            StudentId = studentId,
            TermId = nextTerm.Id,
            RequestType = KeepingConstants.RequestType,
            Status = KeepingConstants.StatusCompleted,
            Term = nextTerm,
            Residencies = new List<Residency>
            {
                new() { Id = 90, StudentId = studentId, BedId = bed.Id, TermId = nextTerm.Id, Status = KeepingConstants.ResidencyUpcoming, Bed = bed }
            }
        };

        _eligibilityServiceMock.Setup(s => s.CheckEligibilityAsync(studentId))
            .ReturnsAsync(CreateEligibleResponse(nextTerm.Id, nextTerm.Name));
        _keepingRepoMock.Setup(r => r.GetCompletedKeepingRequestAsync(studentId, nextTerm.Id))
            .ReturnsAsync(existingRequest);
        _keepingRepoMock.Setup(r => r.GetKeepingBedPriceAsync(bed.Id, nextTerm.Id))
            .ReturnsAsync(1_300_000m);

        var sut = CreateSut(context);
        var dto = new CreateKeepingRequestDto { AgreedToRegulations = true };

        // Act
        var result = await sut.CreateAsync(studentId, dto);

        // Assert
        result.RequestId.Should().Be(80);
        result.Bed!.BedCode.Should().Be("A1-101-1");
        _keepingRepoMock.Verify(r => r.BeginTransactionAsync(), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_WhenValid_CreatesRequestResidencyLogsAndSendsNotifications()
    {
        // Arrange
        const int studentId = 105;
        using var context = CreateInMemoryContext();
        var (dorm, block, room, bed, _, nextTerm, activeResidency, studentUser) = CreateSampleEntities(studentId);

        await context.Dorms.AddAsync(dorm);
        await context.Blocks.AddAsync(block);
        await context.Rooms.AddAsync(room);
        await context.Beds.AddAsync(bed);
        await context.Terms.AddAsync(nextTerm);
        await context.Users.AddAsync(studentUser);
        await context.SaveChangesAsync();

        var txMock = CreateMockTransaction();
        _eligibilityServiceMock.Setup(s => s.CheckEligibilityAsync(studentId))
            .ReturnsAsync(CreateEligibleResponse(nextTerm.Id, nextTerm.Name));
        _keepingRepoMock.Setup(r => r.GetCompletedKeepingRequestAsync(studentId, nextTerm.Id))
            .ReturnsAsync((Request?)null);
        _keepingRepoMock.Setup(r => r.BeginTransactionAsync())
            .ReturnsAsync(txMock.Object);
        _keepingRepoMock.Setup(r => r.GetActiveResidencyAsync(studentId))
            .ReturnsAsync(activeResidency);
        _keepingRepoMock.Setup(r => r.IsBedOccupiedForTermAsync(bed.Id, nextTerm.Id))
            .ReturnsAsync(false);
        _keepingRepoMock.Setup(r => r.HasCompletedKeepingRequestAsync(studentId, nextTerm.Id))
            .ReturnsAsync(false);
        _keepingRepoMock.Setup(r => r.HasResidencyForTermAsync(studentId, nextTerm.Id))
            .ReturnsAsync(false);
        _keepingRepoMock.Setup(r => r.GetKeepingBedPriceAsync(bed.Id, nextTerm.Id))
            .ReturnsAsync(1_500_000m);
        _keepingRepoMock.Setup(r => r.GetKeepingRequestByIdAsync(It.IsAny<int>()))
            .ReturnsAsync((int reqId) =>
            {
                var req = context.Requests.First(r => r.Id == reqId);
                req.Term = nextTerm;
                var res = context.Residencies.First(r => r.SourceRequestId == reqId);
                res.Bed = bed;
                req.Residencies = new List<Residency> { res };
                return req;
            });

        var sut = CreateSut(context);
        var dto = new CreateKeepingRequestDto { AgreedToRegulations = true };

        // Act
        var result = await sut.CreateAsync(studentId, dto);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(KeepingConstants.StatusCompleted);
        result.FeeAmount.Should().Be(1_500_000m);
        result.Bed!.BedCode.Should().Be("A1-101-1");

        context.Requests.Should().ContainSingle(r =>
            r.StudentId == studentId &&
            r.TermId == nextTerm.Id &&
            r.Status == KeepingConstants.StatusCompleted);

        context.Residencies.Should().ContainSingle(r =>
            r.StudentId == studentId &&
            r.BedId == bed.Id &&
            r.TermId == nextTerm.Id &&
            r.Status == KeepingConstants.ResidencyUpcoming);

        context.RequestLogs.Should().ContainSingle(l => l.ToStatus == KeepingConstants.StatusCompleted);
        context.AuditLogs.Should().ContainSingle(a => a.TargetEntity == "KeepingRequest" && a.ActionType == "CREATE");
        context.Notifications.Should().ContainSingle(n => n.UserId == studentId && n.Channel == "InApp");

        txMock.Verify(t => t.CommitAsync(), Times.Once);
        _notificationServiceMock.Verify(n => n.SendEmailAsync(
            studentUser.Email,
            It.Is<string>(s => s.Contains("Giữ phòng thành công")),
            It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_WhenActiveResidencyLostDuringTransaction_ThrowsNotEligibleAndRollsBack()
    {
        // Arrange
        const int studentId = 106;
        using var context = CreateInMemoryContext();
        var txMock = CreateMockTransaction();

        _eligibilityServiceMock.Setup(s => s.CheckEligibilityAsync(studentId))
            .ReturnsAsync(CreateEligibleResponse());
        _keepingRepoMock.Setup(r => r.GetCompletedKeepingRequestAsync(studentId, 2))
            .ReturnsAsync((Request?)null);
        _keepingRepoMock.Setup(r => r.BeginTransactionAsync())
            .ReturnsAsync(txMock.Object);
        _keepingRepoMock.Setup(r => r.GetActiveResidencyAsync(studentId))
            .ReturnsAsync((Residency?)null);

        var sut = CreateSut(context);
        var dto = new CreateKeepingRequestDto { AgreedToRegulations = true };

        // Act
        Func<Task> act = async () => await sut.CreateAsync(studentId, dto);

        // Assert
        var ex = await act.Should().ThrowAsync<KeepingNotEligibleException>();
        ex.Which.ReasonCode.Should().Be(KeepingReasonCodes.NotResident);
        txMock.Verify(t => t.RollbackAsync(), Times.Once);
        txMock.Verify(t => t.CommitAsync(), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_WhenStudentGenderMismatchesBlockGender_ThrowsKeepingConflictException()
    {
        // Arrange
        const int studentId = 107;
        using var context = CreateInMemoryContext();
        var (_, _, _, _, _, nextTerm, activeResidency, studentUser) =
            CreateSampleEntities(studentId, studentGender: "Male", blockGender: "Female");

        await context.Users.AddAsync(studentUser);
        await context.SaveChangesAsync();

        var txMock = CreateMockTransaction();
        _eligibilityServiceMock.Setup(s => s.CheckEligibilityAsync(studentId))
            .ReturnsAsync(CreateEligibleResponse(nextTerm.Id, nextTerm.Name));
        _keepingRepoMock.Setup(r => r.GetCompletedKeepingRequestAsync(studentId, nextTerm.Id))
            .ReturnsAsync((Request?)null);
        _keepingRepoMock.Setup(r => r.BeginTransactionAsync())
            .ReturnsAsync(txMock.Object);
        _keepingRepoMock.Setup(r => r.GetActiveResidencyAsync(studentId))
            .ReturnsAsync(activeResidency);

        var sut = CreateSut(context);
        var dto = new CreateKeepingRequestDto { AgreedToRegulations = true };

        // Act
        Func<Task> act = async () => await sut.CreateAsync(studentId, dto);

        // Assert
        await act.Should().ThrowAsync<KeepingConflictException>()
            .WithMessage("*không khớp giới tính*");
        txMock.Verify(t => t.RollbackAsync(), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_WhenBedAlreadyOccupiedForNextTerm_ThrowsKeepingConflictException()
    {
        // Arrange
        const int studentId = 108;
        using var context = CreateInMemoryContext();
        var (_, _, _, bed, _, nextTerm, activeResidency, studentUser) = CreateSampleEntities(studentId);

        await context.Users.AddAsync(studentUser);
        await context.SaveChangesAsync();

        var txMock = CreateMockTransaction();
        _eligibilityServiceMock.Setup(s => s.CheckEligibilityAsync(studentId))
            .ReturnsAsync(CreateEligibleResponse(nextTerm.Id, nextTerm.Name));
        _keepingRepoMock.Setup(r => r.GetCompletedKeepingRequestAsync(studentId, nextTerm.Id))
            .ReturnsAsync((Request?)null);
        _keepingRepoMock.Setup(r => r.BeginTransactionAsync())
            .ReturnsAsync(txMock.Object);
        _keepingRepoMock.Setup(r => r.GetActiveResidencyAsync(studentId))
            .ReturnsAsync(activeResidency);
        _keepingRepoMock.Setup(r => r.IsBedOccupiedForTermAsync(bed.Id, nextTerm.Id))
            .ReturnsAsync(true);

        var sut = CreateSut(context);
        var dto = new CreateKeepingRequestDto { AgreedToRegulations = true };

        // Act
        Func<Task> act = async () => await sut.CreateAsync(studentId, dto);

        // Assert
        await act.Should().ThrowAsync<KeepingConflictException>()
            .WithMessage("*đã có sinh viên khác đăng ký*");
        txMock.Verify(t => t.RollbackAsync(), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_WhenStudentAlreadyHasResidencyForNextTerm_ThrowsKeepingConflictException()
    {
        // Arrange
        const int studentId = 109;
        using var context = CreateInMemoryContext();
        var (_, _, _, bed, _, nextTerm, activeResidency, studentUser) = CreateSampleEntities(studentId);

        await context.Users.AddAsync(studentUser);
        await context.SaveChangesAsync();

        var txMock = CreateMockTransaction();
        _eligibilityServiceMock.Setup(s => s.CheckEligibilityAsync(studentId))
            .ReturnsAsync(CreateEligibleResponse(nextTerm.Id, nextTerm.Name));
        _keepingRepoMock.Setup(r => r.GetCompletedKeepingRequestAsync(studentId, nextTerm.Id))
            .ReturnsAsync((Request?)null);
        _keepingRepoMock.Setup(r => r.BeginTransactionAsync())
            .ReturnsAsync(txMock.Object);
        _keepingRepoMock.Setup(r => r.GetActiveResidencyAsync(studentId))
            .ReturnsAsync(activeResidency);
        _keepingRepoMock.Setup(r => r.IsBedOccupiedForTermAsync(bed.Id, nextTerm.Id))
            .ReturnsAsync(false);
        _keepingRepoMock.Setup(r => r.HasCompletedKeepingRequestAsync(studentId, nextTerm.Id))
            .ReturnsAsync(false);
        _keepingRepoMock.Setup(r => r.HasResidencyForTermAsync(studentId, nextTerm.Id))
            .ReturnsAsync(true);

        var sut = CreateSut(context);
        var dto = new CreateKeepingRequestDto { AgreedToRegulations = true };

        // Act
        Func<Task> act = async () => await sut.CreateAsync(studentId, dto);

        // Assert
        await act.Should().ThrowAsync<KeepingConflictException>()
            .WithMessage("*đã có cư trú cho học kỳ kế tiếp*");
        txMock.Verify(t => t.RollbackAsync(), Times.Once);
    }

    [Fact]
    public async Task CompleteKeepingAsync_WhenRequestNotFound_ThrowsKeyNotFoundException()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var sut = CreateSut(context);

        // Act
        Func<Task> act = async () => await sut.CompleteKeepingAsync(999);

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("*999*");
    }

    [Fact]
    public async Task CompleteKeepingAsync_WhenAlreadyCompletedWithNextResidency_ReturnsExistingResponse()
    {
        // Arrange
        const int studentId = 110;
        using var context = CreateInMemoryContext();
        var (_, _, _, bed, _, nextTerm, _, _) = CreateSampleEntities(studentId);

        var request = new Request
        {
            Id = 55,
            StudentId = studentId,
            TermId = nextTerm.Id,
            RequestType = KeepingConstants.RequestType,
            Status = KeepingConstants.StatusCompleted,
            Reason = "Giữ phòng",
            CreatedTime = DateTime.UtcNow,
            Term = nextTerm
        };
        await context.Terms.AddAsync(nextTerm);
        await context.Requests.AddAsync(request);
        await context.SaveChangesAsync();

        var loadedWithResidency = new Request
        {
            Id = 55,
            StudentId = studentId,
            TermId = nextTerm.Id,
            RequestType = KeepingConstants.RequestType,
            Status = KeepingConstants.StatusCompleted,
            Term = nextTerm,
            Residencies = new List<Residency>
            {
                new()
                {
                    Id = 66,
                    StudentId = studentId,
                    BedId = bed.Id,
                    TermId = nextTerm.Id,
                    Status = KeepingConstants.ResidencyUpcoming,
                    Bed = bed
                }
            }
        };

        _keepingRepoMock.Setup(r => r.GetKeepingRequestByIdAsync(55))
            .ReturnsAsync(loadedWithResidency);
        _keepingRepoMock.Setup(r => r.GetKeepingBedPriceAsync(bed.Id, nextTerm.Id))
            .ReturnsAsync(1_400_000m);

        var sut = CreateSut(context);

        // Act
        var result = await sut.CompleteKeepingAsync(55);

        // Assert
        result.RequestId.Should().Be(55);
        result.Status.Should().Be(KeepingConstants.StatusCompleted);
        result.FeeAmount.Should().Be(1_400_000m);
        _keepingRepoMock.Verify(r => r.BeginTransactionAsync(), Times.Never);
    }

    [Fact]
    public async Task GetRequestAsync_And_GetRequestOwnerIdAsync_ReturnExpectedData()
    {
        // Arrange
        const int studentId = 111;
        using var context = CreateInMemoryContext();
        var (_, _, _, bed, _, nextTerm, _, _) = CreateSampleEntities(studentId);

        var request = new Request
        {
            Id = 60,
            StudentId = studentId,
            TermId = nextTerm.Id,
            RequestType = KeepingConstants.RequestType,
            Status = KeepingConstants.StatusCompleted,
            Term = nextTerm,
            Residencies = new List<Residency>
            {
                new() { Id = 70, StudentId = studentId, BedId = bed.Id, TermId = nextTerm.Id, Status = KeepingConstants.ResidencyUpcoming, Bed = bed }
            }
        };
        await context.Terms.AddAsync(nextTerm);
        await context.Requests.AddAsync(new Request
        {
            Id = 60,
            StudentId = studentId,
            TermId = nextTerm.Id,
            RequestType = KeepingConstants.RequestType,
            Status = KeepingConstants.StatusCompleted,
            CreatedTime = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        _keepingRepoMock.Setup(r => r.GetKeepingRequestByIdAsync(60)).ReturnsAsync(request);
        _keepingRepoMock.Setup(r => r.GetKeepingRequestByIdAsync(999)).ReturnsAsync((Request?)null);
        _keepingRepoMock.Setup(r => r.GetKeepingBedPriceAsync(bed.Id, nextTerm.Id)).ReturnsAsync(1_100_000m);

        var sut = CreateSut(context);

        // Act
        var foundDto = await sut.GetRequestAsync(60);
        var notFoundDto = await sut.GetRequestAsync(999);
        var ownerId = await sut.GetRequestOwnerIdAsync(60);
        var missingOwnerId = await sut.GetRequestOwnerIdAsync(999);

        // Assert
        foundDto.Should().NotBeNull();
        foundDto!.RequestId.Should().Be(60);
        foundDto.FeeAmount.Should().Be(1_100_000m);
        notFoundDto.Should().BeNull();
        ownerId.Should().Be(studentId);
        missingOwnerId.Should().BeNull();
    }
}

