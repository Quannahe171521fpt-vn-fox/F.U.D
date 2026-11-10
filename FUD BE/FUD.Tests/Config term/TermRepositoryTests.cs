using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using FUD.Models;
using FUD.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FUD.Tests.ConfigTerm;

public class TermRepositoryTests
{
    private FudContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<FudContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new FudContext(options);
    }

    [Fact]
    public async Task GetAllTermsAsync_ReturnsAllTermsWithRelationsOrderedByStartDateDesc()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var term1 = new Term
        {
            Id = 1,
            Name = "Spring 2026",
            AcademicYear = "2025-2026",
            StartDate = new DateOnly(2026, 1, 1),
            EndDate = new DateOnly(2026, 4, 30),
            RegistrationPeriods = new List<RegistrationPeriod>
            {
                new RegistrationPeriod { Id = 1, TermId = 1, PeriodType = "Keeping", StartDate = new DateOnly(2025, 12, 1), EndDate = new DateOnly(2025, 12, 10), ActiveFlag = false }
            }
        };
        var term2 = new Term
        {
            Id = 2,
            Name = "Summer 2026",
            AcademicYear = "2025-2026",
            StartDate = new DateOnly(2026, 5, 1),
            EndDate = new DateOnly(2026, 8, 31)
        };

        await context.Terms.AddRangeAsync(term1, term2);
        await context.SaveChangesAsync();

        var repository = new TermRepository(context);

        // Act
        var result = await repository.GetAllTermsAsync();

        // Assert
        result.Should().HaveCount(2);
        result[0].Id.Should().Be(2); // Summer (StartDate: 2026-05-01) ordered before Spring (StartDate: 2026-01-01)
        result[1].Id.Should().Be(1);
        result[1].RegistrationPeriods.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetTermByIdAsync_ExistingId_ReturnsTermWithPriceListAndPeriods()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var priceList = new PriceList { Id = 10, TermId = 1, Status = "Published" };
        var term = new Term
        {
            Id = 1,
            Name = "Fall 2026",
            AcademicYear = "2026-2027",
            StartDate = new DateOnly(2026, 9, 1),
            EndDate = new DateOnly(2026, 12, 31),
            PriceList = priceList,
            RegistrationPeriods = new List<RegistrationPeriod>
            {
                new RegistrationPeriod { Id = 100, TermId = 1, PeriodType = "Keeping", StartDate = new DateOnly(2026, 8, 1), EndDate = new DateOnly(2026, 8, 10), ActiveFlag = true }
            }
        };

        await context.Terms.AddAsync(term);
        await context.SaveChangesAsync();

        var repository = new TermRepository(context);

        // Act
        var result = await repository.GetTermByIdAsync(1);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(1);
        result.Name.Should().Be("Fall 2026");
        result.PriceList.Should().NotBeNull();
        result.PriceList!.Status.Should().Be("Published");
        result.RegistrationPeriods.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetTermByIdAsync_NonExistingId_ReturnsNull()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var repository = new TermRepository(context);

        // Act
        var result = await repository.GetTermByIdAsync(999);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task ExistsTermAsync_SameNameAndYear_CaseInsensitiveAndTrimmed_ReturnsTrue()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var term = new Term
        {
            Id = 1,
            Name = "Spring 2026",
            AcademicYear = "2025-2026",
            StartDate = new DateOnly(2026, 1, 1),
            EndDate = new DateOnly(2026, 4, 30)
        };
        await context.Terms.AddAsync(term);
        await context.SaveChangesAsync();

        var repository = new TermRepository(context);

        // Act
        var exists = await repository.ExistsTermAsync("  spring 2026  ", " 2025-2026 ");

        // Assert
        exists.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsTermAsync_DifferentNameOrYear_ReturnsFalse()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var term = new Term
        {
            Id = 1,
            Name = "Spring 2026",
            AcademicYear = "2025-2026",
            StartDate = new DateOnly(2026, 1, 1),
            EndDate = new DateOnly(2026, 4, 30)
        };
        await context.Terms.AddAsync(term);
        await context.SaveChangesAsync();

        var repository = new TermRepository(context);

        // Act
        var existsDiffName = await repository.ExistsTermAsync("Summer 2026", "2025-2026");
        var existsDiffYear = await repository.ExistsTermAsync("Spring 2026", "2026-2027");

        // Assert
        existsDiffName.Should().BeFalse();
        existsDiffYear.Should().BeFalse();
    }

    [Fact]
    public async Task ExistsTermAsync_WithExcludeId_IgnoresSelf()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var term = new Term
        {
            Id = 5,
            Name = "Spring 2026",
            AcademicYear = "2025-2026",
            StartDate = new DateOnly(2026, 1, 1),
            EndDate = new DateOnly(2026, 4, 30)
        };
        await context.Terms.AddAsync(term);
        await context.SaveChangesAsync();

        var repository = new TermRepository(context);

        // Act
        var existsSelf = await repository.ExistsTermAsync("Spring 2026", "2025-2026", excludeId: 5);

        // Assert
        existsSelf.Should().BeFalse();
    }

    [Fact]
    public async Task CreateTermAsync_ValidTerm_AddsToDbAndReturnsTerm()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var repository = new TermRepository(context);
        var newTerm = new Term
        {
            Name = "Fall 2026",
            AcademicYear = "2026-2027",
            StartDate = new DateOnly(2026, 9, 1),
            EndDate = new DateOnly(2026, 12, 31)
        };

        // Act
        var result = await repository.CreateTermAsync(newTerm);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().BeGreaterThan(0);

        var dbTerm = await context.Terms.FindAsync(result.Id);
        dbTerm.Should().NotBeNull();
        dbTerm!.Name.Should().Be("Fall 2026");
    }

    [Fact]
    public async Task UpdateTermAsync_ValidChanges_UpdatesDb()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var term = new Term
        {
            Id = 1,
            Name = "Old Term Name",
            AcademicYear = "2025-2026",
            StartDate = new DateOnly(2026, 1, 1),
            EndDate = new DateOnly(2026, 4, 30)
        };
        await context.Terms.AddAsync(term);
        await context.SaveChangesAsync();

        var repository = new TermRepository(context);

        // Act
        term.Name = "Updated Term Name";
        term.EndDate = new DateOnly(2026, 5, 15);
        await repository.UpdateTermAsync(term);

        // Assert
        var updated = await context.Terms.FindAsync(1);
        updated.Should().NotBeNull();
        updated!.Name.Should().Be("Updated Term Name");
        updated.EndDate.Should().Be(new DateOnly(2026, 5, 15));
    }

    [Fact]
    public async Task HasDependentDataAsync_HasRequests_ReturnsTrue()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var term = new Term { Id = 1, Name = "T1", AcademicYear = "2026", StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 4, 30) };
        var request = new Request { Id = 1, TermId = 1, StudentId = 1, RequestType = "Booking", Status = "Pending", CreatedTime = DateTime.UtcNow };

        await context.Terms.AddAsync(term);
        await context.Requests.AddAsync(request);
        await context.SaveChangesAsync();

        var repository = new TermRepository(context);

        // Act
        var hasDeps = await repository.HasDependentDataAsync(1);

        // Assert
        hasDeps.Should().BeTrue();
    }

    [Fact]
    public async Task HasDependentDataAsync_HasResidencies_ReturnsTrue()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var term = new Term { Id = 1, Name = "T1", AcademicYear = "2026", StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 4, 30) };
        var residency = new Residency { Id = 1, TermId = 1, StudentId = 1, BedId = 1, Status = "Active" };

        await context.Terms.AddAsync(term);
        await context.Residencies.AddAsync(residency);
        await context.SaveChangesAsync();

        var repository = new TermRepository(context);

        // Act
        var hasDeps = await repository.HasDependentDataAsync(1);

        // Assert
        hasDeps.Should().BeTrue();
    }

    [Fact]
    public async Task HasDependentDataAsync_HasInvoices_ReturnsTrue()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var term = new Term { Id = 1, Name = "T1", AcademicYear = "2026", StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 4, 30) };
        var invoice = new Invoice { Id = 1, TermId = 1, StudentId = 1, InvoiceNumber = "INV001", InvoiceType = "Room", Status = "Unpaid", Amount = 1000000 };

        await context.Terms.AddAsync(term);
        await context.Invoices.AddAsync(invoice);
        await context.SaveChangesAsync();

        var repository = new TermRepository(context);

        // Act
        var hasDeps = await repository.HasDependentDataAsync(1);

        // Assert
        hasDeps.Should().BeTrue();
    }

    [Fact]
    public async Task HasDependentDataAsync_HasBillingPeriods_ReturnsTrue()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var term = new Term { Id = 1, Name = "T1", AcademicYear = "2026", StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 4, 30) };
        var billingPeriod = new BillingPeriod { Id = 1, TermId = 1, TargetMonth = 1, TargetYear = 2026, Status = "Pending" };

        await context.Terms.AddAsync(term);
        await context.BillingPeriods.AddAsync(billingPeriod);
        await context.SaveChangesAsync();

        var repository = new TermRepository(context);

        // Act
        var hasDeps = await repository.HasDependentDataAsync(1);

        // Assert
        hasDeps.Should().BeTrue();
    }

    [Fact]
    public async Task HasDependentDataAsync_NoDependencies_ReturnsFalse()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var term = new Term { Id = 1, Name = "T1", AcademicYear = "2026", StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 4, 30) };
        await context.Terms.AddAsync(term);
        await context.SaveChangesAsync();

        var repository = new TermRepository(context);

        // Act
        var hasDeps = await repository.HasDependentDataAsync(1);

        // Assert
        hasDeps.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteTermAsync_NonExistingTerm_DoesNotThrow()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var repository = new TermRepository(context);

        // Act
        var act = () => repository.DeleteTermAsync(999);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task DeleteTermAsync_ExistingTerm_CascadesDeletesConfigDataAndTerm()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var priceList = new PriceList
        {
            Id = 1,
            TermId = 1,
            Status = "Draft",
            PriceItems = new List<PriceItem>
            {
                new PriceItem { Id = 1, PriceListId = 1, DormId = 1, RoomTypeId = 1, UnitPrice = 500000 }
            }
        };

        var term = new Term
        {
            Id = 1,
            Name = "Term to Delete",
            AcademicYear = "2026",
            StartDate = new DateOnly(2026, 1, 1),
            EndDate = new DateOnly(2026, 4, 30),
            PriceList = priceList,
            RegistrationPeriods = new List<RegistrationPeriod>
            {
                new RegistrationPeriod { Id = 1, TermId = 1, PeriodType = "Keeping", StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 1, 10), ActiveFlag = false }
            },
            UtilityPrices = new List<UtilityPrice>
            {
                new UtilityPrice { Id = 1, TermId = 1, MeterType = "Electricity", UnitPrice = 3000 }
            },
            BedPrices = new List<BedPrice>
            {
                new BedPrice { Id = 1, BedId = 1, TermId = 1, UnitPrice = 1200000, EffectiveFrom = new DateOnly(2026, 1, 1), EffectiveTo = new DateOnly(2026, 4, 30) }
            }
        };

        await context.Terms.AddAsync(term);
        await context.SaveChangesAsync();

        var repository = new TermRepository(context);

        // Act
        await repository.DeleteTermAsync(1);

        // Assert
        (await context.Terms.FindAsync(1)).Should().BeNull();
        (await context.PriceLists.FindAsync(1)).Should().BeNull();
        (await context.PriceItems.FindAsync(1)).Should().BeNull();
        (await context.RegistrationPeriods.FindAsync(1)).Should().BeNull();
        (await context.UtilityPrices.FindAsync(1)).Should().BeNull();
        (await context.BedPrices.FindAsync(1)).Should().BeNull();
    }

    [Fact]
    public async Task GetPeriodsByTermIdAsync_ReturnsPeriodsOrderedByStartDate()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var p1 = new RegistrationPeriod { Id = 1, TermId = 1, PeriodType = "Booking", StartDate = new DateOnly(2026, 2, 1), EndDate = new DateOnly(2026, 2, 10), ActiveFlag = false };
        var p2 = new RegistrationPeriod { Id = 2, TermId = 1, PeriodType = "Keeping", StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 1, 10), ActiveFlag = false };
        var pOtherTerm = new RegistrationPeriod { Id = 3, TermId = 2, PeriodType = "Keeping", StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 1, 10), ActiveFlag = false };

        await context.RegistrationPeriods.AddRangeAsync(p1, p2, pOtherTerm);
        await context.SaveChangesAsync();

        var repository = new TermRepository(context);

        // Act
        var result = await repository.GetPeriodsByTermIdAsync(1);

        // Assert
        result.Should().HaveCount(2);
        result[0].Id.Should().Be(2); // Keeping (Jan 1) ordered before Booking (Feb 1)
        result[1].Id.Should().Be(1);
    }

    [Fact]
    public async Task SaveRegistrationPeriodsAsync_AddNewAndUpdatesExisting_PersistsCorrectly()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var existing = new RegistrationPeriod
        {
            Id = 10,
            TermId = 1,
            PeriodType = "Keeping",
            StartDate = new DateOnly(2026, 1, 1),
            EndDate = new DateOnly(2026, 1, 5),
            ActiveFlag = false
        };
        await context.RegistrationPeriods.AddAsync(existing);
        await context.SaveChangesAsync();

        var repository = new TermRepository(context);

        var incomingList = new List<RegistrationPeriod>
        {
            // Cập nhật existing (Id: 10)
            new RegistrationPeriod
            {
                Id = 10,
                TermId = 1,
                PeriodType = "Keeping",
                StartDate = new DateOnly(2026, 1, 1),
                EndDate = new DateOnly(2026, 1, 7),
                ActiveFlag = true
            },
            // Thêm mới Booking (Id: 0)
            new RegistrationPeriod
            {
                Id = 0,
                TermId = 1,
                PeriodType = "Booking",
                StartDate = new DateOnly(2026, 1, 8),
                EndDate = new DateOnly(2026, 1, 15),
                ActiveFlag = false
            }
        };

        // Act
        await repository.SaveRegistrationPeriodsAsync(1, incomingList);

        // Assert
        var periods = await context.RegistrationPeriods.Where(p => p.TermId == 1).ToListAsync();
        periods.Should().HaveCount(2);

        var updatedKeeping = periods.First(p => p.PeriodType == "Keeping");
        updatedKeeping.EndDate.Should().Be(new DateOnly(2026, 1, 7));
        updatedKeeping.ActiveFlag.Should().BeTrue();

        var addedBooking = periods.First(p => p.PeriodType == "Booking");
        addedBooking.StartDate.Should().Be(new DateOnly(2026, 1, 8));
        addedBooking.EndDate.Should().Be(new DateOnly(2026, 1, 15));
    }

    [Fact]
    public async Task IsPriceListPublishedAsync_WhenPublished_ReturnsTrue()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var pl = new PriceList { Id = 1, TermId = 1, Status = "Published" };
        await context.PriceLists.AddAsync(pl);
        await context.SaveChangesAsync();

        var repository = new TermRepository(context);

        // Act
        var isPublished = await repository.IsPriceListPublishedAsync(1);

        // Assert
        isPublished.Should().BeTrue();
    }

    [Fact]
    public async Task IsPriceListPublishedAsync_WhenDraftOrNotCreated_ReturnsFalse()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var pl = new PriceList { Id = 1, TermId = 1, Status = "Draft" };
        await context.PriceLists.AddAsync(pl);
        await context.SaveChangesAsync();

        var repository = new TermRepository(context);

        // Act
        var isPublishedDraft = await repository.IsPriceListPublishedAsync(1);
        var isPublishedNotCreated = await repository.IsPriceListPublishedAsync(999);

        // Assert
        isPublishedDraft.Should().BeFalse();
        isPublishedNotCreated.Should().BeFalse();
    }

    [Fact]
    public async Task TogglePeriodActiveAsync_SetActive_DeactivatesOtherActivePeriodsOfSameType()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var period1 = new RegistrationPeriod { Id = 1, TermId = 1, PeriodType = "Keeping", StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 1, 5), ActiveFlag = true };
        var period2 = new RegistrationPeriod { Id = 2, TermId = 1, PeriodType = "Keeping", StartDate = new DateOnly(2026, 1, 6), EndDate = new DateOnly(2026, 1, 10), ActiveFlag = false };
        await context.RegistrationPeriods.AddRangeAsync(period1, period2);
        await context.SaveChangesAsync();

        var repository = new TermRepository(context);

        // Act
        await repository.TogglePeriodActiveAsync(2, active: true);

        // Assert
        var p1InDb = await context.RegistrationPeriods.FindAsync(1);
        var p2InDb = await context.RegistrationPeriods.FindAsync(2);

        p2InDb!.ActiveFlag.Should().BeTrue();
        p1InDb!.ActiveFlag.Should().BeFalse(); // Tự động deactive để tránh đụng độ unique index
    }

    [Fact]
    public async Task TogglePeriodActiveAsync_SetInactive_UpdatesActiveFlagToFalse()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var period1 = new RegistrationPeriod { Id = 1, TermId = 1, PeriodType = "Booking", StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 1, 5), ActiveFlag = true };
        await context.RegistrationPeriods.AddAsync(period1);
        await context.SaveChangesAsync();

        var repository = new TermRepository(context);

        // Act
        await repository.TogglePeriodActiveAsync(1, active: false);

        // Assert
        var p1InDb = await context.RegistrationPeriods.FindAsync(1);
        p1InDb!.ActiveFlag.Should().BeFalse();
    }

    [Fact]
    public async Task GetOverlappingPeriodsAsync_OverlapFound_ReturnsOverlappingPeriods()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        // Term 1 có đợt Keeping: 2026-05-01 -> 2026-05-10
        var periodTerm1 = new RegistrationPeriod
        {
            Id = 1,
            TermId = 1,
            PeriodType = "Keeping",
            StartDate = new DateOnly(2026, 5, 1),
            EndDate = new DateOnly(2026, 5, 10),
            ActiveFlag = true
        };
        await context.RegistrationPeriods.AddAsync(periodTerm1);
        await context.SaveChangesAsync();

        var repository = new TermRepository(context);

        // Act: Đợt Keeping của Term 2 từ 2026-05-08 -> 2026-05-15 (chồng lấn từ 08 đến 10)
        var overlaps = await repository.GetOverlappingPeriodsAsync(
            "Keeping",
            new DateOnly(2026, 5, 8),
            new DateOnly(2026, 5, 15),
            excludeTermId: 2);

        // Assert
        overlaps.Should().HaveCount(1);
        overlaps.First().Id.Should().Be(1);
    }

    [Fact]
    public async Task GetOverlappingPeriodsAsync_NoOverlap_ReturnsEmptyList()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var periodTerm1 = new RegistrationPeriod
        {
            Id = 1,
            TermId = 1,
            PeriodType = "Keeping",
            StartDate = new DateOnly(2026, 5, 1),
            EndDate = new DateOnly(2026, 5, 10),
            ActiveFlag = true
        };
        await context.RegistrationPeriods.AddAsync(periodTerm1);
        await context.SaveChangesAsync();

        var repository = new TermRepository(context);

        // Act: Đợt Keeping của Term 2 từ 2026-05-11 -> 2026-05-20 (không chồng lấn)
        var overlaps = await repository.GetOverlappingPeriodsAsync(
            "Keeping",
            new DateOnly(2026, 5, 11),
            new DateOnly(2026, 5, 20),
            excludeTermId: 2);

        // Assert
        overlaps.Should().BeEmpty();
    }
}
