using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using FUD.Common;
using FUD.Controllers.Facilities;
using FUD.DTOs.Facilities.Block;
using FUD.DTOs.Facilities.Room;
using FUD.Services.Interfaces.Facilities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace FUD.Tests.BlockManagement;

/// <summary>
/// Unit tests cho BlocksController (Quản lý Block/Tòa nhà) - Tầng Controller.
/// Tuân thủ nghiêm ngặt quy tắc AAA (Arrange - Act - Assert) và chuẩn kiểm thử ASP.NET Core API.
/// </summary>
public class BlocksControllerTests
{
    private readonly Mock<IBlockService> _blockServiceMock = new();

    private BlocksController CreateSut()
    {
        var controller = new BlocksController(_blockServiceMock.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
        return controller;
    }

    private static BlockDto CreateSampleBlockDto(
        int id = 1,
        int dormId = 1,
        string code = "B101",
        string name = "Block 101",
        string genderType = "Male",
        string status = "Active") => new()
    {
        Id = id,
        DormId = dormId,
        DormName = "Dorm A",
        Code = code,
        Name = name,
        NumberOfFloors = 5,
        GenderType = genderType,
        Status = status,
        TotalRooms = 10
    };

    #region GetAll Tests

    [Fact]
    public async Task GetAll_WhenCalled_Returns200OkWithPagedResult()
    {
        // Arrange
        var query = new BlockQueryDto { Page = 1, PageSize = 10 };
        var pagedData = new PagedResult<BlockDto>
        {
            Items = new List<BlockDto> { CreateSampleBlockDto(1), CreateSampleBlockDto(2, code: "B102") },
            TotalCount = 2,
            Page = 1,
            PageSize = 10
        };

        _blockServiceMock.Setup(s => s.GetAllAsync(query)).ReturnsAsync(pagedData);
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
    public async Task GetById_WhenBlockExists_Returns200OkWithBlockDto()
    {
        // Arrange
        const int blockId = 1;
        var sampleDto = CreateSampleBlockDto(blockId);
        _blockServiceMock.Setup(s => s.GetByIdAsync(blockId)).ReturnsAsync(sampleDto);
        var sut = CreateSut();

        // Act
        var result = await sut.GetById(blockId);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(StatusCodes.Status200OK);

        var response = okResult.Value.Should().BeOfType<ApiResponse<object>>().Subject;
        response.Success.Should().BeTrue();
        response.Data.Should().BeEquivalentTo(sampleDto);
    }

    [Fact]
    public async Task GetById_WhenBlockNotFound_Returns404NotFound()
    {
        // Arrange
        const int nonExistentId = 999;
        _blockServiceMock.Setup(s => s.GetByIdAsync(nonExistentId)).ReturnsAsync((BlockDto?)null);
        var sut = CreateSut();

        // Act
        var result = await sut.GetById(nonExistentId);

        // Assert
        var notFoundResult = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFoundResult.StatusCode.Should().Be(StatusCodes.Status404NotFound);

        var response = notFoundResult.Value.Should().BeOfType<ApiResponse>().Subject;
        response.Success.Should().BeFalse();
        response.Message.Should().Be($"Block {nonExistentId} không tồn tại");
    }

    #endregion

    #region GetRooms Tests

    [Fact]
    public async Task GetRooms_WhenBlockExists_Returns200OkWithRoomsList()
    {
        // Arrange
        const int blockId = 1;
        var sampleRooms = new List<RoomDto>
        {
            new() { Id = 10, BlockId = blockId, RoomNumber = "101L", FloorLevel = 1, Status = "Active" }
        };
        _blockServiceMock.Setup(s => s.GetRoomsByBlockIdAsync(blockId)).ReturnsAsync(sampleRooms);
        var sut = CreateSut();

        // Act
        var result = await sut.GetRooms(blockId);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(StatusCodes.Status200OK);

        var response = okResult.Value.Should().BeOfType<ApiResponse<object>>().Subject;
        response.Success.Should().BeTrue();
        response.Data.Should().BeEquivalentTo(sampleRooms);
    }

    [Fact]
    public async Task GetRooms_WhenBlockNotFound_Returns404NotFound()
    {
        // Arrange
        const int nonExistentId = 999;
        _blockServiceMock.Setup(s => s.GetRoomsByBlockIdAsync(nonExistentId))
            .ThrowsAsync(new KeyNotFoundException($"Block {nonExistentId} không tồn tại"));
        var sut = CreateSut();

        // Act
        var result = await sut.GetRooms(nonExistentId);

        // Assert
        var notFoundResult = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFoundResult.StatusCode.Should().Be(StatusCodes.Status404NotFound);

        var response = notFoundResult.Value.Should().BeOfType<ApiResponse>().Subject;
        response.Success.Should().BeFalse();
        response.Message.Should().Be($"Block {nonExistentId} không tồn tại");
    }

    #endregion

    #region Create Tests

    [Fact]
    public async Task Create_WhenModelStateInvalid_Returns400BadRequest()
    {
        // Arrange
        var sut = CreateSut();
        sut.ModelState.AddModelError("Code", "Code là bắt buộc");
        var dto = new CreateBlockDto();

        // Act
        var result = await sut.Create(dto);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);

        var response = badRequestResult.Value.Should().BeOfType<ApiResponse>().Subject;
        response.Success.Should().BeFalse();
        response.Errors.Should().Contain("Code là bắt buộc");
    }

    [Fact]
    public async Task Create_WhenDuplicateCode_Returns409Conflict()
    {
        // Arrange
        var dto = new CreateBlockDto { DormId = 1, Code = "B101", Name = "Block 101", GenderType = "Male" };
        _blockServiceMock.Setup(s => s.CreateAsync(dto))
            .ThrowsAsync(new InvalidOperationException("Code 'B101' đã tồn tại trong Dorm này"));
        var sut = CreateSut();

        // Act
        var result = await sut.Create(dto);

        // Assert
        var conflictResult = result.Should().BeOfType<ConflictObjectResult>().Subject;
        conflictResult.StatusCode.Should().Be(StatusCodes.Status409Conflict);

        var response = conflictResult.Value.Should().BeOfType<ApiResponse>().Subject;
        response.Success.Should().BeFalse();
        response.Message.Should().Be("Code 'B101' đã tồn tại trong Dorm này");
    }

    [Fact]
    public async Task Create_WhenValidInput_Returns201CreatedAtAction()
    {
        // Arrange
        var dto = new CreateBlockDto { DormId = 1, Code = "B101", Name = "Block 101", GenderType = "Male" };
        var createdDto = CreateSampleBlockDto(10, dto.DormId, dto.Code, dto.Name);
        _blockServiceMock.Setup(s => s.CreateAsync(dto)).ReturnsAsync(createdDto);
        var sut = CreateSut();

        // Act
        var result = await sut.Create(dto);

        // Assert
        var createdResult = result.Should().BeOfType<CreatedAtActionResult>().Subject;
        createdResult.StatusCode.Should().Be(StatusCodes.Status201Created);
        createdResult.ActionName.Should().Be(nameof(BlocksController.GetById));
        createdResult.RouteValues!["id"].Should().Be(10);

        var response = createdResult.Value.Should().BeOfType<ApiResponse<object>>().Subject;
        response.Success.Should().BeTrue();
        response.Message.Should().Be("Tạo block thành công");
    }

    #endregion

    #region Update Tests

    [Fact]
    public async Task Update_WhenModelStateInvalid_Returns400BadRequest()
    {
        // Arrange
        var sut = CreateSut();
        sut.ModelState.AddModelError("Name", "Name là bắt buộc");
        var dto = new UpdateBlockDto();

        // Act
        var result = await sut.Update(1, dto);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task Update_WhenBlockNotFound_Returns404NotFound()
    {
        // Arrange
        const int nonExistentId = 999;
        var dto = new UpdateBlockDto { Name = "New Name" };
        _blockServiceMock.Setup(s => s.UpdateAsync(nonExistentId, dto))
            .ThrowsAsync(new KeyNotFoundException($"Block {nonExistentId} không tồn tại"));
        var sut = CreateSut();

        // Act
        var result = await sut.Update(nonExistentId, dto);

        // Assert
        var notFoundResult = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFoundResult.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task Update_WhenValidInput_Returns200OkWithUpdatedDto()
    {
        // Arrange
        const int blockId = 1;
        var dto = new UpdateBlockDto { Name = "Block 101 Renamed", NumberOfFloors = 6 };
        var updatedDto = CreateSampleBlockDto(blockId, name: dto.Name);
        _blockServiceMock.Setup(s => s.UpdateAsync(blockId, dto)).ReturnsAsync(updatedDto);
        var sut = CreateSut();

        // Act
        var result = await sut.Update(blockId, dto);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(StatusCodes.Status200OK);

        var response = okResult.Value.Should().BeOfType<ApiResponse<object>>().Subject;
        response.Success.Should().BeTrue();
        response.Message.Should().Be("Cập nhật block thành công");
    }

    #endregion

    #region UpdateStatus Tests

    [Fact]
    public async Task UpdateStatus_WhenModelStateInvalid_Returns400BadRequest()
    {
        // Arrange
        var sut = CreateSut();
        sut.ModelState.AddModelError("Status", "Status là bắt buộc");
        var dto = new UpdateBlockStatusDto();

        // Act
        var result = await sut.UpdateStatus(1, dto);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task UpdateStatus_WhenBlockNotFound_Returns404NotFound()
    {
        // Arrange
        const int nonExistentId = 999;
        var dto = new UpdateBlockStatusDto { Status = "Maintenance" };
        _blockServiceMock.Setup(s => s.UpdateStatusAsync(nonExistentId, dto))
            .ThrowsAsync(new KeyNotFoundException($"Block {nonExistentId} không tồn tại"));
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
        const int blockId = 1;
        var dto = new UpdateBlockStatusDto { Status = "Maintenance" };
        var updatedDto = CreateSampleBlockDto(blockId, status: "Maintenance");
        _blockServiceMock.Setup(s => s.UpdateStatusAsync(blockId, dto)).ReturnsAsync(updatedDto);
        var sut = CreateSut();

        // Act
        var result = await sut.UpdateStatus(blockId, dto);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(StatusCodes.Status200OK);

        var response = okResult.Value.Should().BeOfType<ApiResponse<object>>().Subject;
        response.Success.Should().BeTrue();
        response.Message.Should().Be("Cập nhật trạng thái block thành công");
    }

    #endregion

    #region UpdateGender Tests

    [Fact]
    public async Task UpdateGender_WhenModelStateInvalid_Returns400BadRequest()
    {
        // Arrange
        var sut = CreateSut();
        sut.ModelState.AddModelError("GenderType", "GenderType là bắt buộc");
        var dto = new UpdateBlockGenderDto();

        // Act
        var result = await sut.UpdateGender(1, dto);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task UpdateGender_WhenBlockNotFound_Returns404NotFound()
    {
        // Arrange
        const int nonExistentId = 999;
        var dto = new UpdateBlockGenderDto { GenderType = "Female" };
        _blockServiceMock.Setup(s => s.UpdateGenderAsync(nonExistentId, dto))
            .ThrowsAsync(new KeyNotFoundException($"Block {nonExistentId} không tồn tại"));
        var sut = CreateSut();

        // Act
        var result = await sut.UpdateGender(nonExistentId, dto);

        // Assert
        var notFoundResult = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFoundResult.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task UpdateGender_WhenHasActiveResidencies_Returns409Conflict()
    {
        // Arrange
        const int blockId = 1;
        var dto = new UpdateBlockGenderDto { GenderType = "Female" };
        _blockServiceMock.Setup(s => s.UpdateGenderAsync(blockId, dto))
            .ThrowsAsync(new InvalidOperationException("Không thể đổi giới tính khi block còn sinh viên đang cư trú"));
        var sut = CreateSut();

        // Act
        var result = await sut.UpdateGender(blockId, dto);

        // Assert
        var conflictResult = result.Should().BeOfType<ConflictObjectResult>().Subject;
        conflictResult.StatusCode.Should().Be(StatusCodes.Status409Conflict);

        var response = conflictResult.Value.Should().BeOfType<ApiResponse>().Subject;
        response.Success.Should().BeFalse();
        response.Message.Should().Be("Không thể đổi giới tính khi block còn sinh viên đang cư trú");
    }

    [Fact]
    public async Task UpdateGender_WhenValidInput_Returns200OkWithUpdatedDto()
    {
        // Arrange
        const int blockId = 1;
        var dto = new UpdateBlockGenderDto { GenderType = "Female" };
        var updatedDto = CreateSampleBlockDto(blockId, genderType: "Female");
        _blockServiceMock.Setup(s => s.UpdateGenderAsync(blockId, dto)).ReturnsAsync(updatedDto);
        var sut = CreateSut();

        // Act
        var result = await sut.UpdateGender(blockId, dto);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(StatusCodes.Status200OK);

        var response = okResult.Value.Should().BeOfType<ApiResponse<object>>().Subject;
        response.Success.Should().BeTrue();
        response.Message.Should().Be("Đổi giới tính block thành công");
    }

    #endregion
}
