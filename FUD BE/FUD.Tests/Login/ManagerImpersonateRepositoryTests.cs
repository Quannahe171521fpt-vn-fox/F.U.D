using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using FUD.Models;
using FUD.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FUD.Tests.Login;

/// <summary>
/// Unit tests cho ManagerImpersonateRepository (Tầng Repository - Đăng nhập hộ sinh viên).
/// Sử dụng EF Core InMemory Database, tuân thủ quy tắc AAA (Arrange - Act - Assert).
/// </summary>
public class ManagerImpersonateRepositoryTests
{
    private static FudContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<FudContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new FudContext(options);
    }

    private static async Task SeedUsersAsync(FudContext context)
    {
        var studentRole = new Role { Id = 1, RoleName = AccountConstants.Roles.Student };
        var managerRole = new Role { Id = 2, RoleName = AccountConstants.Roles.Manager };

        var dorm = new Dorm { Id = 1, CampusId = 1, Code = "DA", Name = "Dorm A", Status = "Active" };
        var block = new Block { Id = 1, DormId = 1, Code = "A1", Name = "Block A1", GenderType = "Male", Status = "Active", Dorm = dorm };
        var roomType = new RoomType { Id = 1, Name = "Phòng 4 giường", BedCapacity = 4 };
        var room = new Room { Id = 1, BlockId = 1, RoomTypeId = 1, RoomNumber = "101L", FloorLevel = 1, Status = "Active", Block = block, RoomType = roomType };
        var bed = new Bed { Id = 10, RoomId = 1, BedCode = "A1-101L-1", PositionInRoom = "Dưới trái", Status = "Occupied", Room = room };
        var term = new Term
        {
            Id = 5,
            Name = "Fall 2026",
            AcademicYear = "2026-2027",
            StartDate = new DateOnly(2026, 9, 1),
            EndDate = new DateOnly(2026, 12, 31)
        };

        var activeStudent = new User
        {
            Id = 100,
            Code = "HE170100",
            FullName = "Nguyễn Văn A",
            Email = "AnvHE170100@fpt.edu.vn",
            Gender = "Male",
            AccountStatus = AccountConstants.Statuses.Active,
            CreatedAt = DateTime.UtcNow,
            Student = new Student
            {
                UserId = 100,
                Major = "SE",
                Cohort = "K17",
                Residencies = new List<Residency>
                {
                    new()
                    {
                        Id = 50,
                        StudentId = 100,
                        BedId = 10,
                        TermId = 5,
                        Status = "Active",
                        Bed = bed,
                        Term = term
                    }
                }
            },
            UserRoleUsers = new List<UserRole>
            {
                new() { UserId = 100, RoleId = 1, Role = studentRole }
            }
        };

        var deletedStudent = new User
        {
            Id = 101,
            Code = "HE170101",
            FullName = "Deleted Student",
            Email = "deleted@fpt.edu.vn",
            Gender = "Male",
            AccountStatus = AccountConstants.Statuses.Active,
            DeletedAt = DateTime.UtcNow.AddDays(-1),
            CreatedAt = DateTime.UtcNow,
            UserRoleUsers = new List<UserRole>
            {
                new() { UserId = 101, RoleId = 1, Role = studentRole }
            }
        };

        var managerOnly = new User
        {
            Id = 200,
            Code = "MGR001",
            FullName = "Trần Quản Lý",
            Email = "mgr@fpt.edu.vn",
            Gender = "Female",
            AccountStatus = AccountConstants.Statuses.Active,
            CreatedAt = DateTime.UtcNow,
            UserRoleUsers = new List<UserRole>
            {
                new() { UserId = 200, RoleId = 2, Role = managerRole }
            }
        };

        context.Roles.AddRange(studentRole, managerRole);
        context.Dorms.Add(dorm);
        context.Blocks.Add(block);
        context.RoomTypes.Add(roomType);
        context.Rooms.Add(room);
        context.Beds.Add(bed);
        context.Terms.Add(term);
        context.Users.AddRange(activeStudent, deletedStudent, managerOnly);
        await context.SaveChangesAsync();
    }

    [Theory]
    [InlineData("anvhe170100@fpt.edu.vn")]
    [InlineData("he170100")]
    public async Task FindStudentByEmailOrCodeAsync_WhenMatched_ReturnsStudentWithFullResidencyHierarchy(string normalizedQuery)
    {
        // Arrange
        using var context = CreateInMemoryContext();
        await SeedUsersAsync(context);
        var sut = new ManagerImpersonateRepository(context);

        // Act
        var user = await sut.FindStudentByEmailOrCodeAsync(normalizedQuery);

        // Assert
        user.Should().NotBeNull();
        user!.Id.Should().Be(100);
        user.Student.Should().NotBeNull();
        user.Student!.Residencies.Should().ContainSingle();
        var residency = user.Student.Residencies.First();
        residency.Term.Name.Should().Be("Fall 2026");
        residency.Bed.Room.Block.Dorm.Name.Should().Be("Dorm A");
    }

    [Fact]
    public async Task FindStudentByEmailOrCodeAsync_WhenSoftDeletedOrNotStudent_ReturnsNull()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        await SeedUsersAsync(context);
        var sut = new ManagerImpersonateRepository(context);

        // Act
        var deletedResult = await sut.FindStudentByEmailOrCodeAsync("he170101");
        var managerResult = await sut.FindStudentByEmailOrCodeAsync("mgr001");
        var unknownResult = await sut.FindStudentByEmailOrCodeAsync("nonexistent");

        // Assert
        deletedResult.Should().BeNull();
        managerResult.Should().BeNull();
        unknownResult.Should().BeNull();
    }

    [Fact]
    public async Task FindStudentByIdAsync_WhenValidStudentExists_ReturnsUserWithRolesAndResidency()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        await SeedUsersAsync(context);
        var sut = new ManagerImpersonateRepository(context);

        // Act
        var user = await sut.FindStudentByIdAsync(100);

        // Assert
        user.Should().NotBeNull();
        user!.Code.Should().Be("HE170100");
        user.UserRoleUsers.Should().ContainSingle(ur => ur.Role.RoleName == AccountConstants.Roles.Student);
        user.Student!.Residencies.Should().ContainSingle();
    }

    [Theory]
    [InlineData(101)] // Soft deleted
    [InlineData(200)] // Manager role only
    [InlineData(999)] // Non-existent
    public async Task FindStudentByIdAsync_WhenInvalidTarget_ReturnsNull(int targetId)
    {
        // Arrange
        using var context = CreateInMemoryContext();
        await SeedUsersAsync(context);
        var sut = new ManagerImpersonateRepository(context);

        // Act
        var user = await sut.FindStudentByIdAsync(targetId);

        // Assert
        user.Should().BeNull();
    }

    [Fact]
    public async Task AddAuditLogAsync_WhenCalled_PersistsAuditLogInDatabase()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var sut = new ManagerImpersonateRepository(context);
        var log = new AuditLog
        {
            UserId = 10,
            ImpersonatedStudentId = 100,
            ActionType = "IMPERSONATE_STUDENT",
            TargetEntity = "User",
            TargetId = "100",
            NewValue = "{\"ManagerId\":10,\"StudentId\":100}",
            IpAddress = "127.0.0.1",
            UserAgent = "TestAgent",
            CreatedAt = DateTime.UtcNow
        };

        // Act
        await sut.AddAuditLogAsync(log);

        // Assert
        var saved = await context.AuditLogs.SingleAsync();
        saved.UserId.Should().Be(10);
        saved.ImpersonatedStudentId.Should().Be(100);
        saved.ActionType.Should().Be("IMPERSONATE_STUDENT");
        saved.IpAddress.Should().Be("127.0.0.1");
    }
}

