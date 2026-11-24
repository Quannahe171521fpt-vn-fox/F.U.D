using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using FUD.DTOs.Utilities;
using FUD.Models;
using FUD.Repositories.Interfaces.Utilities;
using FUD.Services.Utilities;
using Moq;
using Xunit;

namespace FUD.Tests.Utilities;

public class EwGuardDormAuthorizationTests
{
    private readonly Mock<IEwRepository> _ewRepoMock = new();
    private readonly EwService _sut;

    public EwGuardDormAuthorizationTests()
    {
        _sut = new EwService(_ewRepoMock.Object, null);
    }

    [Fact]
    public async Task ConfirmManualReading_WhenGuardBelongsToDifferentDorm_ShouldThrowUnauthorizedAccessException()
    {
        // Arrange
        int guardUserId = 10;
        int guardDormId = 1; // Dorm A
        int otherDormId = 2; // Dorm B

        var period = new BillingPeriod { Id = 1, Status = "Open", TargetMonth = 11, TargetYear = 2026 };
        _ewRepoMock.Setup(r => r.GetBillingPeriodByIdAsync(1)).ReturnsAsync(period);

        var guard = new Guard
        {
            UserId = guardUserId,
            DormId = guardDormId,
            Dorm = new Dorm { Id = guardDormId, Name = "Dom A", Code = "A" }
        };
        _ewRepoMock.Setup(r => r.GetGuardByUserIdAsync(guardUserId)).ReturnsAsync(guard);

        var roomInOtherDorm = new Room
        {
            Id = 101,
            RoomNumber = "B101L",
            Block = new Block
            {
                Id = 20,
                Code = "B1",
                DormId = otherDormId,
                Dorm = new Dorm { Id = otherDormId, Name = "Dom B", Code = "B" }
            }
        };
        _ewRepoMock.Setup(r => r.GetRoomsByBlockCodeAsync("B101")).ReturnsAsync(new List<Room> { roomInOtherDorm });

        var request = new ManualConfirmRequestDto
        {
            BillingPeriodId = 1,
            BlockCode = "B101",
            ElectricityLeftNewValue = 150
        };

        // Act
        Func<Task> act = async () => await _sut.ConfirmManualReadingAsync(guardUserId, request, isManager: false);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*không có quyền ghi nhận chỉ số*");
    }

    [Fact]
    public async Task ConfirmManualReading_WhenGuardBelongsToSameDorm_ShouldSucceed()
    {
        // Arrange
        int guardUserId = 10;
        int guardDormId = 1;

        var period = new BillingPeriod { Id = 1, Status = "Open", TargetMonth = 11, TargetYear = 2026 };
        _ewRepoMock.Setup(r => r.GetBillingPeriodByIdAsync(1)).ReturnsAsync(period);

        var guard = new Guard
        {
            UserId = guardUserId,
            DormId = guardDormId,
            Dorm = new Dorm { Id = guardDormId, Name = "Dom A", Code = "A" }
        };
        _ewRepoMock.Setup(r => r.GetGuardByUserIdAsync(guardUserId)).ReturnsAsync(guard);

        var roomInSameDorm = new Room
        {
            Id = 101,
            RoomNumber = "A101L",
            Block = new Block
            {
                Id = 10,
                Code = "A1",
                DormId = guardDormId,
                Dorm = guard.Dorm
            }
        };
        _ewRepoMock.Setup(r => r.GetRoomsByBlockCodeAsync("A101")).ReturnsAsync(new List<Room> { roomInSameDorm });
        _ewRepoMock.Setup(r => r.GetLatestReadingBeforePeriodAsync(101, "Electricity", 11, 2026)).ReturnsAsync((MeterReading?)null);
        _ewRepoMock.Setup(r => r.GetReadingForPeriodAsync(1, 101, "Electricity")).ReturnsAsync((MeterReading?)null);

        var request = new ManualConfirmRequestDto
        {
            BillingPeriodId = 1,
            BlockCode = "A101",
            ElectricityLeftNewValue = 120
        };

        // Act
        var result = await _sut.ConfirmManualReadingAsync(guardUserId, request, isManager: false);

        // Assert
        result.Should().NotBeNull();
        result.Saved.Should().Be(1);
        _ewRepoMock.Verify(r => r.AddMeterReadingAsync(It.Is<MeterReading>(m => m.RoomId == 101 && m.RecordedById == guardUserId)), Times.Once);
        _ewRepoMock.Verify(r => r.SaveChangesAsync(), Times.AtLeastOnce);
    }

    [Fact]
    public async Task UpdateReading_WhenGuardEditsReadingInDifferentDorm_ShouldThrowUnauthorizedAccessException()
    {
        // Arrange
        int guardUserId = 10;
        int guardDormId = 1;
        int readingDormId = 2;

        var guard = new Guard
        {
            UserId = guardUserId,
            DormId = guardDormId,
            Dorm = new Dorm { Id = guardDormId, Name = "Dom A", Code = "A" }
        };
        _ewRepoMock.Setup(r => r.GetGuardByUserIdAsync(guardUserId)).ReturnsAsync(guard);

        var reading = new MeterReading
        {
            Id = 55,
            CurrentValue = 100,
            PreviousValue = 50,
            Room = new Room
            {
                Id = 202,
                RoomNumber = "B202L",
                Block = new Block
                {
                    Id = 20,
                    DormId = readingDormId,
                    Dorm = new Dorm { Id = readingDormId, Name = "Dom B", Code = "B" }
                }
            }
        };
        _ewRepoMock.Setup(r => r.GetReadingByIdAsync(55)).ReturnsAsync(reading);

        // Act
        Func<Task> act = async () => await _sut.UpdateReadingAsync(55, 120, "Guard sửa", guardUserId, isManager: false);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*không có quyền chỉnh sửa chỉ số*");
    }

    [Fact]
    public async Task UpdateReading_WhenManagerEditsReadingInAnyDorm_ShouldSucceed()
    {
        // Arrange
        int managerUserId = 1;

        var reading = new MeterReading
        {
            Id = 55,
            CurrentValue = 100,
            PreviousValue = 50,
            RecordedById = 10,
            Room = new Room
            {
                Id = 202,
                RoomNumber = "B202L",
                Block = new Block
                {
                    Id = 20,
                    DormId = 2,
                    Dorm = new Dorm { Id = 2, Name = "Dom B", Code = "B" }
                }
            }
        };
        _ewRepoMock.Setup(r => r.GetReadingByIdAsync(55)).ReturnsAsync(reading);

        // Act
        var result = await _sut.UpdateReadingAsync(55, 130, "Sửa bởi Quản lý", managerUserId, isManager: true);

        // Assert
        result.Should().BeTrue();
        reading.CurrentValue.Should().Be(130);
        reading.Note.Should().Contain("[Quản lý cập nhật]");
        _ewRepoMock.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task DeleteReading_WhenGuardDeletesReadingInDifferentDorm_ShouldThrowUnauthorizedAccessException()
    {
        // Arrange
        int guardUserId = 10;
        int guardDormId = 1;

        var guard = new Guard
        {
            UserId = guardUserId,
            DormId = guardDormId,
            Dorm = new Dorm { Id = guardDormId, Name = "Dom A", Code = "A" }
        };
        _ewRepoMock.Setup(r => r.GetGuardByUserIdAsync(guardUserId)).ReturnsAsync(guard);

        var reading = new MeterReading
        {
            Id = 77,
            Room = new Room
            {
                Id = 303,
                RoomNumber = "B303R",
                Block = new Block
                {
                    Id = 25,
                    DormId = 2,
                    Dorm = new Dorm { Id = 2, Name = "Dom B", Code = "B" }
                }
            }
        };
        _ewRepoMock.Setup(r => r.GetReadingByIdAsync(77)).ReturnsAsync(reading);

        // Act
        Func<Task> act = async () => await _sut.DeleteReadingAsync(77, guardUserId, isManager: false);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*không có quyền xóa chỉ số*");
    }

    [Fact]
    public async Task GetBlockFormData_WhenGuardAccessesBlockOfDifferentDorm_ShouldThrowUnauthorizedAccessException()
    {
        // Arrange
        int guardUserId = 10;
        int guardDormId = 1;

        _ewRepoMock.Setup(r => r.GetBillingPeriodByIdAsync(1))
            .ReturnsAsync(new BillingPeriod { Id = 1, TargetMonth = 11, TargetYear = 2026 });

        var guard = new Guard
        {
            UserId = guardUserId,
            DormId = guardDormId,
            Dorm = new Dorm { Id = guardDormId, Name = "Dom A", Code = "A" }
        };
        _ewRepoMock.Setup(r => r.GetGuardByUserIdAsync(guardUserId)).ReturnsAsync(guard);

        var room = new Room
        {
            Id = 404,
            RoomNumber = "B404",
            Block = new Block
            {
                Id = 30,
                Code = "B4",
                DormId = 2,
                Dorm = new Dorm { Id = 2, Name = "Dom B", Code = "B" }
            }
        };
        _ewRepoMock.Setup(r => r.GetRoomsByBlockCodeAsync("B4")).ReturnsAsync(new List<Room> { room });

        // Act
        Func<Task> act = async () => await _sut.GetBlockFormDataAsync("B4", 1, guardUserId, isManager: false);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*Bạn không có quyền truy cập toà/phòng*");
    }

    [Fact]
    public async Task GetUsagesAsync_WhenGuardHasNoDorm_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        _ewRepoMock.Setup(r => r.GetGuardDormIdAsync(10)).ReturnsAsync((int?)null);

        // Act
        Func<Task> act = async () => await _sut.GetUsagesAsync(null, null, null, null, userId: 10, isManager: false);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        _ewRepoMock.Verify(
            r => r.GetGroupedUsagesAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<int?>()),
            Times.Never);
    }

    [Fact]
    public async Task GetBlockProgressAsync_WhenGuardHasNoDorm_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        _ewRepoMock.Setup(r => r.GetBillingPeriodByIdAsync(1))
            .ReturnsAsync(new BillingPeriod { Id = 1, TargetMonth = 11, TargetYear = 2026 });
        _ewRepoMock.Setup(r => r.GetAllActiveRoomsWithBlockAsync()).ReturnsAsync(new List<Room>());
        _ewRepoMock.Setup(r => r.GetReadingsByPeriodAsync(1)).ReturnsAsync(new List<MeterReading>());
        _ewRepoMock.Setup(r => r.GetGuardDormIdAsync(10)).ReturnsAsync((int?)null);

        // Act
        Func<Task> act = async () => await _sut.GetBlockProgressAsync(1, userId: 10, isManager: false);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task GetImportBatchesAsync_WhenGuardHasNoDorm_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        _ewRepoMock.Setup(r => r.GetGuardDormIdAsync(10)).ReturnsAsync((int?)null);

        // Act
        Func<Task> act = async () => await _sut.GetImportBatchesAsync(1, userId: 10, isManager: false);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        _ewRepoMock.Verify(r => r.GetImportBatchesAsync(It.IsAny<int?>(), It.IsAny<int?>()), Times.Never);
    }

    [Fact]
    public async Task GenerateExcelTemplateAsync_WhenGuardHasNoDorm_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        _ewRepoMock.Setup(r => r.GetGuardByUserIdAsync(10)).ReturnsAsync((Guard?)null);

        // Act
        Func<Task> act = async () => await _sut.GenerateExcelTemplateAsync(1, "B", "ALL", userId: 10, isManager: false);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task GetUsagesAsync_WhenGuardSuppliesDifferentDormId_ForcesGuardAssignedDorm()
    {
        // Arrange
        _ewRepoMock.Setup(r => r.GetGuardDormIdAsync(10)).ReturnsAsync(1);
        _ewRepoMock.Setup(r => r.GetGroupedUsagesAsync(null, null, null, null, null, null, 1))
            .ReturnsAsync(Array.Empty<object>());

        // Act
        await _sut.GetUsagesAsync(null, null, null, null, dormId: 2, userId: 10, isManager: false);

        // Assert
        _ewRepoMock.Verify(r => r.GetGroupedUsagesAsync(null, null, null, null, null, null, 1), Times.Once);
    }

    [Fact]
    public async Task GetBlockFormData_WhenBlockCodeExistsInMultipleDorms_ShouldReturnRoomsOfGuardDorm()
    {
        // Arrange
        int guardUserId = 20;
        int dormAId = 1;
        int dormBId = 2;

        _ewRepoMock.Setup(r => r.GetBillingPeriodByIdAsync(3))
            .ReturnsAsync(new BillingPeriod { Id = 3, TargetMonth = 11, TargetYear = 2026 });

        var dormA = new Dorm { Id = dormAId, Name = "Ký túc xá Dom A", Code = "A" };
        var dormB = new Dorm { Id = dormBId, Name = "Ký túc xá Dom B", Code = "B" };

        var guardB = new Guard { UserId = guardUserId, DormId = dormBId, Dorm = dormB };
        _ewRepoMock.Setup(r => r.GetGuardByUserIdAsync(guardUserId)).ReturnsAsync(guardB);

        var roomsWithSameBlockCode = new List<Room>
        {
            new() { Id = 1, RoomNumber = "101L", Block = new Block { Id = 1, Code = "101", DormId = dormAId, Dorm = dormA } },
            new() { Id = 2, RoomNumber = "101R", Block = new Block { Id = 1, Code = "101", DormId = dormAId, Dorm = dormA } },
            new() { Id = 11, RoomNumber = "101L", Block = new Block { Id = 5, Code = "101", DormId = dormBId, Dorm = dormB } },
            new() { Id = 12, RoomNumber = "101R", Block = new Block { Id = 5, Code = "101", DormId = dormBId, Dorm = dormB } }
        };
        _ewRepoMock.Setup(r => r.GetRoomsByBlockCodeAsync("101")).ReturnsAsync(roomsWithSameBlockCode);

        // Act
        var result = await _sut.GetBlockFormDataAsync("101", 3, guardUserId, isManager: false);

        // Assert
        result.Should().NotBeNull();
        result.ElectricityLeft.Should().NotBeNull();
        result.ElectricityLeft!.RoomId.Should().Be(11);
        result.ElectricityRight.Should().NotBeNull();
        result.ElectricityRight!.RoomId.Should().Be(12);
    }

    [Fact]
    public async Task ConfirmManualReading_WhenBlockCodeExistsInMultipleDorms_ShouldSaveToGuardDormRoomsOnly()
    {
        // Arrange
        int guardUserId = 20;
        int dormAId = 1;
        int dormBId = 2;

        var period = new BillingPeriod { Id = 3, Status = "Open", TargetMonth = 11, TargetYear = 2026 };
        _ewRepoMock.Setup(r => r.GetBillingPeriodByIdAsync(3)).ReturnsAsync(period);

        var dormA = new Dorm { Id = dormAId, Name = "Ký túc xá Dom A", Code = "A" };
        var dormB = new Dorm { Id = dormBId, Name = "Ký túc xá Dom B", Code = "B" };

        var guardB = new Guard { UserId = guardUserId, DormId = dormBId, Dorm = dormB };
        _ewRepoMock.Setup(r => r.GetGuardByUserIdAsync(guardUserId)).ReturnsAsync(guardB);

        var roomsWithSameBlockCode = new List<Room>
        {
            new() { Id = 1, RoomNumber = "101L", Block = new Block { Id = 1, Code = "101", DormId = dormAId, Dorm = dormA } },
            new() { Id = 11, RoomNumber = "101L", Block = new Block { Id = 5, Code = "101", DormId = dormBId, Dorm = dormB } }
        };
        _ewRepoMock.Setup(r => r.GetRoomsByBlockCodeAsync("101")).ReturnsAsync(roomsWithSameBlockCode);

        var request = new ManualConfirmRequestDto
        {
            BillingPeriodId = 3,
            BlockCode = "101",
            ElectricityLeftNewValue = 200
        };

        // Act
        var result = await _sut.ConfirmManualReadingAsync(guardUserId, request, isManager: false);

        // Assert
        result.Saved.Should().Be(1);
        _ewRepoMock.Verify(r => r.AddMeterReadingAsync(It.Is<MeterReading>(m => m.RoomId == 11 && m.RecordedById == guardUserId)), Times.Once);
        _ewRepoMock.Verify(r => r.AddMeterReadingAsync(It.Is<MeterReading>(m => m.RoomId == 1)), Times.Never);
    }
}
