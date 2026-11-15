using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using FUD.Models;
using FUD.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FUD.Tests.Login;

public class RefreshTokenRepositoryTests
{
    private static FudContext CreateInMemoryDbContext(string? dbName = null)
    {
        var options = new DbContextOptionsBuilder<FudContext>()
            .UseInMemoryDatabase(databaseName: dbName ?? Guid.NewGuid().ToString())
            .Options;

        return new FudContext(options);
    }

    private static async Task<User> SeedUserWithRoleAsync(FudContext context, int userId = 1)
    {
        var role = new Role { Id = 2, RoleName = "Manager" };
        var user = new User
        {
            Id = userId,
            Code = $"MGR{userId:000}",
            FullName = "Manager Repo Test",
            Email = $"mgr{userId}@fpt.edu.vn",
            Gender = "Male",
            AccountStatus = "Active",
            CreatedAt = DateTime.UtcNow,
            UserRoleUsers = new List<UserRole>
            {
                new() { UserId = userId, RoleId = 2, Role = role }
            }
        };

        await context.Roles.AddAsync(role);
        await context.Users.AddAsync(user);
        await context.SaveChangesAsync();
        return user;
    }

    [Fact]
    public async Task TryRotateAsync_RevokesFirstTokenOnceAndRejectsSecondAttempt()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var context = CreateInMemoryDbContext(dbName);
        var user = await SeedUserWithRoleAsync(context);

        var now = DateTime.UtcNow;
        var initial = new RefreshToken
        {
            UserId = user.Id,
            TokenHash = "hash_initial",
            CreatedAt = now,
            ExpiresAt = now.AddMinutes(30),
            AbsoluteExpiresAt = now.AddHours(24),
            CreatedByIp = "127.0.0.1"
        };
        await context.RefreshTokens.AddAsync(initial);
        await context.SaveChangesAsync();

        var repo = new RefreshTokenRepository(context);
        var replacement1 = new RefreshToken
        {
            UserId = user.Id,
            TokenHash = "hash_replacement_1",
            CreatedAt = now,
            ExpiresAt = now.AddMinutes(30),
            AbsoluteExpiresAt = now.AddHours(24),
            CreatedByIp = "127.0.0.1"
        };
        var replacement2 = new RefreshToken
        {
            UserId = user.Id,
            TokenHash = "hash_replacement_2",
            CreatedAt = now,
            ExpiresAt = now.AddMinutes(30),
            AbsoluteExpiresAt = now.AddHours(24),
            CreatedByIp = "127.0.0.2"
        };

        var firstResult = await repo.TryRotateAsync(initial, replacement1, "127.0.0.1");
        var secondResult = await repo.TryRotateAsync(initial, replacement2, "127.0.0.2");

        firstResult.Should().BeTrue();
        secondResult.Should().BeFalse();

        var tokens = await context.RefreshTokens.AsNoTracking().ToListAsync();
        tokens.Should().HaveCount(2);
        tokens.Single(t => t.TokenHash == "hash_initial").RevokedAt.Should().NotBeNull();
        tokens.Single(t => t.TokenHash == "hash_initial").ReplacedByTokenHash.Should().Be("hash_replacement_1");
        tokens.Should().NotContain(t => t.TokenHash == "hash_replacement_2");
    }

    [Fact]
    public async Task GetByTokenHashNoTrackingAsync_ObservesUpdatedRevokedStateAfterTrackedRead()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var writerContext = CreateInMemoryDbContext(dbName);
        await using var readerContext = CreateInMemoryDbContext(dbName);

        var user = await SeedUserWithRoleAsync(writerContext);
        var now = DateTime.UtcNow;
        var token = new RefreshToken
        {
            UserId = user.Id,
            TokenHash = "hash_race",
            CreatedAt = now,
            ExpiresAt = now.AddMinutes(30),
            AbsoluteExpiresAt = now.AddHours(24),
            CreatedByIp = "127.0.0.1"
        };
        await writerContext.RefreshTokens.AddAsync(token);
        await writerContext.SaveChangesAsync();

        var readerRepo = new RefreshTokenRepository(readerContext);
        var trackedInitial = await readerRepo.GetByTokenHashAsync("hash_race");
        trackedInitial.Should().NotBeNull();
        trackedInitial!.RevokedAt.Should().BeNull();

        // Simulate another request rotating the token via writerContext
        var writerRepo = new RefreshTokenRepository(writerContext);
        var replacement = new RefreshToken
        {
            UserId = user.Id,
            TokenHash = "hash_winner",
            CreatedAt = now,
            ExpiresAt = now.AddMinutes(30),
            AbsoluteExpiresAt = now.AddHours(24),
            CreatedByIp = "127.0.0.1"
        };
        (await writerRepo.TryRotateAsync(token, replacement, "127.0.0.1")).Should().BeTrue();

        // Tracked lookup still sees stale cached state; NoTracking sees updated RevokedAt & ReplacedByTokenHash
        var noTrackingFresh = await readerRepo.GetByTokenHashNoTrackingAsync("hash_race");
        noTrackingFresh.Should().NotBeNull();
        noTrackingFresh!.RevokedAt.Should().NotBeNull();
        noTrackingFresh.ReplacedByTokenHash.Should().Be("hash_winner");
    }

    [Fact]
    public async Task RevokeAllUserTokensAsync_InMemoryBranchRevokesAllActiveTokens()
    {
        await using var context = CreateInMemoryDbContext();
        var user = await SeedUserWithRoleAsync(context);
        var now = DateTime.UtcNow;

        await context.RefreshTokens.AddRangeAsync(
            new RefreshToken
            {
                UserId = user.Id,
                TokenHash = "hash_a",
                CreatedAt = now,
                ExpiresAt = now.AddMinutes(30),
                AbsoluteExpiresAt = now.AddHours(24)
            },
            new RefreshToken
            {
                UserId = user.Id,
                TokenHash = "hash_b",
                CreatedAt = now,
                ExpiresAt = now.AddMinutes(30),
                AbsoluteExpiresAt = now.AddHours(24)
            });
        await context.SaveChangesAsync();

        var repo = new RefreshTokenRepository(context);
        await repo.RevokeAllUserTokensAsync(user.Id, "10.0.0.5");

        var all = await context.RefreshTokens.AsNoTracking().ToListAsync();
        all.Should().HaveCount(2);
        all.Should().OnlyContain(t => t.RevokedAt != null && t.RevokedByIp == "10.0.0.5");
    }
}
