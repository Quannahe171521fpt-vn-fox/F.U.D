using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using FUD.Common;
using FUD.DTOs.Facilities.Dorm;
using FUD.Models;
using FUD.Repositories.Interfaces.Facilities;
using FUD.Services.Facilities;
using Moq;
using Xunit;

namespace FUD.Tests.Services.Facilities
{
    public class DormServiceTests
    {
        private readonly Mock<IDormRepository> _dormRepoMock;
        private readonly DormService _dormService;

        public DormServiceTests()
        {
            _dormRepoMock = new Mock<IDormRepository>();
            _dormService = new DormService(_dormRepoMock.Object);
        }

        #region Helper Methods

        private static Dorm CreateSampleDorm(
            int id = 1,
            int campusId = 1,
            string code = "DOM-A",
            string name = "Ký túc xá A",
            string address = "Khu đô thị FPT",
            string status = "Active",
            string campusName = "FPT University Da Nang",
            int blockCount = 0)
        {
            var dorm = new Dorm
            {
                Id = id,
                CampusId = campusId,
                Code = code,
                Name = name,
                Address = address,
                Status = status,
                Campus = new Campus { Id = campusId, Code = "FPT-DN", Name = campusName }
            };

            for (int i = 1; i <= blockCount; i++)
            {
                dorm.Blocks.Add(new Block
                {
                    Id = i,
                    DormId = id,
                    Code = $"BLK-{i}",
                    Name = $"Tòa {i}",
                    GenderType = "Male",
                    Status = "Active"
                });
            }

            return dorm;
        }

        #endregion

        #region GetAllAsync Tests

        [Fact]
        public async Task GetAllAsync_WhenDormsExist_ReturnsPagedResultWithMappedDtos()
        {
            // Arrange
            var query = new DormQueryDto { Page = 1, PageSize = 10 };
            var sampleDorms = new List<Dorm>
            {
                CreateSampleDorm(1, 1, "DOM-A", "Ký túc xá A", blockCount: 2),
                CreateSampleDorm(2, 1, "DOM-B", "Ký túc xá B", blockCount: 1)
            };

            _dormRepoMock
                .Setup(repo => repo.GetAllAsync(query))
                .ReturnsAsync((sampleDorms, sampleDorms.Count));

            // Act
            var result = await _dormService.GetAllAsync(query);

            // Assert
            result.Should().NotBeNull();
            result.TotalCount.Should().Be(2);
            result.Page.Should().Be(1);
            result.PageSize.Should().Be(10);
            result.Items.Should().HaveCount(2);

            var firstItem = result.Items.First();
            firstItem.Id.Should().Be(1);
            firstItem.Code.Should().Be("DOM-A");
            firstItem.Name.Should().Be("Ký túc xá A");
            firstItem.TotalBlocks.Should().Be(2);
            firstItem.CampusName.Should().Be("FPT University Da Nang");

            _dormRepoMock.Verify(repo => repo.GetAllAsync(query), Times.Once);
        }

        [Fact]
        public async Task GetAllAsync_WhenNoDormsFound_ReturnsEmptyPagedResult()
        {
            // Arrange
            var query = new DormQueryDto { Page = 1, PageSize = 10, DormName = "NonExistent" };
            _dormRepoMock
                .Setup(repo => repo.GetAllAsync(query))
                .ReturnsAsync((Enumerable.Empty<Dorm>(), 0));

            // Act
            var result = await _dormService.GetAllAsync(query);

            // Assert
            result.Should().NotBeNull();
            result.TotalCount.Should().Be(0);
            result.Items.Should().BeEmpty();
        }

        #endregion

        #region GetByIdAsync Tests

        [Fact]
        public async Task GetByIdAsync_WhenDormExists_ReturnsMappedDormDto()
        {
            // Arrange
            const int dormId = 10;
            var sampleDorm = CreateSampleDorm(dormId, 1, "DOM-10", "KTX Hòa Hải", blockCount: 3);

            _dormRepoMock
                .Setup(repo => repo.GetByIdAsync(dormId))
                .ReturnsAsync(sampleDorm);

            // Act
            var result = await _dormService.GetByIdAsync(dormId);

            // Assert
            result.Should().NotBeNull();
            result!.Id.Should().Be(dormId);
            result.Code.Should().Be("DOM-10");
            result.Name.Should().Be("KTX Hòa Hải");
            result.TotalBlocks.Should().Be(3);
            result.CampusName.Should().Be("FPT University Da Nang");

            _dormRepoMock.Verify(repo => repo.GetByIdAsync(dormId), Times.Once);
        }

        [Fact]
        public async Task GetByIdAsync_WhenDormDoesNotExist_ReturnsNull()
        {
            // Arrange
            const int nonExistentId = 999;
            _dormRepoMock
                .Setup(repo => repo.GetByIdAsync(nonExistentId))
                .ReturnsAsync((Dorm?)null);

            // Act
            var result = await _dormService.GetByIdAsync(nonExistentId);

            // Assert
            result.Should().BeNull();
            _dormRepoMock.Verify(repo => repo.GetByIdAsync(nonExistentId), Times.Once);
        }

        #endregion

        #region CreateAsync Tests

        [Fact]
        public async Task CreateAsync_WhenCampusDoesNotExist_ThrowsKeyNotFoundException()
        {
            // Arrange
            var dto = new CreateDormDto
            {
                CampusId = 99,
                Code = "DOM-NEW",
                Name = "KTX Mới"
            };

            _dormRepoMock
                .Setup(repo => repo.CampusExistsAsync(dto.CampusId))
                .ReturnsAsync(false);

            // Act
            var act = async () => await _dormService.CreateAsync(dto);

            // Assert
            await act.Should().ThrowAsync<KeyNotFoundException>()
                .WithMessage($"*Campus {dto.CampusId} không tồn tại*");

            _dormRepoMock.Verify(repo => repo.CreateAsync(It.IsAny<Dorm>()), Times.Never);
        }

        [Fact]
        public async Task CreateAsync_WhenDormCodeAlreadyExistsInCampus_ThrowsInvalidOperationException()
        {
            // Arrange
            var dto = new CreateDormDto
            {
                CampusId = 1,
                Code = "DOM-DUP",
                Name = "KTX Trùng Mã"
            };

            _dormRepoMock
                .Setup(repo => repo.CampusExistsAsync(dto.CampusId))
                .ReturnsAsync(true);

            _dormRepoMock
                .Setup(repo => repo.IsCodeUniqueAsync(dto.CampusId, dto.Code, null))
                .ReturnsAsync(false);

            // Act
            var act = async () => await _dormService.CreateAsync(dto);

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage($"*Mã KTX '{dto.Code}' đã tồn tại trong Campus này*");

            _dormRepoMock.Verify(repo => repo.CreateAsync(It.IsAny<Dorm>()), Times.Never);
        }

        [Fact]
        public async Task CreateAsync_WhenValidInput_CreatesDormAndReturnsDto()
        {
            // Arrange
            var dto = new CreateDormDto
            {
                CampusId = 1,
                Code = "DOM-C",
                Name = "Ký túc xá C",
                Address = "Khu đô thị FPT, Đà Nẵng"
            };

            var createdEntity = new Dorm
            {
                Id = 100,
                CampusId = dto.CampusId,
                Code = dto.Code,
                Name = dto.Name,
                Address = dto.Address,
                Status = "Active"
            };

            var hydratedDorm = CreateSampleDorm(100, dto.CampusId, dto.Code, dto.Name, dto.Address!, "Active");

            _dormRepoMock
                .Setup(repo => repo.CampusExistsAsync(dto.CampusId))
                .ReturnsAsync(true);

            _dormRepoMock
                .Setup(repo => repo.IsCodeUniqueAsync(dto.CampusId, dto.Code, null))
                .ReturnsAsync(true);

            _dormRepoMock
                .Setup(repo => repo.CreateAsync(It.Is<Dorm>(d =>
                    d.CampusId == dto.CampusId &&
                    d.Code == dto.Code &&
                    d.Name == dto.Name &&
                    d.Status == "Active")))
                .ReturnsAsync(createdEntity);

            _dormRepoMock
                .Setup(repo => repo.GetByIdAsync(createdEntity.Id))
                .ReturnsAsync(hydratedDorm);

            // Act
            var result = await _dormService.CreateAsync(dto);

            // Assert
            result.Should().NotBeNull();
            result.Id.Should().Be(100);
            result.Code.Should().Be(dto.Code);
            result.Name.Should().Be(dto.Name);
            result.Status.Should().Be("Active");
            result.CampusName.Should().Be("FPT University Da Nang");

            _dormRepoMock.Verify(repo => repo.CreateAsync(It.IsAny<Dorm>()), Times.Once);
            _dormRepoMock.Verify(repo => repo.GetByIdAsync(100), Times.Once);
        }

        #endregion

        #region UpdateAsync Tests

        [Fact]
        public async Task UpdateAsync_WhenDormDoesNotExist_ThrowsKeyNotFoundException()
        {
            // Arrange
            const int dormId = 404;
            var updateDto = new UpdateDormDto { Name = "Tên mới", Address = "Địa chỉ mới" };

            _dormRepoMock
                .Setup(repo => repo.GetByIdAsync(dormId))
                .ReturnsAsync((Dorm?)null);

            // Act
            var act = async () => await _dormService.UpdateAsync(dormId, updateDto);

            // Assert
            await act.Should().ThrowAsync<KeyNotFoundException>()
                .WithMessage($"*KTX {dormId} không tồn tại*");

            _dormRepoMock.Verify(repo => repo.UpdateAsync(It.IsAny<Dorm>()), Times.Never);
        }

        [Fact]
        public async Task UpdateAsync_WhenDormExists_UpdatesInfoAndReturnsUpdatedDto()
        {
            // Arrange
            const int dormId = 1;
            var existingDorm = CreateSampleDorm(dormId, name: "Tên cũ", address: "Địa chỉ cũ");
            var updateDto = new UpdateDormDto { Name = "Tên KTX Đã Cập Nhật", Address = "Địa chỉ mới 2026" };

            var updatedDorm = CreateSampleDorm(dormId, name: updateDto.Name, address: updateDto.Address!);

            _dormRepoMock
                .SetupSequence(repo => repo.GetByIdAsync(dormId))
                .ReturnsAsync(existingDorm)  // Lần 1: tìm để cập nhật
                .ReturnsAsync(updatedDorm);   // Lần 2: lấy lại để map trả về

            _dormRepoMock
                .Setup(repo => repo.UpdateAsync(It.Is<Dorm>(d => d.Id == dormId && d.Name == updateDto.Name)))
                .ReturnsAsync(existingDorm);

            // Act
            var result = await _dormService.UpdateAsync(dormId, updateDto);

            // Assert
            result.Should().NotBeNull();
            result.Name.Should().Be(updateDto.Name);
            result.Address.Should().Be(updateDto.Address);

            _dormRepoMock.Verify(repo => repo.UpdateAsync(It.Is<Dorm>(d => d.Name == updateDto.Name && d.Address == updateDto.Address)), Times.Once);
        }

        #endregion

        #region UpdateStatusAsync Tests

        [Fact]
        public async Task UpdateStatusAsync_WhenDormDoesNotExist_ThrowsKeyNotFoundException()
        {
            // Arrange
            const int dormId = 500;
            var statusDto = new UpdateDormStatusDto { Status = "Inactive" };

            _dormRepoMock
                .Setup(repo => repo.GetByIdAsync(dormId))
                .ReturnsAsync((Dorm?)null);

            // Act
            var act = async () => await _dormService.UpdateStatusAsync(dormId, statusDto);

            // Assert
            await act.Should().ThrowAsync<KeyNotFoundException>()
                .WithMessage($"*KTX {dormId} không tồn tại*");

            _dormRepoMock.Verify(repo => repo.UpdateAsync(It.IsAny<Dorm>()), Times.Never);
        }

        [Fact]
        public async Task UpdateStatusAsync_WhenDormExists_UpdatesStatusSuccessfully()
        {
            // Arrange
            const int dormId = 1;
            var existingDorm = CreateSampleDorm(dormId, status: "Active");
            var statusDto = new UpdateDormStatusDto { Status = "Inactive" };

            var updatedDorm = CreateSampleDorm(dormId, status: "Inactive");

            _dormRepoMock
                .SetupSequence(repo => repo.GetByIdAsync(dormId))
                .ReturnsAsync(existingDorm)
                .ReturnsAsync(updatedDorm);

            _dormRepoMock
                .Setup(repo => repo.UpdateAsync(It.Is<Dorm>(d => d.Status == "Inactive")))
                .ReturnsAsync(existingDorm);

            // Act
            var result = await _dormService.UpdateStatusAsync(dormId, statusDto);

            // Assert
            result.Should().NotBeNull();
            result.Status.Should().Be("Inactive");

            _dormRepoMock.Verify(repo => repo.UpdateAsync(It.Is<Dorm>(d => d.Status == "Inactive")), Times.Once);
        }

        #endregion

        #region SoftDeleteAsync Tests

        [Fact]
        public async Task SoftDeleteAsync_WhenDormDoesNotExist_ThrowsKeyNotFoundException()
        {
            // Arrange
            const int dormId = 404;
            _dormRepoMock
                .Setup(repo => repo.GetByIdAsync(dormId))
                .ReturnsAsync((Dorm?)null);

            // Act
            var act = async () => await _dormService.SoftDeleteAsync(dormId);

            // Assert
            await act.Should().ThrowAsync<KeyNotFoundException>()
                .WithMessage($"*KTX {dormId} không tồn tại*");

            _dormRepoMock.Verify(repo => repo.SoftDeleteAsync(It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task SoftDeleteAsync_WhenDormHasActiveBlocks_ThrowsInvalidOperationException()
        {
            // Arrange
            const int dormId = 1;
            var dormWithBlocks = CreateSampleDorm(dormId, name: "KTX A", blockCount: 3);

            _dormRepoMock
                .Setup(repo => repo.GetByIdAsync(dormId))
                .ReturnsAsync(dormWithBlocks);

            _dormRepoMock
                .Setup(repo => repo.HasActiveBlocksAsync(dormId))
                .ReturnsAsync(true);

            // Act
            var act = async () => await _dormService.SoftDeleteAsync(dormId);

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*Không thể xóa KTX*còn 3 tòa đang hoạt động*");

            _dormRepoMock.Verify(repo => repo.SoftDeleteAsync(It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task SoftDeleteAsync_WhenDormHasNoActiveBlocks_CallsSoftDeleteRepository()
        {
            // Arrange
            const int dormId = 1;
            var dormWithoutBlocks = CreateSampleDorm(dormId, name: "KTX Rỗng", blockCount: 0);

            _dormRepoMock
                .Setup(repo => repo.GetByIdAsync(dormId))
                .ReturnsAsync(dormWithoutBlocks);

            _dormRepoMock
                .Setup(repo => repo.HasActiveBlocksAsync(dormId))
                .ReturnsAsync(false);

            _dormRepoMock
                .Setup(repo => repo.SoftDeleteAsync(dormId))
                .ReturnsAsync(true);

            // Act
            await _dormService.SoftDeleteAsync(dormId);

            // Assert
            _dormRepoMock.Verify(repo => repo.SoftDeleteAsync(dormId), Times.Once);
        }

        #endregion
    }
}

