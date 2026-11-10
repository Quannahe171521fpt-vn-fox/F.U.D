using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using FUD.Common;
using FUD.Controllers;
using FUD.DTOs.Pricing;
using FUD.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace FUD.Tests.ConfigTerm;

public class TermManagementControllerTests
{
    private readonly Mock<ITermService> _termServiceMock;
    private readonly TermManagementController _controller;

    public TermManagementControllerTests()
    {
        _termServiceMock = new Mock<ITermService>();
        _controller = new TermManagementController(_termServiceMock.Object);
    }

    [Fact]
    public async Task GetAllTerms_ReturnsOkResultWithTermsList()
    {
        // Arrange
        var mockTerms = new List<TermSummaryDto>
        {
            new TermSummaryDto { Id = 1, Name = "Spring 2026", AcademicYear = "2025-2026" },
            new TermSummaryDto { Id = 2, Name = "Summer 2026", AcademicYear = "2025-2026" }
        };
        _termServiceMock.Setup(s => s.GetAllTermsAsync()).ReturnsAsync(mockTerms);

        // Act
        var result = await _controller.GetAllTerms();

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(StatusCodes.Status200OK);

        var apiResponse = okResult.Value.Should().BeOfType<ApiResponse<List<TermSummaryDto>>>().Subject;
        apiResponse.Success.Should().BeTrue();
        apiResponse.Data.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetTermDetail_ExistingId_ReturnsOkWithDetail()
    {
        // Arrange
        var detailDto = new TermDetailDto { Id = 1, Name = "Spring 2026", AcademicYear = "2025-2026" };
        _termServiceMock.Setup(s => s.GetTermDetailAsync(1)).ReturnsAsync(detailDto);

        // Act
        var result = await _controller.GetTermDetail(1);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(StatusCodes.Status200OK);

        var apiResponse = okResult.Value.Should().BeOfType<ApiResponse<TermDetailDto>>().Subject;
        apiResponse.Success.Should().BeTrue();
        apiResponse.Data!.Name.Should().Be("Spring 2026");
    }

    [Fact]
    public async Task GetTermDetail_NonExistingId_ReturnsNotFound()
    {
        // Arrange
        _termServiceMock.Setup(s => s.GetTermDetailAsync(999))
            .ThrowsAsync(new KeyNotFoundException("Không tìm thấy học kỳ với mã 999"));

        // Act
        var result = await _controller.GetTermDetail(999);

        // Assert
        var notFoundResult = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFoundResult.StatusCode.Should().Be(StatusCodes.Status404NotFound);

        var apiResponse = notFoundResult.Value.Should().BeOfType<ApiResponse>().Subject;
        apiResponse.Success.Should().BeFalse();
        apiResponse.Message.Should().Contain("Không tìm thấy học kỳ với mã 999");
    }

    [Fact]
    public async Task CreateTerm_InvalidModelState_ReturnsBadRequest()
    {
        // Arrange
        _controller.ModelState.AddModelError("Name", "Tên học kỳ không được để trống");
        var request = new CreateTermRequestDto();

        // Act
        var result = await _controller.CreateTerm(request);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);

        var apiResponse = badRequestResult.Value.Should().BeOfType<ApiResponse>().Subject;
        apiResponse.Success.Should().BeFalse();
        apiResponse.Errors.Should().Contain("Tên học kỳ không được để trống");
    }

    [Fact]
    public async Task CreateTerm_ServiceThrowsArgumentException_ReturnsBadRequest()
    {
        // Arrange
        var request = new CreateTermRequestDto
        {
            Name = "Spring 2026",
            AcademicYear = "2025-2026",
            StartDate = new DateOnly(2026, 4, 30),
            EndDate = new DateOnly(2026, 1, 1)
        };

        _termServiceMock.Setup(s => s.CreateTermAsync(request))
            .ThrowsAsync(new ArgumentException("Ngày kết thúc học kỳ phải lớn hơn ngày bắt đầu."));

        // Act
        var result = await _controller.CreateTerm(request);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);

        var apiResponse = badRequestResult.Value.Should().BeOfType<ApiResponse>().Subject;
        apiResponse.Success.Should().BeFalse();
        apiResponse.Message.Should().Be("Ngày kết thúc học kỳ phải lớn hơn ngày bắt đầu.");
    }

    [Fact]
    public async Task CreateTerm_ValidRequest_ReturnsCreated201()
    {
        // Arrange
        var request = new CreateTermRequestDto
        {
            Name = "Fall 2026",
            AcademicYear = "2026-2027",
            StartDate = new DateOnly(2026, 9, 1),
            EndDate = new DateOnly(2026, 12, 31)
        };
        var created = new TermDetailDto { Id = 10, Name = "Fall 2026", AcademicYear = "2026-2027" };

        _termServiceMock.Setup(s => s.CreateTermAsync(request)).ReturnsAsync(created);

        // Act
        var result = await _controller.CreateTerm(request);

        // Assert
        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status201Created);

        var apiResponse = objectResult.Value.Should().BeOfType<ApiResponse<TermDetailDto>>().Subject;
        apiResponse.Success.Should().BeTrue();
        apiResponse.Message.Should().Be("Tạo học kỳ mới thành công");
        apiResponse.Data!.Id.Should().Be(10);
    }

    [Fact]
    public async Task UpdateTerm_InvalidModelState_ReturnsBadRequest()
    {
        // Arrange
        _controller.ModelState.AddModelError("AcademicYear", "Năm học không được để trống");
        var request = new UpdateTermRequestDto();

        // Act
        var result = await _controller.UpdateTerm(1, request);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);

        var apiResponse = badRequestResult.Value.Should().BeOfType<ApiResponse>().Subject;
        apiResponse.Success.Should().BeFalse();
        apiResponse.Errors.Should().Contain("Năm học không được để trống");
    }

    [Fact]
    public async Task UpdateTerm_NonExistingId_ReturnsNotFound()
    {
        // Arrange
        var request = new UpdateTermRequestDto { Name = "T", AcademicYear = "2026", StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 4, 30) };
        _termServiceMock.Setup(s => s.UpdateTermAsync(999, request))
            .ThrowsAsync(new KeyNotFoundException("Không tìm thấy học kỳ với mã 999"));

        // Act
        var result = await _controller.UpdateTerm(999, request);

        // Assert
        var notFoundResult = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFoundResult.StatusCode.Should().Be(StatusCodes.Status404NotFound);

        var apiResponse = notFoundResult.Value.Should().BeOfType<ApiResponse>().Subject;
        apiResponse.Success.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateTerm_DuplicateName_ReturnsBadRequest()
    {
        // Arrange
        var request = new UpdateTermRequestDto { Name = "Spring 2026", AcademicYear = "2025-2026", StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 4, 30) };
        _termServiceMock.Setup(s => s.UpdateTermAsync(1, request))
            .ThrowsAsync(new ArgumentException("Học kỳ 'Spring 2026' cho năm học '2025-2026' đã tồn tại."));

        // Act
        var result = await _controller.UpdateTerm(1, request);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);

        var apiResponse = badRequestResult.Value.Should().BeOfType<ApiResponse>().Subject;
        apiResponse.Success.Should().BeFalse();
        apiResponse.Message.Should().Contain("đã tồn tại");
    }

    [Fact]
    public async Task UpdateTerm_ValidRequest_ReturnsOkWithUpdatedTerm()
    {
        // Arrange
        var request = new UpdateTermRequestDto { Name = "Spring 2026 Updated", AcademicYear = "2025-2026", StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 5, 1) };
        var updated = new TermDetailDto { Id = 1, Name = "Spring 2026 Updated", AcademicYear = "2025-2026" };

        _termServiceMock.Setup(s => s.UpdateTermAsync(1, request)).ReturnsAsync(updated);

        // Act
        var result = await _controller.UpdateTerm(1, request);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(StatusCodes.Status200OK);

        var apiResponse = okResult.Value.Should().BeOfType<ApiResponse<TermDetailDto>>().Subject;
        apiResponse.Success.Should().BeTrue();
        apiResponse.Message.Should().Be("Cập nhật thông tin học kỳ thành công");
        apiResponse.Data!.Name.Should().Be("Spring 2026 Updated");
    }

    [Fact]
    public async Task DeleteTerm_NotFound_ReturnsNotFound()
    {
        // Arrange
        _termServiceMock.Setup(s => s.DeleteTermAsync(999))
            .ThrowsAsync(new KeyNotFoundException("Không tìm thấy học kỳ với mã 999"));

        // Act
        var result = await _controller.DeleteTerm(999);

        // Assert
        var notFoundResult = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFoundResult.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task DeleteTerm_HasDependentData_ReturnsBadRequest()
    {
        // Arrange
        _termServiceMock.Setup(s => s.DeleteTermAsync(1))
            .ThrowsAsync(new InvalidOperationException("Không thể xóa học kỳ vì đã có dữ liệu"));

        // Act
        var result = await _controller.DeleteTerm(1);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);

        var apiResponse = badRequestResult.Value.Should().BeOfType<ApiResponse>().Subject;
        apiResponse.Success.Should().BeFalse();
        apiResponse.Message.Should().Contain("Không thể xóa học kỳ vì đã có dữ liệu");
    }

    [Fact]
    public async Task DeleteTerm_Success_ReturnsOk()
    {
        // Arrange
        _termServiceMock.Setup(s => s.DeleteTermAsync(1)).Returns(Task.CompletedTask);

        // Act
        var result = await _controller.DeleteTerm(1);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(StatusCodes.Status200OK);

        var apiResponse = okResult.Value.Should().BeOfType<ApiResponse>().Subject;
        apiResponse.Success.Should().BeTrue();
        apiResponse.Message.Should().Be("Xóa học kỳ thành công");
    }

    [Fact]
    public async Task SaveRegistrationSchedule_InvalidModelState_ReturnsBadRequest()
    {
        // Arrange
        _controller.ModelState.AddModelError("Periods", "Cần cung cấp ít nhất một giai đoạn đăng ký");
        var request = new SaveRegistrationScheduleRequestDto();

        // Act
        var result = await _controller.SaveRegistrationSchedule(1, request);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task SaveRegistrationSchedule_NotFound_ReturnsNotFound()
    {
        // Arrange
        var request = new SaveRegistrationScheduleRequestDto { TermId = 999, Periods = new List<PeriodConfigItemDto>() };
        _termServiceMock.Setup(s => s.SaveRegistrationScheduleAsync(999, request))
            .ThrowsAsync(new KeyNotFoundException("Không tìm thấy học kỳ với mã 999"));

        // Act
        var result = await _controller.SaveRegistrationSchedule(999, request);

        // Assert
        var notFoundResult = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFoundResult.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task SaveRegistrationSchedule_BusinessRuleViolation_ReturnsBadRequest()
    {
        // Arrange
        var request = new SaveRegistrationScheduleRequestDto { TermId = 1, Periods = new List<PeriodConfigItemDto>() };
        _termServiceMock.Setup(s => s.SaveRegistrationScheduleAsync(1, request))
            .ThrowsAsync(new ArgumentException("Giai đoạn Giữ phòng cũ phải kết thúc trước hoặc cùng ngày khi Booking bắt đầu."));

        // Act
        var result = await _controller.SaveRegistrationSchedule(1, request);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        var apiResponse = badRequestResult.Value.Should().BeOfType<ApiResponse>().Subject;
        apiResponse.Success.Should().BeFalse();
        apiResponse.Message.Should().Contain("Giai đoạn Giữ phòng cũ phải kết thúc trước");
    }

    [Fact]
    public async Task SaveRegistrationSchedule_PriceNotPublished_ReturnsBadRequest()
    {
        // Arrange
        var request = new SaveRegistrationScheduleRequestDto { TermId = 1, Periods = new List<PeriodConfigItemDto>() };
        _termServiceMock.Setup(s => s.SaveRegistrationScheduleAsync(1, request))
            .ThrowsAsync(new InvalidOperationException("Không thể kích hoạt đợt đăng ký: kỳ học chưa có bảng giá"));

        // Act
        var result = await _controller.SaveRegistrationSchedule(1, request);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        var apiResponse = badRequestResult.Value.Should().BeOfType<ApiResponse>().Subject;
        apiResponse.Success.Should().BeFalse();
        apiResponse.Message.Should().Contain("chưa có bảng giá");
    }

    [Fact]
    public async Task SaveRegistrationSchedule_ValidRequest_OverridesTermIdAndReturnsOk()
    {
        // Arrange
        var request = new SaveRegistrationScheduleRequestDto { TermId = 0, Periods = new List<PeriodConfigItemDto>() };
        var detail = new TermDetailDto { Id = 1, Name = "Spring 2026" };

        _termServiceMock.Setup(s => s.SaveRegistrationScheduleAsync(1, request)).ReturnsAsync(detail);

        // Act
        var result = await _controller.SaveRegistrationSchedule(1, request);

        // Assert
        request.TermId.Should().Be(1); // Controller tự động gán request.TermId = id
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(StatusCodes.Status200OK);

        var apiResponse = okResult.Value.Should().BeOfType<ApiResponse<TermDetailDto>>().Subject;
        apiResponse.Success.Should().BeTrue();
        apiResponse.Message.Should().Be("Cập nhật lịch trình đăng ký thành công");
    }

    [Fact]
    public async Task TogglePeriodActive_ActiveTrue_Success_ReturnsOkWithMessage()
    {
        // Arrange
        _termServiceMock.Setup(s => s.TogglePeriodActiveAsync(1, 10, true)).Returns(Task.CompletedTask);

        // Act
        var result = await _controller.TogglePeriodActive(1, 10, active: true);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(StatusCodes.Status200OK);

        var apiResponse = okResult.Value.Should().BeOfType<ApiResponse>().Subject;
        apiResponse.Success.Should().BeTrue();
        apiResponse.Message.Should().Be("Kích hoạt đợt đăng ký thành công");
    }

    [Fact]
    public async Task TogglePeriodActive_ActiveFalse_Success_ReturnsOkWithMessage()
    {
        // Arrange
        _termServiceMock.Setup(s => s.TogglePeriodActiveAsync(1, 10, false)).Returns(Task.CompletedTask);

        // Act
        var result = await _controller.TogglePeriodActive(1, 10, active: false);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(StatusCodes.Status200OK);

        var apiResponse = okResult.Value.Should().BeOfType<ApiResponse>().Subject;
        apiResponse.Success.Should().BeTrue();
        apiResponse.Message.Should().Be("Tắt đợt đăng ký thành công");
    }

    [Fact]
    public async Task TogglePeriodActive_PriceNotPublished_ReturnsBadRequest()
    {
        // Arrange
        _termServiceMock.Setup(s => s.TogglePeriodActiveAsync(1, 10, true))
            .ThrowsAsync(new InvalidOperationException("Không thể kích hoạt đợt đăng ký: kỳ học chưa có bảng giá"));

        // Act
        var result = await _controller.TogglePeriodActive(1, 10, active: true);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);

        var apiResponse = badRequestResult.Value.Should().BeOfType<ApiResponse>().Subject;
        apiResponse.Success.Should().BeFalse();
        apiResponse.Message.Should().Contain("chưa có bảng giá");
    }

    [Fact]
    public async Task TogglePeriodActive_OverlappingKeeping_ReturnsBadRequest()
    {
        // Arrange
        _termServiceMock.Setup(s => s.TogglePeriodActiveAsync(1, 10, true))
            .ThrowsAsync(new ArgumentException("Kỳ giữ phòng bị trùng thời gian"));

        // Act
        var result = await _controller.TogglePeriodActive(1, 10, active: true);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);

        var apiResponse = badRequestResult.Value.Should().BeOfType<ApiResponse>().Subject;
        apiResponse.Success.Should().BeFalse();
        apiResponse.Message.Should().Contain("bị trùng thời gian");
    }
}
