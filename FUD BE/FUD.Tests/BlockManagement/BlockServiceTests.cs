using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using FUD.Common;
using FUD.DTOs.Facilities.Block;
using FUD.Models;
using FUD.Repositories.Interfaces.Facilities;
using FUD.Services.Facilities;
using Moq;
using Xunit;

namespace FUD.Tests.BlockManagement;

/// <summary>
/// Unit tests cho BlockService (Quản lý Block/Tòa nhà) - Tầng Service.
/// Tuân thủ nghiêm ngặt quy tắc AAA (Arrange - Act - Assert) và chuẩn kiểm thử chuyên nghiệp.
/// </summary>
public class BlockServiceTests
{
    private readonly Mock<IBlockRepository> _blockRepoMock = new();

    private BlockService CreateSut() => new(_blockRepoMock.Object);

    private static (Block Block, Dorm Dorm) CreateSampleBlockEntity(
        int blockId = 1,
        int dormId = 1,
        string code = "B101",
        string name = "Block 101",
        int floors = 5,
        string genderType = "Male",
        string status = "Active",
        int roomCount = 0)
    {
        var dorm = new Dorm { Id = dormId, Name = "Dorm A", Code = "DA", Status = "Active" };
        var block = new Block
        {
            Id = blockId,
            DormId = dormId,
            Code = code,
            Name = name,
            FloorLevel = floors,
            GenderType = genderType,
            Status = status,
            Dorm = dorm
        };

        for (int i = 1; i <= roomCount; i++)
        {
            var roomType = new RoomType { Id = 1, Name = "Phòng 4", BedCapacity = 4 };
            var room = new Room
            {
                Id = i,
                BlockId = blockId,
                RoomNumber = $"10{i}L",
                FloorLevel = 1,
                Status = "Active",
                RoomType = roomType,
                Block = block,
                Beds = new List<Bed>
                {
                    new() { Id = i * 10 + 1, RoomId = i, BedCode = $"10{i}L-B1", Status = "Free" },
                    new() { Id = i * 10 + 2, RoomId = i, BedCode = $"10{i}L-B2", Status = "Occupied" }
                }
            };
            block.Rooms.Add(room);
        }

        return (block, dorm);
    }

    #region GetAllAsync Tests

    [Fact]
    public async Task GetAllAsync_WhenBlocksExist_ReturnsPagedResultWithMappedDtos()
    {
        // Arrange
        var query = new BlockQueryDto { Page = 1, PageSize = 10 };
        var (block1, _) = CreateSampleBlockEntity(1, 1, "B101", "Block 101", roomCount: 2);
        var (block2, _) = CreateSampleBlockEntity(2, 1, "B102", "Block 102", roomCount: 1);
        var list = new List<Block> { block1, block2 };

        _blockRepoMock.Setup(r => r.GetAllAsync(query)).ReturnsAsync((list, list.Count));
        var sut = CreateSut();

        // Act
        var result = await sut.GetAllAsync(query);

        // Assert
        _blockRepoMock.Verify(r => r.GetAllAsync(query), Times.Once);
        result.Should().NotBeNull();
        result.TotalCount.Should().Be(2);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(10);
        result.Items.Should().HaveCount(2);

        var first = result.Items.First();
        first.Id.Should().Be(block1.Id);
        first.DormId.Should().Be(block1.DormId);
        first.DormName.Should().Be("Dorm A");
        first.Code.Should().Be("B101");
        first.Name.Should().Be("Block 101");
        first.NumberOfFloors.Should().Be(5);
        first.GenderType.Should().Be("Male");
        first.Status.Should().Be("Active");
        first.TotalRooms.Should().Be(2);
    }

    [Fact]
    public async Task GetAllAsync_WhenNoBlocksMatchFilter_ReturnsEmptyPagedResult()
    {
        // Arrange
        var query = new BlockQueryDto { BlockName = "NON_EXISTING", Page = 1, PageSize = 10 };
        _blockRepoMock.Setup(r => r.GetAllAsync(query))
            .ReturnsAsync((Enumerable.Empty<Block>(), 0));

        var sut = CreateSut();

        // Act
        var result = await sut.GetAllAsync(query);

        // Assert
        result.Should().NotBeNull();
        result.TotalCount.Should().Be(0);
        result.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAllAsync_WhenBlockHasNullNavigationProperties_HandlesNullsGracefully()
    {
        // Arrange
        var query = new BlockQueryDto { Page = 1, PageSize = 10 };
        var blockWithNulls = new Block
        {
            Id = 99,
            DormId = 1,
            Code = "B99",
            Name = "Block 99",
            FloorLevel = 1,
            GenderType = "Male",
            Status = "Active",
            Dorm = null!,
            Rooms = null!
        };
        var list = new List<Block> { blockWithNulls };
        _blockRepoMock.Setup(r => r.GetAllAsync(query)).ReturnsAsync((list, 1));

        var sut = CreateSut();

        // Act
        var result = await sut.GetAllAsync(query);

        // Assert
        result.Items.Should().HaveCount(1);
        var item = result.Items.First();
        item.DormName.Should().BeEmpty();
        item.TotalRooms.Should().Be(0);
    }

    #endregion

    #region GetByIdAsync Tests

    [Fact]
    public async Task GetByIdAsync_WhenBlockExists_ReturnsMappedDto()
    {
        // Arrange
        const int blockId = 1;
        var (block, _) = CreateSampleBlockEntity(blockId, 1, "B101", "Block 101", roomCount: 3);
        _blockRepoMock.Setup(r => r.GetByIdAsync(blockId)).ReturnsAsync(block);

        var sut = CreateSut();

        // Act
        var result = await sut.GetByIdAsync(blockId);

        // Assert
        _blockRepoMock.Verify(r => r.GetByIdAsync(blockId), Times.Once);
        result.Should().NotBeNull();
        result!.Id.Should().Be(blockId);
        result.Code.Should().Be("B101");
        result.TotalRooms.Should().Be(3);
        result.DormName.Should().Be("Dorm A");
    }

    [Fact]
    public async Task GetByIdAsync_WhenBlockNotFound_ReturnsNull()
    {
        // Arrange
        const int nonExistentId = 999;
        _blockRepoMock.Setup(r => r.GetByIdAsync(nonExistentId)).ReturnsAsync((Block?)null);

        var sut = CreateSut();

        // Act
        var result = await sut.GetByIdAsync(nonExistentId);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region GetRoomsByBlockIdAsync Tests

    [Fact]
    public async Task GetRoomsByBlockIdAsync_WhenBlockExists_ReturnsMappedRoomDtos()
    {
        // Arrange
        const int blockId = 1;
        var (block, _) = CreateSampleBlockEntity(blockId, 1, "B101", "Block 101", roomCount: 2);
        _blockRepoMock.Setup(r => r.GetByIdWithRoomsAsync(blockId)).ReturnsAsync(block);

        var sut = CreateSut();

        // Act
        var result = await sut.GetRoomsByBlockIdAsync(blockId);

        // Assert
        _blockRepoMock.Verify(r => r.GetByIdWithRoomsAsync(blockId), Times.Once);
        result.Should().NotBeNull();
        var roomList = result.ToList();
        roomList.Should().HaveCount(2);

        var firstRoom = roomList.First();
        firstRoom.BlockId.Should().Be(blockId);
        firstRoom.BlockName.Should().Be("Block 101");
        firstRoom.BlockGenderType.Should().Be("Male");
        firstRoom.RoomTypeName.Should().Be("Phòng 4");
        firstRoom.BedCapacity.Should().Be(4);
        firstRoom.TotalBeds.Should().Be(2);
        firstRoom.FreeBeds.Should().Be(1);
    }

    [Fact]
    public async Task GetRoomsByBlockIdAsync_WhenBlockNotFound_ThrowsKeyNotFoundException()
    {
        // Arrange
        const int nonExistentId = 999;
        _blockRepoMock.Setup(r => r.GetByIdWithRoomsAsync(nonExistentId)).ReturnsAsync((Block?)null);

        var sut = CreateSut();

        // Act
        Func<Task> act = async () => await sut.GetRoomsByBlockIdAsync(nonExistentId);

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage($"Block {nonExistentId} không tồn tại");
    }

    #endregion

    #region CreateAsync Tests

    [Fact]
    public async Task CreateAsync_WhenValidInput_CreatesBlockAndReturnsDto()
    {
        // Arrange
        var dto = new CreateBlockDto
        {
            DormId = 1,
            Code = "B105",
            Name = "Block 105",
            NumberOfFloors = 6,
            GenderType = "Male"
        };
        var (createdBlock, _) = CreateSampleBlockEntity(5, dto.DormId, dto.Code, dto.Name, dto.NumberOfFloors.Value, dto.GenderType);

        _blockRepoMock.Setup(r => r.IsCodeUniqueAsync(dto.DormId, dto.Code, null)).ReturnsAsync(true);
        _blockRepoMock.Setup(r => r.CreateAsync(It.IsAny<Block>())).ReturnsAsync(createdBlock);
        _blockRepoMock.Setup(r => r.GetByIdAsync(createdBlock.Id)).ReturnsAsync(createdBlock);

        var sut = CreateSut();

        // Act
        var result = await sut.CreateAsync(dto);

        // Assert
        _blockRepoMock.Verify(r => r.IsCodeUniqueAsync(dto.DormId, dto.Code, null), Times.Once);
        _blockRepoMock.Verify(r => r.CreateAsync(It.Is<Block>(b =>
            b.DormId == dto.DormId &&
            b.Code == dto.Code &&
            b.Name == dto.Name &&
            b.FloorLevel == dto.NumberOfFloors &&
            b.GenderType == dto.GenderType &&
            b.Status == "Active")), Times.Once);

        result.Should().NotBeNull();
        result.Id.Should().Be(createdBlock.Id);
        result.Code.Should().Be(dto.Code);
        result.Status.Should().Be("Active");
    }

    [Fact]
    public async Task CreateAsync_WhenCodeAlreadyExistsInDorm_ThrowsInvalidOperationException()
    {
        // Arrange
        var dto = new CreateBlockDto
        {
            DormId = 1,
            Code = "B101",
            Name = "Block 101",
            GenderType = "Male"
        };
        _blockRepoMock.Setup(r => r.IsCodeUniqueAsync(dto.DormId, dto.Code, null)).ReturnsAsync(false);

        var sut = CreateSut();

        // Act
        Func<Task> act = async () => await sut.CreateAsync(dto);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"Code '{dto.Code}' đã tồn tại trong Dorm này");
        _blockRepoMock.Verify(r => r.CreateAsync(It.IsAny<Block>()), Times.Never);
    }

    #endregion

    #region UpdateAsync Tests

    [Fact]
    public async Task UpdateAsync_WhenValidInput_UpdatesBlockAndReturnsDto()
    {
        // Arrange
        const int blockId = 1;
        var dto = new UpdateBlockDto { Name = "Block 101 Renamed", NumberOfFloors = 8 };
        var (existingBlock, _) = CreateSampleBlockEntity(blockId, 1, "B101", "Block 101", 5);

        _blockRepoMock.Setup(r => r.GetByIdAsync(blockId)).ReturnsAsync(existingBlock);
        _blockRepoMock.Setup(r => r.UpdateAsync(existingBlock)).ReturnsAsync(existingBlock);

        var sut = CreateSut();

        // Act
        var result = await sut.UpdateAsync(blockId, dto);

        // Assert
        _blockRepoMock.Verify(r => r.GetByIdAsync(blockId), Times.AtLeastOnce);
        _blockRepoMock.Verify(r => r.UpdateAsync(It.Is<Block>(b =>
            b.Id == blockId &&
            b.Name == dto.Name &&
            b.FloorLevel == dto.NumberOfFloors)), Times.Once);

        result.Should().NotBeNull();
        result.Name.Should().Be(dto.Name);
        result.NumberOfFloors.Should().Be(dto.NumberOfFloors);
    }

    [Fact]
    public async Task UpdateAsync_WhenBlockNotFound_ThrowsKeyNotFoundException()
    {
        // Arrange
        const int nonExistentId = 999;
        var dto = new UpdateBlockDto { Name = "Block New Name", NumberOfFloors = 3 };
        _blockRepoMock.Setup(r => r.GetByIdAsync(nonExistentId)).ReturnsAsync((Block?)null);

        var sut = CreateSut();

        // Act
        Func<Task> act = async () => await sut.UpdateAsync(nonExistentId, dto);

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage($"Block {nonExistentId} không tồn tại");
        _blockRepoMock.Verify(r => r.UpdateAsync(It.IsAny<Block>()), Times.Never);
    }

    #endregion

    #region UpdateStatusAsync Tests

    [Fact]
    public async Task UpdateStatusAsync_WhenValidInput_UpdatesStatusAndReturnsDto()
    {
        // Arrange
        const int blockId = 1;
        var dto = new UpdateBlockStatusDto { Status = "Maintenance" };
        var (existingBlock, _) = CreateSampleBlockEntity(blockId, 1, "B101", "Block 101", status: "Active");

        _blockRepoMock.Setup(r => r.GetByIdAsync(blockId)).ReturnsAsync(existingBlock);
        _blockRepoMock.Setup(r => r.UpdateAsync(existingBlock)).ReturnsAsync(existingBlock);

        var sut = CreateSut();

        // Act
        var result = await sut.UpdateStatusAsync(blockId, dto);

        // Assert
        _blockRepoMock.Verify(r => r.UpdateAsync(It.Is<Block>(b => b.Status == "Maintenance")), Times.Once);
        result.Should().NotBeNull();
        result.Status.Should().Be("Maintenance");
    }

    [Fact]
    public async Task UpdateStatusAsync_WhenBlockNotFound_ThrowsKeyNotFoundException()
    {
        // Arrange
        const int nonExistentId = 999;
        var dto = new UpdateBlockStatusDto { Status = "Maintenance" };
        _blockRepoMock.Setup(r => r.GetByIdAsync(nonExistentId)).ReturnsAsync((Block?)null);

        var sut = CreateSut();

        // Act
        Func<Task> act = async () => await sut.UpdateStatusAsync(nonExistentId, dto);

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage($"Block {nonExistentId} không tồn tại");
        _blockRepoMock.Verify(r => r.UpdateAsync(It.IsAny<Block>()), Times.Never);
    }

    #endregion

    #region UpdateGenderAsync Tests

    [Fact]
    public async Task UpdateGenderAsync_WhenNoActiveResidencies_UpdatesGenderSuccessfully()
    {
        // Arrange
        const int blockId = 1;
        var dto = new UpdateBlockGenderDto { GenderType = "Female" };
        var (existingBlock, _) = CreateSampleBlockEntity(blockId, 1, "B101", "Block 101", genderType: "Male");

        _blockRepoMock.Setup(r => r.GetByIdAsync(blockId)).ReturnsAsync(existingBlock);
        _blockRepoMock.Setup(r => r.HasActiveResidenciesAsync(blockId)).ReturnsAsync(false);
        _blockRepoMock.Setup(r => r.UpdateAsync(existingBlock)).ReturnsAsync(existingBlock);

        var sut = CreateSut();

        // Act
        var result = await sut.UpdateGenderAsync(blockId, dto);

        // Assert
        _blockRepoMock.Verify(r => r.HasActiveResidenciesAsync(blockId), Times.Once);
        _blockRepoMock.Verify(r => r.UpdateAsync(It.Is<Block>(b => b.GenderType == "Female")), Times.Once);
        result.Should().NotBeNull();
        result.GenderType.Should().Be("Female");
    }

    [Fact]
    public async Task UpdateGenderAsync_WhenHasActiveResidencies_ThrowsInvalidOperationException()
    {
        // Arrange
        const int blockId = 1;
        var dto = new UpdateBlockGenderDto { GenderType = "Female" };
        var (existingBlock, _) = CreateSampleBlockEntity(blockId, 1, "B101", "Block 101", genderType: "Male");

        _blockRepoMock.Setup(r => r.GetByIdAsync(blockId)).ReturnsAsync(existingBlock);
        _blockRepoMock.Setup(r => r.HasActiveResidenciesAsync(blockId)).ReturnsAsync(true);

        var sut = CreateSut();

        // Act
        Func<Task> act = async () => await sut.UpdateGenderAsync(blockId, dto);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Không thể đổi giới tính khi block còn sinh viên đang cư trú");
        _blockRepoMock.Verify(r => r.UpdateAsync(It.IsAny<Block>()), Times.Never);
    }

    [Fact]
    public async Task UpdateGenderAsync_WhenBlockNotFound_ThrowsKeyNotFoundException()
    {
        // Arrange
        const int nonExistentId = 999;
        var dto = new UpdateBlockGenderDto { GenderType = "Female" };
        _blockRepoMock.Setup(r => r.GetByIdAsync(nonExistentId)).ReturnsAsync((Block?)null);

        var sut = CreateSut();

        // Act
        Func<Task> act = async () => await sut.UpdateGenderAsync(nonExistentId, dto);

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage($"Block {nonExistentId} không tồn tại");
        _blockRepoMock.Verify(r => r.UpdateAsync(It.IsAny<Block>()), Times.Never);
    }

    #endregion
}
