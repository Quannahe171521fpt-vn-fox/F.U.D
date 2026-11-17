using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using FUD.DTOs.Facilities.Block;
using FUD.Models;
using FUD.Repositories.Facilities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FUD.Tests.BlockManagement;

/// <summary>
/// Unit tests cho BlockRepository (Quản lý Block/Tòa nhà) - Tầng Repository.
/// Sử dụng EF Core InMemory Database, tuân thủ nghiêm ngặt quy tắc AAA (Arrange - Act - Assert).
/// </summary>
public class BlockRepositoryTests
{
    private static FudContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<FudContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new FudContext(options);
    }

    private static async Task<(Dorm DormA, Dorm DormB, List<Block> Blocks, List<Room> Rooms, List<Bed> Beds, List<Residency> Residencies)>
        SeedBasicFacilitiesAsync(FudContext context)
    {
        var dormA = new Dorm { Id = 1, Name = "Dorm A", Code = "DA", CampusId = 1, Status = "Active" };
        var dormB = new Dorm { Id = 2, Name = "Dorm B", Code = "DB", CampusId = 1, Status = "Active" };

        var block1 = new Block
        {
            Id = 1,
            DormId = 1,
            Code = "B101",
            Name = "Block 101",
            FloorLevel = 5,
            GenderType = "Male",
            Status = "Active",
            Dorm = dormA
        };
        var block2 = new Block
        {
            Id = 2,
            DormId = 1,
            Code = "B102",
            Name = "Block 102",
            FloorLevel = 4,
            GenderType = "Female",
            Status = "Active",
            Dorm = dormA
        };
        var block3 = new Block
        {
            Id = 3,
            DormId = 2,
            Code = "B201",
            Name = "Block 201",
            FloorLevel = 3,
            GenderType = "Male",
            Status = "Maintenance",
            Dorm = dormB
        };
        var block4Empty = new Block
        {
            Id = 4,
            DormId = 2,
            Code = "B202",
            Name = "Block 202 Empty",
            FloorLevel = 2,
            GenderType = "Female",
            Status = "Active",
            Dorm = dormB
        };

        var roomType = new RoomType { Id = 1, Name = "Phòng 4 giường", BedCapacity = 4 };

        var room1 = new Room { Id = 10, BlockId = 1, RoomTypeId = 1, RoomNumber = "101L", FloorLevel = 1, Status = "Active", Block = block1, RoomType = roomType };
        var room2 = new Room { Id = 20, BlockId = 2, RoomTypeId = 1, RoomNumber = "201L", FloorLevel = 2, Status = "Active", Block = block2, RoomType = roomType };

        var bed1 = new Bed { Id = 1, RoomId = 10, BedCode = "101L-B1", Status = "Occupied", Room = room1 };
        var bed2 = new Bed { Id = 2, RoomId = 10, BedCode = "101L-B2", Status = "Free", Room = room1 };
        var bed3 = new Bed { Id = 3, RoomId = 20, BedCode = "201L-B1", Status = "Occupied", Room = room2 };

        var residency1 = new Residency { Id = 1, BedId = 1, Status = "Active", Bed = bed1 };
        var residency2 = new Residency { Id = 2, BedId = 3, Status = "Reserved", Bed = bed3 };

        await context.Dorms.AddRangeAsync(dormA, dormB);
        await context.Blocks.AddRangeAsync(block1, block2, block3, block4Empty);
        await context.RoomTypes.AddAsync(roomType);
        await context.Rooms.AddRangeAsync(room1, room2);
        await context.Beds.AddRangeAsync(bed1, bed2, bed3);
        await context.Residencies.AddRangeAsync(residency1, residency2);
        await context.SaveChangesAsync();

        return (dormA, dormB, new List<Block> { block1, block2, block3, block4Empty }, new List<Room> { room1, room2 }, new List<Bed> { bed1, bed2, bed3 }, new List<Residency> { residency1, residency2 });
    }

    #region GetAllAsync Tests

    [Fact]
    public async Task GetAllAsync_WhenNoFilter_ReturnsAllBlocksPaged()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        await SeedBasicFacilitiesAsync(context);
        var sut = new BlockRepository(context);
        var query = new BlockQueryDto { Page = 1, PageSize = 10 };

        // Act
        var (items, totalCount) = await sut.GetAllAsync(query);

        // Assert
        totalCount.Should().Be(4);
        items.Should().HaveCount(4);
        items.First().Dorm.Should().NotBeNull();
    }

    [Fact]
    public async Task GetAllAsync_WhenFilteredByDormName_ReturnsMatchingBlocks()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        await SeedBasicFacilitiesAsync(context);
        var sut = new BlockRepository(context);
        var query = new BlockQueryDto { DormName = "Dorm A", Page = 1, PageSize = 10 };

        // Act
        var (items, totalCount) = await sut.GetAllAsync(query);

        // Assert
        totalCount.Should().Be(2);
        items.Should().HaveCount(2);
        items.Should().OnlyContain(b => b.Dorm.Name.Contains("Dorm A"));
    }

    [Fact]
    public async Task GetAllAsync_WhenFilteredByBlockNameOrCode_ReturnsMatchingBlocks()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        await SeedBasicFacilitiesAsync(context);
        var sut = new BlockRepository(context);
        var query = new BlockQueryDto { BlockName = "B101", Page = 1, PageSize = 10 };

        // Act
        var (items, totalCount) = await sut.GetAllAsync(query);

        // Assert
        totalCount.Should().Be(1);
        items.First().Code.Should().Be("B101");
    }

    [Fact]
    public async Task GetAllAsync_WhenFilteredByGenderType_ReturnsOnlyMatchingGender()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        await SeedBasicFacilitiesAsync(context);
        var sut = new BlockRepository(context);
        var query = new BlockQueryDto { GenderType = "Female", Page = 1, PageSize = 10 };

        // Act
        var (items, totalCount) = await sut.GetAllAsync(query);

        // Assert
        totalCount.Should().Be(2);
        items.Should().OnlyContain(b => b.GenderType == "Female");
    }

    [Fact]
    public async Task GetAllAsync_WhenFilteredByStatus_ReturnsOnlyMatchingStatus()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        await SeedBasicFacilitiesAsync(context);
        var sut = new BlockRepository(context);
        var query = new BlockQueryDto { Status = "Maintenance", Page = 1, PageSize = 10 };

        // Act
        var (items, totalCount) = await sut.GetAllAsync(query);

        // Assert
        totalCount.Should().Be(1);
        items.First().Status.Should().Be("Maintenance");
    }

    [Fact]
    public async Task GetAllAsync_WhenPaginationApplied_ReturnsCorrectSubset()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        await SeedBasicFacilitiesAsync(context);
        var sut = new BlockRepository(context);
        var query = new BlockQueryDto { Page = 2, PageSize = 2 };

        // Act
        var (items, totalCount) = await sut.GetAllAsync(query);

        // Assert
        totalCount.Should().Be(4);
        items.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetAllAsync_WhenFilterMatchesNothing_ReturnsEmpty()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        await SeedBasicFacilitiesAsync(context);
        var sut = new BlockRepository(context);
        var query = new BlockQueryDto { BlockName = "NON_EXISTING", Page = 1, PageSize = 10 };

        // Act
        var (items, totalCount) = await sut.GetAllAsync(query);

        // Assert
        totalCount.Should().Be(0);
        items.Should().BeEmpty();
    }

    #endregion

    #region GetByIdAsync Tests

    [Fact]
    public async Task GetByIdAsync_WhenBlockExists_ReturnsBlockWithDormAndRooms()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        await SeedBasicFacilitiesAsync(context);
        var sut = new BlockRepository(context);

        // Act
        var result = await sut.GetByIdAsync(1);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(1);
        result.Dorm.Should().NotBeNull();
        result.Dorm.Name.Should().Be("Dorm A");
        result.Rooms.Should().NotBeNull();
        result.Rooms.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetByIdAsync_WhenBlockNotFound_ReturnsNull()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        await SeedBasicFacilitiesAsync(context);
        var sut = new BlockRepository(context);

        // Act
        var result = await sut.GetByIdAsync(999);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region GetByIdWithRoomsAsync Tests

    [Fact]
    public async Task GetByIdWithRoomsAsync_WhenBlockExists_ReturnsBlockWithRoomsAndRoomType()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        await SeedBasicFacilitiesAsync(context);
        var sut = new BlockRepository(context);

        // Act
        var result = await sut.GetByIdWithRoomsAsync(1);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(1);
        result.Rooms.Should().HaveCount(1);
        result.Rooms.First().RoomType.Should().NotBeNull();
        result.Rooms.First().RoomType.Name.Should().Be("Phòng 4 giường");
    }

    [Fact]
    public async Task GetByIdWithRoomsAsync_WhenBlockNotFound_ReturnsNull()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        await SeedBasicFacilitiesAsync(context);
        var sut = new BlockRepository(context);

        // Act
        var result = await sut.GetByIdWithRoomsAsync(999);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region IsCodeUniqueAsync Tests

    [Fact]
    public async Task IsCodeUniqueAsync_WhenCodeDoesNotExistInDorm_ReturnsTrue()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        await SeedBasicFacilitiesAsync(context);
        var sut = new BlockRepository(context);

        // Act
        var result = await sut.IsCodeUniqueAsync(1, "NEW_CODE");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsCodeUniqueAsync_WhenCodeAlreadyExistsInSameDorm_ReturnsFalse()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        await SeedBasicFacilitiesAsync(context);
        var sut = new BlockRepository(context);

        // Act
        var result = await sut.IsCodeUniqueAsync(1, "B101");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsCodeUniqueAsync_WhenCodeExistsInSameDormButExcludedSelfId_ReturnsTrue()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        await SeedBasicFacilitiesAsync(context);
        var sut = new BlockRepository(context);

        // Act
        var result = await sut.IsCodeUniqueAsync(1, "B101", excludeId: 1);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsCodeUniqueAsync_WhenSameCodeExistsInAnotherDorm_ReturnsTrue()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        await SeedBasicFacilitiesAsync(context);
        var sut = new BlockRepository(context);

        // Act - Dorm 2 does not have code B101
        var result = await sut.IsCodeUniqueAsync(2, "B101");

        // Assert
        result.Should().BeTrue();
    }

    #endregion

    #region HasActiveResidenciesAsync Tests

    [Fact]
    public async Task HasActiveResidenciesAsync_WhenBlockHasActiveResidency_ReturnsTrue()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        await SeedBasicFacilitiesAsync(context);
        var sut = new BlockRepository(context);

        // Act - Block 1 has an active residency
        var result = await sut.HasActiveResidenciesAsync(1);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task HasActiveResidenciesAsync_WhenBlockHasReservedResidency_ReturnsTrue()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        await SeedBasicFacilitiesAsync(context);
        var sut = new BlockRepository(context);

        // Act - Block 2 has a reserved residency
        var result = await sut.HasActiveResidenciesAsync(2);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task HasActiveResidenciesAsync_WhenBlockHasNoResidencies_ReturnsFalse()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        await SeedBasicFacilitiesAsync(context);
        var sut = new BlockRepository(context);

        // Act - Block 3 has no residencies
        var result = await sut.HasActiveResidenciesAsync(3);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region HasRoomsAsync Tests

    [Fact]
    public async Task HasRoomsAsync_WhenBlockHasRooms_ReturnsTrue()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        await SeedBasicFacilitiesAsync(context);
        var sut = new BlockRepository(context);

        // Act
        var result = await sut.HasRoomsAsync(1);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task HasRoomsAsync_WhenBlockHasNoRooms_ReturnsFalse()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        await SeedBasicFacilitiesAsync(context);
        var sut = new BlockRepository(context);

        // Act
        var result = await sut.HasRoomsAsync(4);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region CreateAsync & UpdateAsync Tests

    [Fact]
    public async Task CreateAsync_WhenValidBlock_PersistsToDatabase()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var sut = new BlockRepository(context);
        var newBlock = new Block
        {
            DormId = 1,
            Code = "B999",
            Name = "Block 999",
            FloorLevel = 4,
            GenderType = "Male",
            Status = "Active"
        };

        // Act
        var created = await sut.CreateAsync(newBlock);

        // Assert
        created.Id.Should().BeGreaterThan(0);
        var inDb = await context.Blocks.FindAsync(created.Id);
        inDb.Should().NotBeNull();
        inDb!.Code.Should().Be("B999");
    }

    [Fact]
    public async Task UpdateAsync_WhenBlockUpdated_PersistsChanges()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        await SeedBasicFacilitiesAsync(context);
        var sut = new BlockRepository(context);
        var block = await context.Blocks.FirstAsync(b => b.Id == 1);
        block.Name = "Block 101 Updated";
        block.FloorLevel = 7;

        // Act
        var updated = await sut.UpdateAsync(block);

        // Assert
        updated.Name.Should().Be("Block 101 Updated");
        var inDb = await context.Blocks.FindAsync(1);
        inDb!.Name.Should().Be("Block 101 Updated");
        inDb.FloorLevel.Should().Be(7);
    }

    #endregion
}
