using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using FUD.Models;
using FUD.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FUD.Tests.Login;

public class UserRepositoryLoginTests
{
    private FudContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<FudContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new FudContext(options);
    }

    [Fact]
    public async Task GetByAccountAsync_FindByEmail_ReturnsUserWithRoles()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var role = new Role { Id = 1, RoleName = "Manager" };
        var user = new User
        {
            Id = 1,
            Code = "MGR001",
            FullName = "Nguyen Van A",
            Email = "manager_test@fpt.edu.vn",
            Gender = "Male",
            AccountStatus = "Active",
            CreatedAt = DateTime.UtcNow,
            UserRoleUsers = new List<UserRole>
            {
                new UserRole { UserId = 1, RoleId = 1, Role = role }
            }
        };

        await context.Roles.AddAsync(role);
        await context.Users.AddAsync(user);
        await context.SaveChangesAsync();

        var repository = new UserRepository(context);

        // Act
        var result = await repository.GetByAccountAsync("manager_test@fpt.edu.vn");

        // Assert
        result.Should().NotBeNull();
        result!.Email.Should().Be("manager_test@fpt.edu.vn");
        result.UserRoleUsers.Should().HaveCount(1);
        result.UserRoleUsers.First().Role.RoleName.Should().Be("Manager");
    }

    [Fact]
    public async Task GetByAccountAsync_FindByCode_ReturnsUserWithRoles()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var role = new Role { Id = 2, RoleName = "Guard" };
        var user = new User
        {
            Id = 2,
            Code = "GRD002",
            FullName = "Tran Van Guard",
            Email = "guard@fpt.edu.vn",
            Gender = "Male",
            AccountStatus = "Active",
            CreatedAt = DateTime.UtcNow,
            UserRoleUsers = new List<UserRole>
            {
                new UserRole { UserId = 2, RoleId = 2, Role = role }
            }
        };

        await context.Roles.AddAsync(role);
        await context.Users.AddAsync(user);
        await context.SaveChangesAsync();

        var repository = new UserRepository(context);

        // Act
        var result = await repository.GetByAccountAsync("GRD002");

        // Assert
        result.Should().NotBeNull();
        result!.Code.Should().Be("GRD002");
        result.Email.Should().Be("guard@fpt.edu.vn");
        result.UserRoleUsers.Should().HaveCount(1);
        result.UserRoleUsers.First().Role.RoleName.Should().Be("Guard");
    }

    [Fact]
    public async Task GetByAccountAsync_WithLeadingAndTrailingWhitespace_TrimsAndReturnsUser()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var user = new User
        {
            Id = 3,
            Code = "ADM003",
            FullName = "Admin Clean",
            Email = "admin@fpt.edu.vn",
            Gender = "Female",
            AccountStatus = "Active",
            CreatedAt = DateTime.UtcNow
        };

        await context.Users.AddAsync(user);
        await context.SaveChangesAsync();

        var repository = new UserRepository(context);

        // Act
        var result = await repository.GetByAccountAsync("   admin@fpt.edu.vn   ");

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(3);
        result.Email.Should().Be("admin@fpt.edu.vn");
    }

    [Fact]
    public async Task GetByAccountAsync_SoftDeletedUser_ReturnsNull()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var user = new User
        {
            Id = 4,
            Code = "DEL004",
            FullName = "Deleted User",
            Email = "deleted@fpt.edu.vn",
            Gender = "Male",
            AccountStatus = "Inactive",
            CreatedAt = DateTime.UtcNow,
            DeletedAt = DateTime.UtcNow
        };

        await context.Users.AddAsync(user);
        await context.SaveChangesAsync();

        var repository = new UserRepository(context);

        // Act
        var result = await repository.GetByAccountAsync("deleted@fpt.edu.vn");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByAccountAsync_NonExistentAccount_ReturnsNull()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var repository = new UserRepository(context);

        // Act
        var result = await repository.GetByAccountAsync("nonexistent@fpt.edu.vn");

        // Assert
        result.Should().BeNull();
    }
}
