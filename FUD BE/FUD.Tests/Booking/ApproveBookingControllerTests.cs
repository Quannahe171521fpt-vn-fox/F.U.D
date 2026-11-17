using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using FluentAssertions;
using FUD.Common;
using FUD.Controllers.Booking;
using FUD.DTOs.Booking;
using FUD.Services.Interfaces.Booking;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace FUD.Tests.Booking;

/// <summary>
/// Unit tests cho nghiệp vụ Phê duyệt Đơn đặt phòng (Approve Booking) tại tầng Controller.
/// Kiểm thử endpoint ProcessBooking với Decision = "Approved" theo quy tắc AAA.
/// </summary>
public class ApproveBookingControllerTests
{
    private readonly Mock<IBookingService> _bookingServiceMock = new();

    private BookingController CreateSut(int? managerId = 88)
    {
        var controller = new BookingController(_bookingServiceMock.Object);

        var httpContext = new DefaultHttpContext();
        if (managerId.HasValue)
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, managerId.Value.ToString()),
                new(ClaimTypes.Role, "Manager")
            };
            var identity = new ClaimsIdentity(claims, "TestAuth");
            httpContext.User = new ClaimsPrincipal(identity);
        }

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };

        return controller;
    }

    private static BookingRequestManagerDto CreateSampleApprovedManagerDto(int bookingId = 1, int managerId = 88) => new()
    {
        Id = bookingId,
        StudentId = 10,
        StudentCode = "HE170001",
        StudentName = "Nguyen Van A",
        StudentGender = "Male",
        BedId = 20,
        BedCode = "A1-101-1",
        RoomNumber = "101",
        BlockName = "Block A1",
        DormName = "Dorm A",
        TermId = 5,
        TermName = "Fall 2026",
        TermPrice = 4200000m,
        Status = "Approved",
        CreatedTime = DateTime.UtcNow.AddHours(-2),
        ProcessedTime = DateTime.UtcNow,
        ProcessedByName = "Tran Quan Ly",
        RejectReason = null
    };

    [Fact]
    public async Task ProcessBooking_WhenApprovedAndManagerAuthenticated_Returns200OkWithSuccessPayload()
    {
        // Arrange
        const int bookingId = 1;
        const int managerId = 88;
        var expectedDto = CreateSampleApprovedManagerDto(bookingId, managerId);

        _bookingServiceMock.Setup(s => s.ApproveAsync(bookingId, managerId))
            .ReturnsAsync(expectedDto);

        var sut = CreateSut(managerId);
        var requestDto = new ProcessBookingDto { Decision = "Approved" };

        // Act
        var result = await sut.ProcessBooking(bookingId, requestDto);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(StatusCodes.Status200OK);

        var response = okResult.Value.Should().BeOfType<ApiResponse<BookingRequestManagerDto>>().Subject;
        response.Success.Should().BeTrue();
        response.Message.Should().Contain("Đã phê duyệt đơn đặt phòng");
        response.Data.Should().NotBeNull();
        response.Data!.Id.Should().Be(bookingId);
        response.Data.Status.Should().Be("Approved");
        response.Data.ProcessedByName.Should().Be("Tran Quan Ly");

        _bookingServiceMock.Verify(s => s.ApproveAsync(bookingId, managerId), Times.Once);
    }

    [Theory]
    [InlineData("approved")]
    [InlineData("APPROVED")]
    [InlineData("ApPrOvEd")]
    public async Task ProcessBooking_WhenApprovedWithVariousCasings_AcceptsAndReturns200Ok(string decisionCase)
    {
        // Arrange
        const int bookingId = 2;
        const int managerId = 88;
        var expectedDto = CreateSampleApprovedManagerDto(bookingId, managerId);

        _bookingServiceMock.Setup(s => s.ApproveAsync(bookingId, managerId))
            .ReturnsAsync(expectedDto);

        var sut = CreateSut(managerId);
        var requestDto = new ProcessBookingDto { Decision = decisionCase };

        // Act
        var result = await sut.ProcessBooking(bookingId, requestDto);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(StatusCodes.Status200OK);
        _bookingServiceMock.Verify(s => s.ApproveAsync(bookingId, managerId), Times.Once);
    }

    [Fact]
    public async Task ProcessBooking_WhenManagerNotAuthenticated_Returns401Unauthorized()
    {
        // Arrange
        const int bookingId = 1;
        var sut = CreateSut(managerId: null);
        var requestDto = new ProcessBookingDto { Decision = "Approved" };

        // Act
        var result = await sut.ProcessBooking(bookingId, requestDto);

        // Assert
        var unauthorizedResult = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        unauthorizedResult.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);

        var response = unauthorizedResult.Value.Should().BeOfType<ApiResponse>().Subject;
        response.Success.Should().BeFalse();
        response.Message.Should().Contain("Không xác định được danh tính người quản lý");

        _bookingServiceMock.Verify(s => s.ApproveAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task ProcessBooking_WhenBookingNotFound_Returns404NotFound()
    {
        // Arrange
        const int nonExistentBookingId = 999;
        const int managerId = 88;

        _bookingServiceMock.Setup(s => s.ApproveAsync(nonExistentBookingId, managerId))
            .ThrowsAsync(new KeyNotFoundException("Không tìm thấy đơn đặt phòng."));

        var sut = CreateSut(managerId);
        var requestDto = new ProcessBookingDto { Decision = "Approved" };

        // Act
        var result = await sut.ProcessBooking(nonExistentBookingId, requestDto);

        // Assert
        var notFoundResult = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFoundResult.StatusCode.Should().Be(StatusCodes.Status404NotFound);

        var response = notFoundResult.Value.Should().BeOfType<ApiResponse>().Subject;
        response.Success.Should().BeFalse();
        response.Message.Should().Be("Không tìm thấy đơn đặt phòng.");
    }

    [Fact]
    public async Task ProcessBooking_WhenInvalidOperationOccurs_Returns409Conflict()
    {
        // Arrange
        const int bookingId = 1;
        const int managerId = 88;

        _bookingServiceMock.Setup(s => s.ApproveAsync(bookingId, managerId))
            .ThrowsAsync(new InvalidOperationException("Giường này hiện không còn trống cho học kỳ này."));

        var sut = CreateSut(managerId);
        var requestDto = new ProcessBookingDto { Decision = "Approved" };

        // Act
        var result = await sut.ProcessBooking(bookingId, requestDto);

        // Assert
        var conflictResult = result.Should().BeOfType<ConflictObjectResult>().Subject;
        conflictResult.StatusCode.Should().Be(StatusCodes.Status409Conflict);

        var response = conflictResult.Value.Should().BeOfType<ApiResponse>().Subject;
        response.Success.Should().BeFalse();
        response.Message.Should().Be("Giường này hiện không còn trống cho học kỳ này.");
    }

    [Fact]
    public async Task ProcessBooking_WhenDecisionIsInvalid_Returns400BadRequest()
    {
        // Arrange
        const int bookingId = 1;
        const int managerId = 88;
        var sut = CreateSut(managerId);
        var requestDto = new ProcessBookingDto { Decision = "UnknownDecision" };

        // Act
        var result = await sut.ProcessBooking(bookingId, requestDto);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);

        var response = badRequestResult.Value.Should().BeOfType<ApiResponse>().Subject;
        response.Success.Should().BeFalse();
        response.Message.Should().Contain("Quyết định xử lý không hợp lệ");

        _bookingServiceMock.Verify(s => s.ApproveAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task ProcessBooking_WhenUnexpectedExceptionOccurs_Returns500InternalServerError()
    {
        // Arrange
        const int bookingId = 1;
        const int managerId = 88;

        _bookingServiceMock.Setup(s => s.ApproveAsync(bookingId, managerId))
            .ThrowsAsync(new Exception("Database connection failure"));

        var sut = CreateSut(managerId);
        var requestDto = new ProcessBookingDto { Decision = "Approved" };

        // Act
        var result = await sut.ProcessBooking(bookingId, requestDto);

        // Assert
        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);

        var response = objectResult.Value.Should().BeOfType<ApiResponse>().Subject;
        response.Success.Should().BeFalse();
        response.Message.Should().Be("Database connection failure");
    }
}

