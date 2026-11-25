using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using FUD.Common;
using FUD.Infrastructure.Auth;
using FUD.Models;
using FUD.Repositories.Interfaces;
using FUD.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace FUD.Tests.Login;

/// <summary>
/// Unit tests cho ManagerImpersonateService (Tầng Service - Đăng nhập hộ sinh viên).
/// Tuân thủ quy tắc AAA (Arrange - Act - Assert) và chuẩn kiểm thử chuyên nghiệp.
/// </summary>
public class ManagerImpersonateServiceTests
{
    private readonly Mock<IManagerImpersonateRepository> _repoMock = new();
    private readonly Mock<IJwtTokenGenerator> _jwtGenMock = new();
    private readonly Mock<ILogger<ManagerImpersonateService>> _loggerMock = new();

    private ManagerImpersonateService CreateSut() => new(
        _repoMock.Object,
        _jwtGenMock.Object,
        _loggerMock.Object);

    private static User CreateSampleStudentUser(
        int userId = 100,
        string code = "HE170100",
        string accountStatus = AccountConstants.Statuses.Active,
        bool includeProfile = true,
        bool includeActiveResidency = true,
        params string[] roleNames)
    {
        if (roleNames.Length == 0)
            roleNames = new[] { AccountConstants.Roles.Student };

        var dorm = new Dorm { Id = 1, Code = "DA", Name = "Dorm A", Status = "Active" };
        var block = new Block { Id = 1, DormId = 1, Code = "A1", Name = "Block A1", GenderType = "Male", Status = "Active", Dorm = dorm };
        var room = new Room { Id = 1, BlockId = 1, RoomNumber = "101L", Status = "Active", Block = block };
        var bed = new Bed { Id = 10, RoomId = 1, BedCode = "A1-101L-1", Status = "Occupied", Room = room };
        var term = new Term { Id = 5, Name = "Fall 2026", AcademicYear = "2026-2027" };

        var residencies = includeActiveResidency
            ? new List<Residency>
            {
                new()
                {
                    Id = 50,
                    StudentId = userId,
                    BedId = bed.Id,
                    TermId = term.Id,
                    Status = "Active",
                    CheckInDate = new DateOnly(2026, 9, 2),
                    PlannedCheckOutDate = new DateOnly(2026, 12, 31),
                    Bed = bed,
                    Term = term
                }
            }
            : new List<Residency>();

        var userRoles = new List<UserRole>();
        for (int i = 0; i < roleNames.Length; i++)
        {
            userRoles.Add(new UserRole
            {
                UserId = userId,
                RoleId = i + 1,
                Role = new Role { Id = i + 1, RoleName = roleNames[i] }
            });
        }

        return new User
        {
            Id = userId,
            Code = code,
            FullName = "Nguyễn Văn Sinh Viên",
            Email = $"{code.ToLowerInvariant()}@fpt.edu.vn",
            Phone = "0901234567",
            Gender = "Male",
            AccountStatus = accountStatus,
            CreatedAt = DateTime.UtcNow,
            Student = includeProfile ? new Student
            {
                UserId = userId,
                Major = "SE",
                Cohort = "K17",
                Residencies = residencies
            } : null,
            UserRoleUsers = userRoles
        };
    }

    #region LookupStudentAsync Tests

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("a")]
    [InlineData(" a ")]
    public async Task LookupStudentAsync_WhenQueryIsEmptyOrTooShort_ThrowsArgumentException(string? invalidQuery)
    {
        // Arrange
        var sut = CreateSut();

        // Act
        Func<Task> act = async () => await sut.LookupStudentAsync(invalidQuery!);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*tối thiểu 2 ký tự*");
        _repoMock.Verify(r => r.FindStudentByEmailOrCodeAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task LookupStudentAsync_WhenStudentNotFound_ThrowsKeyNotFoundException()
    {
        // Arrange
        _repoMock.Setup(r => r.FindStudentByEmailOrCodeAsync("he999999"))
            .ReturnsAsync((User?)null);
        var sut = CreateSut();

        // Act
        Func<Task> act = async () => await sut.LookupStudentAsync("  HE999999  ");

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("*Không tìm thấy sinh viên*");
        _repoMock.Verify(r => r.FindStudentByEmailOrCodeAsync("he999999"), Times.Once);
    }

    [Fact]
    public async Task LookupStudentAsync_WhenValidStudentFound_ReturnsMappedStudentLookupDto()
    {
        // Arrange
        var user = CreateSampleStudentUser(userId: 100, code: "HE170100");
        _repoMock.Setup(r => r.FindStudentByEmailOrCodeAsync("he170100"))
            .ReturnsAsync(user);
        var sut = CreateSut();

        // Act
        var result = await sut.LookupStudentAsync("  HE170100 ");

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(100);
        result.Code.Should().Be("HE170100");
        result.PrimaryRole.Should().Be("Student");
        result.Roles.Should().ContainSingle().Which.Should().Be("Student");
        result.Major.Should().Be("SE");
        result.Cohort.Should().Be("K17");
        result.CurrentResidency.Should().NotBeNull();
        result.CurrentResidency!.Id.Should().Be(50);
        result.CurrentResidency.DormName.Should().Be("Dorm A");
        result.CurrentResidency.BedCode.Should().Be("A1-101L-1");
        result.CurrentResidency.CheckInDate.Should().Be(new DateOnly(2026, 9, 2));
    }

    [Fact]
    public async Task LookupStudentAsync_WhenMultipleResidencies_SelectsLatestActiveOrCheckedInResidency()
    {
        // Arrange
        var user = CreateSampleStudentUser(userId: 102, code: "HE170102");
        var first = user.Student!.Residencies.First();
        user.Student.Residencies.Add(new Residency
        {
            Id = 60,
            StudentId = 102,
            BedId = first.BedId,
            TermId = first.TermId,
            Status = "CheckedIn",
            Bed = first.Bed,
            Term = first.Term
        });
        user.Student.Residencies.Add(new Residency
        {
            Id = 70,
            StudentId = 102,
            BedId = first.BedId,
            TermId = first.TermId,
            Status = "Completed",
            Bed = first.Bed,
            Term = first.Term
        });

        _repoMock.Setup(r => r.FindStudentByEmailOrCodeAsync("he170102")).ReturnsAsync(user);
        var sut = CreateSut();

        // Act
        var result = await sut.LookupStudentAsync("HE170102");

        // Assert
        result.CurrentResidency.Should().NotBeNull();
        result.CurrentResidency!.Id.Should().Be(60);
        result.CurrentResidency.Status.Should().Be("CheckedIn");
    }

    [Fact]
    public async Task LookupStudentAsync_WhenNoActiveResidency_ReturnsNullCurrentResidency()
    {
        // Arrange
        var user = CreateSampleStudentUser(userId: 103, code: "HE170103", includeActiveResidency: false);
        _repoMock.Setup(r => r.FindStudentByEmailOrCodeAsync("he170103")).ReturnsAsync(user);
        var sut = CreateSut();

        // Act
        var result = await sut.LookupStudentAsync("HE170103");

        // Assert
        result.CurrentResidency.Should().BeNull();
    }

    #endregion

    #region ImpersonateStudentAsync Tests

    [Fact]
    public async Task ImpersonateStudentAsync_WhenCallerIsAlreadyImpersonating_ThrowsForbiddenException()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        Func<Task> act = async () => await sut.ImpersonateStudentAsync(
            managerId: 10,
            studentId: 100,
            isCallerImpersonating: true,
            ipAddress: "127.0.0.1",
            userAgent: "Test");

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("*đang đăng nhập hộ*");
        _repoMock.Verify(r => r.FindStudentByIdAsync(It.IsAny<int>()), Times.Never);
        _jwtGenMock.Verify(
            j => j.GenerateImpersonationToken(It.IsAny<User>(), It.IsAny<IEnumerable<string>>(), It.IsAny<int>()),
            Times.Never);
    }

    [Fact]
    public async Task ImpersonateStudentAsync_WhenStudentNotFound_ThrowsKeyNotFoundException()
    {
        // Arrange
        _repoMock.Setup(r => r.FindStudentByIdAsync(999)).ReturnsAsync((User?)null);
        var sut = CreateSut();

        // Act
        Func<Task> act = async () => await sut.ImpersonateStudentAsync(10, 999, false, null, null);

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("*Sinh viên không tồn tại*");
    }

    [Theory]
    [InlineData(AccountConstants.Statuses.Suspended)]
    [InlineData(AccountConstants.Statuses.Pending)]
    public async Task ImpersonateStudentAsync_WhenAccountNotActive_ThrowsInvalidOperationException(string inactiveStatus)
    {
        // Arrange
        var user = CreateSampleStudentUser(userId: 104, accountStatus: inactiveStatus);
        _repoMock.Setup(r => r.FindStudentByIdAsync(104)).ReturnsAsync(user);
        var sut = CreateSut();

        // Act
        Func<Task> act = async () => await sut.ImpersonateStudentAsync(10, 104, false, null, null);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"*{inactiveStatus}*");
        _repoMock.Verify(r => r.AddAuditLogAsync(It.IsAny<AuditLog>()), Times.Never);
    }

    [Fact]
    public async Task ImpersonateStudentAsync_WhenTargetHasNonStudentRole_ThrowsForbiddenException()
    {
        // Arrange
        var multiRoleUser = CreateSampleStudentUser(
            userId: 105,
            roleNames: new[] { AccountConstants.Roles.Student, AccountConstants.Roles.Admin });
        _repoMock.Setup(r => r.FindStudentByIdAsync(105)).ReturnsAsync(multiRoleUser);
        var sut = CreateSut();

        // Act
        Func<Task> act = async () => await sut.ImpersonateStudentAsync(10, 105, false, null, null);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("*duy nhất vai trò Sinh viên*");
        _repoMock.Verify(r => r.AddAuditLogAsync(It.IsAny<AuditLog>()), Times.Never);
    }

    [Fact]
    public async Task ImpersonateStudentAsync_WhenAuditLogSaveFails_ThrowsAuditLogWriteExceptionWithoutGeneratingToken()
    {
        // Arrange
        var user = CreateSampleStudentUser(userId: 106);
        _repoMock.Setup(r => r.FindStudentByIdAsync(106)).ReturnsAsync(user);
        _repoMock.Setup(r => r.AddAuditLogAsync(It.IsAny<AuditLog>()))
            .ThrowsAsync(new InvalidOperationException("DB down"));
        var sut = CreateSut();

        // Act
        Func<Task> act = async () => await sut.ImpersonateStudentAsync(10, 106, false, "127.0.0.1", "Test");

        // Assert
        await act.Should().ThrowAsync<AuditLogWriteException>()
            .WithMessage("Không thể ghi nhận nhật ký kiểm toán cho phiên đăng nhập hộ.");
        _jwtGenMock.Verify(
            j => j.GenerateImpersonationToken(It.IsAny<User>(), It.IsAny<IEnumerable<string>>(), It.IsAny<int>()),
            Times.Never);
    }

    [Fact]
    public async Task ImpersonateStudentAsync_WhenValidStudent_SavesTruncatedAuditLogAndReturnsTokenAndProfile()
    {
        // Arrange
        var user = CreateSampleStudentUser(userId: 120, code: "HE170120");
        AuditLog? capturedLog = null;

        _repoMock.Setup(r => r.FindStudentByIdAsync(120)).ReturnsAsync(user);
        _repoMock.Setup(r => r.AddAuditLogAsync(It.IsAny<AuditLog>()))
            .Callback<AuditLog>(log => capturedLog = log)
            .Returns(Task.CompletedTask);
        _jwtGenMock.Setup(j => j.GenerateImpersonationToken(user, It.IsAny<IEnumerable<string>>(), 10))
            .Returns("imp-token-120");

        var longIp = new string('1', 60);
        var longUa = new string('A', 300);
        var sut = CreateSut();

        // Act
        var result = await sut.ImpersonateStudentAsync(10, 120, false, longIp, longUa);

        // Assert
        result.Should().NotBeNull();
        result.ImpersonationToken.Should().Be("imp-token-120");
        result.ExpiresIn.Should().Be(1800);
        result.Student.Id.Should().Be(120);
        result.Student.Code.Should().Be("HE170120");
        result.Student.DormId.Should().Be(1);
        result.Student.DormName.Should().Be("Dorm A");
        result.Student.CurrentResidency.Should().NotBeNull();
        result.Student.CurrentResidency!.BedCode.Should().Be("A1-101L-1");

        capturedLog.Should().NotBeNull();
        capturedLog!.UserId.Should().Be(10);
        capturedLog.ImpersonatedStudentId.Should().Be(120);
        capturedLog.ActionType.Should().Be("IMPERSONATE_STUDENT");
        capturedLog.IpAddress!.Length.Should().Be(50);
        capturedLog.UserAgent!.Length.Should().Be(255);

        var newValue = JsonSerializer.Deserialize<JsonElement>(capturedLog.NewValue!);
        newValue.GetProperty("ManagerId").GetInt32().Should().Be(10);
        newValue.GetProperty("StudentId").GetInt32().Should().Be(120);
        newValue.GetProperty("StudentCode").GetString().Should().Be("HE170120");
    }

    [Fact]
    public async Task ImpersonateStudentAsync_WhenStudentHasNoProfile_SetsImpersonatedStudentIdNullInAuditLog()
    {
        // Arrange
        var userWithoutProfile = CreateSampleStudentUser(userId: 121, code: "HE170121", includeProfile: false);
        AuditLog? capturedLog = null;

        _repoMock.Setup(r => r.FindStudentByIdAsync(121)).ReturnsAsync(userWithoutProfile);
        _repoMock.Setup(r => r.AddAuditLogAsync(It.IsAny<AuditLog>()))
            .Callback<AuditLog>(log => capturedLog = log)
            .Returns(Task.CompletedTask);
        _jwtGenMock.Setup(j => j.GenerateImpersonationToken(userWithoutProfile, It.IsAny<IEnumerable<string>>(), 10))
            .Returns("imp-token-121");

        var sut = CreateSut();

        // Act
        var result = await sut.ImpersonateStudentAsync(10, 121, false, null, null);

        // Assert
        result.ImpersonationToken.Should().Be("imp-token-121");
        result.Student.CurrentResidency.Should().BeNull();
        capturedLog.Should().NotBeNull();
        capturedLog!.ImpersonatedStudentId.Should().BeNull();
        capturedLog.IpAddress.Should().BeNull();
        capturedLog.UserAgent.Should().BeNull();
    }

    #endregion
}

