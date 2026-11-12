using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using FUD.DTOs.Residencies;
using FUD.Models;
using FUD.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FUD.Tests.Admin;

/// <summary>
/// Unit tests cho ResidencyService — tuân thủ quy tắc AAA.
/// Dùng EF Core InMemory vì ResidencyService inject FudContext trực tiếp (không qua interface).
/// </summary>
public class ResidencyServiceTests : IDisposable
{
    private readonly FudContext _context;

    public ResidencyServiceTests()
    {
        var options = new DbContextOptionsBuilder<FudContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()) // DB mới cho mỗi test
            .Options;
        _context = new FudContext(options);
        SeedLookupData();
    }

    public void Dispose() => _context.Dispose();

    // ─── Seed helpers ────────────────────────────────────────────────────────────

    /// <summary>Seed dữ liệu tra cứu dùng chung cho mọi test.</summary>
    private void SeedLookupData()
    {
        var campus  = new Campus  { Id = 1, Code = "HL", Name = "Hòa Lạc" };
        var dorm    = new Dorm    { Id = 1, Name = "KTX A", Code = "A", CampusId = 1, Status = "Active" };
        var block   = new Block   { Id = 1, Name = "Block 1", Code = "B1", DormId = 1, GenderType = "Male", Status = "Active" };
        var rtype   = new RoomType{ Id = 1, Name = "4 người", BedCapacity = 4 };
        var room    = new Room    { Id = 1, RoomNumber = "101", FloorLevel = 1, BlockId = 1, RoomTypeId = 1, Status = "Active" };
        var bed     = new Bed     { Id = 1, BedCode = "B1-101-01", PositionInRoom = "1", RoomId = 1, Status = "Occupied" };
        var term    = new Term    { Id = 1, Name = "HK1", AcademicYear = "2025-2026",
                                    StartDate = new DateOnly(2025,9,1), EndDate = new DateOnly(2026,1,15) };

        _context.AddRange(campus, dorm, block, rtype, room, bed, term);
        _context.SaveChanges();
    }

    private ResidencyService CreateSut() => new(_context);

    // ─── GetStudentResidencyHistoryAsync ─────────────────────────────────────────

    [Fact]
    public async Task GetHistory_WhenStudentHasNoResidencies_ReturnsEmptyList()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var result = await sut.GetStudentResidencyHistoryAsync(studentUserId: 999);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetHistory_WhenFilteringByStatus_ReturnsOnlyMatchingItems()
    {
        // Arrange
        _context.Residencies.AddRange(
            new Residency { Id = 1, StudentId = 1, TermId = 1, BedId = 1, Status = "Active",   RegistrationType = "Booking" },
            new Residency { Id = 2, StudentId = 1, TermId = 1, BedId = 1, Status = "Ended",    RegistrationType = "Booking" }
        );
        await _context.SaveChangesAsync();

        var sut = CreateSut();

        // Act
        var result = await sut.GetStudentResidencyHistoryAsync(1, new ResidencyHistoryQueryDto { Status = "Active" });

        // Assert
        result.Should().HaveCount(1);
        result[0].Status.Should().Be("Active");
    }

    [Fact]
    public async Task GetHistory_WhenFilteringByRegistrationType_ReturnsOnlyMatchingItems()
    {
        // Arrange
        _context.Residencies.AddRange(
            new Residency { Id = 3, StudentId = 2, TermId = 1, BedId = 1, Status = "Active", RegistrationType = "Booking" },
            new Residency { Id = 4, StudentId = 2, TermId = 1, BedId = 1, Status = "Ended",  RegistrationType = "Transfer" }
        );
        await _context.SaveChangesAsync();

        var sut = CreateSut();

        // Act
        var result = await sut.GetStudentResidencyHistoryAsync(2, new ResidencyHistoryQueryDto { RegistrationType = "Transfer" });

        // Assert
        result.Should().HaveCount(1);
        result[0].RegistrationType.Should().Be("Transfer");
    }

    [Fact]
    public async Task GetHistory_WhenFilteringByTermKeyword_ReturnsMatchingItems()
    {
        // Arrange
        _context.Residencies.Add(
            new Residency { Id = 5, StudentId = 3, TermId = 1, BedId = 1, Status = "Active", RegistrationType = "Booking" }
        );
        await _context.SaveChangesAsync();

        var sut = CreateSut();

        // Act
        var result = await sut.GetStudentResidencyHistoryAsync(3, new ResidencyHistoryQueryDto { Term = "HK1" });

        // Assert
        result.Should().HaveCount(1);
        result[0].TermName.Should().Be("HK1");
    }

    [Fact]
    public async Task GetHistory_WhenFilteringBySearch_ReturnsMatchingByRoomNumber()
    {
        // Arrange
        _context.Residencies.Add(
            new Residency { Id = 6, StudentId = 4, TermId = 1, BedId = 1, Status = "Active", RegistrationType = "Booking" }
        );
        await _context.SaveChangesAsync();

        var sut = CreateSut();

        // Act
        var result = await sut.GetStudentResidencyHistoryAsync(4, new ResidencyHistoryQueryDto { Search = "101" });

        // Assert
        result.Should().HaveCount(1);
        result[0].RoomNumber.Should().Be("101");
    }

    [Fact]
    public async Task GetHistory_SortOrder_ActiveComesBeforeEnded()
    {
        // Arrange
        _context.Residencies.AddRange(
            new Residency { Id = 7, StudentId = 5, TermId = 1, BedId = 1, Status = "Ended",  RegistrationType = "Booking" },
            new Residency { Id = 8, StudentId = 5, TermId = 1, BedId = 1, Status = "Active",  RegistrationType = "Booking" }
        );
        await _context.SaveChangesAsync();

        var sut = CreateSut();

        // Act
        var result = await sut.GetStudentResidencyHistoryAsync(5);

        // Assert
        result[0].Status.Should().Be("Active");
    }

    [Fact]
    public async Task GetHistory_ResidencyCodeFormat_IsCorrect()
    {
        // Arrange
        _context.Residencies.Add(
            new Residency { Id = 5, StudentId = 6, TermId = 1, BedId = 1, Status = "Active", RegistrationType = "Booking" }
        );
        await _context.SaveChangesAsync();

        var sut = CreateSut();

        // Act
        var result = await sut.GetStudentResidencyHistoryAsync(6);

        // Assert
        result[0].ResidencyCode.Should().Be("RES-0005");
    }

    // ─── GetStudentResidencyDetailAsync ──────────────────────────────────────────

    [Fact]
    public async Task GetDetail_WhenResidencyBelongsToDifferentStudent_ReturnsNull()
    {
        // Arrange
        _context.Residencies.Add(
            new Residency { Id = 10, StudentId = 1, TermId = 1, BedId = 1, Status = "Active", RegistrationType = "Booking" }
        );
        await _context.SaveChangesAsync();

        var sut = CreateSut();

        // Act
        var result = await sut.GetStudentResidencyDetailAsync(studentUserId: 999, residencyId: 10);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetDetail_WhenResidencyHasHandover_SetsHasCheckinHandoverTrue()
    {
        // Arrange
        var user    = new User  { Id = 20, Code = "guard1", FullName = "Guard 1", Email = "g@t.com",
                                  Gender = "Male", AccountStatus = "Active", CreatedAt = DateTime.UtcNow };
        var guard   = new Guard { UserId = 20, User = user };

        _context.Users.Add(user);
        _context.Guards.Add(guard);
        _context.Residencies.Add(new Residency
        {
            Id = 11, StudentId = 7, TermId = 1, BedId = 1,
            Status = "Active", RegistrationType = "Booking"
        });
        _context.Handovers.Add(new Handover
        {
            Id = 1, HandoverType = "CheckIn", Status = "Completed",
            GuardId = 20, RoomId = 1, ResidencyId = 11,
            CreatedTime = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        var sut = CreateSut();

        // Act
        var result = await sut.GetStudentResidencyDetailAsync(studentUserId: 7, residencyId: 11);

        // Assert
        result.Should().NotBeNull();
        result!.HasCheckinHandover.Should().BeTrue();
        result.CheckinHandoverId.Should().Be(1);
    }

    [Fact]
    public async Task GetDetail_WhenResidencyHasInvoice_InvoicesAreMapped()
    {
        // Arrange
        var request = new Request
        {
            Id = 1, StudentId = 8, TermId = 1, RequestType = "Booking",
            Status = "Approved", CreatedTime = DateTime.UtcNow
        };
        var bookingRequest = new BookingRequest
        {
            RequestId = 1, Request = request, BedId = 1, BookingKind = "New",
            AgreementToRegulations = true, PaymentDeadline = DateTime.UtcNow.AddDays(3)
        };
        var residency = new Residency
        {
            Id = 12, StudentId = 8, TermId = 1, BedId = 1,
            Status = "Active", RegistrationType = "Booking",
            SourceRequestId = 1
        };
        var invoice = new Invoice
        {
            Id = 1, StudentId = 8, TermId = 1, BookingRequestId = 1,
            InvoiceNumber = "INV-0001", InvoiceType = "RoomFee",
            Amount = 2_000_000m, Status = "Paid",
            IssuedTime = DateTime.UtcNow, DueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30))
        };

        _context.Requests.Add(request);
        _context.BookingRequests.Add(bookingRequest);
        _context.Residencies.Add(residency);
        _context.Invoices.Add(invoice);
        await _context.SaveChangesAsync();

        var sut = CreateSut();

        // Act
        var result = await sut.GetStudentResidencyDetailAsync(studentUserId: 8, residencyId: 12);

        // Assert
        result.Should().NotBeNull();
        result!.Invoices.Should().HaveCount(1);
        result.Invoices[0].InvoiceNumber.Should().Be("INV-0001");
    }
}

