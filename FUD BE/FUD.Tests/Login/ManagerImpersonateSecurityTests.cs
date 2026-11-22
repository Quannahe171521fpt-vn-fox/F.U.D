using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using FUD.Controllers;
using FUD.Infrastructure.Auth;
using FUD.Models;
using FUD.Settings.pattern;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace FUD.Tests.Login;

public class ManagerImpersonateSecurityTests
{
    private sealed class FailingAuditLogFudContext : FudContext
    {
        public FailingAuditLogFudContext(DbContextOptions<FudContext> options)
            : base(options)
        {
        }

        public bool FailOnSave { get; set; }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            if (FailOnSave)
            {
                throw new DbUpdateException("Simulated AuditLog database persistence failure.");
            }

            return base.SaveChangesAsync(cancellationToken);
        }
    }

    private static FailingAuditLogFudContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<FudContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new FailingAuditLogFudContext(options);
    }

    private static void SetCallerClaims(ControllerBase controller, int managerId, bool isImpersonating = false)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, managerId.ToString()),
            new(ClaimTypes.Role, "Manager")
        };
        if (isImpersonating)
        {
            claims.Add(new Claim("imp", "true"));
        }

        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"))
        };
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };
    }

    [Fact]
    public async Task Impersonate_WhenTargetHasAdminAndStudentRoles_Returns403Forbidden()
    {
        // Arrange
        await using var db = CreateInMemoryContext();
        var studentRole = new Role { Id = 1, RoleName = "Student" };
        var adminRole = new Role { Id = 2, RoleName = "Admin" };
        var multiRoleUser = new User
        {
            Id = 50,
            Code = "HE170050",
            FullName = "Multi Role User",
            Email = "multirole@fpt.edu.vn",
            Gender = "Male",
            AccountStatus = AccountConstants.Statuses.Active,
            UserRoleUsers = new List<UserRole>
            {
                new() { UserId = 50, RoleId = 1, Role = studentRole },
                new() { UserId = 50, RoleId = 2, Role = adminRole }
            }
        };
        db.Roles.AddRange(studentRole, adminRole);
        db.Users.Add(multiRoleUser);
        await db.SaveChangesAsync();

        var jwtGenMock = new Mock<IJwtTokenGenerator>();
        var controller = new ManagerImpersonateController(db, jwtGenMock.Object);
        SetCallerClaims(controller, managerId: 10);

        // Act
        var result = await controller.Impersonate(50);

        // Assert
        var objResult = result.Should().BeOfType<ObjectResult>().Subject;
        objResult.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        jwtGenMock.Verify(
            j => j.GenerateImpersonationToken(It.IsAny<User>(), It.IsAny<IEnumerable<string>>(), It.IsAny<int>()),
            Times.Never);
    }

    [Fact]
    public async Task Impersonate_WhenCallerIsAlreadyImpersonating_Returns403Forbidden()
    {
        // Arrange
        await using var db = CreateInMemoryContext();
        var jwtGenMock = new Mock<IJwtTokenGenerator>();
        var controller = new ManagerImpersonateController(db, jwtGenMock.Object);
        SetCallerClaims(controller, managerId: 10, isImpersonating: true);

        // Act
        var result = await controller.Impersonate(50);

        // Assert
        var objResult = result.Should().BeOfType<ObjectResult>().Subject;
        objResult.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        jwtGenMock.Verify(
            j => j.GenerateImpersonationToken(It.IsAny<User>(), It.IsAny<IEnumerable<string>>(), It.IsAny<int>()),
            Times.Never);
    }

    [Fact]
    public async Task Impersonate_WhenAuditLogSaveFails_DoesNotReturnToken()
    {
        // Arrange
        await using var db = CreateInMemoryContext();
        var studentRole = new Role { Id = 1, RoleName = "Student" };
        var studentUser = new User
        {
            Id = 51,
            Code = "HE170051",
            FullName = "Student Only",
            Email = "student51@fpt.edu.vn",
            Gender = "Male",
            AccountStatus = AccountConstants.Statuses.Active,
            UserRoleUsers = new List<UserRole>
            {
                new() { UserId = 51, RoleId = 1, Role = studentRole }
            }
        };
        db.Roles.Add(studentRole);
        db.Users.Add(studentUser);
        await db.SaveChangesAsync();

        db.FailOnSave = true;

        var jwtGenMock = new Mock<IJwtTokenGenerator>();
        var controller = new ManagerImpersonateController(db, jwtGenMock.Object);
        SetCallerClaims(controller, managerId: 10);

        // Act
        var result = await controller.Impersonate(51);

        // Assert
        var objResult = result.Should().BeOfType<ObjectResult>().Subject;
        objResult.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
        jwtGenMock.Verify(
            j => j.GenerateImpersonationToken(It.IsAny<User>(), It.IsAny<IEnumerable<string>>(), It.IsAny<int>()),
            Times.Never);
    }

    [Fact]
    public void GenerateImpersonationToken_Sets30MinuteExpiryAndStudentRoleOnly()
    {
        // Arrange
        var jwtOptions = Options.Create(new JwtSettings
        {
            Key = "test_secret_key_that_is_at_least_32_bytes_long",
            Issuer = "FUD-API",
            Audience = "FUD-Client"
        });
        var generator = new JwtTokenGenerator(jwtOptions);
        var student = new User
        {
            Id = 99,
            Code = "HE170099",
            FullName = "Test Student",
            Email = "student99@fpt.edu.vn"
        };

        var before = DateTime.UtcNow;

        // Act
        var rawToken = generator.GenerateImpersonationToken(student, new[] { "Student", "Admin", "Manager" }, managerId: 7);

        // Assert
        var parsed = new JwtSecurityTokenHandler().ReadJwtToken(rawToken);
        parsed.ValidTo.Should().BeCloseTo(before.AddMinutes(30), TimeSpan.FromSeconds(10));

        var roleClaims = parsed.Claims
            .Where(c => c.Type == ClaimTypes.Role || c.Type == "role")
            .Select(c => c.Value)
            .Distinct()
            .ToList();

        roleClaims.Should().ContainSingle().Which.Should().Be("Student");
        parsed.Claims.First(c => c.Type == "imp").Value.Should().Be("true");
        parsed.Claims.First(c => c.Type == "impersonated_by").Value.Should().Be("7");
    }
}
