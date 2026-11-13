using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using FUD.Models;
using FUD.Repositories.Interfaces.Keeping;
using FUD.Services.Keeping;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace FUD.Tests.Keeping;

/// <summary>
/// Unit tests cho KeepingPeriodCloseJob (UC02) — tuân thủ quy tắc AAA (Arrange / Act / Assert).
/// </summary>
public class KeepingPeriodCloseJobTests
{
    private readonly Mock<IKeepingRepository> _keepingRepoMock = new();
    private readonly Mock<ILogger<KeepingPeriodCloseJob>> _loggerMock = new();

    private static FudContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<FudContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new FudContext(options);
    }

    private KeepingPeriodCloseJob CreateSut(FudContext context) => new(
        _keepingRepoMock.Object,
        context,
        _loggerMock.Object);

    private static Term CreateActiveCurrentTerm(int termId = 1)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        return new Term
        {
            Id = termId,
            Name = "Current Term",
            AcademicYear = "2026-2027",
            StartDate = today.AddDays(-30),
            EndDate = today.AddDays(30)
        };
    }

    [Fact]
    public async Task ExecuteAsync_WhenNoCurrentTermFound_ReturnsZeroCounts()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var sut = CreateSut(context);

        // Act
        var result = await sut.ExecuteAsync(nextTermId: 2);

        // Assert
        result.TotalResidents.Should().Be(0);
        result.ReleasedBeds.Should().Be(0);
        result.KeptBeds.Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_ReleasesBedsForStudentsWithoutKeeping_AndKeepsBedsForStudentsWithKeeping()
    {
        // Arrange
        const int currentTermId = 1;
        const int nextTermId = 2;
        using var context = CreateInMemoryContext();

        var currentTerm = CreateActiveCurrentTerm(currentTermId);
        var bedKept = new Bed { Id = 10, RoomId = 1, BedCode = "A-101-1", Status = "Occupied" };
        var bedReleased = new Bed { Id = 20, RoomId = 1, BedCode = "A-101-2", Status = "Occupied" };

        await context.Terms.AddAsync(currentTerm);
        await context.Beds.AddRangeAsync(bedKept, bedReleased);
        await context.SaveChangesAsync();

        var activeResidencies = new List<Residency>
        {
            new() { Id = 101, StudentId = 1, BedId = bedKept.Id, TermId = currentTermId, Status = "Active", Bed = bedKept },
            new() { Id = 102, StudentId = 2, BedId = bedReleased.Id, TermId = currentTermId, Status = "Active", Bed = bedReleased }
        };

        _keepingRepoMock.Setup(r => r.GetStudentsWithCompletedKeepingAsync(nextTermId))
            .ReturnsAsync(new HashSet<int> { 1 });
        _keepingRepoMock.Setup(r => r.GetActiveResidenciesForTermAsync(currentTermId))
            .ReturnsAsync(activeResidencies);

        var sut = CreateSut(context);

        // Act
        var result = await sut.ExecuteAsync(nextTermId);

        // Assert
        result.TotalResidents.Should().Be(2);
        result.KeptBeds.Should().Be(1);
        result.ReleasedBeds.Should().Be(1);

        var dbBedKept = await context.Beds.FirstAsync(b => b.Id == bedKept.Id);
        var dbBedReleased = await context.Beds.FirstAsync(b => b.Id == bedReleased.Id);
        dbBedKept.Status.Should().Be("Occupied");
        dbBedReleased.Status.Should().Be("Free");

        context.AuditLogs.Should().ContainSingle(a =>
            a.ActionType == "RELEASE_BED" &&
            a.TargetEntity == "KeepingPeriodCloseBed" &&
            a.UserId == 2);
    }

    [Fact]
    public async Task ExecuteAsync_WhenBedAlreadyFree_SkipsReleasingIdempotently()
    {
        // Arrange
        const int currentTermId = 1;
        const int nextTermId = 2;
        using var context = CreateInMemoryContext();

        var currentTerm = CreateActiveCurrentTerm(currentTermId);
        var bedAlreadyFree = new Bed { Id = 30, RoomId = 1, BedCode = "A-101-3", Status = "Free" };

        await context.Terms.AddAsync(currentTerm);
        await context.Beds.AddAsync(bedAlreadyFree);
        await context.SaveChangesAsync();

        var activeResidencies = new List<Residency>
        {
            new() { Id = 103, StudentId = 3, BedId = bedAlreadyFree.Id, TermId = currentTermId, Status = "Active", Bed = bedAlreadyFree }
        };

        _keepingRepoMock.Setup(r => r.GetStudentsWithCompletedKeepingAsync(nextTermId))
            .ReturnsAsync(new HashSet<int>());
        _keepingRepoMock.Setup(r => r.GetActiveResidenciesForTermAsync(currentTermId))
            .ReturnsAsync(activeResidencies);

        var sut = CreateSut(context);

        // Act
        var result = await sut.ExecuteAsync(nextTermId);

        // Assert
        result.TotalResidents.Should().Be(1);
        result.ReleasedBeds.Should().Be(0);
        result.KeptBeds.Should().Be(0);
        context.AuditLogs.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_WithPeriodId_WritesCloseKeepingPeriodAuditMarker()
    {
        // Arrange
        const int currentTermId = 1;
        const int nextTermId = 2;
        const int periodId = 77;
        using var context = CreateInMemoryContext();

        var currentTerm = CreateActiveCurrentTerm(currentTermId);
        await context.Terms.AddAsync(currentTerm);
        await context.SaveChangesAsync();

        _keepingRepoMock.Setup(r => r.GetStudentsWithCompletedKeepingAsync(nextTermId))
            .ReturnsAsync(new HashSet<int>());
        _keepingRepoMock.Setup(r => r.GetActiveResidenciesForTermAsync(currentTermId))
            .ReturnsAsync(new List<Residency>());

        var sut = CreateSut(context);

        // Act
        var result = await sut.ExecuteAsync(nextTermId, periodId);

        // Assert
        result.TotalResidents.Should().Be(0);
        context.AuditLogs.Should().ContainSingle(a =>
            a.ActionType == "CLOSE_KEEPING_PERIOD" &&
            a.TargetEntity == "KeepingPeriodClose" &&
            a.TargetId == periodId.ToString());
    }
}

