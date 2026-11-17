using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using FUD.Common;
using FUD.Controllers.Facilities;
using FUD.DTOs.Facilities.Room;
using FUD.Services.Interfaces.Facilities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace FUD.Tests.RoomManagement;

/// <summary>
/// Unit tests cho RoomsController (Quản lý Phòng) - Tầng Controller.
/// Tuân thủ nghiêm ngặt quy tắc AAA (Arrange - Act - Assert) và chuẩn kiểm thử ASP.NET Core API.
/// </summary>
public class RoomsControllerTests
{
    private readonly Mock<IRoomService> _roomServiceMock = new();

    private RoomsController CreateSut()
    {
        var controller = new RoomsController(_roomServiceMock.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
        return controller;
    }

    private static RoomDto CreateSampleRoomDto(
        int id = 10,
        int blockId = 1,
        string roomNumber = "101L",
        string status = "Active") => new()
    {
        Id = id,
        BlockId = blockId,
        BlockName = "Block 101",
        BlockGenderType = "Male",
        RoomTypeId = 1,
        RoomTypeName = "Phòng 4 giường",
        BedCapacity = 4,
        RoomNumber = roomNumber,
        FloorLevel = 1,
        Status = status,
        TotalBeds = 4,
        FreeBeds = 2
    };

    #region GetAll Tests

    [Fact]
    public async Task GetAll_WhenCalled_Returns200OkWithPagedResult()
    {
        // Arrange
        var query = new RoomQueryDto { Page = 1, PageSize = 10 };
        var pagedData = new PagedResult<RoomDto>
        {
            Items = new List<RoomDto> { CreateSampleRoomDto(10), CreateSampleRoomDto(20, roomNumber: "102L") },
            TotalCount = 2,
            Page = 1,
            PageSize = 10
        };

        _roomServiceMock.Setup(s => s.GetAllAsync(query)).ReturnsAsync(pagedData);
        var sut = CreateSut();

        // Act
        var result = await sut.GetAll(query);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(StatusCodes.Status200OK);

        var response = okResult.Value.Should().BeOfType<ApiResponse<object>>().Subject;
        response.Success.Should().BeTrue();
        response.Data.Should().BeEquivalentTo(pagedData);
    }

    #endregion

    #region GetById Tests

    [Fact]
    public async Task GetById_WhenRoomExists_Returns200OkWithRoomDto()
    {
        // Arrange
        const int roomId = 10;
        var sampleDto = CreateSampleRoomDto(roomId);
        _roomServiceMock.Setup(s => s.GetByIdAsync(roomId)).ReturnsAsync(sampleDto);
        var sut = CreateSut();

        // Act
        var result = await sut.GetById(roomId);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(StatusCodes.Status200OK);

        var response = okResult.Value.Should().BeOfType<ApiResponse<object>>().Subject;
        response.Success.Should().BeTrue();
        response.Data.Should().BeEquivalentTo(sampleDto);
    }

    [Fact]
    public async Task GetById_WhenRoomNotFound_Returns404NotFound()
    {
        // Arrange
        const int nonExistentId = 999;
        _roomServiceMock.Setup(s => s.GetByIdAsync(nonExistentId)).ReturnsAsync((RoomDto?)null);
        var sut = CreateSut();

        // Act
        var result = await sut.GetById(nonExistentId);

        // Assert
        var notFoundResult = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFoundResult.StatusCode.Should().Be(StatusCodes.Status404NotFound);

        var response = notFoundResult.Value.Should().BeOfType<ApiResponse>().Subject;
        response.Success.Should().BeFalse();
        response.Message.Should().Be($"Phòng {nonExistentId} không tồn tại");
    }

    #endregion

    #region Create Tests

    [Fact]
    public async Task Create_WhenModelStateInvalid_Returns400BadRequest()
    {
        // Arrange
        var sut = CreateSut();
        sut.ModelState.AddModelError("RoomNumber", "RoomNumber là bắt buộc");
        var dto = new CreateRoomDto();

        // Act
        var result = await sut.Create(dto);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);

        var response = badRequestResult.Value.Should().BeOfType<ApiResponse>().Subject;
        response.Success.Should().BeFalse();
        response.Errors.Should().Contain("RoomNumber là bắt buộc");
    }

    [Fact]
    public async Task Create_WhenRoomNumberAlreadyExists_Returns409Conflict()
    {
        // Arrange
        var dto = new CreateRoomDto { BlockId = 1, RoomTypeId = 1, RoomNumber = "101L", FloorLevel = 1 };
        _roomServiceMock.Setup(s => s.CreateAsync(dto))
            .ThrowsAsync(new InvalidOperationException("Phòng '101L' đã tồn tại trong block này"));
        var sut = CreateSut();

        // Act
        var result = await sut.Create(dto);

        // Assert
        var conflictResult = result.Should().BeOfType<ConflictObjectResult>().Subject;
        conflictResult.StatusCode.Should().Be(StatusCodes.Status409Conflict);

        var response = conflictResult.Value.Should().BeOfType<ApiResponse>().Subject;
        response.Success.Should().BeFalse();
        response.Message.Should().Be("Phòng '101L' đã tồn tại trong block này");
    }

    [Fact]
    public async Task Create_WhenValidInput_Returns201CreatedAtAction()
    {
        // Arrange
        var dto = new CreateRoomDto { BlockId = 1, RoomTypeId = 1, RoomNumber = "101L", FloorLevel = 1 };
        var createdDto = CreateSampleRoomDto(15, dto.BlockId, dto.RoomNumber);
        _roomServiceMock.Setup(s => s.CreateAsync(dto)).ReturnsAsync(createdDto);
        var sut = CreateSut();

        // Act
        var result = await sut.Create(dto);

        // Assert
        var createdResult = result.Should().BeOfType<CreatedAtActionResult>().Subject;
        createdResult.StatusCode.Should().Be(StatusCodes.Status201Created);
        createdResult.ActionName.Should().Be(nameof(RoomsController.GetById));
        createdResult.RouteValues!["id"].Should().Be(15);

        var response = createdResult.Value.Should().BeOfType<ApiResponse<object>>().Subject;
        response.Success.Should().BeTrue();
        response.Message.Should().Be("Tạo phòng thành công");
    }

    #endregion

    #region Update Tests

    [Fact]
    public async Task Update_WhenModelStateInvalid_Returns400BadRequest()
    {
        // Arrange
        var sut = CreateSut();
        sut.ModelState.AddModelError("RoomNumber", "RoomNumber là bắt buộc");
        var dto = new UpdateRoomDto();

        // Act
        var result = await sut.Update(10, dto);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task Update_WhenRoomNotFound_Returns404NotFound()
    {
        // Arrange
        const int nonExistentId = 999;
        var dto = new UpdateRoomDto { RoomTypeId = 1, RoomNumber = "101L", FloorLevel = 1 };
        _roomServiceMock.Setup(s => s.UpdateAsync(nonExistentId, dto))
            .ThrowsAsync(new KeyNotFoundException($"Phòng {nonExistentId} không tồn tại"));
        var sut = CreateSut();

        // Act
        var result = await sut.Update(nonExistentId, dto);

        // Assert
        var notFoundResult = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFoundResult.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task Update_WhenNewRoomNumberAlreadyExists_Returns409Conflict()
    {
        // Arrange
        const int roomId = 10;
        var dto = new UpdateRoomDto { RoomTypeId = 1, RoomNumber = "102L", FloorLevel = 1 };
        _roomServiceMock.Setup(s => s.UpdateAsync(roomId, dto))
            .ThrowsAsync(new InvalidOperationException("Phòng '102L' đã tồn tại trong block này"));
        var sut = CreateSut();

        // Act
        var result = await sut.Update(roomId, dto);

        // Assert
        var conflictResult = result.Should().BeOfType<ConflictObjectResult>().Subject;
        conflictResult.StatusCode.Should().Be(StatusCodes.Status409Conflict);

        var response = conflictResult.Value.Should().BeOfType<ApiResponse>().Subject;
        response.Success.Should().BeFalse();
        response.Message.Should().Be("Phòng '102L' đã tồn tại trong block này");
    }

    [Fact]
    public async Task Update_WhenValidInput_Returns200OkWithUpdatedDto()
    {
        // Arrange
        const int roomId = 10;
        var dto = new UpdateRoomDto { RoomTypeId = 1, RoomNumber = "101L-Renamed", FloorLevel = 2 };
        var updatedDto = CreateSampleRoomDto(roomId, roomNumber: dto.RoomNumber);
        _roomServiceMock.Setup(s => s.UpdateAsync(roomId, dto)).ReturnsAsync(updatedDto);
        var sut = CreateSut();

        // Act
        var result = await sut.Update(roomId, dto);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(StatusCodes.Status200OK);

        var response = okResult.Value.Should().BeOfType<ApiResponse<object>>().Subject;
        response.Success.Should().BeTrue();
        response.Message.Should().Be("Cập nhật phòng thành công");
    }

    #endregion

    #region UpdateStatus Tests

    [Fact]
    public async Task UpdateStatus_WhenModelStateInvalid_Returns400BadRequest()
    {
        // Arrange
        var sut = CreateSut();
        sut.ModelState.AddModelError("Status", "Status là bắt buộc");
        var dto = new UpdateRoomStatusDto();

        // Act
        var result = await sut.UpdateStatus(10, dto);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task UpdateStatus_WhenRoomNotFound_Returns404NotFound()
    {
        // Arrange
        const int nonExistentId = 999;
        var dto = new UpdateRoomStatusDto { Status = "Maintenance" };
        _roomServiceMock.Setup(s => s.UpdateStatusAsync(nonExistentId, dto))
            .ThrowsAsync(new KeyNotFoundException($"Phòng {nonExistentId} không tồn tại"));
        var sut = CreateSut();

        // Act
        var result = await sut.UpdateStatus(nonExistentId, dto);

        // Assert
        var notFoundResult = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFoundResult.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task UpdateStatus_WhenValidInput_Returns200OkWithUpdatedDto()
    {
        // Arrange
        const int roomId = 10;
        var dto = new UpdateRoomStatusDto { Status = "Maintenance" };
        var updatedDto = CreateSampleRoomDto(roomId, status: "Maintenance");
        _roomServiceMock.Setup(s => s.UpdateStatusAsync(roomId, dto)).ReturnsAsync(updatedDto);
        var sut = CreateSut();

        // Act
        var result = await sut.UpdateStatus(roomId, dto);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(StatusCodes.Status200OK);

        var response = okResult.Value.Should().BeOfType<ApiResponse<object>>().Subject;
        response.Success.Should().BeTrue();
        response.Message.Should().Be("Cập nhật trạng thái phòng thành công");
    }

    #endregion

    #region GetRoomTypes Tests

    [Fact]
    public async Task GetRoomTypes_WhenCalled_Returns200OkWithRoomTypes()
    {
        // Arrange
        var types = new List<RoomTypeLookupDto>
        {
            new() { Id = 1, Name = "Phòng 2 giường", BedCapacity = 2 },
            new() { Id = 2, Name = "Phòng 4 giường", BedCapacity = 4 }
        };
        _roomServiceMock.Setup(s => s.GetRoomTypesAsync()).ReturnsAsync(types);
        var sut = CreateSut();

        // Act
        var result = await sut.GetRoomTypes();

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(StatusCodes.Status200OK);

        var response = okResult.Value.Should().BeOfType<ApiResponse<object>>().Subject;
        response.Success.Should().BeTrue();
        response.Data.Should().BeEquivalentTo(types);
    }

    #endregion
}
