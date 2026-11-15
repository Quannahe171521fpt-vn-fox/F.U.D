using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using FUD.Models;
using FUD.Repositories.Keeping;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FUD.Tests.Keeping;

/// <summary>
/// Unit tests cho KeepingRepository (UC02) — tuân thủ quy tắc AAA (Arrange / Act / Assert).
/// Sử dụng EF Core InMemory Database để giả lập toàn bộ truy vấn dữ liệu.
/// </summary>
public class KeepingRepositoryTests
{
    private static FudContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<FudContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new FudContext(options);
    }

    private static async Task<(Dorm Dorm, Block Block, Room Room, Bed Bed, Term CurrentTerm, Term NextTerm, Student Student)>
        SeedBasicStructureAsync(FudContext context, int studentId = 10)
    {
        var dorm = new Dorm { Id = 1, CampusId = 1, Code = "DA", Name = "Dorm A", Status = "Active" };
        var block = new Block { Id = 1, DormId = 1, Code = "A1", Name = "Block A1", GenderType = "Male", Status = "Active", Dorm = dorm };
        var room = new Room { Id = 1, BlockId = 1, RoomTypeId = 1, RoomNumber = "101", Status = "Active", Block = block };
        var bed = new Bed { Id = 100, RoomId = 1, BedCode = "A1-101-1", Status = "Occupied", Room = room };

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

        var user = new User
        {
            Id = studentId,
            Code = $"HE{studentId}",
            FullName = $"Student {studentId}",
            Email = $"s{studentId}@fpt.edu.vn",
            Gender = "Male",
            AccountStatus = "Active",
            CreatedAt = DateTime.UtcNow
        };
        var student = new Student { UserId = studentId, User = user };

        await context.Dorms.AddAsync(dorm);
        await context.Blocks.AddAsync(block);
        await context.Rooms.AddAsync(room);
        await context.Beds.AddAsync(bed);
        await context.Terms.AddRangeAsync(currentTerm, nextTerm);
        await context.Users.AddAsync(user);
        await context.Students.AddAsync(student);
        await context.SaveChangesAsync();

        return (dorm, block, room, bed, currentTerm, nextTerm, student);
    }

    [Fact]
    public async Task GetActiveResidencyAsync_ReturnsActiveResidencyWithFullHierarchy()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var (_, _, _, bed, currentTerm, nextTerm, student) = await SeedBasicStructureAsync(context, studentId: 10);

        await context.Residencies.AddRangeAsync(
            new Residency { Id = 1, StudentId = student.UserId, BedId = bed.Id, TermId = currentTerm.Id, Status = "Completed" },
            new Residency { Id = 2, StudentId = student.UserId, BedId = bed.Id, TermId = nextTerm.Id, Status = "Active" }
        );
        await context.SaveChangesAsync();

        var sut = new KeepingRepository(context);

        // Act
        var activeResidency = await sut.GetActiveResidencyAsync(student.UserId);
        var missingResidency = await sut.GetActiveResidencyAsync(999);

        // Assert
        activeResidency.Should().NotBeNull();
        activeResidency!.Id.Should().Be(2);
        activeResidency.Bed.Should().NotBeNull();
        activeResidency.Bed.BedCode.Should().Be("A1-101-1");
        activeResidency.Bed.Room.RoomNumber.Should().Be("101");
        activeResidency.Bed.Room.Block.Name.Should().Be("Block A1");
        activeResidency.Bed.Room.Block.Dorm.Name.Should().Be("Dorm A");
        activeResidency.Term.Name.Should().Be("Fall 2026");
        missingResidency.Should().BeNull();
    }

    [Fact]
    public async Task GetOpenKeepingPeriodAsync_WhenValidOpenPeriodForNextTerm_ReturnsPeriodAndTerms()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var (_, _, _, bed, currentTerm, nextTerm, student) = await SeedBasicStructureAsync(context, studentId: 10);

        await context.Residencies.AddAsync(new Residency
        {
            Id = 1,
            StudentId = student.UserId,
            BedId = bed.Id,
            TermId = currentTerm.Id,
            Status = "Active"
        });

        await context.RegistrationPeriods.AddAsync(new RegistrationPeriod
        {
            Id = 50,
            TermId = nextTerm.Id,
            PeriodType = "Keeping",
            StartDate = new DateOnly(2026, 8, 1),
            EndDate = new DateOnly(2026, 8, 20),
            ActiveFlag = true
        });
        await context.SaveChangesAsync();

        var sut = new KeepingRepository(context);
        var today = new DateOnly(2026, 8, 10);

        // Act
        var (period, foundNextTerm, foundCurrentTerm) = await sut.GetOpenKeepingPeriodAsync(student.UserId, today);

        // Assert
        period.Should().NotBeNull();
        period!.Id.Should().Be(50);
        foundNextTerm.Should().NotBeNull();
        foundNextTerm!.Id.Should().Be(nextTerm.Id);
        foundCurrentTerm.Should().NotBeNull();
        foundCurrentTerm!.Id.Should().Be(currentTerm.Id);
    }

    [Fact]
    public async Task GetOpenKeepingPeriodAsync_WhenPeriodExpiredOrInactive_ReturnsNullPeriod()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var (_, _, _, bed, currentTerm, nextTerm, student) = await SeedBasicStructureAsync(context, studentId: 10);

        await context.Residencies.AddAsync(new Residency
        {
            Id = 1,
            StudentId = student.UserId,
            BedId = bed.Id,
            TermId = currentTerm.Id,
            Status = "Active"
        });

        await context.RegistrationPeriods.AddAsync(new RegistrationPeriod
        {
            Id = 51,
            TermId = nextTerm.Id,
            PeriodType = "Keeping",
            StartDate = new DateOnly(2026, 8, 1),
            EndDate = new DateOnly(2026, 8, 20),
            ActiveFlag = false
        });
        await context.SaveChangesAsync();

        var sut = new KeepingRepository(context);

        // Act
        var (period, foundNextTerm, foundCurrentTerm) =
            await sut.GetOpenKeepingPeriodAsync(student.UserId, new DateOnly(2026, 8, 10));

        // Assert
        period.Should().BeNull();
        foundNextTerm.Should().BeNull();
        foundCurrentTerm.Should().NotBeNull();
        foundCurrentTerm!.Id.Should().Be(currentTerm.Id);
    }

    [Fact]
    public async Task IsPriceListPublishedAsync_ReturnsTrueOnlyWhenPublished()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        await context.PriceLists.AddRangeAsync(
            new PriceList { Id = 1, TermId = 1, Status = "Draft", CreationDate = DateTime.UtcNow, CreatedById = 1 },
            new PriceList { Id = 2, TermId = 2, Status = "Published", CreationDate = DateTime.UtcNow, CreatedById = 1 }
        );
        await context.SaveChangesAsync();

        var sut = new KeepingRepository(context);

        // Act
        var draftTermResult = await sut.IsPriceListPublishedAsync(1);
        var publishedTermResult = await sut.IsPriceListPublishedAsync(2);
        var missingTermResult = await sut.IsPriceListPublishedAsync(99);

        // Assert
        draftTermResult.Should().BeFalse();
        publishedTermResult.Should().BeTrue();
        missingTermResult.Should().BeFalse();
    }

    [Fact]
    public async Task HasOverdueInvoiceAsync_And_HasUnpaidInvoiceAsync_ReturnExpectedBooleans()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        await context.Invoices.AddRangeAsync(
            new Invoice { Id = 1, StudentId = 10, TermId = 1, InvoiceNumber = "INV-1", InvoiceType = "Room", Amount = 100, Status = "Overdue", IssuedTime = DateTime.UtcNow, DueDate = new DateOnly(2026, 6, 1) },
            new Invoice { Id = 2, StudentId = 20, TermId = 1, InvoiceNumber = "INV-2", InvoiceType = "Room", Amount = 100, Status = "Unpaid", IssuedTime = DateTime.UtcNow, DueDate = new DateOnly(2026, 9, 1) },
            new Invoice { Id = 3, StudentId = 30, TermId = 1, InvoiceNumber = "INV-3", InvoiceType = "Room", Amount = 100, Status = "Paid", IssuedTime = DateTime.UtcNow, DueDate = new DateOnly(2026, 6, 1) }
        );
        await context.SaveChangesAsync();

        var sut = new KeepingRepository(context);

        // Act
        var student10Overdue = await sut.HasOverdueInvoiceAsync(10);
        var student10Unpaid = await sut.HasUnpaidInvoiceAsync(10);
        var student20Overdue = await sut.HasOverdueInvoiceAsync(20);
        var student20Unpaid = await sut.HasUnpaidInvoiceAsync(20);
        var student30Overdue = await sut.HasOverdueInvoiceAsync(30);
        var student30Unpaid = await sut.HasUnpaidInvoiceAsync(30);

        // Assert
        student10Overdue.Should().BeTrue();
        student10Unpaid.Should().BeFalse();
        student20Overdue.Should().BeFalse();
        student20Unpaid.Should().BeTrue();
        student30Overdue.Should().BeFalse();
        student30Unpaid.Should().BeFalse();
    }

    [Fact]
    public async Task HasCompletedKeepingRequestAsync_GetCompletedKeepingRequestAsync_And_GetResidencyByStudentAndTermAsync_WorkCorrectly()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var (_, _, _, bed, _, nextTerm, student) = await SeedBasicStructureAsync(context, studentId: 10);

        var request = new Request
        {
            Id = 500,
            StudentId = student.UserId,
            TermId = nextTerm.Id,
            RequestType = "Keeping",
            Status = "Completed",
            CreatedTime = DateTime.UtcNow
        };
        var residency = new Residency
        {
            Id = 600,
            StudentId = student.UserId,
            BedId = bed.Id,
            TermId = nextTerm.Id,
            Status = "Upcoming"
        };
        await context.Requests.AddAsync(request);
        await context.Residencies.AddAsync(residency);
        await context.SaveChangesAsync();

        var sut = new KeepingRepository(context);

        // Act
        var hasCompleted = await sut.HasCompletedKeepingRequestAsync(student.UserId, nextTerm.Id);
        var notCompleted = await sut.HasCompletedKeepingRequestAsync(student.UserId, 999);
        var completedReq = await sut.GetCompletedKeepingRequestAsync(student.UserId, nextTerm.Id);
        var reqById = await sut.GetKeepingRequestByIdAsync(500);
        var foundResidency = await sut.GetResidencyByStudentAndTermAsync(student.UserId, nextTerm.Id);

        // Assert
        hasCompleted.Should().BeTrue();
        notCompleted.Should().BeFalse();
        completedReq.Should().NotBeNull();
        completedReq!.Id.Should().Be(500);
        reqById.Should().NotBeNull();
        reqById!.Student.UserId.Should().Be(student.UserId);
        foundResidency.Should().NotBeNull();
        foundResidency!.Id.Should().Be(600);
        foundResidency.Bed.BedCode.Should().Be("A1-101-1");
    }

    [Fact]
    public async Task GetKeepingBedPriceAsync_And_GetKeepingMonthsAsync_CalculateCorrectly()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var (dorm, _, room, bed, _, nextTerm, _) = await SeedBasicStructureAsync(context, studentId: 10);

        var priceList = new PriceList
        {
            Id = 1,
            TermId = nextTerm.Id,
            Status = "Published",
            CreationDate = DateTime.UtcNow,
            CreatedById = 1
        };
        var priceItem = new PriceItem
        {
            Id = 10,
            PriceListId = priceList.Id,
            DormId = dorm.Id,
            RoomTypeId = room.RoomTypeId,
            UnitPrice = 1_200_000m,
            PriceList = priceList
        };

        await context.PriceLists.AddAsync(priceList);
        await context.PriceItems.AddAsync(priceItem);
        await context.SaveChangesAsync();

        var sut = new KeepingRepository(context);

        // Act
        var price = await sut.GetKeepingBedPriceAsync(bed.Id, nextTerm.Id);
        var missingBedPrice = await sut.GetKeepingBedPriceAsync(999, nextTerm.Id);
        var missingTermPrice = await sut.GetKeepingBedPriceAsync(bed.Id, 999);
        var months = await sut.GetKeepingMonthsAsync(bed.Id, nextTerm.Id);

        // Assert
        price.Should().Be(1_200_000m);
        missingBedPrice.Should().BeNull();
        missingTermPrice.Should().BeNull();
        months.Should().Be(1);
    }

    [Fact]
    public async Task IsBedOccupiedForTermAsync_And_HasResidencyForTermAsync_CheckActiveReservedUpcoming()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        await context.Residencies.AddRangeAsync(
            new Residency { Id = 1, StudentId = 10, BedId = 100, TermId = 2, Status = "Upcoming" },
            new Residency { Id = 2, StudentId = 20, BedId = 200, TermId = 2, Status = "Cancelled" }
        );
        await context.SaveChangesAsync();

        var sut = new KeepingRepository(context);

        // Act
        var bed100Occupied = await sut.IsBedOccupiedForTermAsync(100, 2);
        var bed200Occupied = await sut.IsBedOccupiedForTermAsync(200, 2);
        var student10HasRes = await sut.HasResidencyForTermAsync(10, 2);
        var student20HasRes = await sut.HasResidencyForTermAsync(20, 2);

        // Assert
        bed100Occupied.Should().BeTrue();
        bed200Occupied.Should().BeFalse();
        student10HasRes.Should().BeTrue();
        student20HasRes.Should().BeFalse();
    }

    [Fact]
    public async Task GetStudentsWithCompletedKeepingAsync_ReturnsDistinctStudentIdSet()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        await context.Requests.AddRangeAsync(
            new Request { Id = 1, StudentId = 10, TermId = 2, RequestType = "Keeping", Status = "Completed", CreatedTime = DateTime.UtcNow },
            new Request { Id = 2, StudentId = 11, TermId = 2, RequestType = "Keeping", Status = "Pending", CreatedTime = DateTime.UtcNow },
            new Request { Id = 3, StudentId = 12, TermId = 2, RequestType = "Booking", Status = "Completed", CreatedTime = DateTime.UtcNow }
        );
        await context.SaveChangesAsync();

        var sut = new KeepingRepository(context);

        // Act
        var studentIds = await sut.GetStudentsWithCompletedKeepingAsync(2);

        // Assert
        studentIds.Should().ContainSingle().Which.Should().Be(10);
    }

    [Fact]
    public async Task BeginTransactionAsync_WithInMemoryProvider_ReturnsNoOpTransaction()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var sut = new KeepingRepository(context);

        // Act
        await using var tx = await sut.BeginTransactionAsync();
        Func<Task> commitAct = async () => await tx.CommitAsync();
        Func<Task> rollbackAct = async () => await tx.RollbackAsync();

        // Assert
        await commitAct.Should().NotThrowAsync();
        await rollbackAct.Should().NotThrowAsync();
    }
}
