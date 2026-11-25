using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using FUD.DTOs.Facilities.Room;
using FUD.Models;
using FUD.Repositories.Facilities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FUD.Tests.RoomManagement;

/// <summary>
/// Unit tests cho RoomRepository (Quản lý Phòng) - Tầng Repository.
/// Sử dụng EF Core InMemory Database, tuân thủ nghiêm ngặt quy tắc AAA (Arrange - Act - Assert).
/// </summary>
public class RoomRepositoryTests
{
    private static FudContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<FudContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new FudContext(options);
    }

    private static async Task<(Dorm Dorm, Block Block1, Block Block2, List<RoomType> RoomTypes, List<Room> Rooms, List<Bed> Beds)>
        SeedBasicFacilitiesAsync(FudContext context)
    {
        var dorm = new Dorm { Id = 1, Name = "Dorm A", Code = "DA", CampusId = 1, Status = "Active" };
        var block1 = new Block { Id = 1, DormId = 1, Name = "Block 101", Code = "B101", FloorLevel = 5, GenderType = "Male", Status = "Active", Dorm = dorm };
        var block2 = new Block { Id = 2, DormId = 1, Name = "Block 102", Code = "B102", FloorLevel = 3, GenderType = "Female", Status = "Active", Dorm = dorm };

        var type2 = new RoomType { Id = 1, Name = "Phòng 2 giường", BedCapacity = 2 };
        var type4 = new RoomType { Id = 2, Name = "Phòng 4 giường", BedCapacity = 4 };
        var type6 = new RoomType { Id = 3, Name = "Phòng 6 giường", BedCapacity = 6 };

        var room1 = new Room { Id = 10, BlockId = 1, RoomTypeId = 2, RoomNumber = "101L", FloorLevel = 1, Status = "Active", Block = block1, RoomType = type4 };
        var room2 = new Room { Id = 20, BlockId = 1, RoomTypeId = 1, RoomNumber = "102L", FloorLevel = 1, Status = "Maintenance", Block = block1, RoomType = type2 };
        var room3 = new Room { Id = 30, BlockId = 2, RoomTypeId = 2, RoomNumber = "201L", FloorLevel = 2, Status = "Active", Block = block2, RoomType = type4 };
        var room4 = new Room { Id = 40, BlockId = 2, RoomTypeId = 3, RoomNumber = "202L", FloorLevel = 2, Status = "Active", Block = block2, RoomType = type6 };

        var beds = new List<Bed>
        {
            new() { Id = 1, RoomId = 10, BedCode = "101L-B1", PositionInRoom = "Dưới trái", Status = "Occupied", Room = room1 },
            new() { Id = 2, RoomId = 10, BedCode = "101L-B2", PositionInRoom = "Trên trái", Status = "Free", Room = room1 },
            new() { Id = 3, RoomId = 20, BedCode = "102L-B1", PositionInRoom = "Dưới trái", Status = "Free", Room = room2 },
            new() { Id = 4, RoomId = 30, BedCode = "201L-B1", PositionInRoom = "Dưới trái", Status = "Holding", Room = room3 },
            new() { Id = 5, RoomId = 30, BedCode = "201L-B2", PositionInRoom = "Trên trái", Status = "Pending Transfer", Room = room3 }
        };

        await context.Dorms.AddAsync(dorm);
        await context.Blocks.AddRangeAsync(block1, block2);
        await context.RoomTypes.AddRangeAsync(type2, type4, type6);
        await context.Rooms.AddRangeAsync(room1, room2, room3, room4);
        await context.Beds.AddRangeAsync(beds);
        await context.SaveChangesAsync();

        return (dorm, block1, block2, new List<RoomType> { type2, type4, type6 }, new List<Room> { room1, room2, room3, room4 }, beds);
    }

    #region GetAllAsync Tests

    [Fact]
    public async Task GetAllAsync_WhenNoFilter_ReturnsAllRoomsPaged()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        await SeedBasicFacilitiesAsync(context);
        var sut = new RoomRepository(context);
        var query = new RoomQueryDto { Page = 1, PageSize = 10 };

        // Act
        var (items, totalCount) = await sut.GetAllAsync(query);

        // Assert
        totalCount.Should().Be(4);
        items.Should().HaveCount(4);
        items.First().Block.Should().NotBeNull();
        items.First().RoomType.Should().NotBeNull();
    }

    [Fact]
    public async Task GetAllAsync_WhenFilteredByBlockNameOrCode_ReturnsOnlyRoomsInMatchingBlock()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        await SeedBasicFacilitiesAsync(context);
        var sut = new RoomRepository(context);
        var query = new RoomQueryDto { BlockName = "B101", Page = 1, PageSize = 10 };

        // Act
        var (items, totalCount) = await sut.GetAllAsync(query);

        // Assert
        totalCount.Should().Be(2);
        items.Should().HaveCount(2);
        items.Should().OnlyContain(r => r.Block.Code == "B101");
    }

    [Fact]
    public async Task GetAllAsync_WhenFilteredByRoomNumber_ReturnsMatchingRooms()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        await SeedBasicFacilitiesAsync(context);
        var sut = new RoomRepository(context);
        var query = new RoomQueryDto { RoomNumber = "101", Page = 1, PageSize = 10 };

        // Act
        var (items, totalCount) = await sut.GetAllAsync(query);

        // Assert
        totalCount.Should().Be(1);
        items.First().RoomNumber.Should().Be("101L");
    }

    [Fact]
    public async Task GetAllAsync_WhenFilteredByRoomTypeId_ReturnsRoomsWithThatType()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        await SeedBasicFacilitiesAsync(context);
        var sut = new RoomRepository(context);
        var query = new RoomQueryDto { RoomTypeId = 2, Page = 1, PageSize = 10 };

        // Act
        var (items, totalCount) = await sut.GetAllAsync(query);

        // Assert
        totalCount.Should().Be(2);
        items.Should().OnlyContain(r => r.RoomTypeId == 2);
    }

    [Fact]
    public async Task GetAllAsync_WhenFilteredByGenderType_ReturnsRoomsInBlocksWithThatGender()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        await SeedBasicFacilitiesAsync(context);
        var sut = new RoomRepository(context);
        var query = new RoomQueryDto { GenderType = "Female", Page = 1, PageSize = 10 };

        // Act
        var (items, totalCount) = await sut.GetAllAsync(query);

        // Assert
        totalCount.Should().Be(2);
        items.Should().OnlyContain(r => r.Block.GenderType == "Female");
    }

    [Fact]
    public async Task GetAllAsync_WhenFilteredByStatus_ReturnsOnlyMatchingStatusRooms()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        await SeedBasicFacilitiesAsync(context);
        var sut = new RoomRepository(context);
        var query = new RoomQueryDto { Status = "Maintenance", Page = 1, PageSize = 10 };

        // Act
        var (items, totalCount) = await sut.GetAllAsync(query);

        // Assert
        totalCount.Should().Be(1);
        items.First().Status.Should().Be("Maintenance");
    }

    [Fact]
    public async Task GetAllAsync_WhenPagingApplied_ReturnsCorrectSlice()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        await SeedBasicFacilitiesAsync(context);
        var sut = new RoomRepository(context);
        var query = new RoomQueryDto { Page = 2, PageSize = 2 };

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
        var sut = new RoomRepository(context);
        var query = new RoomQueryDto { RoomNumber = "NON_EXISTING", Page = 1, PageSize = 10 };

        // Act
        var (items, totalCount) = await sut.GetAllAsync(query);

        // Assert
        totalCount.Should().Be(0);
        items.Should().BeEmpty();
    }

    #endregion

    #region GetByIdAsync & GetByIdWithBedsAsync Tests

    [Fact]
    public async Task GetByIdAsync_WhenRoomExists_ReturnsRoomWithIncludes()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        await SeedBasicFacilitiesAsync(context);
        var sut = new RoomRepository(context);

        // Act
        var result = await sut.GetByIdAsync(10);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(10);
        result.Block.Should().NotBeNull();
        result.Block.Dorm.Should().NotBeNull();
        result.RoomType.Should().NotBeNull();
        result.Beds.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetByIdAsync_WhenRoomNotFound_ReturnsNull()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        await SeedBasicFacilitiesAsync(context);
        var sut = new RoomRepository(context);

        // Act
        var result = await sut.GetByIdAsync(999);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdWithBedsAsync_WhenRoomExists_ReturnsRoomWithBeds()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        await SeedBasicFacilitiesAsync(context);
        var sut = new RoomRepository(context);

        // Act
        var result = await sut.GetByIdWithBedsAsync(10);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(10);
        result.Beds.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetByIdWithBedsAsync_WhenRoomNotFound_ReturnsNull()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        await SeedBasicFacilitiesAsync(context);
        var sut = new RoomRepository(context);

        // Act
        var result = await sut.GetByIdWithBedsAsync(999);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region IsRoomNumberUniqueAsync Tests

    [Fact]
    public async Task IsRoomNumberUniqueAsync_WhenNumberDoesNotExistInBlock_ReturnsTrue()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        await SeedBasicFacilitiesAsync(context);
        var sut = new RoomRepository(context);

        // Act
        var result = await sut.IsRoomNumberUniqueAsync(1, "103L");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsRoomNumberUniqueAsync_WhenNumberAlreadyExistsInSameBlock_ReturnsFalse()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        await SeedBasicFacilitiesAsync(context);
        var sut = new RoomRepository(context);

        // Act
        var result = await sut.IsRoomNumberUniqueAsync(1, "101L");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsRoomNumberUniqueAsync_WhenNumberMatchesCurrentRoomSelfExclude_ReturnsTrue()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        await SeedBasicFacilitiesAsync(context);
        var sut = new RoomRepository(context);

        // Act
        var result = await sut.IsRoomNumberUniqueAsync(1, "101L", excludeId: 10);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsRoomNumberUniqueAsync_WhenSameNumberExistsInAnotherBlock_ReturnsTrue()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        await SeedBasicFacilitiesAsync(context);
        var sut = new RoomRepository(context);

        // Act - Block 2 does not have 101L
        var result = await sut.IsRoomNumberUniqueAsync(2, "101L");

        // Assert
        result.Should().BeTrue();
    }

    #endregion

    #region HasActiveBedsAsync Tests

    [Fact]
    public async Task HasActiveBedsAsync_WhenRoomHasOccupiedBed_ReturnsTrue()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        await SeedBasicFacilitiesAsync(context);
        var sut = new RoomRepository(context);

        // Act - Room 10 has Bed 1 which is Occupied
        var result = await sut.HasActiveBedsAsync(10);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task HasActiveBedsAsync_WhenRoomHasHoldingOrPendingTransferBed_ReturnsTrue()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        await SeedBasicFacilitiesAsync(context);
        var sut = new RoomRepository(context);

        // Act - Room 30 has Holding and Pending Transfer beds
        var result = await sut.HasActiveBedsAsync(30);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task HasActiveBedsAsync_WhenRoomHasOnlyFreeBeds_ReturnsFalse()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        await SeedBasicFacilitiesAsync(context);
        var sut = new RoomRepository(context);

        // Act - Room 20 has only Bed 3 which is Free
        var result = await sut.HasActiveBedsAsync(20);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task HasActiveBedsAsync_WhenRoomHasNoBeds_ReturnsFalse()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        await SeedBasicFacilitiesAsync(context);
        var sut = new RoomRepository(context);

        // Act - Room 40 has no beds
        var result = await sut.HasActiveBedsAsync(40);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region GetBedsByRoomIdAsync Tests

    [Fact]
    public async Task GetBedsByRoomIdAsync_WhenRoomHasBeds_ReturnsThoseBeds()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        await SeedBasicFacilitiesAsync(context);
        var sut = new RoomRepository(context);

        // Act
        var beds = await sut.GetBedsByRoomIdAsync(10);

        // Assert
        beds.Should().HaveCount(2);
        beds.Should().OnlyContain(b => b.RoomId == 10);
    }

    [Fact]
    public async Task GetBedsByRoomIdAsync_WhenRoomHasNoBeds_ReturnsEmptyList()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        await SeedBasicFacilitiesAsync(context);
        var sut = new RoomRepository(context);

        // Act
        var beds = await sut.GetBedsByRoomIdAsync(40);

        // Assert
        beds.Should().BeEmpty();
    }

    #endregion

    #region GetRoomTypesAsync Tests

    [Fact]
    public async Task GetRoomTypesAsync_WhenCalled_ReturnsTypesOrderedByBedCapacity()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        await SeedBasicFacilitiesAsync(context);
        var sut = new RoomRepository(context);

        // Act
        var types = (await sut.GetRoomTypesAsync()).ToList();

        // Assert
        types.Should().HaveCount(3);
        types[0].BedCapacity.Should().Be(2);
        types[1].BedCapacity.Should().Be(4);
        types[2].BedCapacity.Should().Be(6);
    }

    #endregion

    #region GetBlockNumberOfFloorsAsync Tests

    [Fact]
    public async Task GetBlockNumberOfFloorsAsync_WhenBlockExists_ReturnsFloorLevel()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        await SeedBasicFacilitiesAsync(context);
        var sut = new RoomRepository(context);

        // Act
        var floors = await sut.GetBlockNumberOfFloorsAsync(1);

        // Assert
        floors.Should().Be(5);
    }

    [Fact]
    public async Task GetBlockNumberOfFloorsAsync_WhenBlockNotFound_ReturnsNull()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        await SeedBasicFacilitiesAsync(context);
        var sut = new RoomRepository(context);

        // Act
        var floors = await sut.GetBlockNumberOfFloorsAsync(999);

        // Assert
        floors.Should().BeNull();
    }

    #endregion

    #region CreateAsync & UpdateAsync Tests

    [Fact]
    public async Task CreateAsync_WhenValidRoom_PersistsToDatabase()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var sut = new RoomRepository(context);
        var newRoom = new Room
        {
            BlockId = 1,
            RoomTypeId = 2,
            RoomNumber = "109L",
            FloorLevel = 1,
            Status = "Active"
        };

        // Act
        var created = await sut.CreateAsync(newRoom);

        // Assert
        created.Id.Should().BeGreaterThan(0);
        var inDb = await context.Rooms.FindAsync(created.Id);
        inDb.Should().NotBeNull();
        inDb!.RoomNumber.Should().Be("109L");
    }

    [Fact]
    public async Task UpdateAsync_WhenRoomUpdated_PersistsChanges()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        await SeedBasicFacilitiesAsync(context);
        var sut = new RoomRepository(context);
        var room = await context.Rooms.FirstAsync(r => r.Id == 10);
        room.RoomNumber = "101L-Renamed";
        room.FloorLevel = 3;

        // Act
        var updated = await sut.UpdateAsync(room);

        // Assert
        updated.RoomNumber.Should().Be("101L-Renamed");
        var inDb = await context.Rooms.FindAsync(10);
        inDb!.RoomNumber.Should().Be("101L-Renamed");
        inDb.FloorLevel.Should().Be(3);
    }

    #endregion

    #region IsRoomNumberUniqueOnFloorAsync Tests

    [Fact]
    public async Task IsRoomNumberUniqueOnFloorAsync_WhenNoDuplicateOnFloor_ReturnsTrue()
    {
        // Arrange — Block 1 tầng 1 có phòng "101L" và "102L", kiểm tra "103L" trên tầng 1 → unique
        using var context = CreateInMemoryContext();
        await SeedBasicFacilitiesAsync(context);
        var sut = new RoomRepository(context);

        // Act
        var result = await sut.IsRoomNumberUniqueOnFloorAsync(blockId: 1, floorLevel: 1, roomNumber: "103L");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsRoomNumberUniqueOnFloorAsync_WhenDuplicateExistsOnSameFloor_ReturnsFalse()
    {
        // Arrange — Block 1 tầng 1 đã có phòng "101L", thêm "101L" nữa → trùng
        using var context = CreateInMemoryContext();
        await SeedBasicFacilitiesAsync(context);
        var sut = new RoomRepository(context);

        // Act — Phòng id=10 đã có RoomNumber="101L", FloorLevel=1, BlockId=1
        var result = await sut.IsRoomNumberUniqueOnFloorAsync(blockId: 1, floorLevel: 1, roomNumber: "101L");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsRoomNumberUniqueOnFloorAsync_WhenExcludeIdProvided_ExcludesCurrentRoom()
    {
        // Arrange — Khi sửa chính phòng đó, phải bỏ qua phòng hiện tại khỏi check trùng
        using var context = CreateInMemoryContext();
        await SeedBasicFacilitiesAsync(context);
        var sut = new RoomRepository(context);

        // Act — Phòng id=10 (101L tầng 1 block 1) exclude chính nó → phải unique
        var result = await sut.IsRoomNumberUniqueOnFloorAsync(blockId: 1, floorLevel: 1, roomNumber: "101L", excludeId: 10);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsRoomNumberUniqueOnFloorAsync_WhenSameNameDifferentFloor_ReturnsTrue()
    {
        // Arrange — Tầng 2 chưa có phòng "101L" → unique dù tầng 1 đã có
        using var context = CreateInMemoryContext();
        await SeedBasicFacilitiesAsync(context);
        var sut = new RoomRepository(context);

        // Act
        var result = await sut.IsRoomNumberUniqueOnFloorAsync(blockId: 1, floorLevel: 2, roomNumber: "101L");

        // Assert
        result.Should().BeTrue();
    }

    #endregion

    #region Guard Room Management & Detail Tests

    [Fact]
    public async Task GetAllAsync_WhenFilteredByDormBlockFloorAndExactRoomNumber_ReturnsMatchingRooms()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        await SeedBasicFacilitiesAsync(context);
        var dormB = new Dorm { Id = 2, Name = "Dorm B", Code = "DB", CampusId = 1, Status = "Active" };
        var blockB = new Block { Id = 3, DormId = 2, Name = "Block 301", Code = "B301", FloorLevel = 3, GenderType = "Male", Status = "Active", Dorm = dormB };
        var roomB = new Room { Id = 50, BlockId = 3, RoomTypeId = 2, RoomNumber = "101L", FloorLevel = 1, Status = "Active", Block = blockB };
        await context.Dorms.AddAsync(dormB);
        await context.Blocks.AddAsync(blockB);
        await context.Rooms.AddAsync(roomB);
        await context.SaveChangesAsync();

        var sut = new RoomRepository(context);
        var query = new RoomQueryDto
        {
            DormId = 1,
            BlockId = 1,
            FloorLevel = 1,
            ExactRoomNumber = "101L",
            Page = 1,
            PageSize = 10
        };

        // Act
        var (items, totalCount) = await sut.GetAllAsync(query);

        // Assert
        totalCount.Should().Be(1);
        var matched = items.Single();
        matched.Id.Should().Be(10);
        matched.Block.DormId.Should().Be(1);
    }

    [Fact]
    public async Task GetGuardDormIdAsync_WhenGuardExists_ReturnsAssignedDormId()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        await SeedBasicFacilitiesAsync(context);
        context.Guards.Add(new Guard { UserId = 100, DormId = 1 });
        await context.SaveChangesAsync();
        var sut = new RoomRepository(context);

        // Act
        var dormId = await sut.GetGuardDormIdAsync(100);

        // Assert
        dormId.Should().Be(1);
    }

    [Fact]
    public async Task GetRoomDetailWithResidentsAsync_WhenRoomHasResidencies_LoadsStudentTermAndHandover()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        await SeedBasicFacilitiesAsync(context);

        var studentUser = new User
        {
            Id = 501,
            Code = "SE180001",
            FullName = "Nguyen Van A",
            Gender = "Male",
            Email = "anvse180001@fpt.edu.vn",
            AccountStatus = "Active"
        };
        var student = new Student { UserId = 501, Major = "SE", Cohort = "K18", User = studentUser };
        var term = new Term { Id = 1, Name = "Fall", AcademicYear = "2026-2027", StartDate = new DateOnly(2026, 9, 1), EndDate = new DateOnly(2026, 12, 31) };
        var residency = new Residency
        {
            Id = 701,
            StudentId = 501,
            BedId = 1,
            TermId = 1,
            Status = "Active",
            CheckInDate = new DateOnly(2026, 9, 2),
            Student = student,
            Term = term
        };
        var handover = new Handover
        {
            Id = 801,
            HandoverType = "Checkin",
            ResidencyId = 701,
            RoomId = 10,
            GuardId = 100,
            Status = "Completed",
            CreatedTime = DateTime.UtcNow
        };

        await context.Users.AddAsync(studentUser);
        await context.Students.AddAsync(student);
        await context.Terms.AddAsync(term);
        await context.Residencies.AddAsync(residency);
        await context.Handovers.AddAsync(handover);
        await context.SaveChangesAsync();

        var sut = new RoomRepository(context);

        // Act
        var detailRoom = await sut.GetRoomDetailWithResidentsAsync(10);

        // Assert
        detailRoom.Should().NotBeNull();
        detailRoom!.Beds.Should().HaveCount(2);
        var occupiedBed = detailRoom.Beds.Single(b => b.Id == 1);
        occupiedBed.Residencies.Should().ContainSingle();
        occupiedBed.Residencies.First().Student.User.Code.Should().Be("SE180001");
        occupiedBed.Residencies.First().Handover!.Status.Should().Be("Completed");
    }

    [Fact]
    public async Task GetAllAsync_WhenManagerFiltersByExactRoomFloorAndGenderAcrossAllDorms_ReturnsMatchingRooms()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        await SeedBasicFacilitiesAsync(context);
        var dormB = new Dorm { Id = 2, Name = "Dorm B", Code = "DB", CampusId = 1, Status = "Active" };
        var blockBFemale = new Block { Id = 3, DormId = 2, Name = "Block 202 - Dorm B", Code = "B202", FloorLevel = 3, GenderType = "Female", Status = "Active", Dorm = dormB };
        var roomBFemale = new Room { Id = 60, BlockId = 3, RoomTypeId = 2, RoomNumber = "201L", FloorLevel = 2, Status = "Active", Block = blockBFemale };
        await context.Dorms.AddAsync(dormB);
        await context.Blocks.AddAsync(blockBFemale);
        await context.Rooms.AddAsync(roomBFemale);
        await context.SaveChangesAsync();

        var sut = new RoomRepository(context);
        var query = new RoomQueryDto
        {
            ExactRoomNumber = "201L",
            FloorLevel = 2,
            GenderType = "Female",
            Page = 1,
            PageSize = 10
        };

        // Act
        var (items, totalCount) = await sut.GetAllAsync(query);

        // Assert
        totalCount.Should().Be(2);
        items.Select(r => r.Id).Should().BeEquivalentTo(new[] { 30, 60 });
    }

    [Fact]
    public async Task GetBlocksWithRoomsAsync_WhenDormIdIsNullForManager_ReturnsAllBlocksAcrossAllDorms()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        await SeedBasicFacilitiesAsync(context);
        var dormB = new Dorm { Id = 2, Name = "Dorm B", Code = "DB", CampusId = 1, Status = "Active" };
        var blockB = new Block { Id = 3, DormId = 2, Name = "Block 101 - Dorm B", Code = "B101", FloorLevel = 3, GenderType = "Male", Status = "Active", Dorm = dormB };
        var roomB = new Room { Id = 55, BlockId = 3, RoomTypeId = 2, RoomNumber = "101L", FloorLevel = 1, Status = "Active", Block = blockB };
        await context.Dorms.AddAsync(dormB);
        await context.Blocks.AddAsync(blockB);
        await context.Rooms.AddAsync(roomB);
        await context.SaveChangesAsync();

        var sut = new RoomRepository(context);

        // Act
        var blocks = (await sut.GetBlocksWithRoomsAsync(dormId: null)).ToList();

        // Assert
        blocks.Should().HaveCount(3);
        blocks.Select(b => b.Dorm.Name).Distinct().Should().BeEquivalentTo(new[] { "Dorm A", "Dorm B" });
    }

    #endregion
}
