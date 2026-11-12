using System.Threading.Tasks;
using FluentAssertions;
using FUD.Common;
using FUD.Controllers.Facilities;
using FUD.DTOs.Facilities.Equipment;
using FUD.Services.Interfaces.Facilities;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace FUD.Tests.FUD.Facilities
{
    public class EquipmentControllerTests
    {
        private readonly Mock<IEquipmentService> _serviceMock;
        private readonly EquipmentController _controller;

        public EquipmentControllerTests()
        {
            _serviceMock = new Mock<IEquipmentService>();
            _controller = new EquipmentController(_serviceMock.Object);
        }

        #region GetAll Tests

        [Fact]
        public async Task GetAll_ReturnsOkResultWithPagedEquipment()
        {
            // [Arrange] Chuẩn bị query và mock kết quả từ service
            var query = new EquipmentQueryDto();
            var pagedResult = new PagedResult<EquipmentDto>
            {
                Items = new[] { new EquipmentDto { Id = 1, AssetCode = "EQ-001" } },
                TotalCount = 1,
                Page = 1,
                PageSize = 10
            };

            _serviceMock.Setup(s => s.GetAllAsync(query))
                        .ReturnsAsync(pagedResult);

            // [Act] Gọi API Action
            var response = await _controller.GetAll(query);

            // [Assert] Kiểm tra kết quả
            var okResult = response.Should().BeOfType<OkObjectResult>().Subject;
            var body = okResult.Value as ApiResponse<PagedResult<EquipmentDto>>;
            body.Should().NotBeNull();
            body!.Success.Should().BeTrue();
            body.Data.Items.Should().HaveCount(1);
        }

        #endregion

        #region GetById Tests

        [Fact]
        public async Task GetById_WhenFound_ReturnsOkResultWithEquipment()
        {
            // [Arrange]
            int equipId = 1;
            var dto = new EquipmentDto { Id = equipId, AssetCode = "EQ-001" };
            _serviceMock.Setup(s => s.GetByIdAsync(equipId))
                        .ReturnsAsync(dto);

            // [Act]
            var response = await _controller.GetById(equipId);

            // [Assert]
            var okResult = response.Should().BeOfType<OkObjectResult>().Subject;
            var body = okResult.Value as ApiResponse<EquipmentDto>;
            body.Should().NotBeNull();
            body!.Success.Should().BeTrue();
            body.Data.Id.Should().Be(equipId);
        }

        [Fact]
        public async Task GetById_WhenNotFound_ReturnsNotFound()
        {
            // [Arrange]
            int equipId = 999;
            _serviceMock.Setup(s => s.GetByIdAsync(equipId))
                        .ReturnsAsync((EquipmentDto?)null);

            // [Act]
            var response = await _controller.GetById(equipId);

            // [Assert]
            var notFoundResult = response.Should().BeOfType<NotFoundObjectResult>().Subject;
            var body = notFoundResult.Value as ApiResponse;
            body.Should().NotBeNull();
            body!.Success.Should().BeFalse();
        }

        #endregion

        #region Create Tests

        [Fact]
        public async Task Create_WhenModelStateInvalid_ReturnsBadRequest()
        {
            // [Arrange]
            _controller.ModelState.AddModelError("AssetCode", "AssetCode is required");
            var dto = new CreateEquipmentDto();

            // [Act]
            var response = await _controller.Create(dto);

            // [Assert]
            var badRequest = response.Should().BeOfType<BadRequestObjectResult>().Subject;
            var body = badRequest.Value as ApiResponse;
            body.Should().NotBeNull();
            body!.Success.Should().BeFalse();
        }

        [Fact]
        public async Task Create_WhenValid_ReturnsCreatedAtAction()
        {
            // [Arrange]
            var dto = new CreateEquipmentDto { AssetCode = "EQ-001", CatalogId = 1 };
            var resultDto = new EquipmentDto { Id = 10, AssetCode = "EQ-001" };

            _serviceMock.Setup(s => s.CreateAsync(dto, null))
                        .ReturnsAsync(resultDto);

            // [Act]
            var response = await _controller.Create(dto);

            // [Assert]
            var createdResult = response.Should().BeOfType<CreatedAtActionResult>().Subject;
            createdResult.ActionName.Should().Be(nameof(_controller.GetById));
            var body = createdResult.Value as ApiResponse<EquipmentDto>;
            body.Should().NotBeNull();
            body!.Success.Should().BeTrue();
            body.Data.Id.Should().Be(10);
        }

        #endregion

        #region Move & Condition & Liquidate Tests

        [Fact]
        public async Task Move_WhenCalled_ReturnsOkResult()
        {
            // [Arrange]
            int equipId = 1;
            var dto = new MoveEquipmentDto { RoomId = 101, BedId = 2 };
            var resultDto = new EquipmentDto { Id = equipId, RoomId = 101, BedId = 2 };

            _serviceMock.Setup(s => s.MoveAsync(equipId, dto, null))
                        .ReturnsAsync(resultDto);

            // [Act]
            var response = await _controller.Move(equipId, dto);

            // [Assert]
            var okResult = response.Should().BeOfType<OkObjectResult>().Subject;
            var body = okResult.Value as ApiResponse<EquipmentDto>;
            body.Should().NotBeNull();
            body!.Success.Should().BeTrue();
        }

        [Fact]
        public async Task Liquidate_WhenSuccess_ReturnsOkResult()
        {
            // [Arrange]
            int equipId = 1;
            _serviceMock.Setup(s => s.LiquidateAsync(equipId, null))
                        .Returns(Task.CompletedTask);

            // [Act]
            var response = await _controller.Liquidate(equipId);

            // [Assert]
            var okResult = response.Should().BeOfType<OkObjectResult>().Subject;
            var body = okResult.Value as ApiResponse;
            body.Should().NotBeNull();
            body!.Success.Should().BeTrue();
        }

        [Fact]
        public async Task Liquidate_WhenEquipmentInUse_ReturnsBadRequest()
        {
            // [Arrange]
            int equipId = 1;
            _serviceMock.Setup(s => s.LiquidateAsync(equipId, null))
                        .ThrowsAsync(new InvalidOperationException("Thiết bị đang có phiếu báo hỏng"));

            // [Act]
            var response = await _controller.Liquidate(equipId);

            // [Assert]
            var badRequest = response.Should().BeOfType<BadRequestObjectResult>().Subject;
            var body = badRequest.Value as ApiResponse;
            body.Should().NotBeNull();
            body!.Success.Should().BeFalse();
        }

        #endregion
    }
}

