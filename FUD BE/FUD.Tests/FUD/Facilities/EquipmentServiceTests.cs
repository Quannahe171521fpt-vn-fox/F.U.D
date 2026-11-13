using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using FUD.Common;
using FUD.DTOs.Facilities.Equipment;
using FUD.Models;
using FUD.Repositories.Interfaces.Facilities;
using FUD.Services.Facilities;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace FUD.Tests.FUD.Facilities
{
    public class EquipmentServiceTests : IDisposable
    {
        private readonly Mock<IEquipmentRepository> _repoMock;
        private readonly FudContext _context;
        private readonly EquipmentService _service;

        public EquipmentServiceTests()
        {
            _repoMock = new Mock<IEquipmentRepository>();

            // Giả lập DbContext với InMemory Database Provider
            var options = new DbContextOptionsBuilder<FudContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new FudContext(options);
            _service = new EquipmentService(_repoMock.Object, _context);
        }

        public void Dispose()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
        }

        #region GetAllAsync Tests

        [Fact]
        public async Task GetAllAsync_WhenCalled_ReturnsPagedResultOfEquipmentDto()
        {
            // [Arrange] Chuẩn bị dữ liệu mẫu và mock repository
            var query = new EquipmentQueryDto { Page = 1, PageSize = 10 };
            var equipments = new List<Equipment>
            {
                new Equipment
                {
                    Id = 1,
                    AssetCode = "EQ-001",
                    Catalog = new EquipmentCatalog { Name = "Bàn học", Category = "Nội thất" },
                    Room = new Room { RoomNumber = "101", Block = new Block { Code = "A" } },
                    Condition = "Good"
                },
                new Equipment
                {
                    Id = 2,
                    AssetCode = "EQ-002",
                    Catalog = new EquipmentCatalog { Name = "Ghế xoay", Category = "Nội thất" },
                    Condition = "Normal"
                }
            };

            _repoMock.Setup(r => r.GetAllAsync(query))
                     .ReturnsAsync((equipments, 2));

            // [Act] Thực thi phương thức cần test
            var result = await _service.GetAllAsync(query);

            // [Assert] Kiểm tra kết quả trả về
            result.Should().NotBeNull();
            result.TotalCount.Should().Be(2);
            result.Items.Should().HaveCount(2);
            result.Page.Should().Be(1);
            result.PageSize.Should().Be(10);
            _repoMock.Verify(r => r.GetAllAsync(query), Times.Once);
        }

        #endregion

        #region GetByIdAsync Tests

        [Fact]
        public async Task GetByIdAsync_WhenEquipmentExists_ReturnsEquipmentDto()
        {
            // [Arrange]
            int equipId = 1;
            var equip = new Equipment
            {
                Id = equipId,
                AssetCode = "EQ-001",
                Condition = "Good",
                Catalog = new EquipmentCatalog { Name = "Điều hòa" }
            };

            _repoMock.Setup(r => r.GetByIdAsync(equipId))
                     .ReturnsAsync(equip);

            // [Act]
            var result = await _service.GetByIdAsync(equipId);

            // [Assert]
            result.Should().NotBeNull();
            result!.Id.Should().Be(equipId);
            result.AssetCode.Should().Be("EQ-001");
            _repoMock.Verify(r => r.GetByIdAsync(equipId), Times.Once);
        }

        [Fact]
        public async Task GetByIdAsync_WhenEquipmentDoesNotExist_ReturnsNull()
        {
            // [Arrange]
            int nonExistentId = 999;
            _repoMock.Setup(r => r.GetByIdAsync(nonExistentId))
                     .ReturnsAsync((Equipment?)null);

            // [Act]
            var result = await _service.GetByIdAsync(nonExistentId);

            // [Assert]
            result.Should().BeNull();
            _repoMock.Verify(r => r.GetByIdAsync(nonExistentId), Times.Once);
        }

        #endregion

        #region CreateAsync Tests

        [Fact]
        public async Task CreateAsync_WhenAssetCodeAlreadyExists_ThrowsInvalidOperationException()
        {
            // [Arrange]
            var dto = new CreateEquipmentDto
            {
                AssetCode = "EQ-DUPLICATE",
                CatalogId = 1
            };

            _repoMock.Setup(r => r.GetByAssetCodeAsync(dto.AssetCode))
                     .ReturnsAsync(new Equipment { Id = 1, AssetCode = dto.AssetCode });

            // [Act]
            var act = async () => await _service.CreateAsync(dto);

            // [Assert]
            await act.Should().ThrowAsync<InvalidOperationException>()
                     .WithMessage("*đã tồn tại*");
            _repoMock.Verify(r => r.AddAsync(It.IsAny<Equipment>()), Times.Never);
        }

        [Fact]
        public async Task CreateAsync_WhenValidInput_AddsEquipmentAndLogsAudit()
        {
            // [Arrange]
            var dto = new CreateEquipmentDto
            {
                AssetCode = "EQ-NEW",
                CatalogId = 2,
                RoomId = 10,
                Condition = "Good"
            };

            _repoMock.Setup(r => r.GetByAssetCodeAsync(dto.AssetCode))
                     .ReturnsAsync((Equipment?)null);

            var createdEquipment = new Equipment
            {
                Id = 10,
                AssetCode = dto.AssetCode,
                CatalogId = dto.CatalogId,
                RoomId = dto.RoomId,
                Condition = dto.Condition
            };

            _repoMock.Setup(r => r.AddAsync(It.IsAny<Equipment>()))
                     .ReturnsAsync(createdEquipment);

            _repoMock.Setup(r => r.GetByIdAsync(createdEquipment.Id))
                     .ReturnsAsync(createdEquipment);

            // [Act]
            var result = await _service.CreateAsync(dto, userId: 1);

            // [Assert]
            result.Should().NotBeNull();
            result.AssetCode.Should().Be("EQ-NEW");
            _repoMock.Verify(r => r.AddAsync(It.Is<Equipment>(e => e.AssetCode == dto.AssetCode)), Times.Once);

            // Kiểm tra AuditLog được ghi vào DbContext giả lập
            var auditLog = await _context.AuditLogs.FirstOrDefaultAsync(a => a.TargetId == "10");
            auditLog.Should().NotBeNull();
            auditLog!.ActionType.Should().Be("CREATE");
            auditLog.TargetEntity.Should().Be("Equipment");
        }

        #endregion

        #region UpdateAsync Tests

        [Fact]
        public async Task UpdateAsync_WhenEquipmentNotFound_ThrowsKeyNotFoundException()
        {
            // [Arrange]
            int equipId = 404;
            var dto = new UpdateEquipmentDto { CatalogId = 1, Condition = "Good" };
            _repoMock.Setup(r => r.GetByIdAsync(equipId)).ReturnsAsync((Equipment?)null);

            // [Act]
            var act = async () => await _service.UpdateAsync(equipId, dto);

            // [Assert]
            await act.Should().ThrowAsync<KeyNotFoundException>()
                     .WithMessage("*không tồn tại*");
            _repoMock.Verify(r => r.UpdateAsync(It.IsAny<Equipment>()), Times.Never);
        }

        [Fact]
        public async Task UpdateAsync_WhenEquipmentExists_UpdatesAndReturnsDto()
        {
            // [Arrange]
            int equipId = 1;
            var existing = new Equipment
            {
                Id = equipId,
                CatalogId = 1,
                RoomId = 5,
                Condition = "Good"
            };
            var dto = new UpdateEquipmentDto
            {
                CatalogId = 2,
                RoomId = 6,
                Condition = "Broken",
                Note = "Bị hỏng chân bàn"
            };

            _repoMock.Setup(r => r.GetByIdAsync(equipId)).ReturnsAsync(existing);
            _repoMock.Setup(r => r.UpdateAsync(existing)).Returns(Task.CompletedTask);

            // [Act]
            var result = await _service.UpdateAsync(equipId, dto, userId: 2);

            // [Assert]
            result.Should().NotBeNull();
            existing.Condition.Should().Be("Broken");
            existing.CatalogId.Should().Be(2);
            existing.RoomId.Should().Be(6);
            _repoMock.Verify(r => r.UpdateAsync(existing), Times.Once);

            // Kiểm tra AuditLog
            var auditLog = await _context.AuditLogs.FirstOrDefaultAsync(a => a.TargetId == equipId.ToString() && a.ActionType == "UPDATE");
            auditLog.Should().NotBeNull();
        }

        #endregion

        #region MoveAsync Tests

        [Fact]
        public async Task MoveAsync_WhenEquipmentNotFound_ThrowsKeyNotFoundException()
        {
            // [Arrange]
            int equipId = 99;
            var dto = new MoveEquipmentDto { RoomId = 1, BedId = 2 };
            _repoMock.Setup(r => r.GetByIdAsync(equipId)).ReturnsAsync((Equipment?)null);

            // [Act]
            var act = async () => await _service.MoveAsync(equipId, dto);

            // [Assert]
            await act.Should().ThrowAsync<KeyNotFoundException>();
        }

        [Fact]
        public async Task MoveAsync_WhenValid_UpdatesLocationAndLogsAudit()
        {
            // [Arrange]
            int equipId = 1;
            var existing = new Equipment { Id = equipId, RoomId = 101, BedId = null };
            var dto = new MoveEquipmentDto { RoomId = 102, BedId = 3 };

            _repoMock.Setup(r => r.GetByIdAsync(equipId)).ReturnsAsync(existing);
            _repoMock.Setup(r => r.UpdateAsync(existing)).Returns(Task.CompletedTask);

            // [Act]
            var result = await _service.MoveAsync(equipId, dto, userId: 1);

            // [Assert]
            result.Should().NotBeNull();
            existing.RoomId.Should().Be(102);
            existing.BedId.Should().Be(3);
            _repoMock.Verify(r => r.UpdateAsync(existing), Times.Once);

            var auditLog = await _context.AuditLogs.FirstOrDefaultAsync(a => a.TargetId == equipId.ToString() && a.ActionType == "MOVE");
            auditLog.Should().NotBeNull();
            auditLog!.NewValue.Should().Contain("Room: 102, Bed: 3");
        }

        #endregion

        #region ChangeConditionAsync Tests

        [Fact]
        public async Task ChangeConditionAsync_WhenEquipmentNotFound_ThrowsKeyNotFoundException()
        {
            // [Arrange]
            _repoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync((Equipment?)null);
            var dto = new ChangeConditionDto { Condition = "Broken" };

            // [Act]
            var act = async () => await _service.ChangeConditionAsync(1, dto);

            // [Assert]
            await act.Should().ThrowAsync<KeyNotFoundException>();
        }

        [Fact]
        public async Task ChangeConditionAsync_WhenEquipmentExists_UpdatesCondition()
        {
            // [Arrange]
            var equip = new Equipment { Id = 1, Condition = "Good" };
            var dto = new ChangeConditionDto { Condition = "Repairing" };

            _repoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(equip);
            _repoMock.Setup(r => r.UpdateAsync(equip)).Returns(Task.CompletedTask);

            // [Act]
            var result = await _service.ChangeConditionAsync(1, dto);

            // [Assert]
            result.Should().NotBeNull();
            equip.Condition.Should().Be("Repairing");
            _repoMock.Verify(r => r.UpdateAsync(equip), Times.Once);

            var audit = await _context.AuditLogs.FirstOrDefaultAsync(a => a.TargetId == "1" && a.ActionType == "CHANGE_CONDITION");
            audit.Should().NotBeNull();
            audit!.NewValue.Should().Be("Repairing");
        }

        #endregion

        #region LiquidateAsync Tests

        [Fact]
        public async Task LiquidateAsync_WhenEquipmentNotFound_ThrowsKeyNotFoundException()
        {
            // [Arrange]
            _repoMock.Setup(r => r.GetByIdAsync(10)).ReturnsAsync((Equipment?)null);

            // [Act]
            var act = async () => await _service.LiquidateAsync(10);

            // [Assert]
            await act.Should().ThrowAsync<KeyNotFoundException>();
        }

        [Fact]
        public async Task LiquidateAsync_LiquidatesSuccessfully()
        {
            // [Arrange]
            var equip = new Equipment { Id = 10, Condition = "Good" };
            _repoMock.Setup(r => r.GetByIdAsync(10)).ReturnsAsync(equip);
            _repoMock.Setup(r => r.UpdateAsync(equip)).Returns(Task.CompletedTask);

            // [Act]
            await _service.LiquidateAsync(10, userId: 3);

            // [Assert]
            equip.Condition.Should().Be("Liquidated");
            _repoMock.Verify(r => r.UpdateAsync(equip), Times.Once);

            var audit = await _context.AuditLogs.FirstOrDefaultAsync(a => a.TargetId == "10" && a.ActionType == "LIQUIDATE");
            audit.Should().NotBeNull();
            audit!.NewValue.Should().Be("Liquidated");
        }

        #endregion

        #region Catalog & History Tests

        [Fact]
        public async Task CreateCatalogAsync_WhenValid_ReturnsCreatedCatalogDto()
        {
            // [Arrange]
            var dto = new CreateEquipmentCatalogDto
            {
                Category = "Điện tử",
                Name = "Máy sấy tóc"
            };
            var catalog = new EquipmentCatalog
            {
                Id = 1,
                Category = dto.Category,
                Name = dto.Name,
                ActiveFlag = true
            };
            _repoMock.Setup(r => r.AddCatalogAsync(It.IsAny<EquipmentCatalog>()))
                     .ReturnsAsync(catalog);

            // [Act]
            var result = await _service.CreateCatalogAsync(dto);

            // [Assert]
            result.Should().NotBeNull();
            result.Name.Should().Be("Máy sấy tóc");
            result.Category.Should().Be("Điện tử");
            result.ActiveFlag.Should().BeTrue();
            _repoMock.Verify(r => r.AddCatalogAsync(It.IsAny<EquipmentCatalog>()), Times.Once);
        }

        [Fact]
        public async Task DeleteCatalogAsync_WhenCalled_DelegatesToRepository()
        {
            // [Arrange]
            _repoMock.Setup(r => r.DeleteCatalogAsync(1)).ReturnsAsync(true);

            // [Act]
            var result = await _service.DeleteCatalogAsync(1);

            // [Assert]
            result.Should().BeTrue();
            _repoMock.Verify(r => r.DeleteCatalogAsync(1), Times.Once);
        }

        #endregion
    }
}

