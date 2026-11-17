using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using FUD.Common;
using FUD.DTOs.Facilities.Bed;
using FUD.Models;
using FUD.Repositories.Interfaces.Facilities;
using FUD.Services.Facilities;
using Moq;
using Xunit;

namespace FUD.Tests.BedManagement;

/// <summary>
/// Unit tests cho BedService (Quản lý giường) - Tầng Service.
/// Tuân thủ nghiêm ngặt quy tắc AAA (Arrange - Act - Assert) và chuẩn kiểm thử khớp 1-1 với Report-5.1_Unit Test.xlsx.
/// </summary>
public class BedServiceTests
{
    private readonly Mock<IBedRepository> _bedRepoMock = new();

    private BedService CreateSut() => new(_bedRepoMock.Object);

    #region Helper Methods

    private static (Bed Bed, Room Room, Block Block, Dorm Dorm) CreateSampleBedEntity(
        int bedId = 1,
        int roomId = 10,
        string bedCode = "101L-B1",
        string position = "Tầng dưới gần cửa",
        string status = "Free")
    {
        var dorm = new Dorm { Id = 1, Name = "Dorm A", Code = "DA", Status = "Active" };
        var block = new Block { Id = 1, DormId = 1, Name = "Block 101", Code = "B101", GenderType = "Male", Status = "Active", Dorm = dorm };
        var room = new Room { Id = roomId, BlockId = 1, RoomNumber = "101L", FloorLevel = 1, Status = "Active", Block = block };
        var bed = new Bed
        {
            Id = bedId,
            RoomId = roomId,
            BedCode = bedCode,
            PositionInRoom = position,
            Status = status,
            Room = room
        };

        return (bed, room, block, dorm);
    }

    #endregion

    #region GetAllAsync Tests

    /// <summary>
    /// UTC001 - Normal: Lấy danh sách giường khi có dữ liệu -> Trả về PagedResult đã map DTO chính xác.
    /// </summary>
    [Fact]
    public async Task UTC001_GetAllAsync_WhenBedsExist_ReturnsPagedResultWithMappedDtos()
    {
        // Arrange
        var query = new BedQueryDto { Page = 1, PageSize = 10 };
        var (bed1, _, _, _) = CreateSampleBedEntity(1, 10, "101L-B1");
        var (bed2, _, _, _) = CreateSampleBedEntity(2, 10, "101L-B2");
        var sampleList = new List<Bed> { bed1, bed2 };

        _bedRepoMock.Setup(r => r.GetAllAsync(query))
            .ReturnsAsync((sampleList, sampleList.Count));

        var sut = CreateSut();

        // Act
        var result = await sut.GetAllAsync(query);

        // Assert
        _bedRepoMock.Verify(r => r.GetAllAsync(query), Times.Once);
        result.Should().NotBeNull();
        result.TotalCount.Should().Be(2);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(10);
        result.Items.Should().HaveCount(2);

        var first = result.Items.First();
        first.Id.Should().Be(bed1.Id);
        first.RoomId.Should().Be(bed1.RoomId);
        first.RoomNumber.Should().Be("101L");
        first.BlockName.Should().Be("Block 101");
        first.BedCode.Should().Be("101L-B1");
        first.Status.Should().Be("Free");
    }

    /// <summary>
    /// UTC002 - Boundary: Tìm kiếm giường với bộ lọc không khớp dữ liệu nào -> Trả về PagedResult rỗng (TotalCount = 0).
    /// </summary>
    [Fact]
    public async Task UTC002_GetAllAsync_WhenNoBedsMatchFilter_ReturnsEmptyPagedResult()
    {
        // Arrange
        var query = new BedQueryDto { BedCode = "NON_EXISTING", Page = 1, PageSize = 10 };
        _bedRepoMock.Setup(r => r.GetAllAsync(query))
            .ReturnsAsync((Enumerable.Empty<Bed>(), 0));

        var sut = CreateSut();

        // Act
        var result = await sut.GetAllAsync(query);

        // Assert
        result.Should().NotBeNull();
        result.TotalCount.Should().Be(0);
        result.Items.Should().BeEmpty();
    }

    #endregion

    #region GetByIdAsync Tests

    /// <summary>
    /// UTC003 - Normal: Tìm giường theo ID tồn tại -> Trả về BedDto tương ứng.
    /// </summary>
    [Fact]
    public async Task UTC003_GetByIdAsync_WhenBedExists_ReturnsMappedDto()
    {
        // Arrange
        const int bedId = 5;
        var (bed, _, _, _) = CreateSampleBedEntity(bedId, 10, "101L-B5", "Gần cửa sổ");
        _bedRepoMock.Setup(r => r.GetByIdAsync(bedId)).ReturnsAsync(bed);

        var sut = CreateSut();

        // Act
        var result = await sut.GetByIdAsync(bedId);

        // Assert
        _bedRepoMock.Verify(r => r.GetByIdAsync(bedId), Times.Once);
        result.Should().NotBeNull();
        result!.Id.Should().Be(bedId);
        result.BedCode.Should().Be("101L-B5");
        result.PositionInRoom.Should().Be("Gần cửa sổ");
        result.RoomNumber.Should().Be("101L");
        result.BlockName.Should().Be("Block 101");
    }

    /// <summary>
    /// UTC004 - Boundary: Tìm giường theo ID không tồn tại -> Trả về null.
    /// </summary>
    [Fact]
    public async Task UTC004_GetByIdAsync_WhenBedNotFound_ReturnsNull()
    {
        // Arrange
        const int nonExistentBedId = 999;
        _bedRepoMock.Setup(r => r.GetByIdAsync(nonExistentBedId)).ReturnsAsync((Bed?)null);

        var sut = CreateSut();

        // Act
        var result = await sut.GetByIdAsync(nonExistentBedId);

        // Assert
        result.Should().BeNull();
        _bedRepoMock.Verify(r => r.GetByIdAsync(nonExistentBedId), Times.Once);
    }

    #endregion

    #region CreateAsync Tests

    /// <summary>
    /// UTC005 - Normal: Tạo giường mới với thông tin hợp lệ -> Lưu thành công với trạng thái "Free" và trả về DTO.
    /// </summary>
    [Fact]
    public async Task UTC005_CreateAsync_WhenValidInput_CreatesBedWithFreeStatusAndReturnsDto()
    {
        // Arrange
        var dto = new CreateBedDto
        {
            RoomId = 10,
            BedCode = "101L-B3",
            PositionInRoom = "Tầng 2 bên phải"
        };
        var (createdBed, _, _, _) = CreateSampleBedEntity(3, 10, dto.BedCode, dto.PositionInRoom!, "Free");

        _bedRepoMock.Setup(r => r.RoomExistsAsync(dto.RoomId)).ReturnsAsync(true);
        _bedRepoMock.Setup(r => r.IsBedCodeUniqueAsync(dto.RoomId, dto.BedCode, null)).ReturnsAsync(true);
        _bedRepoMock.Setup(r => r.CreateAsync(It.IsAny<Bed>())).ReturnsAsync(createdBed);
        _bedRepoMock.Setup(r => r.GetByIdAsync(createdBed.Id)).ReturnsAsync(createdBed);

        var sut = CreateSut();

        // Act
        var result = await sut.CreateAsync(dto);

        // Assert
        _bedRepoMock.Verify(r => r.RoomExistsAsync(dto.RoomId), Times.Once);
        _bedRepoMock.Verify(r => r.IsBedCodeUniqueAsync(dto.RoomId, dto.BedCode, null), Times.Once);
        _bedRepoMock.Verify(r => r.CreateAsync(It.Is<Bed>(b =>
            b.RoomId == dto.RoomId &&
            b.BedCode == dto.BedCode &&
            b.PositionInRoom == dto.PositionInRoom &&
            b.Status == "Free")), Times.Once);

        result.Should().NotBeNull();
        result.Id.Should().Be(createdBed.Id);
        result.BedCode.Should().Be(dto.BedCode);
        result.Status.Should().Be("Free");
    }

    /// <summary>
    /// UTC006 - Abnormal: Phòng không tồn tại -> Bắn ra KeyNotFoundException.
    /// </summary>
    [Fact]
    public async Task UTC006_CreateAsync_WhenRoomDoesNotExist_ThrowsKeyNotFoundException()
    {
        // Arrange
        var dto = new CreateBedDto { RoomId = 999, BedCode = "999-B1" };
        _bedRepoMock.Setup(r => r.RoomExistsAsync(dto.RoomId)).ReturnsAsync(false);

        var sut = CreateSut();

        // Act
        Func<Task> act = async () => await sut.CreateAsync(dto);

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage($"Phòng {dto.RoomId} không tồn tại.");
        _bedRepoMock.Verify(r => r.CreateAsync(It.IsAny<Bed>()), Times.Never);
    }

    /// <summary>
    /// UTC007 - Abnormal: Mã giường đã tồn tại trong cùng phòng -> Bắn ra InvalidOperationException.
    /// </summary>
    [Fact]
    public async Task UTC007_CreateAsync_WhenBedCodeAlreadyExistsInRoom_ThrowsInvalidOperationException()
    {
        // Arrange
        var dto = new CreateBedDto { RoomId = 10, BedCode = "101L-B1" };
        _bedRepoMock.Setup(r => r.RoomExistsAsync(dto.RoomId)).ReturnsAsync(true);
        _bedRepoMock.Setup(r => r.IsBedCodeUniqueAsync(dto.RoomId, dto.BedCode, null)).ReturnsAsync(false);

        var sut = CreateSut();

        // Act
        Func<Task> act = async () => await sut.CreateAsync(dto);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"Mã giường '{dto.BedCode}' đã tồn tại trong phòng này.");
        _bedRepoMock.Verify(r => r.CreateAsync(It.IsAny<Bed>()), Times.Never);
    }

    #endregion

    #region UpdateAsync Tests

    /// <summary>
    /// UTC008 - Normal: Cập nhật thông tin giường hợp lệ -> Lưu thành công và trả về DTO đã cập nhật.
    /// </summary>
    [Fact]
    public async Task UTC008_UpdateAsync_WhenValidInput_UpdatesBedAndReturnsDto()
    {
        // Arrange
        const int bedId = 1;
        var dto = new UpdateBedDto
        {
            BedCode = "101L-B1-NEW",
            PositionInRoom = "Vị trí mới sau khi sửa"
        };
        var (existingBed, _, _, _) = CreateSampleBedEntity(bedId, 10, "101L-B1");

        _bedRepoMock.Setup(r => r.GetByIdAsync(bedId)).ReturnsAsync(existingBed);
        _bedRepoMock.Setup(r => r.IsBedCodeUniqueAsync(existingBed.RoomId, dto.BedCode, bedId)).ReturnsAsync(true);
        _bedRepoMock.Setup(r => r.UpdateAsync(existingBed)).ReturnsAsync(existingBed);

        var sut = CreateSut();

        // Act
        var result = await sut.UpdateAsync(bedId, dto);

        // Assert
        _bedRepoMock.Verify(r => r.GetByIdAsync(bedId), Times.AtLeastOnce);
        _bedRepoMock.Verify(r => r.IsBedCodeUniqueAsync(existingBed.RoomId, dto.BedCode, bedId), Times.Once);
        _bedRepoMock.Verify(r => r.UpdateAsync(It.Is<Bed>(b =>
            b.Id == bedId &&
            b.BedCode == dto.BedCode &&
            b.PositionInRoom == dto.PositionInRoom)), Times.Once);

        result.Should().NotBeNull();
        result.BedCode.Should().Be(dto.BedCode);
        result.PositionInRoom.Should().Be(dto.PositionInRoom);
    }

    /// <summary>
    /// UTC009 - Abnormal: Cập nhật giường không tồn tại -> Bắn ra KeyNotFoundException.
    /// </summary>
    [Fact]
    public async Task UTC009_UpdateAsync_WhenBedNotFound_ThrowsKeyNotFoundException()
    {
        // Arrange
        const int nonExistentBedId = 999;
        var dto = new UpdateBedDto { BedCode = "NEW-CODE" };
        _bedRepoMock.Setup(r => r.GetByIdAsync(nonExistentBedId)).ReturnsAsync((Bed?)null);

        var sut = CreateSut();

        // Act
        Func<Task> act = async () => await sut.UpdateAsync(nonExistentBedId, dto);

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage($"Giường {nonExistentBedId} không tồn tại.");
        _bedRepoMock.Verify(r => r.UpdateAsync(It.IsAny<Bed>()), Times.Never);
    }

    /// <summary>
    /// UTC010 - Abnormal: Đổi mã giường trùng với mã giường khác trong cùng phòng -> Bắn ra InvalidOperationException.
    /// </summary>
    [Fact]
    public async Task UTC010_UpdateAsync_WhenNewBedCodeDuplicateInRoom_ThrowsInvalidOperationException()
    {
        // Arrange
        const int bedId = 1;
        var dto = new UpdateBedDto { BedCode = "DUPLICATE-CODE" };
        var (existingBed, _, _, _) = CreateSampleBedEntity(bedId, 10, "101L-B1");

        _bedRepoMock.Setup(r => r.GetByIdAsync(bedId)).ReturnsAsync(existingBed);
        _bedRepoMock.Setup(r => r.IsBedCodeUniqueAsync(existingBed.RoomId, dto.BedCode, bedId)).ReturnsAsync(false);

        var sut = CreateSut();

        // Act
        Func<Task> act = async () => await sut.UpdateAsync(bedId, dto);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"Mã giường '{dto.BedCode}' đã tồn tại trong phòng này.");
        _bedRepoMock.Verify(r => r.UpdateAsync(It.IsAny<Bed>()), Times.Never);
    }

    #endregion

    #region UpdateStatusAsync Tests

    /// <summary>
    /// UTC011 - Normal: Chuyển trạng thái sang Maintenance khi giường không có sinh viên cư trú/giữ chỗ -> Thành công.
    /// </summary>
    [Fact]
    public async Task UTC011_UpdateStatusAsync_WhenChangingToMaintenanceAndNoActiveResidency_UpdatesSuccessfully()
    {
        // Arrange
        const int bedId = 1;
        var dto = new UpdateBedStatusDto { Status = "Maintenance" };
        var (existingBed, _, _, _) = CreateSampleBedEntity(bedId, 10, "101L-B1", status: "Free");

        _bedRepoMock.Setup(r => r.GetByIdAsync(bedId)).ReturnsAsync(existingBed);
        _bedRepoMock.Setup(r => r.HasActiveResidencyAsync(bedId)).ReturnsAsync(false);
        _bedRepoMock.Setup(r => r.UpdateAsync(existingBed)).ReturnsAsync(existingBed);

        var sut = CreateSut();

        // Act
        var result = await sut.UpdateStatusAsync(bedId, dto);

        // Assert
        _bedRepoMock.Verify(r => r.HasActiveResidencyAsync(bedId), Times.Once);
        _bedRepoMock.Verify(r => r.UpdateAsync(It.Is<Bed>(b => b.Status == "Maintenance")), Times.Once);
        result.Status.Should().Be("Maintenance");
    }

    /// <summary>
    /// UTC012 - Abnormal: Chuyển trạng thái sang Maintenance khi giường đang có người ở/giữ chỗ -> Bắn ra InvalidOperationException.
    /// </summary>
    [Fact]
    public async Task UTC012_UpdateStatusAsync_WhenChangingToMaintenanceAndBedHasActiveResidency_ThrowsInvalidOperationException()
    {
        // Arrange
        const int bedId = 1;
        var dto = new UpdateBedStatusDto { Status = "Maintenance" };
        var (existingBed, _, _, _) = CreateSampleBedEntity(bedId, 10, "101L-B1", status: "Occupied");

        _bedRepoMock.Setup(r => r.GetByIdAsync(bedId)).ReturnsAsync(existingBed);
        _bedRepoMock.Setup(r => r.HasActiveResidencyAsync(bedId)).ReturnsAsync(true);

        var sut = CreateSut();

        // Act
        Func<Task> act = async () => await sut.UpdateStatusAsync(bedId, dto);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Không thể chuyển giường sang trạng thái bảo trì vì đang có sinh viên cư trú hoặc giữ chỗ.");
        _bedRepoMock.Verify(r => r.UpdateAsync(It.IsAny<Bed>()), Times.Never);
    }

    /// <summary>
    /// UTC013 - Normal: Chuyển trạng thái sang "Free" từ bảo trì -> Cập nhật thành công mà không cần kiểm tra cư trú.
    /// </summary>
    [Fact]
    public async Task UTC013_UpdateStatusAsync_WhenChangingToFree_UpdatesStatusWithoutResidencyCheck()
    {
        // Arrange
        const int bedId = 1;
        var dto = new UpdateBedStatusDto { Status = "Free" };
        var (existingBed, _, _, _) = CreateSampleBedEntity(bedId, 10, "101L-B1", status: "Maintenance");

        _bedRepoMock.Setup(r => r.GetByIdAsync(bedId)).ReturnsAsync(existingBed);
        _bedRepoMock.Setup(r => r.UpdateAsync(existingBed)).ReturnsAsync(existingBed);

        var sut = CreateSut();

        // Act
        var result = await sut.UpdateStatusAsync(bedId, dto);

        // Assert
        _bedRepoMock.Verify(r => r.HasActiveResidencyAsync(It.IsAny<int>()), Times.Never);
        _bedRepoMock.Verify(r => r.UpdateAsync(It.Is<Bed>(b => b.Status == "Free")), Times.Once);
        result.Status.Should().Be("Free");
    }

    /// <summary>
    /// UTC014 - Abnormal: Cập nhật trạng thái giường không tồn tại -> Bắn ra KeyNotFoundException.
    /// </summary>
    [Fact]
    public async Task UTC014_UpdateStatusAsync_WhenBedNotFound_ThrowsKeyNotFoundException()
    {
        // Arrange
        const int nonExistentBedId = 999;
        var dto = new UpdateBedStatusDto { Status = "Free" };
        _bedRepoMock.Setup(r => r.GetByIdAsync(nonExistentBedId)).ReturnsAsync((Bed?)null);

        var sut = CreateSut();

        // Act
        Func<Task> act = async () => await sut.UpdateStatusAsync(nonExistentBedId, dto);

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage($"Giường {nonExistentBedId} không tồn tại.");
        _bedRepoMock.Verify(r => r.UpdateAsync(It.IsAny<Bed>()), Times.Never);
    }

    #endregion
}
