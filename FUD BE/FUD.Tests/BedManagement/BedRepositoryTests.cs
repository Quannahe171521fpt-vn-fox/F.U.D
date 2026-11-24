using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using FUD.DTOs.Facilities.Bed;
using FUD.Models;
using FUD.Repositories.Facilities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FUD.Tests.BedManagement;

/// <summary>
/// Unit tests cho BedRepository (Quản lý giường) - Tầng Repository.
/// Sử dụng EF Core InMemory Database, tuân thủ nghiêm ngặt quy tắc AAA (Arrange - Act - Assert).
/// </summary>
public class BedRepositoryTests
{
    private static FudContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<FudContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new FudContext(options);
    }

    private static async Task<(Dorm Dorm, Block Block, Room Room1, Room Room2, List<Bed> Beds)>
        SeedBasicFacilitiesAsync(FudContext context)
    {
        var dorm = new Dorm { Id = 1, Name = "Dorm A", Code = "DA", CampusId = 1, Status = "Active" };
        var block = new Block { Id = 1, DormId = 1, Name = "Block 101", Code = "B101", GenderType = "Male", Status = "Active", Dorm = dorm };
        var roomType = new RoomType { Id = 1, Name = "Phòng 4 giường", BedCapacity = 4 };

        var room1 = new Room { Id = 10, BlockId = 1, RoomTypeId = 1, RoomNumber = "101L", FloorLevel = 1, Status = "Active", Block = block, RoomType = roomType };
        var room2 = new Room { Id = 20, BlockId = 1, RoomTypeId = 1, RoomNumber = "102L", FloorLevel = 1, Status = "Active", Block = block, RoomType = roomType };

        var beds = new List<Bed>
        {
            new() { Id = 1, RoomId = 10, BedCode = "101L-B1", PositionInRoom = "Dưới trái", Status = "Free", Room = room1 },
            new() { Id = 2, RoomId = 10, BedCode = "101L-B2", PositionInRoom = "Trên trái", Status = "Occupied", Room = room1 },
            new() { Id = 3, RoomId = 10, BedCode = "101L-B3", PositionInRoom = "Dưới phải", Status = "Maintenance", Room = room1 },
            new() { Id = 4, RoomId = 20, BedCode = "102L-B1", PositionInRoom = "Dưới trái", Status = "Holding", Room = room2 },
            new() { Id = 5, RoomId = 20, BedCode = "102L-B2", PositionInRoom = "Trên trái", Status = "Pending Transfer", Room = room2 }
        };

        await context.Dorms.AddAsync(dorm);
        await context.Blocks.AddAsync(block);
        await context.RoomTypes.AddAsync(roomType);
        await context.Rooms.AddRangeAsync(room1, room2);
        await context.Beds.AddRangeAsync(beds);
        await context.SaveChangesAsync();

        return (dorm, block, room1, room2, beds);
    }

    #region GetAllAsync Tests

    [Fact]
    public async Task GetAllAsync_WhenNoFilter_ReturnsAllBedsPaged()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        await SeedBasicFacilitiesAsync(context);
        var sut = new BedRepository(context);
        var query = new BedQueryDto { Page = 1, PageSize = 10 };

        // Act
        var (items, totalCount) = await sut.GetAllAsync(query);

        // Assert
        totalCount.Should().Be(5);
        items.Should().HaveCount(5);
    }

    [Fact]
    public async Task GetAllAsync_WhenFilteredByRoomId_ReturnsOnlyBedsInThatRoom()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        await SeedBasicFacilitiesAsync(context);
        var sut = new BedRepository(context);
        var query = new BedQueryDto { RoomId = 10, Page = 1, PageSize = 10 };

        // Act
        var (items, totalCount) = await sut.GetAllAsync(query);

        // Assert
        totalCount.Should().Be(3);
        items.Should().HaveCount(3);
        items.Should().OnlyContain(b => b.RoomId == 10);
    }

    [Fact]
    public async Task GetAllAsync_WhenFilteredByBedCode_ReturnsMatchingBeds()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        await SeedBasicFacilitiesAsync(context);
        var sut = new BedRepository(context);
        var query = new BedQueryDto { BedCode = "102L", Page = 1, PageSize = 10 };

        // Act
        var (items, totalCount) = await sut.GetAllAsync(query);

        // Assert
        totalCount.Should().Be(2);
        items.Should().HaveCount(2);
        items.Should().OnlyContain(b => b.BedCode.Contains("102L"));
    }

    [Fact]
    public async Task GetAllAsync_WhenFilteredByStatus_ReturnsOnlyBedsWithThatStatus()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        await SeedBasicFacilitiesAsync(context);
        var sut = new BedRepository(context);
        var query = new BedQueryDto { Status = "Maintenance", Page = 1, PageSize = 10 };

        // Act
        var (items, totalCount) = await sut.GetAllAsync(query);

        // Assert
        totalCount.Should().Be(1);
        items.Single().BedCode.Should().Be("101L-B3");
        items.Single().Status.Should().Be("Maintenance");
    }

    [Fact]
    public async Task GetAllAsync_WhenPaginationApplied_ReturnsCorrectSlice()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        await SeedBasicFacilitiesAsync(context);
        var sut = new BedRepository(context);
        var query = new BedQueryDto { Page = 2, PageSize = 2 };

        // Act
        var (items, totalCount) = await sut.GetAllAsync(query);

        // Assert
        totalCount.Should().Be(5);
        items.Should().HaveCount(2);
    }

    #endregion

    #region GetByIdAsync Tests

    [Fact]
    public async Task GetByIdAsync_WhenBedExists_ReturnsBedWithRoomAndBlock()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        await SeedBasicFacilitiesAsync(context);
        var sut = new BedRepository(context);

        // Act
        var bed = await sut.GetByIdAsync(1);

        // Assert
        bed.Should().NotBeNull();
        bed!.Id.Should().Be(1);
        bed.BedCode.Should().Be("101L-B1");
        bed.Room.Should().NotBeNull();
        bed.Room.RoomNumber.Should().Be("101L");
        bed.Room.Block.Should().NotBeNull();
        bed.Room.Block.Name.Should().Be("Block 101");
    }

    [Fact]
    public async Task GetByIdAsync_WhenBedNotFound_ReturnsNull()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        await SeedBasicFacilitiesAsync(context);
        var sut = new BedRepository(context);

        // Act
        var bed = await sut.GetByIdAsync(9999);

        // Assert
        bed.Should().BeNull();
    }

    #endregion

    #region RoomExistsAsync Tests

    [Fact]
    public async Task RoomExistsAsync_WhenRoomExists_ReturnsTrue()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        await SeedBasicFacilitiesAsync(context);
        var sut = new BedRepository(context);

        // Act
        var exists = await sut.RoomExistsAsync(10);

        // Assert
        exists.Should().BeTrue();
    }

    [Fact]
    public async Task RoomExistsAsync_WhenRoomDoesNotExist_ReturnsFalse()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        await SeedBasicFacilitiesAsync(context);
        var sut = new BedRepository(context);

        // Act
        var exists = await sut.RoomExistsAsync(999);

        // Assert
        exists.Should().BeFalse();
    }

    #endregion

    #region GetRoomWithTypeAndBedsAsync Tests

    [Fact]
    public async Task GetRoomWithTypeAndBedsAsync_WhenRoomExists_ReturnsRoomWithRoomTypeAndBeds()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        await SeedBasicFacilitiesAsync(context);
        var sut = new BedRepository(context);

        // Act
        var room = await sut.GetRoomWithTypeAndBedsAsync(10);

        // Assert
        room.Should().NotBeNull();
        room!.Id.Should().Be(10);
        room.RoomNumber.Should().Be("101L");
        room.RoomType.Should().NotBeNull();
        room.RoomType.BedCapacity.Should().Be(4);
        room.Beds.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetRoomWithTypeAndBedsAsync_WhenRoomDoesNotExist_ReturnsNull()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        await SeedBasicFacilitiesAsync(context);
        var sut = new BedRepository(context);

        // Act
        var room = await sut.GetRoomWithTypeAndBedsAsync(999);

        // Assert
        room.Should().BeNull();
    }

    #endregion

    #region IsBedCodeUniqueAsync Tests

    [Fact]
    public async Task IsBedCodeUniqueAsync_WhenCodeDoesNotExistInRoom_ReturnsTrue()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        await SeedBasicFacilitiesAsync(context);
        var sut = new BedRepository(context);

        // Act
        var isUnique = await sut.IsBedCodeUniqueAsync(10, "101L-B4-BRAND-NEW");

        // Assert
        isUnique.Should().BeTrue();
    }

    [Fact]
    public async Task IsBedCodeUniqueAsync_WhenCodeAlreadyExistsInSameRoom_ReturnsFalse()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        await SeedBasicFacilitiesAsync(context);
        var sut = new BedRepository(context);

        // Act
        var isUnique = await sut.IsBedCodeUniqueAsync(10, "101L-B1");

        // Assert
        isUnique.Should().BeFalse();
    }

    [Fact]
    public async Task IsBedCodeUniqueAsync_WhenSameCodeExistsInAnotherRoom_ReturnsTrue()
    {
        // Arrange: Mã giường 101L-B1 tồn tại ở phòng 10, nhưng kiểm tra tại phòng 20 thì phải là duy nhất
        using var context = CreateInMemoryContext();
        await SeedBasicFacilitiesAsync(context);
        var sut = new BedRepository(context);

        // Act
        var isUnique = await sut.IsBedCodeUniqueAsync(20, "101L-B1");

        // Assert
        isUnique.Should().BeTrue();
    }

    [Fact]
    public async Task IsBedCodeUniqueAsync_WhenUpdatingSameBedWithSameCode_ReturnsTrue()
    {
        // Arrange: Khi cập nhật giường Id=1 và giữ nguyên mã "101L-B1"
        using var context = CreateInMemoryContext();
        await SeedBasicFacilitiesAsync(context);
        var sut = new BedRepository(context);

        // Act
        var isUnique = await sut.IsBedCodeUniqueAsync(10, "101L-B1", excludeId: 1);

        // Assert
        isUnique.Should().BeTrue();
    }

    #endregion

    #region HasActiveResidencyAsync Tests

    [Theory]
    [InlineData(2, true)]  // Occupied
    [InlineData(4, true)]  // Holding
    [InlineData(5, true)]  // Pending Transfer
    [InlineData(1, false)] // Free
    [InlineData(3, false)] // Maintenance
    public async Task HasActiveResidencyAsync_ReturnsExpectedResultBasedOnStatus(int bedId, bool expectedResult)
    {
        // Arrange
        using var context = CreateInMemoryContext();
        await SeedBasicFacilitiesAsync(context);
        var sut = new BedRepository(context);

        // Act
        var result = await sut.HasActiveResidencyAsync(bedId);

        // Assert
        result.Should().Be(expectedResult);
    }

    #endregion

    #region CreateAsync & UpdateAsync Tests

    [Fact]
    public async Task CreateAsync_WhenValidBed_PersistsInDatabase()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        await SeedBasicFacilitiesAsync(context);
        var sut = new BedRepository(context);

        var newBed = new Bed
        {
            RoomId = 10,
            BedCode = "101L-B4",
            PositionInRoom = "Trên phải",
            Status = "Free"
        };

        // Act
        var created = await sut.CreateAsync(newBed);

        // Assert
        created.Id.Should().BeGreaterThan(0);
        var persisted = await context.Beds.FindAsync(created.Id);
        persisted.Should().NotBeNull();
        persisted!.BedCode.Should().Be("101L-B4");
        persisted.Status.Should().Be("Free");
    }

    [Fact]
    public async Task UpdateAsync_WhenBedModified_SavesChangesToDatabase()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        await SeedBasicFacilitiesAsync(context);
        var sut = new BedRepository(context);

        var bed = await context.Beds.FindAsync(1);
        bed!.BedCode = "101L-B1-EDITED";
        bed.PositionInRoom = "Vị trí đã đổi";

        // Act
        await sut.UpdateAsync(bed);

        // Assert
        var updated = await context.Beds.FindAsync(1);
        updated.Should().NotBeNull();
        updated!.BedCode.Should().Be("101L-B1-EDITED");
        updated.PositionInRoom.Should().Be("Vị trí đã đổi");
    }

    #endregion
}

