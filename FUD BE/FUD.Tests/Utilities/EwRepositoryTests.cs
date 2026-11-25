using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using FUD.Models;
using FUD.Repositories.Utilities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FUD.Tests.Utilities;

/// <summary>
/// Unit tests cho EwRepository (Truy vấn & tính phụ trội Điện – Nước) - Tầng Repository.
/// Sử dụng EF Core InMemory cô lập mỗi test case, kiểm tra công thức định mức, làm tròn 1.000 VNĐ, UtilityBill & Invoice.
/// </summary>
public class EwRepositoryTests
{
    private static FudContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<FudContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new FudContext(options);
    }

    private static async Task<(Term Term, BillingPeriod PeriodOct, BillingPeriod PeriodNov, Room RoomL, Room RoomR)>
        SeedStandardDormAndTermAsync(FudContext ctx)
    {
        var dorm = new Dorm { Id = 1, CampusId = 1, Code = "A", Name = "Dom A", Status = "Active" };
        var block = new Block { Id = 10, DormId = 1, Code = "A1", Name = "Block A1", GenderType = "Male", Status = "Active", Dorm = dorm };
        var roomL = new Room { Id = 101, BlockId = 10, RoomTypeId = 1, RoomNumber = "01L", FloorLevel = 1, Status = "Active", Block = block };
        var roomR = new Room { Id = 102, BlockId = 10, RoomTypeId = 1, RoomNumber = "01R", FloorLevel = 1, Status = "Active", Block = block };
        var inactiveRoom = new Room { Id = 103, BlockId = 10, RoomTypeId = 1, RoomNumber = "02L", FloorLevel = 1, Status = "Inactive", Block = block };

        var term = new Term
        {
            Id = 1,
            Name = "Fall",
            AcademicYear = "2026",
            StartDate = new DateOnly(2026, 9, 1),
            EndDate = new DateOnly(2026, 12, 31)
        };

        var periodOct = new BillingPeriod { Id = 1, TermId = 1, TargetMonth = 10, TargetYear = 2026, Status = "Calculated", Term = term };
        var periodNov = new BillingPeriod { Id = 2, TermId = 1, TargetMonth = 11, TargetYear = 2026, Status = "Open", Term = term };

        var prices = new List<UtilityPrice>
        {
            new() { Id = 1, TermId = 1, MeterType = "Electricity", UnitPrice = 3000m },
            new() { Id = 2, TermId = 1, MeterType = "Water", UnitPrice = 10000m }
        };

        await ctx.Dorms.AddAsync(dorm);
        await ctx.Blocks.AddAsync(block);
        await ctx.Rooms.AddRangeAsync(roomL, roomR, inactiveRoom);
        await ctx.Terms.AddAsync(term);
        await ctx.BillingPeriods.AddRangeAsync(periodOct, periodNov);
        await ctx.UtilityPrices.AddRangeAsync(prices);
        await ctx.SaveChangesAsync();

        return (term, periodOct, periodNov, roomL, roomR);
    }

    private static async Task SeedCheckedInStudentsAsync(FudContext ctx, int roomId, int termId, params int[] studentIds)
    {
        int bedIdx = 1;
        foreach (var studentId in studentIds)
        {
            var user = new User
            {
                Id = studentId,
                Code = $"HE180{studentId}",
                FullName = $"Student {studentId}",
                Email = $"sv{studentId}@fpt.edu.vn",
                Gender = "Male",
                AccountStatus = "Active",
                CreatedAt = DateTime.UtcNow
            };
            var student = new Student { UserId = studentId, User = user };
            var bed = new Bed { Id = roomId * 10 + bedIdx, RoomId = roomId, BedCode = $"B{bedIdx}", Status = "Occupied" };
            var residency = new Residency
            {
                Id = roomId * 100 + bedIdx,
                StudentId = studentId,
                BedId = bed.Id,
                TermId = termId,
                CheckInDate = new DateOnly(2026, 9, 5),
                Status = "Active",
                Bed = bed,
                Student = student
            };

            await ctx.Users.AddAsync(user);
            await ctx.Students.AddAsync(student);
            await ctx.Beds.AddAsync(bed);
            await ctx.Residencies.AddAsync(residency);
            bedIdx++;
        }

        await ctx.SaveChangesAsync();
    }

    #region GetRoomsByBlockCodeAsync & Reading History Lookup Tests

    [Theory]
    [InlineData("A1")]
    [InlineData("a101")]
    [InlineData("01l")]
    public async Task GetRoomsByBlockCodeAsync_WhenCodeMatchesBlockOrCombinedRoom_ReturnsActiveRoomsOnly(string searchCode)
    {
        // Arrange
        using var ctx = CreateInMemoryContext();
        await SeedStandardDormAndTermAsync(ctx);
        var sut = new EwRepository(ctx);

        // Act
        var rooms = await sut.GetRoomsByBlockCodeAsync(searchCode);

        // Assert
        rooms.Should().NotBeEmpty();
        rooms.Should().NotContain(r => r.Status == "Inactive");
        rooms.Select(r => r.RoomNumber).Should().Contain("01L");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Z999")]
    public async Task GetRoomsByBlockCodeAsync_WhenCodeIsBlankOrNotFound_ReturnsEmptyList(string invalidCode)
    {
        // Arrange
        using var ctx = CreateInMemoryContext();
        await SeedStandardDormAndTermAsync(ctx);
        var sut = new EwRepository(ctx);

        // Act
        var rooms = await sut.GetRoomsByBlockCodeAsync(invalidCode);

        // Assert
        rooms.Should().BeEmpty();
    }

    [Fact]
    public async Task GetLatestReadingBeforePeriodAsync_WhenMultiplePeriodsExist_ReturnsMostRecentPriorPeriodReading()
    {
        // Arrange
        using var ctx = CreateInMemoryContext();
        var (_, periodOct, periodNov, roomL, _) = await SeedStandardDormAndTermAsync(ctx);
        var periodSep = new BillingPeriod { Id = 3, TermId = 1, TargetMonth = 9, TargetYear = 2026, Status = "Calculated" };
        await ctx.BillingPeriods.AddAsync(periodSep);

        await ctx.MeterReadings.AddRangeAsync(
            new MeterReading { Id = 1, BillingPeriodId = 3, RoomId = roomL.Id, MeterType = "Electricity", PreviousValue = 0, CurrentValue = 100m, Source = "Manual", Status = "Recorded", RecordedById = 10, RecordedTime = new DateTime(2026, 9, 30) },
            new MeterReading { Id = 2, BillingPeriodId = periodOct.Id, RoomId = roomL.Id, MeterType = "Electricity", PreviousValue = 100m, CurrentValue = 230m, Source = "Manual", Status = "Recorded", RecordedById = 10, RecordedTime = new DateTime(2026, 10, 31) },
            new MeterReading { Id = 3, BillingPeriodId = periodNov.Id, RoomId = roomL.Id, MeterType = "Electricity", PreviousValue = 230m, CurrentValue = 350m, Source = "Manual", Status = "Recorded", RecordedById = 10, RecordedTime = new DateTime(2026, 11, 30) });
        await ctx.SaveChangesAsync();

        var sut = new EwRepository(ctx);

        // Act
        var priorToNov = await sut.GetLatestReadingBeforePeriodAsync(roomL.Id, "Electricity", targetMonth: 11, targetYear: 2026);

        // Assert
        priorToNov.Should().NotBeNull();
        priorToNov!.CurrentValue.Should().Be(230m);
    }

    [Fact]
    public async Task GetRoomTermConsumptionBeforePeriodAsync_WhenCheckoutReadingExists_ExcludesCheckoutAndCurrentPeriod()
    {
        // Arrange
        using var ctx = CreateInMemoryContext();
        var (_, periodOct, periodNov, roomL, _) = await SeedStandardDormAndTermAsync(ctx);

        await ctx.MeterReadings.AddRangeAsync(
            new MeterReading { Id = 1, BillingPeriodId = periodOct.Id, RoomId = roomL.Id, MeterType = "Electricity", PreviousValue = 100m, CurrentValue = 220m, Consumption = 120m, Source = "Manual", Status = "Recorded", RecordedById = 10, RecordedTime = DateTime.UtcNow },
            new MeterReading { Id = 2, BillingPeriodId = periodOct.Id, RoomId = roomL.Id, MeterType = "Electricity", PreviousValue = 220m, CurrentValue = 240m, Consumption = 20m, CheckoutRequestId = 77, Source = "Manual", Status = "Recorded", RecordedById = 10, RecordedTime = DateTime.UtcNow },
            new MeterReading { Id = 3, BillingPeriodId = periodNov.Id, RoomId = roomL.Id, MeterType = "Electricity", PreviousValue = 220m, CurrentValue = 400m, Consumption = 180m, Source = "Manual", Status = "Recorded", RecordedById = 10, RecordedTime = DateTime.UtcNow });
        await ctx.SaveChangesAsync();

        var sut = new EwRepository(ctx);

        // Act
        var prevTermConsumption = await sut.GetRoomTermConsumptionBeforePeriodAsync(roomL.Id, "Electricity", termId: 1, currentBillingPeriodId: periodNov.Id);

        // Assert
        prevTermConsumption.Should().Be(120m);
    }

    #endregion

    #region CalculateAndPublishRoomSurchargeAsync Tests

    [Fact]
    public async Task CalculateAndPublishRoomSurchargeAsync_WhenRoomHasFlaggedReading_SkipsBillingAndMarksHasFlagged()
    {
        // Arrange
        using var ctx = CreateInMemoryContext();
        var (_, _, periodNov, roomL, _) = await SeedStandardDormAndTermAsync(ctx);
        await SeedCheckedInStudentsAsync(ctx, roomL.Id, termId: 1, 201, 202);

        await ctx.MeterReadings.AddAsync(new MeterReading
        {
            Id = 1,
            BillingPeriodId = periodNov.Id,
            RoomId = roomL.Id,
            MeterType = "Electricity",
            PreviousValue = 100m,
            CurrentValue = 2500m,
            Source = "Manual",
            Status = "Flagged",
            RecordedById = 10,
            RecordedTime = DateTime.UtcNow
        });
        await ctx.SaveChangesAsync();

        var sut = new EwRepository(ctx);

        // Act
        var result = await sut.CalculateAndPublishRoomSurchargeAsync(periodNov.Id, roomL.Id);

        // Assert
        result.Calculated.Should().BeFalse();
        result.HasFlaggedReadings.Should().BeTrue();
        (await ctx.UtilityBills.CountAsync()).Should().Be(0);
        (await ctx.Invoices.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task CalculateAndPublishRoomSurchargeAsync_WhenUsageWithinQuota_PublishesZeroCostUtilityBillWithoutInvoices()
    {
        // Arrange: 2 students -> free quota = 100 kWh & 6 m3
        using var ctx = CreateInMemoryContext();
        var (_, _, periodNov, roomL, _) = await SeedStandardDormAndTermAsync(ctx);
        await SeedCheckedInStudentsAsync(ctx, roomL.Id, termId: 1, 201, 202);

        await ctx.MeterReadings.AddRangeAsync(
            new MeterReading { Id = 1, BillingPeriodId = periodNov.Id, RoomId = roomL.Id, MeterType = "Electricity", PreviousValue = 100m, CurrentValue = 200m, Consumption = 100m, Source = "Manual", Status = "Recorded", RecordedById = 10, RecordedTime = DateTime.UtcNow },
            new MeterReading { Id = 2, BillingPeriodId = periodNov.Id, RoomId = roomL.Id, MeterType = "Water", PreviousValue = 10m, CurrentValue = 16m, Consumption = 6m, Source = "Manual", Status = "Recorded", RecordedById = 10, RecordedTime = DateTime.UtcNow });
        await ctx.SaveChangesAsync();

        var sut = new EwRepository(ctx);

        // Act
        var result = await sut.CalculateAndPublishRoomSurchargeAsync(periodNov.Id, roomL.Id);

        // Assert
        result.Calculated.Should().BeTrue();
        result.TotalCost.Should().Be(0m);
        result.SharePerStudent.Should().Be(0m);
        result.InvoicesCreated.Should().Be(0);
        (await ctx.UtilityBills.CountAsync()).Should().Be(1);
        (await ctx.Invoices.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task CalculateAndPublishRoomSurchargeAsync_WhenOverQuota_SplitsEquallyRoundsToThousandAndCreatesUtilityInvoices()
    {
        // Arrange:
        // 3 students -> Elec quota = 150 kWh, Water quota = 9 m3
        // Elec consumption = 175 kWh -> over 25 kWh * 3000 = 75,000 VND
        // Water consumption = 11 m3 -> over 2 m3 * 10000 = 20,000 VND
        // TotalCost = 95,000 VND -> 95,000 / 3 = 31,666.67 -> rounded AwayFromZero to 1000 = 32,000 VND/student
        using var ctx = CreateInMemoryContext();
        var (_, _, periodNov, roomL, _) = await SeedStandardDormAndTermAsync(ctx);
        await SeedCheckedInStudentsAsync(ctx, roomL.Id, termId: 1, 301, 302, 303);

        await ctx.MeterReadings.AddRangeAsync(
            new MeterReading { Id = 1, BillingPeriodId = periodNov.Id, RoomId = roomL.Id, MeterType = "Electricity", PreviousValue = 100m, CurrentValue = 275m, Consumption = 175m, Source = "Manual", Status = "Recorded", RecordedById = 10, RecordedTime = DateTime.UtcNow },
            new MeterReading { Id = 2, BillingPeriodId = periodNov.Id, RoomId = roomL.Id, MeterType = "Water", PreviousValue = 20m, CurrentValue = 31m, Consumption = 11m, Source = "Manual", Status = "Recorded", RecordedById = 10, RecordedTime = DateTime.UtcNow });
        await ctx.SaveChangesAsync();

        var sut = new EwRepository(ctx);

        // Act
        var result = await sut.CalculateAndPublishRoomSurchargeAsync(periodNov.Id, roomL.Id);

        // Assert
        result.Calculated.Should().BeTrue();
        result.TotalCost.Should().Be(95000m);
        result.NumberOfResidents.Should().Be(3);
        result.SharePerStudent.Should().Be(32000m);
        result.InvoicesCreated.Should().Be(3);
        result.NotifiedStudentIds.Should().BeEquivalentTo(new[] { 301, 302, 303 });

        var invoices = await ctx.Invoices.Include(i => i.InvoiceItems).ToListAsync();
        invoices.Should().HaveCount(3);
        invoices.Should().OnlyContain(i => i.InvoiceType == "Utility" && i.Status == "Unpaid" && i.Amount == 32000m);
    }

    [Fact]
    public async Task CalculateAndPublishRoomSurchargeAsync_WhenRecalculatedForUnpaidInvoice_UpdatesExistingInvoiceAmountWithoutDuplicating()
    {
        // Arrange
        using var ctx = CreateInMemoryContext();
        var (_, _, periodNov, roomL, _) = await SeedStandardDormAndTermAsync(ctx);
        await SeedCheckedInStudentsAsync(ctx, roomL.Id, termId: 1, 401, 402);

        var elecReading = new MeterReading
        {
            Id = 1,
            BillingPeriodId = periodNov.Id,
            RoomId = roomL.Id,
            MeterType = "Electricity",
            PreviousValue = 0m,
            CurrentValue = 120m, // Over 20 kWh * 3000 = 60,000 -> 30,000 / student
            Consumption = 120m,
            Source = "Manual",
            Status = "Recorded",
            RecordedById = 10,
            RecordedTime = DateTime.UtcNow
        };
        await ctx.MeterReadings.AddAsync(elecReading);
        await ctx.SaveChangesAsync();

        var sut = new EwRepository(ctx);
        await sut.CalculateAndPublishRoomSurchargeAsync(periodNov.Id, roomL.Id);

        // Update consumption to 140 kWh -> Over 40 kWh * 3000 = 120,000 -> 60,000 / student
        elecReading.CurrentValue = 140m;
        elecReading.Consumption = 140m;
        await ctx.SaveChangesAsync();

        // Act
        var secondRun = await sut.CalculateAndPublishRoomSurchargeAsync(periodNov.Id, roomL.Id);

        // Assert
        secondRun.InvoicesCreated.Should().Be(0);
        secondRun.SharePerStudent.Should().Be(60000m);
        (await ctx.Invoices.CountAsync()).Should().Be(2);
        (await ctx.Invoices.ToListAsync()).Should().OnlyContain(i => i.Amount == 60000m);
    }

    #endregion

    #region Student EW Report Tests

    [Fact]
    public async Task GetStudentEwReportAsync_WhenStudentHasNoResidency_ReturnsNull()
    {
        // Arrange
        using var ctx = CreateInMemoryContext();
        var sut = new EwRepository(ctx);

        // Act
        var report = await sut.GetStudentEwReportAsync(studentUserId: 999, termId: 1);

        // Assert
        report.Should().BeNull();
    }

    [Fact]
    public async Task GetStudentEwReportAsync_WhenStudentHasValidAndFlaggedReadings_ExcludesFlaggedAndCalculatesPersonalShare()
    {
        // Arrange: 2 students in roomL
        // Oct Electricity (Recorded): 140 kWh -> per student = 70 kWh -> over quota (50) = +20 kWh -> 20 * 3000 = 60,000 VND
        // Nov Electricity (Flagged): 900 kWh -> MUST BE EXCLUDED until Manager resolves
        using var ctx = CreateInMemoryContext();
        var (_, periodOct, periodNov, roomL, _) = await SeedStandardDormAndTermAsync(ctx);
        await SeedCheckedInStudentsAsync(ctx, roomL.Id, termId: 1, 501, 502);

        await ctx.MeterReadings.AddRangeAsync(
            new MeterReading { Id = 1, BillingPeriodId = periodOct.Id, RoomId = roomL.Id, MeterType = "Electricity", PreviousValue = 0m, CurrentValue = 140m, Consumption = 140m, Source = "Manual", Status = "Recorded", RecordedById = 10, RecordedTime = new DateTime(2026, 10, 31) },
            new MeterReading { Id = 2, BillingPeriodId = periodNov.Id, RoomId = roomL.Id, MeterType = "Electricity", PreviousValue = 140m, CurrentValue = 1040m, Consumption = 900m, Source = "Manual", Status = "Flagged", RecordedById = 10, RecordedTime = new DateTime(2026, 11, 30) });
        await ctx.SaveChangesAsync();

        var sut = new EwRepository(ctx);

        // Act
        var report = await sut.GetStudentEwReportAsync(studentUserId: 501, termId: 1);

        // Assert
        report.Should().NotBeNull();
        report!.NumberOfCheckedInResidents.Should().Be(2);
        report.Rows.Should().ContainSingle();
        report.TotalElectricUsage.Should().Be(70m);
        report.TotalElectricOverUsage.Should().Be(20m);
        report.ElectricOverUsagePayment.Should().Be(60000m);
    }

    #endregion
}
