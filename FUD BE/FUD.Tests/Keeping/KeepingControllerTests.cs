using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using FluentAssertions;
using FUD.Common;
using FUD.Controllers.Keeping;
using FUD.DTOs.Keeping;
using FUD.Services.Interfaces.Keeping;
using FUD.Services.Keeping;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace FUD.Tests.Keeping;

/// <summary>
/// Unit tests cho KeepingController (UC02) — tuân thủ quy tắc AAA (Arrange / Act / Assert).
/// </summary>
public class KeepingControllerTests
{
    private readonly Mock<IKeepingEligibilityService> _eligibilityServiceMock = new();
    private readonly Mock<IKeepingService> _keepingServiceMock = new();

    private KeepingController CreateSut(int? studentId = 10)
    {
        var controller = new KeepingController(
            _eligibilityServiceMock.Object,
            _keepingServiceMock.Object);

        var httpContext = new DefaultHttpContext();
        if (studentId.HasValue)
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, studentId.Value.ToString()),
                new(ClaimTypes.Role, "Student")
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

    [Fact]
    public async Task GetEligibility_WithoutValidUserClaim_Returns401Unauthorized()
    {
        // Arrange
        var sut = CreateSut(studentId: null);

        // Act
        var result = await sut.GetEligibility();

        // Assert
        var unauthorizedResult = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        unauthorizedResult.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        var response = unauthorizedResult.Value.Should().BeOfType<ApiResponse>().Subject;
        response.Success.Should().BeFalse();
    }

    [Fact]
    public async Task GetEligibility_WithValidStudent_Returns200Ok()
    {
        // Arrange
        const int studentId = 15;
        var expectedDto = new KeepingEligibilityResponseDto
        {
            Eligible = true,
            NextTerm = new NextTermDto { Id = 2, Name = "Fall 2026" }
        };
        _eligibilityServiceMock.Setup(s => s.CheckEligibilityAsync(studentId))
            .ReturnsAsync(expectedDto);

        var sut = CreateSut(studentId);

        // Act
        var result = await sut.GetEligibility();

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(StatusCodes.Status200OK);
        var response = okResult.Value.Should().BeOfType<ApiResponse<KeepingEligibilityResponseDto>>().Subject;
        response.Success.Should().BeTrue();
        response.Data.Should().NotBeNull();
        response.Data!.Eligible.Should().BeTrue();
        response.Data.NextTerm!.Name.Should().Be("Fall 2026");
    }

    [Fact]
    public async Task CreateKeepingRequest_WhenSuccess_Returns201Created()
    {
        // Arrange
        const int studentId = 15;
        var dto = new CreateKeepingRequestDto { AgreedToRegulations = true };
        var expectedResponse = new KeepingRequestResponseDto
        {
            RequestId = 99,
            Status = KeepingConstants.StatusCompleted,
            RequestType = KeepingConstants.RequestType
        };
        _keepingServiceMock.Setup(s => s.CreateAsync(studentId, dto))
            .ReturnsAsync(expectedResponse);

        var sut = CreateSut(studentId);

        // Act
        var result = await sut.CreateKeepingRequest(dto);

        // Assert
        var createdResult = result.Should().BeOfType<CreatedResult>().Subject;
        createdResult.StatusCode.Should().Be(StatusCodes.Status201Created);
        createdResult.Location.Should().Be("/api/keeping/requests/99");
        var response = createdResult.Value.Should().BeOfType<ApiResponse<KeepingRequestResponseDto>>().Subject;
        response.Success.Should().BeTrue();
        response.Data!.RequestId.Should().Be(99);
    }

    [Fact]
    public async Task CreateKeepingRequest_WhenArgumentExceptionThrown_Returns400BadRequest()
    {
        // Arrange
        const int studentId = 15;
        var dto = new CreateKeepingRequestDto { AgreedToRegulations = false };
        _keepingServiceMock.Setup(s => s.CreateAsync(studentId, dto))
            .ThrowsAsync(new ArgumentException("KEEP_400: Bạn phải đồng ý với nội quy KTX để giữ phòng."));

        var sut = CreateSut(studentId);

        // Act
        var result = await sut.CreateKeepingRequest(dto);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        var response = badRequestResult.Value.Should().BeOfType<ApiResponse>().Subject;
        response.Success.Should().BeFalse();
        response.Message.Should().Contain("KEEP_400");
    }

    [Fact]
    public async Task CreateKeepingRequest_WhenNotEligibleExceptionThrown_Returns422UnprocessableEntity()
    {
        // Arrange
        const int studentId = 15;
        var dto = new CreateKeepingRequestDto { AgreedToRegulations = true };
        _keepingServiceMock.Setup(s => s.CreateAsync(studentId, dto))
            .ThrowsAsync(new KeepingNotEligibleException(
                KeepingReasonCodes.HasUnpaidInvoice,
                "Bạn đang có hóa đơn chưa thanh toán."));

        var sut = CreateSut(studentId);

        // Act
        var result = await sut.CreateKeepingRequest(dto);

        // Assert
        var unprocessableResult = result.Should().BeOfType<UnprocessableEntityObjectResult>().Subject;
        unprocessableResult.StatusCode.Should().Be(StatusCodes.Status422UnprocessableEntity);
        var response = unprocessableResult.Value.Should().BeOfType<ApiResponse>().Subject;
        response.Success.Should().BeFalse();
        response.Message.Should().Contain("hóa đơn chưa thanh toán");
    }

    [Fact]
    public async Task CreateKeepingRequest_WhenConflictOrDbUpdateExceptionThrown_Returns409Conflict()
    {
        // Arrange
        const int studentId = 15;
        var dto = new CreateKeepingRequestDto { AgreedToRegulations = true };
        _keepingServiceMock.SetupSequence(s => s.CreateAsync(studentId, dto))
            .ThrowsAsync(new KeepingConflictException("Giường này đã có sinh viên khác đăng ký."))
            .ThrowsAsync(new DbUpdateException("Unique constraint violation"));

        var sut = CreateSut(studentId);

        // Act
        var conflictResult1 = await sut.CreateKeepingRequest(dto);
        var conflictResult2 = await sut.CreateKeepingRequest(dto);

        // Assert
        conflictResult1.Should().BeOfType<ConflictObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status409Conflict);
        conflictResult2.Should().BeOfType<ConflictObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status409Conflict);
    }

    [Fact]
    public async Task GetKeepingRequest_WhenRequestNotFound_Returns404NotFound()
    {
        // Arrange
        const int studentId = 15;
        const int requestId = 404;
        _keepingServiceMock.Setup(s => s.GetRequestAsync(requestId))
            .ReturnsAsync((KeepingRequestResponseDto?)null);

        var sut = CreateSut(studentId);

        // Act
        var result = await sut.GetKeepingRequest(requestId);

        // Assert
        var notFoundResult = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFoundResult.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task GetKeepingRequest_WhenNotOwner_Returns403Forbid_And_WhenOwner_Returns200Ok()
    {
        // Arrange
        const int ownerStudentId = 15;
        const int otherStudentId = 99;
        const int requestId = 50;
        var dto = new KeepingRequestResponseDto { RequestId = requestId, Status = KeepingConstants.StatusCompleted };

        _keepingServiceMock.Setup(s => s.GetRequestAsync(requestId)).ReturnsAsync(dto);
        _keepingServiceMock.Setup(s => s.GetRequestOwnerIdAsync(requestId)).ReturnsAsync(ownerStudentId);

        var sutOther = CreateSut(otherStudentId);
        var sutOwner = CreateSut(ownerStudentId);

        // Act
        var forbidResult = await sutOther.GetKeepingRequest(requestId);
        var okResult = await sutOwner.GetKeepingRequest(requestId);

        // Assert
        forbidResult.Should().BeOfType<ForbidResult>();
        var okObject = okResult.Should().BeOfType<OkObjectResult>().Subject;
        var response = okObject.Value.Should().BeOfType<ApiResponse<KeepingRequestResponseDto>>().Subject;
        response.Success.Should().BeTrue();
        response.Data!.RequestId.Should().Be(requestId);
    }
}

