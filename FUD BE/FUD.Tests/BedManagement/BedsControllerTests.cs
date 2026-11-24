using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using FUD.Common;
using FUD.Controllers.Facilities;
using FUD.DTOs.Facilities.Bed;
using FUD.Services.Interfaces.Facilities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace FUD.Tests.BedManagement;

/// <summary>
/// Unit tests cho BedsController (Quản lý giường) - Tầng Controller.
/// Tuân thủ nghiêm ngặt quy tắc AAA (Arrange - Act - Assert) và chuẩn kiểm thử ASP.NET Core API.
/// </summary>
public class BedsControllerTests
{
    private readonly Mock<IBedService> _bedServiceMock = new();

    private BedsController CreateSut()
    {
        var controller = new BedsController(_bedServiceMock.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
        return controller;
    }

    private static BedDto CreateSampleBedDto(
        int id = 1,
        int roomId = 10,
        string bedCode = "101L-B1",
        string status = "Free") => new()
    {
        Id = id,
        RoomId = roomId,
        RoomNumber = "101L",
        BlockName = "Block 101",
        BedCode = bedCode,
        PositionInRoom = "Dưới trái",
        Status = status
    };

    #region GetAll Tests

    [Fact]
    public async Task GetAll_WhenCalled_Returns200OkWithPagedResult()
    {
        // Arrange
        var query = new BedQueryDto { Page = 1, PageSize = 10 };
        var pagedData = new PagedResult<BedDto>
        {
            Items = new List<BedDto> { CreateSampleBedDto(1), CreateSampleBedDto(2, bedCode: "101L-B2") },
            TotalCount = 2,
            Page = 1,
            PageSize = 10
        };

        _bedServiceMock.Setup(s => s.GetAllAsync(query)).ReturnsAsync(pagedData);
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
    public async Task GetById_WhenBedExists_Returns200OkWithBedDto()
    {
        // Arrange
        const int bedId = 1;
        var sampleDto = CreateSampleBedDto(bedId);
        _bedServiceMock.Setup(s => s.GetByIdAsync(bedId)).ReturnsAsync(sampleDto);
        var sut = CreateSut();

        // Act
        var result = await sut.GetById(bedId);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(StatusCodes.Status200OK);

        var response = okResult.Value.Should().BeOfType<ApiResponse<object>>().Subject;
        response.Success.Should().BeTrue();
        response.Data.Should().BeEquivalentTo(sampleDto);
    }

    [Fact]
    public async Task GetById_WhenBedNotFound_Returns404NotFound()
    {
        // Arrange
        const int notFoundId = 999;
        _bedServiceMock.Setup(s => s.GetByIdAsync(notFoundId)).ReturnsAsync((BedDto?)null);
        var sut = CreateSut();

        // Act
        var result = await sut.GetById(notFoundId);

        // Assert
        var notFoundResult = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFoundResult.StatusCode.Should().Be(StatusCodes.Status404NotFound);

        var response = notFoundResult.Value.Should().BeOfType<ApiResponse>().Subject;
        response.Success.Should().BeFalse();
        response.Message.Should().Contain($"Giường {notFoundId} không tồn tại");
    }

    #endregion

    #region Create Tests

    [Fact]
    public async Task Create_WhenValidDto_Returns201CreatedAtActionWithSuccessResponse()
    {
        // Arrange
        var dto = new CreateBedDto { RoomId = 10, BedCode = "101L-B1", PositionInRoom = "Dưới trái" };
        var createdDto = CreateSampleBedDto(1, dto.RoomId, dto.BedCode);

        _bedServiceMock.Setup(s => s.CreateAsync(dto)).ReturnsAsync(createdDto);
        var sut = CreateSut();

        // Act
        var result = await sut.Create(dto);

        // Assert
        var createdResult = result.Should().BeOfType<CreatedAtActionResult>().Subject;
        createdResult.StatusCode.Should().Be(StatusCodes.Status201Created);
        createdResult.ActionName.Should().Be(nameof(BedsController.GetById));
        createdResult.RouteValues!["id"].Should().Be(createdDto.Id);

        var response = createdResult.Value.Should().BeOfType<ApiResponse<object>>().Subject;
        response.Success.Should().BeTrue();
        response.Message.Should().Be("Tạo giường thành công");
    }

    [Fact]
    public async Task Create_WhenModelStateInvalid_Returns400BadRequest()
    {
        // Arrange
        var dto = new CreateBedDto { RoomId = 10, BedCode = "" };
        var sut = CreateSut();
        sut.ModelState.AddModelError("BedCode", "BedCode là bắt buộc");

        // Act
        var result = await sut.Create(dto);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);

        var response = badRequestResult.Value.Should().BeOfType<ApiResponse>().Subject;
        response.Success.Should().BeFalse();
        response.Message.Should().Be("Dữ liệu không hợp lệ");
        _bedServiceMock.Verify(s => s.CreateAsync(It.IsAny<CreateBedDto>()), Times.Never);
    }

    [Fact]
    public async Task Create_WhenRoomNotFound_Returns404NotFound()
    {
        // Arrange
        var dto = new CreateBedDto { RoomId = 999, BedCode = "999-B1" };
        _bedServiceMock.Setup(s => s.CreateAsync(dto))
            .ThrowsAsync(new KeyNotFoundException($"Phòng {dto.RoomId} không tồn tại."));
        var sut = CreateSut();

        // Act
        var result = await sut.Create(dto);

        // Assert
        var notFoundResult = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFoundResult.StatusCode.Should().Be(StatusCodes.Status404NotFound);

        var response = notFoundResult.Value.Should().BeOfType<ApiResponse>().Subject;
        response.Success.Should().BeFalse();
        response.Message.Should().Contain("không tồn tại");
    }

    [Fact]
    public async Task Create_WhenBedCodeDuplicate_Returns409Conflict()
    {
        // Arrange
        var dto = new CreateBedDto { RoomId = 10, BedCode = "101L-B1" };
        _bedServiceMock.Setup(s => s.CreateAsync(dto))
            .ThrowsAsync(new InvalidOperationException($"Mã giường '{dto.BedCode}' đã tồn tại trong phòng này."));
        var sut = CreateSut();

        // Act
        var result = await sut.Create(dto);

        // Assert
        var conflictResult = result.Should().BeOfType<ConflictObjectResult>().Subject;
        conflictResult.StatusCode.Should().Be(StatusCodes.Status409Conflict);

        var response = conflictResult.Value.Should().BeOfType<ApiResponse>().Subject;
        response.Success.Should().BeFalse();
        response.Message.Should().Contain("đã tồn tại trong phòng này");
    }

    [Fact]
    public async Task Create_WhenRoomCapacityFull_Returns409Conflict()
    {
        // Arrange
        var dto = new CreateBedDto { RoomId = 10, BedCode = "101L-B5" };
        _bedServiceMock.Setup(s => s.CreateAsync(dto))
            .ThrowsAsync(new InvalidOperationException("Phòng 101L (Phòng tiêu chuẩn 4 giường) đã đủ 4/4 giường, không thể thêm giường mới."));
        var sut = CreateSut();

        // Act
        var result = await sut.Create(dto);

        // Assert
        var conflictResult = result.Should().BeOfType<ConflictObjectResult>().Subject;
        conflictResult.StatusCode.Should().Be(StatusCodes.Status409Conflict);

        var response = conflictResult.Value.Should().BeOfType<ApiResponse>().Subject;
        response.Success.Should().BeFalse();
        response.Message.Should().Contain("đã đủ 4/4 giường");
    }

    #endregion

    #region Update Tests

    [Fact]
    public async Task Update_WhenValidDto_Returns200OkWithUpdatedBed()
    {
        // Arrange
        const int bedId = 1;
        var dto = new UpdateBedDto { BedCode = "101L-B1-NEW", PositionInRoom = "Vị trí mới" };
        var updatedDto = CreateSampleBedDto(bedId, bedCode: dto.BedCode);

        _bedServiceMock.Setup(s => s.UpdateAsync(bedId, dto)).ReturnsAsync(updatedDto);
        var sut = CreateSut();

        // Act
        var result = await sut.Update(bedId, dto);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(StatusCodes.Status200OK);

        var response = okResult.Value.Should().BeOfType<ApiResponse<object>>().Subject;
        response.Success.Should().BeTrue();
        response.Message.Should().Be("Cập nhật giường thành công");
    }

    [Fact]
    public async Task Update_WhenModelStateInvalid_Returns400BadRequest()
    {
        // Arrange
        const int bedId = 1;
        var dto = new UpdateBedDto { BedCode = "" };
        var sut = CreateSut();
        sut.ModelState.AddModelError("BedCode", "BedCode là bắt buộc");

        // Act
        var result = await sut.Update(bedId, dto);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        _bedServiceMock.Verify(s => s.UpdateAsync(It.IsAny<int>(), It.IsAny<UpdateBedDto>()), Times.Never);
    }

    [Fact]
    public async Task Update_WhenBedNotFound_Returns404NotFound()
    {
        // Arrange
        const int notFoundId = 999;
        var dto = new UpdateBedDto { BedCode = "NEW-CODE" };
        _bedServiceMock.Setup(s => s.UpdateAsync(notFoundId, dto))
            .ThrowsAsync(new KeyNotFoundException($"Giường {notFoundId} không tồn tại."));
        var sut = CreateSut();

        // Act
        var result = await sut.Update(notFoundId, dto);

        // Assert
        var notFoundResult = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFoundResult.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task Update_WhenBedCodeDuplicate_Returns409Conflict()
    {
        // Arrange
        const int bedId = 1;
        var dto = new UpdateBedDto { BedCode = "EXISTING-CODE" };
        _bedServiceMock.Setup(s => s.UpdateAsync(bedId, dto))
            .ThrowsAsync(new InvalidOperationException($"Mã giường '{dto.BedCode}' đã tồn tại trong phòng này."));
        var sut = CreateSut();

        // Act
        var result = await sut.Update(bedId, dto);

        // Assert
        var conflictResult = result.Should().BeOfType<ConflictObjectResult>().Subject;
        conflictResult.StatusCode.Should().Be(StatusCodes.Status409Conflict);
    }

    #endregion

    #region UpdateStatus Tests

    [Fact]
    public async Task UpdateStatus_WhenValidStatus_Returns200OkWithUpdatedBed()
    {
        // Arrange
        const int bedId = 1;
        var dto = new UpdateBedStatusDto { Status = "Maintenance" };
        var updatedDto = CreateSampleBedDto(bedId, status: "Maintenance");

        _bedServiceMock.Setup(s => s.UpdateStatusAsync(bedId, dto)).ReturnsAsync(updatedDto);
        var sut = CreateSut();

        // Act
        var result = await sut.UpdateStatus(bedId, dto);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(StatusCodes.Status200OK);

        var response = okResult.Value.Should().BeOfType<ApiResponse<object>>().Subject;
        response.Success.Should().BeTrue();
        response.Message.Should().Be("Cập nhật trạng thái giường thành công");
    }

    [Fact]
    public async Task UpdateStatus_WhenModelStateInvalid_Returns400BadRequest()
    {
        // Arrange
        const int bedId = 1;
        var dto = new UpdateBedStatusDto { Status = "InvalidStatus" };
        var sut = CreateSut();
        sut.ModelState.AddModelError("Status", "Status chỉ được là 'Free' hoặc 'Maintenance'");

        // Act
        var result = await sut.UpdateStatus(bedId, dto);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        _bedServiceMock.Verify(s => s.UpdateStatusAsync(It.IsAny<int>(), It.IsAny<UpdateBedStatusDto>()), Times.Never);
    }

    [Fact]
    public async Task UpdateStatus_WhenBedNotFound_Returns404NotFound()
    {
        // Arrange
        const int notFoundId = 999;
        var dto = new UpdateBedStatusDto { Status = "Maintenance" };
        _bedServiceMock.Setup(s => s.UpdateStatusAsync(notFoundId, dto))
            .ThrowsAsync(new KeyNotFoundException($"Giường {notFoundId} không tồn tại."));
        var sut = CreateSut();

        // Act
        var result = await sut.UpdateStatus(notFoundId, dto);

        // Assert
        var notFoundResult = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFoundResult.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task UpdateStatus_WhenBedHasActiveResidency_Returns409Conflict()
    {
        // Arrange
        const int bedId = 1;
        var dto = new UpdateBedStatusDto { Status = "Maintenance" };
        _bedServiceMock.Setup(s => s.UpdateStatusAsync(bedId, dto))
            .ThrowsAsync(new InvalidOperationException("Không thể chuyển giường sang trạng thái bảo trì vì đang có sinh viên cư trú hoặc giữ chỗ."));
        var sut = CreateSut();

        // Act
        var result = await sut.UpdateStatus(bedId, dto);

        // Assert
        var conflictResult = result.Should().BeOfType<ConflictObjectResult>().Subject;
        conflictResult.StatusCode.Should().Be(StatusCodes.Status409Conflict);
        var response = conflictResult.Value.Should().BeOfType<ApiResponse>().Subject;
        response.Success.Should().BeFalse();
        response.Message.Should().Contain("Không thể chuyển giường sang trạng thái bảo trì");
    }

    #endregion
}

