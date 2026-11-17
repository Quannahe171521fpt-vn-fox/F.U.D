using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using FUD.Common;
using FUD.DTOs.Facilities.Room;
using FUD.Models;
using FUD.Repositories.Interfaces.Facilities;
using FUD.Services.Facilities;
using Moq;
using Xunit;

namespace FUD.Tests.RoomManagement;

/// <summary>
/// Unit tests cho RoomService (Quản lý Phòng) - Tầng Service.
/// Tuân thủ nghiêm ngặt quy tắc AAA (Arrange - Act - Assert) và chuẩn kiểm thử chuyên nghiệp.
/// </summary>
public class RoomServiceTests
{
    private readonly Mock<IRoomRepository> _roomRepoMock = new();

    private RoomService CreateSut() => new(_roomRepoMock.Object);

    private static (Room Room, Block Block, RoomType RoomType) CreateSampleRoomEntity(
        int roomId = 10,
        int blockId = 1,
        int roomTypeId = 1,
        string roomNumber = "101L",
        int floor = 1,
        string status = "Active",
        int freeBeds = 2,
        int occupiedBeds = 2)
    {
        var dorm = new Dorm { Id = 1, Name = "Dorm A", Code = "DA", Status = "Active" };
        var block = new Block
        {
            Id = blockId,
            DormId = 1,
            Code = "B101",
            Name = "Block 101",
            FloorLevel = 5,
            GenderType = "Male",
            Status = "Active",
            Dorm = dorm
        };

        var roomType = new RoomType
        {
            Id = roomTypeId,
            Name = "Phòng 4 giường",
            BedCapacity = 4
        };

        var room = new Room
        {
            Id = roomId,
            BlockId = blockId,
            RoomTypeId = roomTypeId,
            RoomNumber = roomNumber,
            FloorLevel = floor,
            Status = status,
            Block = block,
            RoomType = roomType,
            Beds = new List<Bed>()
        };

        int bedIdCounter = 1;
        for (int i = 0; i < freeBeds; i++)
        {
            room.Beds.Add(new Bed
            {
                Id = bedIdCounter++,
                RoomId = roomId,
                BedCode = $"{roomNumber}-B{bedIdCounter}",
                Status = "Free",
                Room = room
            });
        }
        for (int i = 0; i < occupiedBeds; i++)
        {
            room.Beds.Add(new Bed
            {
                Id = bedIdCounter++,
                RoomId = roomId,
                BedCode = $"{roomNumber}-B{bedIdCounter}",
                Status = "Occupied",
                Room = room
            });
        }

        return (room, block, roomType);
    }

    #region GetAllAsync Tests

    [Fact]
    public async Task GetAllAsync_WhenRoomsExist_ReturnsPagedResultWithMappedDtos()
    {
        // Arrange
        var query = new RoomQueryDto { Page = 1, PageSize = 10 };
        var (room1, _, _) = CreateSampleRoomEntity(10, 1, 1, "101L", freeBeds: 2, occupiedBeds: 2);
        var (room2, _, _) = CreateSampleRoomEntity(20, 1, 1, "102L", freeBeds: 4, occupiedBeds: 0);
        var list = new List<Room> { room1, room2 };

        _roomRepoMock.Setup(r => r.GetAllAsync(query)).ReturnsAsync((list, list.Count));
        var sut = CreateSut();

        // Act
        var result = await sut.GetAllAsync(query);

        // Assert
        _roomRepoMock.Verify(r => r.GetAllAsync(query), Times.Once);
        result.Should().NotBeNull();
        result.TotalCount.Should().Be(2);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(10);
        result.Items.Should().HaveCount(2);

        var first = result.Items.First();
        first.Id.Should().Be(room1.Id);
        first.BlockId.Should().Be(room1.BlockId);
        first.BlockName.Should().Be("Block 101");
        first.BlockGenderType.Should().Be("Male");
        first.RoomTypeId.Should().Be(room1.RoomTypeId);
        first.RoomTypeName.Should().Be("Phòng 4 giường");
        first.BedCapacity.Should().Be(4);
        first.RoomNumber.Should().Be("101L");
        first.FloorLevel.Should().Be(1);
        first.Status.Should().Be("Active");
        first.TotalBeds.Should().Be(4);
        first.FreeBeds.Should().Be(2);
    }

    [Fact]
    public async Task GetAllAsync_WhenNoRoomsMatchFilter_ReturnsEmptyPagedResult()
    {
        // Arrange
        var query = new RoomQueryDto { RoomNumber = "NON_EXISTING", Page = 1, PageSize = 10 };
        _roomRepoMock.Setup(r => r.GetAllAsync(query))
            .ReturnsAsync((Enumerable.Empty<Room>(), 0));

        var sut = CreateSut();

        // Act
        var result = await sut.GetAllAsync(query);

        // Assert
        result.Should().NotBeNull();
        result.TotalCount.Should().Be(0);
        result.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAllAsync_WhenRoomHasNullNavigationProperties_HandlesNullsGracefully()
    {
        // Arrange
        var query = new RoomQueryDto { Page = 1, PageSize = 10 };
        var roomWithNulls = new Room
        {
            Id = 99,
            BlockId = 1,
            RoomTypeId = 1,
            RoomNumber = "999L",
            FloorLevel = 1,
            Status = "Active",
            Block = null!,
            RoomType = null!,
            Beds = null!
        };
        var list = new List<Room> { roomWithNulls };
        _roomRepoMock.Setup(r => r.GetAllAsync(query)).ReturnsAsync((list, 1));

        var sut = CreateSut();

        // Act
        var result = await sut.GetAllAsync(query);

        // Assert
        result.Items.Should().HaveCount(1);
        var item = result.Items.First();
        item.BlockName.Should().BeEmpty();
        item.BlockGenderType.Should().BeEmpty();
        item.RoomTypeName.Should().BeEmpty();
        item.BedCapacity.Should().Be(0);
        item.TotalBeds.Should().Be(0);
        item.FreeBeds.Should().Be(0);
    }

    #endregion

    #region GetByIdAsync Tests

    [Fact]
    public async Task GetByIdAsync_WhenRoomExists_ReturnsMappedDto()
    {
        // Arrange
        const int roomId = 10;
        var (room, _, _) = CreateSampleRoomEntity(roomId, 1, 1, "101L", freeBeds: 1, occupiedBeds: 3);
        _roomRepoMock.Setup(r => r.GetByIdAsync(roomId)).ReturnsAsync(room);

        var sut = CreateSut();

        // Act
        var result = await sut.GetByIdAsync(roomId);

        // Assert
        _roomRepoMock.Verify(r => r.GetByIdAsync(roomId), Times.Once);
        result.Should().NotBeNull();
        result!.Id.Should().Be(roomId);
        result.RoomNumber.Should().Be("101L");
        result.TotalBeds.Should().Be(4);
        result.FreeBeds.Should().Be(1);
    }

    [Fact]
    public async Task GetByIdAsync_WhenRoomNotFound_ReturnsNull()
    {
        // Arrange
        const int nonExistentId = 999;
        _roomRepoMock.Setup(r => r.GetByIdAsync(nonExistentId)).ReturnsAsync((Room?)null);

        var sut = CreateSut();

        // Act
        var result = await sut.GetByIdAsync(nonExistentId);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region CreateAsync Tests

    [Fact]
    public async Task CreateAsync_WhenValidInput_CreatesRoomWithActiveStatusAndReturnsDto()
    {
        // Arrange
        var dto = new CreateRoomDto
        {
            BlockId = 1,
            RoomTypeId = 2,
            RoomNumber = "103L",
            FloorLevel = 1
        };
        var (createdRoom, _, _) = CreateSampleRoomEntity(15, dto.BlockId, dto.RoomTypeId, dto.RoomNumber, dto.FloorLevel.Value, "Active");

        _roomRepoMock.Setup(r => r.IsRoomNumberUniqueAsync(dto.BlockId, dto.RoomNumber, null)).ReturnsAsync(true);
        _roomRepoMock.Setup(r => r.CreateAsync(It.IsAny<Room>())).ReturnsAsync(createdRoom);
        _roomRepoMock.Setup(r => r.GetByIdAsync(createdRoom.Id)).ReturnsAsync(createdRoom);

        var sut = CreateSut();

        // Act
        var result = await sut.CreateAsync(dto);

        // Assert
        _roomRepoMock.Verify(r => r.IsRoomNumberUniqueAsync(dto.BlockId, dto.RoomNumber, null), Times.Once);
        _roomRepoMock.Verify(r => r.CreateAsync(It.Is<Room>(r =>
            r.BlockId == dto.BlockId &&
            r.RoomTypeId == dto.RoomTypeId &&
            r.RoomNumber == dto.RoomNumber &&
            r.FloorLevel == dto.FloorLevel &&
            r.Status == "Active")), Times.Once);

        result.Should().NotBeNull();
        result.Id.Should().Be(createdRoom.Id);
        result.RoomNumber.Should().Be(dto.RoomNumber);
        result.Status.Should().Be("Active");
    }

    [Fact]
    public async Task CreateAsync_WhenRoomNumberAlreadyExistsInBlock_ThrowsInvalidOperationException()
    {
        // Arrange
        var dto = new CreateRoomDto
        {
            BlockId = 1,
            RoomTypeId = 1,
            RoomNumber = "101L",
            FloorLevel = 1
        };
        _roomRepoMock.Setup(r => r.IsRoomNumberUniqueAsync(dto.BlockId, dto.RoomNumber, null)).ReturnsAsync(false);

        var sut = CreateSut();

        // Act
        Func<Task> act = async () => await sut.CreateAsync(dto);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"Phòng '{dto.RoomNumber}' đã tồn tại trong block này");
        _roomRepoMock.Verify(r => r.CreateAsync(It.IsAny<Room>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_WhenFloorLevelExceedsBlockFloors_ThrowsInvalidOperationException()
    {
        // Arrange
        var dto = new CreateRoomDto
        {
            BlockId = 1,
            RoomTypeId = 1,
            RoomNumber = "601L",
            FloorLevel = 6 // Block chỉ có 5 tầng
        };
        _roomRepoMock.Setup(r => r.IsRoomNumberUniqueAsync(dto.BlockId, dto.RoomNumber, null)).ReturnsAsync(true);
        _roomRepoMock.Setup(r => r.GetBlockNumberOfFloorsAsync(dto.BlockId)).ReturnsAsync(5);

        var sut = CreateSut();

        // Act
        Func<Task> act = async () => await sut.CreateAsync(dto);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Số tầng (6) không hợp lệ. Block chỉ có 5 tầng");
        _roomRepoMock.Verify(r => r.CreateAsync(It.IsAny<Room>()), Times.Never);
    }

    #endregion

    #region UpdateAsync Tests

    [Fact]
    public async Task UpdateAsync_WhenValidInput_UpdatesRoomAndReturnsDto()
    {
        // Arrange
        const int roomId = 10;
        var dto = new UpdateRoomDto
        {
            RoomTypeId = 2,
            RoomNumber = "101L-Renamed",
            FloorLevel = 2
        };
        var (existingRoom, _, _) = CreateSampleRoomEntity(roomId, 1, 1, "101L", 1);

        _roomRepoMock.Setup(r => r.GetByIdAsync(roomId)).ReturnsAsync(existingRoom);
        _roomRepoMock.Setup(r => r.IsRoomNumberUniqueAsync(existingRoom.BlockId, dto.RoomNumber, roomId)).ReturnsAsync(true);
        _roomRepoMock.Setup(r => r.UpdateAsync(existingRoom)).ReturnsAsync(existingRoom);

        var sut = CreateSut();

        // Act
        var result = await sut.UpdateAsync(roomId, dto);

        // Assert
        _roomRepoMock.Verify(r => r.GetByIdAsync(roomId), Times.AtLeastOnce);
        _roomRepoMock.Verify(r => r.IsRoomNumberUniqueAsync(existingRoom.BlockId, dto.RoomNumber, roomId), Times.Once);
        _roomRepoMock.Verify(r => r.UpdateAsync(It.Is<Room>(r =>
            r.Id == roomId &&
            r.RoomTypeId == dto.RoomTypeId &&
            r.RoomNumber == dto.RoomNumber &&
            r.FloorLevel == dto.FloorLevel)), Times.Once);

        result.Should().NotBeNull();
        result.RoomNumber.Should().Be(dto.RoomNumber);
        result.FloorLevel.Should().Be(dto.FloorLevel);
    }

    [Fact]
    public async Task UpdateAsync_WhenRoomNotFound_ThrowsKeyNotFoundException()
    {
        // Arrange
        const int nonExistentId = 999;
        var dto = new UpdateRoomDto { RoomTypeId = 1, RoomNumber = "101L", FloorLevel = 1 };
        _roomRepoMock.Setup(r => r.GetByIdAsync(nonExistentId)).ReturnsAsync((Room?)null);

        var sut = CreateSut();

        // Act
        Func<Task> act = async () => await sut.UpdateAsync(nonExistentId, dto);

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage($"Phòng {nonExistentId} không tồn tại");
        _roomRepoMock.Verify(r => r.UpdateAsync(It.IsAny<Room>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_WhenNewRoomNumberDuplicateInBlock_ThrowsInvalidOperationException()
    {
        // Arrange
        const int roomId = 10;
        var dto = new UpdateRoomDto { RoomTypeId = 1, RoomNumber = "DUPLICATE-ROOM", FloorLevel = 1 };
        var (existingRoom, _, _) = CreateSampleRoomEntity(roomId, 1, 1, "101L", 1);

        _roomRepoMock.Setup(r => r.GetByIdAsync(roomId)).ReturnsAsync(existingRoom);
        _roomRepoMock.Setup(r => r.IsRoomNumberUniqueAsync(existingRoom.BlockId, dto.RoomNumber, roomId)).ReturnsAsync(false);

        var sut = CreateSut();

        // Act
        Func<Task> act = async () => await sut.UpdateAsync(roomId, dto);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"Phòng '{dto.RoomNumber}' đã tồn tại trong block này");
        _roomRepoMock.Verify(r => r.UpdateAsync(It.IsAny<Room>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_WhenFloorLevelExceedsBlockFloors_ThrowsInvalidOperationException()
    {
        // Arrange
        const int roomId = 10;
        var dto = new UpdateRoomDto { RoomTypeId = 1, RoomNumber = "101L", FloorLevel = 7 };
        var (existingRoom, _, _) = CreateSampleRoomEntity(roomId, 1, 1, "101L", 1); // block có 5 tầng

        _roomRepoMock.Setup(r => r.GetByIdAsync(roomId)).ReturnsAsync(existingRoom);
        _roomRepoMock.Setup(r => r.IsRoomNumberUniqueAsync(existingRoom.BlockId, dto.RoomNumber, roomId)).ReturnsAsync(true);

        var sut = CreateSut();

        // Act
        Func<Task> act = async () => await sut.UpdateAsync(roomId, dto);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Số tầng (7) không hợp lệ. Block chỉ có 5 tầng");
        _roomRepoMock.Verify(r => r.UpdateAsync(It.IsAny<Room>()), Times.Never);
    }

    #endregion

    #region UpdateStatusAsync Tests

    [Fact]
    public async Task UpdateStatusAsync_WhenStatusUpdatedToMaintenance_UpdatesFreeBedsToMaintenance()
    {
        // Arrange
        const int roomId = 10;
        var dto = new UpdateRoomStatusDto { Status = "Maintenance" };
        var (existingRoom, _, _) = CreateSampleRoomEntity(roomId, 1, 1, "101L", status: "Active");

        var bed1Free = new Bed { Id = 1, RoomId = roomId, BedCode = "101L-B1", Status = "Free" };
        var bed2Occupied = new Bed { Id = 2, RoomId = roomId, BedCode = "101L-B2", Status = "Occupied" };
        var roomBeds = new List<Bed> { bed1Free, bed2Occupied };

        _roomRepoMock.Setup(r => r.GetByIdAsync(roomId)).ReturnsAsync(existingRoom);
        _roomRepoMock.Setup(r => r.GetBedsByRoomIdAsync(roomId)).ReturnsAsync(roomBeds);
        _roomRepoMock.Setup(r => r.UpdateAsync(existingRoom)).ReturnsAsync(existingRoom);

        var sut = CreateSut();

        // Act
        var result = await sut.UpdateStatusAsync(roomId, dto);

        // Assert
        _roomRepoMock.Verify(r => r.GetBedsByRoomIdAsync(roomId), Times.Once);
        bed1Free.Status.Should().Be("Maintenance");
        bed2Occupied.Status.Should().Be("Occupied"); // Sinh viên đang ở không bị đổi sang Maintenance
        _roomRepoMock.Verify(r => r.UpdateAsync(It.Is<Room>(r => r.Status == "Maintenance")), Times.Once);

        result.Should().NotBeNull();
        result.Status.Should().Be("Maintenance");
    }

    [Fact]
    public async Task UpdateStatusAsync_WhenStatusUpdatedToActive_DoesNotChangeBeds()
    {
        // Arrange
        const int roomId = 10;
        var dto = new UpdateRoomStatusDto { Status = "Active" };
        var (existingRoom, _, _) = CreateSampleRoomEntity(roomId, 1, 1, "101L", status: "Maintenance");

        _roomRepoMock.Setup(r => r.GetByIdAsync(roomId)).ReturnsAsync(existingRoom);
        _roomRepoMock.Setup(r => r.UpdateAsync(existingRoom)).ReturnsAsync(existingRoom);

        var sut = CreateSut();

        // Act
        var result = await sut.UpdateStatusAsync(roomId, dto);

        // Assert
        _roomRepoMock.Verify(r => r.GetBedsByRoomIdAsync(It.IsAny<int>()), Times.Never);
        _roomRepoMock.Verify(r => r.UpdateAsync(It.Is<Room>(r => r.Status == "Active")), Times.Once);
        result.Should().NotBeNull();
        result.Status.Should().Be("Active");
    }

    [Fact]
    public async Task UpdateStatusAsync_WhenRoomNotFound_ThrowsKeyNotFoundException()
    {
        // Arrange
        const int nonExistentId = 999;
        var dto = new UpdateRoomStatusDto { Status = "Maintenance" };
        _roomRepoMock.Setup(r => r.GetByIdAsync(nonExistentId)).ReturnsAsync((Room?)null);

        var sut = CreateSut();

        // Act
        Func<Task> act = async () => await sut.UpdateStatusAsync(nonExistentId, dto);

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage($"Phòng {nonExistentId} không tồn tại");
        _roomRepoMock.Verify(r => r.UpdateAsync(It.IsAny<Room>()), Times.Never);
    }

    #endregion

    #region GetRoomTypesAsync Tests

    [Fact]
    public async Task UpdateAsync_WhenRoomIsActive_ThrowsInvalidOperationException()
    {
        // Arrange
        const int roomId = 10;
        var dto = new UpdateRoomDto { RoomNumber = "101L", RoomTypeId = 1, FloorLevel = 1 };
        var (activeRoom, _, _) = CreateSampleRoomEntity(roomId, status: "Active");

        _roomRepoMock.Setup(r => r.GetByIdAsync(roomId)).ReturnsAsync(activeRoom);
        // Phòng có sinh viên ở → HasActiveBedsAsync = true
        _roomRepoMock.Setup(r => r.HasActiveBedsAsync(roomId)).ReturnsAsync(true);

        var sut = CreateSut();

        // Act
        Func<Task> act = async () => await sut.UpdateAsync(roomId, dto);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*sinh viên cư trú*");
        _roomRepoMock.Verify(r => r.UpdateAsync(It.IsAny<Room>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_WhenRoomIsMaintenance_AllowsUpdate()
    {
        // Arrange
        const int roomId = 20;
        var dto = new UpdateRoomDto { RoomNumber = "102L", RoomTypeId = 1, FloorLevel = 2 };
        var (maintenanceRoom, _, _) = CreateSampleRoomEntity(roomId, roomNumber: "102L", floor: 2, status: "Maintenance");

        _roomRepoMock.Setup(r => r.GetByIdAsync(roomId)).ReturnsAsync(maintenanceRoom);
        // Phòng trống → HasActiveBedsAsync = false → cho phép sửa
        _roomRepoMock.Setup(r => r.HasActiveBedsAsync(roomId)).ReturnsAsync(false);
        _roomRepoMock.Setup(r => r.IsRoomNumberUniqueAsync(maintenanceRoom.BlockId, dto.RoomNumber, roomId)).ReturnsAsync(true);
        _roomRepoMock.Setup(r => r.IsRoomNumberUniqueOnFloorAsync(maintenanceRoom.BlockId, dto.FloorLevel!.Value, dto.RoomNumber, roomId)).ReturnsAsync(true);
        _roomRepoMock.Setup(r => r.UpdateAsync(maintenanceRoom)).Returns(Task.CompletedTask);
        _roomRepoMock.Setup(r => r.GetByIdAsync(roomId)).ReturnsAsync(maintenanceRoom);

        var sut = CreateSut();

        // Act
        Func<Task> act = async () => await sut.UpdateAsync(roomId, dto);

        // Assert
        await act.Should().NotThrowAsync<InvalidOperationException>();
        _roomRepoMock.Verify(r => r.UpdateAsync(maintenanceRoom), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_WhenRoomIsActiveButEmpty_AllowsUpdate()
    {
        // Arrange — Phòng Active nhưng không có ai ở → được phép sửa
        const int roomId = 30;
        var dto = new UpdateRoomDto { RoomNumber = "103L", RoomTypeId = 1, FloorLevel = 3 };
        var (activeEmptyRoom, _, _) = CreateSampleRoomEntity(roomId, roomNumber: "103L", floor: 3, status: "Active", freeBeds: 4, occupiedBeds: 0);

        _roomRepoMock.Setup(r => r.GetByIdAsync(roomId)).ReturnsAsync(activeEmptyRoom);
        _roomRepoMock.Setup(r => r.HasActiveBedsAsync(roomId)).ReturnsAsync(false); // Không có ai ở
        _roomRepoMock.Setup(r => r.IsRoomNumberUniqueAsync(activeEmptyRoom.BlockId, dto.RoomNumber, roomId)).ReturnsAsync(true);
        _roomRepoMock.Setup(r => r.IsRoomNumberUniqueOnFloorAsync(activeEmptyRoom.BlockId, dto.FloorLevel!.Value, dto.RoomNumber, roomId)).ReturnsAsync(true);
        _roomRepoMock.Setup(r => r.UpdateAsync(activeEmptyRoom)).Returns(Task.CompletedTask);
        _roomRepoMock.Setup(r => r.GetByIdAsync(roomId)).ReturnsAsync(activeEmptyRoom);

        var sut = CreateSut();

        // Act
        Func<Task> act = async () => await sut.UpdateAsync(roomId, dto);

        // Assert
        await act.Should().NotThrowAsync<InvalidOperationException>();
        _roomRepoMock.Verify(r => r.UpdateAsync(activeEmptyRoom), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_WhenRoomNumberDuplicateOnSameFloor_ThrowsInvalidOperationException()
    {
        // Arrange — Tầng 3 đã có phòng "103L", không được tạo thêm
        const int roomId = 40;
        var dto = new UpdateRoomDto { RoomNumber = "103L", RoomTypeId = 1, FloorLevel = 3 };
        var (room, _, _) = CreateSampleRoomEntity(roomId, roomNumber: "101L", floor: 3, status: "Maintenance");

        _roomRepoMock.Setup(r => r.GetByIdAsync(roomId)).ReturnsAsync(room);
        _roomRepoMock.Setup(r => r.HasActiveBedsAsync(roomId)).ReturnsAsync(false);
        _roomRepoMock.Setup(r => r.IsRoomNumberUniqueAsync(room.BlockId, dto.RoomNumber, roomId)).ReturnsAsync(true);
        // Trùng trên cùng tầng
        _roomRepoMock.Setup(r => r.IsRoomNumberUniqueOnFloorAsync(room.BlockId, dto.FloorLevel!.Value, dto.RoomNumber, roomId)).ReturnsAsync(false);

        var sut = CreateSut();

        // Act
        Func<Task> act = async () => await sut.UpdateAsync(roomId, dto);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*đã tồn tại ở tầng*");
        _roomRepoMock.Verify(r => r.UpdateAsync(It.IsAny<Room>()), Times.Never);
    }

    #region GetRoomTypesAsync Tests

    [Fact]
    public async Task GetRoomTypesAsync_WhenCalled_ReturnsMappedLookupDtos()
    {
        // Arrange
        var roomTypes = new List<RoomType>
        {
            new() { Id = 1, Name = "Phòng 2 giường", BedCapacity = 2 },
            new() { Id = 2, Name = "Phòng 4 giường", BedCapacity = 4 }
        };
        _roomRepoMock.Setup(r => r.GetRoomTypesAsync()).ReturnsAsync(roomTypes);

        var sut = CreateSut();

        // Act
        var result = (await sut.GetRoomTypesAsync()).ToList();

        // Assert
        _roomRepoMock.Verify(r => r.GetRoomTypesAsync(), Times.Once);
        result.Should().HaveCount(2);
        result[0].Id.Should().Be(1);
        result[0].Name.Should().Be("Phòng 2 giường");
        result[0].BedCapacity.Should().Be(2);
        result[1].Id.Should().Be(2);
        result[1].Name.Should().Be("Phòng 4 giường");
        result[1].BedCapacity.Should().Be(4);
    }

    #endregion
}
