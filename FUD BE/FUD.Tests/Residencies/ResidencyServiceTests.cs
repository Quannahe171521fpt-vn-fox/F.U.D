using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using FUD.DTOs.Residencies;
using FUD.Models;
using FUD.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FUD.Tests.Residencies;

public class ResidencyServiceTests
{
    private static FudContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<FudContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new FudContext(options);
    }

    private static async Task<(Campus Campus, Dorm Dorm, Block Block, RoomType RoomType, Room Room1, Room Room2, Bed Bed1, Bed Bed2, Term Term1, Term Term2, Term Term3)> SeedLocationAndTermsAsync(FudContext context)
    {
        var campus = new Campus { Id = 1, Code = "HL", Name = "Hòa Lạc" };
        var dorm = new Dorm { Id = 1, CampusId = 1, Code = "A", Name = "Dom A", Status = "Active", Campus = campus };
        var block = new Block { Id = 10, DormId = 1, Code = "A1", Name = "Block A1", GenderType = "Male", Status = "Active", Dorm = dorm };
        var roomType = new RoomType { Id = 1, Name = "Phòng 4 giường", BedCapacity = 4 };
        var room1 = new Room { Id = 101, BlockId = 10, RoomTypeId = 1, RoomNumber = "A101", FloorLevel = 1, Status = "Active", Block = block, RoomType = roomType };
        var room2 = new Room { Id = 102, BlockId = 10, RoomTypeId = 1, RoomNumber = "A202", FloorLevel = 2, Status = "Active", Block = block, RoomType = roomType };
        var bed1 = new Bed { Id = 1001, RoomId = 101, BedCode = "A101-1", PositionInRoom = "Dưới trái", Status = "Occupied", Room = room1 };
        var bed2 = new Bed { Id = 1002, RoomId = 102, BedCode = "A202-2", PositionInRoom = "Trên phải", Status = "Occupied", Room = room2 };

        var term1 = new Term { Id = 1, Name = "Spring", AcademicYear = "2025-2026", StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 4, 30) };
        var term2 = new Term { Id = 2, Name = "Summer", AcademicYear = "2025-2026", StartDate = new DateOnly(2026, 5, 1), EndDate = new DateOnly(2026, 8, 31) };
        var term3 = new Term { Id = 3, Name = "Fall", AcademicYear = "2026-2027", StartDate = new DateOnly(2026, 9, 1), EndDate = new DateOnly(2026, 12, 31) };

        context.Campuses.Add(campus);
        context.Dorms.Add(dorm);
        context.Blocks.Add(block);
        context.RoomTypes.Add(roomType);
        context.Rooms.AddRange(room1, room2);
        context.Beds.AddRange(bed1, bed2);
        context.Terms.AddRange(term1, term2, term3);
        await context.SaveChangesAsync();

        return (campus, dorm, block, roomType, room1, room2, bed1, bed2, term1, term2, term3);
    }

    [Fact]
    public async Task GetStudentResidencyHistoryAsync_ReturnsOnlyOwnResidencies_OrderedByStatusThenTermStartDateDesc()
    {
        await using var context = CreateInMemoryContext();
        await SeedLocationAndTermsAsync(context);

        context.Residencies.AddRange(
            new Residency { Id = 1, StudentId = 10, BedId = 1001, TermId = 1, Status = "Cancelled", RegistrationType = null },
            new Residency { Id = 2, StudentId = 10, BedId = 1001, TermId = 1, Status = "Ended", RegistrationType = "Booking", CheckInDate = new DateOnly(2026, 1, 2) },
            new Residency { Id = 3, StudentId = 10, BedId = 1002, TermId = 3, Status = "Reserved", RegistrationType = "Keeping" },
            new Residency { Id = 4, StudentId = 10, BedId = 1001, TermId = 2, Status = "Active", RegistrationType = "Transfer", CheckInDate = new DateOnly(2026, 5, 2) },
            new Residency { Id = 5, StudentId = 99, BedId = 1002, TermId = 2, Status = "Active", RegistrationType = "Booking" });
        await context.SaveChangesAsync();

        var sut = new ResidencyService(context);
        var result = await sut.GetStudentResidencyHistoryAsync(10);

        result.Should().HaveCount(4);
        result.Select(x => x.Status).Should().ContainInOrder("Active", "Reserved", "Ended", "Cancelled");
        result.Should().OnlyContain(x => x.StudentId == 10);
        result[0].ResidencyCode.Should().Be("RES-0004");
        result[0].DormName.Should().Be("Dom A");
        result[0].RoomNumber.Should().Be("A101");
        result[3].RegistrationType.Should().Be("Booking");
    }

    [Fact]
    public async Task GetStudentResidencyHistoryAsync_WhenNoResidencies_ReturnsEmptyList()
    {
        await using var context = CreateInMemoryContext();
        var sut = new ResidencyService(context);

        var result = await sut.GetStudentResidencyHistoryAsync(404);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetStudentResidencyHistoryAsync_ExcludesDismissedViolationsFromCount()
    {
        await using var context = CreateInMemoryContext();
        await SeedLocationAndTermsAsync(context);

        context.Residencies.Add(new Residency { Id = 1, StudentId = 10, BedId = 1001, TermId = 2, Status = "Active" });
        context.Violations.AddRange(
            new Violation { Id = 1, Code = "VIO-001", StudentId = 10, RoomId = 101, ResidencyId = 1, ViolationTypeId = 1, RecordedById = 50, OccurrenceTime = DateTime.UtcNow, Status = "Finalized" },
            new Violation { Id = 2, Code = "VIO-002", StudentId = 10, RoomId = 101, ResidencyId = 1, ViolationTypeId = 1, RecordedById = 50, OccurrenceTime = DateTime.UtcNow, Status = "Pending" },
            new Violation { Id = 3, Code = "VIO-003", StudentId = 10, RoomId = 101, ResidencyId = 1, ViolationTypeId = 1, RecordedById = 50, OccurrenceTime = DateTime.UtcNow, Status = "Dismissed" });
        await context.SaveChangesAsync();

        var sut = new ResidencyService(context);
        var result = await sut.GetStudentResidencyHistoryAsync(10);

        result.Should().ContainSingle();
        result[0].ViolationsCount.Should().Be(2);
    }

    [Fact]
    public async Task GetStudentResidencyHistoryAsync_FilterByStatus_CaseInsensitiveAndTrimmed()
    {
        await using var context = CreateInMemoryContext();
        await SeedLocationAndTermsAsync(context);

        context.Residencies.AddRange(
            new Residency { Id = 1, StudentId = 10, BedId = 1001, TermId = 1, Status = "Ended" },
            new Residency { Id = 2, StudentId = 10, BedId = 1001, TermId = 2, Status = "Active" });
        await context.SaveChangesAsync();

        var sut = new ResidencyService(context);
        var result = await sut.GetStudentResidencyHistoryAsync(10, new ResidencyHistoryQueryDto { Status = "  active  " });

        result.Should().ContainSingle();
        result[0].Id.Should().Be(2);
    }

    [Fact]
    public async Task GetStudentResidencyHistoryAsync_FilterByRegistrationType_ReturnsMatchingOnly()
    {
        await using var context = CreateInMemoryContext();
        await SeedLocationAndTermsAsync(context);

        context.Residencies.AddRange(
            new Residency { Id = 1, StudentId = 10, BedId = 1001, TermId = 1, Status = "Ended", RegistrationType = "Booking" },
            new Residency { Id = 2, StudentId = 10, BedId = 1001, TermId = 2, Status = "Active", RegistrationType = "Keeping" });
        await context.SaveChangesAsync();

        var sut = new ResidencyService(context);
        var result = await sut.GetStudentResidencyHistoryAsync(10, new ResidencyHistoryQueryDto { RegistrationType = "Keeping" });

        result.Should().ContainSingle();
        result[0].Id.Should().Be(2);
    }

    [Fact]
    public async Task GetStudentResidencyHistoryAsync_FilterByTerm_MatchesNameYearOrCombined()
    {
        await using var context = CreateInMemoryContext();
        await SeedLocationAndTermsAsync(context);

        context.Residencies.AddRange(
            new Residency { Id = 1, StudentId = 10, BedId = 1001, TermId = 1, Status = "Ended" },
            new Residency { Id = 2, StudentId = 10, BedId = 1002, TermId = 3, Status = "Reserved" });
        await context.SaveChangesAsync();

        var sut = new ResidencyService(context);
        var result = await sut.GetStudentResidencyHistoryAsync(10, new ResidencyHistoryQueryDto { Term = "fall 2026-2027" });

        result.Should().ContainSingle();
        result[0].Id.Should().Be(2);
    }

    [Fact]
    public async Task GetStudentResidencyHistoryAsync_FilterBySearch_MatchesResidencyCodeAndRoom()
    {
        await using var context = CreateInMemoryContext();
        await SeedLocationAndTermsAsync(context);

        context.Residencies.AddRange(
            new Residency { Id = 1, StudentId = 10, BedId = 1001, TermId = 1, Status = "Ended" },
            new Residency { Id = 2, StudentId = 10, BedId = 1002, TermId = 2, Status = "Active" });
        await context.SaveChangesAsync();

        var sut = new ResidencyService(context);
        var byCode = await sut.GetStudentResidencyHistoryAsync(10, new ResidencyHistoryQueryDto { Search = "RES-0002" });
        var byRoom = await sut.GetStudentResidencyHistoryAsync(10, new ResidencyHistoryQueryDto { Search = "a202" });

        byCode.Should().ContainSingle().Which.Id.Should().Be(2);
        byRoom.Should().ContainSingle().Which.Id.Should().Be(2);
    }

    [Fact]
    public async Task GetStudentResidencyHistoryAsync_WhenFilterMatchesNothing_ReturnsEmptyList()
    {
        await using var context = CreateInMemoryContext();
        await SeedLocationAndTermsAsync(context);

        context.Residencies.Add(new Residency { Id = 1, StudentId = 10, BedId = 1001, TermId = 2, Status = "Active" });
        await context.SaveChangesAsync();

        var sut = new ResidencyService(context);
        var result = await sut.GetStudentResidencyHistoryAsync(10, new ResidencyHistoryQueryDto { Search = "NONEXISTENT-ROOM" });

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetStudentResidencyDetailAsync_WhenResidencyDoesNotExist_ReturnsNull()
    {
        await using var context = CreateInMemoryContext();
        var sut = new ResidencyService(context);

        var result = await sut.GetStudentResidencyDetailAsync(studentUserId: 10, residencyId: 999);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetStudentResidencyDetailAsync_WhenResidencyBelongsToAnotherStudent_ReturnsNull()
    {
        await using var context = CreateInMemoryContext();
        await SeedLocationAndTermsAsync(context);

        context.Residencies.Add(new Residency { Id = 10, StudentId = 10, BedId = 1001, TermId = 2, Status = "Active" });
        await context.SaveChangesAsync();

        var sut = new ResidencyService(context);
        var result = await sut.GetStudentResidencyDetailAsync(studentUserId: 99, residencyId: 10);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetStudentResidencyDetailAsync_WhenValid_ReturnsMetadataHandoverSourceBookingAndActiveInvoicesOnly()
    {
        await using var context = CreateInMemoryContext();
        await SeedLocationAndTermsAsync(context);

        var guardUser = new User { Id = 50, Code = "GD01", FullName = "Tran Bao Ve", Email = "gd01@fpt.edu.vn", Gender = "Male", AccountStatus = "Active" };
        var guard = new Guard { UserId = 50, DormId = 1, User = guardUser };
        var booking = new BookingRequest
        {
            Id = 200,
            StudentId = 10,
            TermId = 2,
            BedId = 1001,
            BookingKind = "Booking",
            Status = "Completed",
            PaymentDeadline = DateTime.UtcNow.AddDays(3),
            CreatedTime = new DateTime(2026, 4, 20, 8, 0, 0, DateTimeKind.Utc)
        };
        var residency = new Residency
        {
            Id = 10,
            StudentId = 10,
            BedId = 1001,
            TermId = 2,
            SourceBookingId = 200,
            Status = "Active",
            RegistrationType = "Booking",
            CheckInDate = new DateOnly(2026, 5, 1)
        };
        var handover = new Handover
        {
            Id = 300,
            HandoverType = "Checkin",
            ResidencyId = 10,
            RoomId = 101,
            GuardId = 50,
            Status = "Completed",
            HasDamageOrMissingFlag = true,
            CompletedTime = new DateTime(2026, 5, 1, 9, 30, 0, DateTimeKind.Utc),
            CreatedTime = new DateTime(2026, 5, 1, 9, 0, 0, DateTimeKind.Utc)
        };

        context.Users.Add(guardUser);
        context.Guards.Add(guard);
        context.BookingRequests.Add(booking);
        context.Residencies.Add(residency);
        context.Handovers.Add(handover);
        context.Invoices.AddRange(
            new Invoice { Id = 1, StudentId = 10, TermId = 2, BookingRequestId = 200, InvoiceNumber = "INV-ROOM-1", InvoiceType = "RoomFee", Amount = 4000000m, Status = "Paid", IssuedTime = DateTime.UtcNow.AddDays(-10), DueDate = new DateOnly(2026, 5, 5) },
            new Invoice { Id = 2, StudentId = 10, TermId = 2, HandoverId = 300, InvoiceNumber = "INV-COMP-1", InvoiceType = "Compensation", Amount = 150000m, Status = "Unpaid", IssuedTime = DateTime.UtcNow.AddDays(-1), DueDate = new DateOnly(2026, 5, 15) },
            new Invoice { Id = 3, StudentId = 10, TermId = 2, BookingRequestId = 200, InvoiceNumber = "INV-DEL-1", InvoiceType = "RoomFee", Amount = 4000000m, Status = "Cancelled", IssuedTime = DateTime.UtcNow, DueDate = new DateOnly(2026, 5, 5), DeletedAt = DateTime.UtcNow });
        await context.SaveChangesAsync();

        var sut = new ResidencyService(context);
        var detail = await sut.GetStudentResidencyDetailAsync(10, 10);

        detail.Should().NotBeNull();
        detail!.ResidencyCode.Should().Be("RES-0010");
        detail.SourceRequestStatus.Should().Be("Completed");
        detail.HasCheckinHandover.Should().BeTrue();
        detail.CheckinHandoverId.Should().Be(300);
        detail.CheckinGuardName.Should().Be("Tran Bao Ve");
        detail.HasDamageOrMissingAtCheckin.Should().BeTrue();
        detail.Invoices.Should().HaveCount(2);
        detail.Invoices.Select(i => i.InvoiceNumber).Should().BeEquivalentTo(new[] { "INV-ROOM-1", "INV-COMP-1" });
    }

    [Fact]
    public async Task GetStudentResidencyDetailAsync_MapsOutgoingAndIncomingTransfers_AndFlagsUnpaidInvoice()
    {
        await using var context = CreateInMemoryContext();
        await SeedLocationAndTermsAsync(context);

        var res1 = new Residency { Id = 10, StudentId = 10, BedId = 1001, TermId = 2, Status = "Active" };
        var res2 = new Residency { Id = 11, StudentId = 10, BedId = 1002, TermId = 2, Status = "Ended" };
        var outReq = new Request { Id = 70, StudentId = 10, TermId = 2, RequestType = "BedTransfer", Status = "Completed", CreatedTime = DateTime.UtcNow.AddDays(-2) };
        var inReq = new Request { Id = 71, StudentId = 10, TermId = 2, RequestType = "BedTransfer", Status = "Completed", CreatedTime = DateTime.UtcNow.AddDays(-5) };

        context.Residencies.AddRange(res1, res2);
        context.Requests.AddRange(outReq, inReq);
        context.BedTransferRequests.AddRange(
            new BedTransferRequest { RequestId = 70, FromResidencyId = 10, NewResidencyId = 11, FromBedId = 1001, ToBedId = 1002, TransferReason = "Chuyển đi", PriceDifference = 300000m },
            new BedTransferRequest { RequestId = 71, FromResidencyId = 11, NewResidencyId = 10, FromBedId = 1002, ToBedId = 1001, TransferReason = "Chuyển đến", PriceDifference = 0m });
        context.Invoices.AddRange(
            new Invoice { Id = 1, StudentId = 10, TermId = 2, TransferRequestId = 70, InvoiceNumber = "INV-TR-70", InvoiceType = "BedTransfer", Amount = 300000m, Status = "Unpaid", IssuedTime = DateTime.UtcNow, DueDate = new DateOnly(2026, 6, 1) },
            new Invoice { Id = 2, StudentId = 10, TermId = 2, TransferRequestId = 71, InvoiceNumber = "INV-TR-71", InvoiceType = "BedTransfer", Amount = 100000m, Status = "Paid", IssuedTime = DateTime.UtcNow, DueDate = new DateOnly(2026, 6, 1) });
        await context.SaveChangesAsync();

        var sut = new ResidencyService(context);
        var detail = await sut.GetStudentResidencyDetailAsync(10, 10);

        detail.Should().NotBeNull();
        detail!.Transfers.Should().HaveCount(2);
        var outgoing = detail.Transfers.Single(t => t.RequestId == 70);
        var incoming = detail.Transfers.Single(t => t.RequestId == 71);
        outgoing.Direction.Should().Be("Outgoing");
        outgoing.HasUnpaidInvoice.Should().BeTrue();
        incoming.Direction.Should().Be("Incoming");
        incoming.HasUnpaidInvoice.Should().BeFalse();
    }

    [Fact]
    public async Task GetStudentResidencyDetailAsync_MapsCheckoutRequestsWithHandoverAndCompensationInvoice()
    {
        await using var context = CreateInMemoryContext();
        await SeedLocationAndTermsAsync(context);

        var res = new Residency { Id = 20, StudentId = 10, BedId = 1001, TermId = 2, Status = "Ended" };
        var coReq = new Request { Id = 80, StudentId = 10, TermId = 2, RequestType = "Checkout", Status = "Completed", Reason = "Nghỉ hè sớm", CreatedTime = DateTime.UtcNow.AddDays(-3) };
        var coHandover = new Handover { Id = 310, HandoverType = "Checkout", RequestId = 80, RoomId = 101, GuardId = 50, Status = "Completed", TotalCompensation = 250000m, CreatedTime = DateTime.UtcNow.AddDays(-2) };

        context.Residencies.Add(res);
        context.Requests.Add(coReq);
        context.Handovers.Add(coHandover);
        context.CheckoutRequests.Add(new CheckoutRequest
        {
            RequestId = 80,
            ResidencyId = 20,
            ExpectedCheckOutDate = new DateOnly(2026, 8, 15),
            IsExceptionalCase = true,
            ExceptionNote = "Bảo lưu học kỳ"
        });
        context.Invoices.Add(new Invoice
        {
            Id = 9,
            StudentId = 10,
            TermId = 2,
            HandoverId = 310,
            InvoiceNumber = "INV-CO-310",
            InvoiceType = "Compensation",
            Amount = 250000m,
            Status = "Unpaid",
            IssuedTime = DateTime.UtcNow.AddDays(-1),
            DueDate = new DateOnly(2026, 8, 25)
        });
        await context.SaveChangesAsync();

        var sut = new ResidencyService(context);
        var detail = await sut.GetStudentResidencyDetailAsync(10, 20);

        detail.Should().NotBeNull();
        detail!.Checkouts.Should().ContainSingle();
        detail.Checkouts[0].RequestId.Should().Be(80);
        detail.Checkouts[0].IsExceptionalCase.Should().BeTrue();
        detail.Checkouts[0].CheckoutHandoverId.Should().Be(310);
        detail.Checkouts[0].TotalCompensation.Should().Be(250000m);
        detail.Invoices.Should().ContainSingle().Which.InvoiceNumber.Should().Be("INV-CO-310");
    }

    [Fact]
    public async Task GetStudentResidencyDetailAsync_WhenSelfReferencingTransferExists_DoesNotDuplicateTransferRecord()
    {
        await using var context = CreateInMemoryContext();
        await SeedLocationAndTermsAsync(context);

        var res = new Residency { Id = 30, StudentId = 10, BedId = 1001, TermId = 2, Status = "Active" };
        var req = new Request { Id = 85, StudentId = 10, TermId = 2, RequestType = "BedTransfer", Status = "Completed", CreatedTime = DateTime.UtcNow };
        context.Residencies.Add(res);
        context.Requests.Add(req);
        context.BedTransferRequests.Add(new BedTransferRequest
        {
            RequestId = 85,
            FromResidencyId = 30,
            NewResidencyId = 30,
            FromBedId = 1001,
            ToBedId = 1002,
            TransferReason = "Self-linked edge case"
        });
        await context.SaveChangesAsync();

        var sut = new ResidencyService(context);
        var detail = await sut.GetStudentResidencyDetailAsync(10, 30);

        detail.Should().NotBeNull();
        detail!.Transfers.Should().ContainSingle().Which.RequestId.Should().Be(85);
    }
}
