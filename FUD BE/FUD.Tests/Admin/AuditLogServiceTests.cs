using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using FUD.Common;
using FUD.DTOs.Account;
using FUD.Repositories.Interfaces;
using FUD.Services.Admin;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace FUD.Tests.Admin;

public class AuditLogServiceTests
{
    private readonly Mock<IAuditLogRepository> _auditRepoMock = new();
    private readonly Mock<ILogger<AuditLogService>> _loggerMock = new();

    private AuditLogService CreateSut() => new(_auditRepoMock.Object, _loggerMock.Object);

    [Fact]
    public async Task GetLogsAsync_ReturnsPagedResult_WhenRepositoryReturnsData()
    {
        // Arrange
        var query = new AuditLogQueryDto { Page = 1, PageSize = 20, LogType = "Impersonation" };
        var sampleItems = new List<AuditLogItemDto>
        {
            new()
            {
                Id = 1,
                LogType = "Impersonation",
                ActionType = "IMPERSONATE_STUDENT",
                ActorName = "Quản lý KTX",
                TargetUserName = "Nguyễn Văn Sinh Viên",
                CreatedAt = DateTime.UtcNow
            }
        };

        _auditRepoMock
            .Setup(r => r.GetUnifiedLogsAsync(query))
            .ReturnsAsync((sampleItems, 1));

        var sut = CreateSut();

        // Act
        var result = await sut.GetLogsAsync(query);

        // Assert
        result.Should().NotBeNull();
        result.TotalCount.Should().Be(1);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(20);
        result.Items.Should().HaveCount(1);
        _auditRepoMock.Verify(r => r.GetUnifiedLogsAsync(query), Times.Once);
    }

    [Fact]
    public async Task GetLogsAsync_NormalizesPagination_WhenPageIsZeroOrNegative()
    {
        // Arrange
        var query = new AuditLogQueryDto { Page = -5, PageSize = 0 };
        _auditRepoMock
            .Setup(r => r.GetUnifiedLogsAsync(query))
            .ReturnsAsync((new List<AuditLogItemDto>(), 0));

        var sut = CreateSut();

        // Act
        var result = await sut.GetLogsAsync(query);

        // Assert
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(20);
    }

    [Fact]
    public async Task GetDetailAsync_ReturnsDetail_WhenRecordExists()
    {
        // Arrange
        var detail = new AuditLogDetailDto
        {
            Id = 10,
            LogType = "RoleChange",
            ActionType = "Grant",
            OldValue = "Student",
            NewValue = "Student,Manager",
            ActorName = "Admin",
            CreatedAt = DateTime.UtcNow
        };

        _auditRepoMock
            .Setup(r => r.GetDetailAsync(10, "RoleChange"))
            .ReturnsAsync(detail);

        var sut = CreateSut();

        // Act
        var result = await sut.GetDetailAsync(10, "RoleChange");

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(10);
        result.LogType.Should().Be("RoleChange");
        result.NewValue.Should().Be("Student,Manager");
    }

    [Fact]
    public async Task GetDetailAsync_ReturnsNull_WhenRecordDoesNotExist()
    {
        // Arrange
        _auditRepoMock
            .Setup(r => r.GetDetailAsync(999, null))
            .ReturnsAsync((AuditLogDetailDto?)null);

        var sut = CreateSut();

        // Act
        var result = await sut.GetDetailAsync(999, null);

        // Assert
        result.Should().BeNull();
    }
}

