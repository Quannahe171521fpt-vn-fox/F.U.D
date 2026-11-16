using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using FUD.DTOs.Violations;
using FUD.Infrastructure.Email;
using FUD.Infrastructure.Storage;
using FUD.Models;
using FUD.Repositories.Interfaces.Violations;
using FUD.Services.Violations;
using Moq;
using Xunit;

namespace FUD.Tests.Violations;

public class ViewViolationHistoryServiceTests
{
    private readonly Mock<IViolationRepository> _violationRepoMock = new();
    private readonly Mock<IFileStorage> _fileStorageMock = new();
    private readonly Mock<IEmailSender> _emailSenderMock = new();

    private ViolationService CreateSut() => new(
        _violationRepoMock.Object,
        _fileStorageMock.Object,
        _emailSenderMock.Object);

    [Fact]
    public async Task GetStudentNoticesAsync_ReturnsNonDismissedViolationsAndAutoFinalizesExpired()
    {
        // Arrange
        var sut = CreateSut();
        var studentUserId = 50;
        var now = DateTime.UtcNow;

        var vPending = new Violation
        {
            Id = 1,
            Code = "VIO-001",
            StudentId = studentUserId,
            Status = "Recorded",
            InitialLevel = "Nhắc nhở",
            RequestDeadline = now.AddHours(10),
            ComplaintRequests = new List<ComplaintRequest>(),
            ViolationEvidences = new List<ViolationEvidence>()
        };

        var vExpired = new Violation
        {
            Id = 2,
            Code = "VIO-002",
            StudentId = studentUserId,
            Status = "Recorded",
            InitialLevel = "Cảnh cáo",
            RequestDeadline = now.AddHours(-1), // Expired deadline
            ComplaintRequests = new List<ComplaintRequest>(),
            ViolationEvidences = new List<ViolationEvidence>()
        };

        var vDismissed = new Violation
        {
            Id = 3,
            Code = "VIO-003",
            StudentId = studentUserId,
            Status = "Dismissed",
            ViolationEvidences = new List<ViolationEvidence>()
        };

        _violationRepoMock.Setup(r => r.GetViolationsAsync(null, studentUserId, null, null))
            .ReturnsAsync(new List<Violation> { vPending, vExpired, vDismissed });
        _violationRepoMock.Setup(r => r.UpdateViolationAsync(It.IsAny<Violation>())).Returns(Task.CompletedTask);

        // Act
        var result = await sut.GetStudentNoticesAsync(studentUserId);

        // Assert
        result.Should().HaveCount(2); // Dismissed is filtered out from notices
        result.Select(r => r.Code).Should().Contain(new[] { "VIO-001", "VIO-002" });

        vExpired.Status.Should().Be("Finalized");
        vExpired.FinalLevel.Should().Be("Cảnh cáo");
        _violationRepoMock.Verify(r => r.UpdateViolationAsync(vExpired), Times.Once);
    }

    [Fact]
    public async Task GetStudentHistoryAsync_FilterByStatusAndTerm_ReturnsMatchingViolations()
    {
        // Arrange
        var sut = CreateSut();
        var studentUserId = 50;

        var termFall = new Term { Id = 1, Name = "Fall 2026" };
        var termSummer = new Term { Id = 2, Name = "Summer 2026" };

        var resFall = new Residency { Id = 10, TermId = 1, Term = termFall };
        var resSummer = new Residency { Id = 20, TermId = 2, Term = termSummer };

        var v1 = new Violation
        {
            Id = 1,
            Code = "VIO-FALL-1",
            StudentId = studentUserId,
            Status = "Finalized",
            FinalLevel = "Cảnh cáo",
            Residency = resFall,
            ViolationEvidences = new List<ViolationEvidence>()
        };

        var v2 = new Violation
        {
            Id = 2,
            Code = "VIO-FALL-2",
            StudentId = studentUserId,
            Status = "Recorded",
            Residency = resFall,
            ViolationEvidences = new List<ViolationEvidence>()
        };

        var v3 = new Violation
        {
            Id = 3,
            Code = "VIO-SUMMER-1",
            StudentId = studentUserId,
            Status = "Finalized",
            FinalLevel = "Nhắc nhở",
            Residency = resSummer,
            ViolationEvidences = new List<ViolationEvidence>()
        };

        _violationRepoMock.Setup(r => r.GetViolationsAsync(null, studentUserId, null, null))
            .ReturnsAsync(new List<Violation> { v1, v2, v3 });

        // Act - Lọc theo Term = "Fall" và Status = "Finalized"
        var result = await sut.GetStudentHistoryAsync(studentUserId, term: "Fall", status: "Finalized");

        // Assert
        result.Should().HaveCount(1);
        result[0].Code.Should().Be("VIO-FALL-1");
        result[0].Status.Should().Be("Finalized");
        result[0].TermName.Should().Be("Fall 2026");
    }

    [Fact]
    public async Task GetStudentHistoryAsync_NoFilters_ReturnsAllViolationsOfStudent()
    {
        // Arrange
        var sut = CreateSut();
        var studentUserId = 60;

        var violations = new List<Violation>
        {
            new() { Id = 1, Code = "VIO-1", StudentId = 60, Status = "Finalized", ViolationEvidences = new List<ViolationEvidence>() },
            new() { Id = 2, Code = "VIO-2", StudentId = 60, Status = "Dismissed", ViolationEvidences = new List<ViolationEvidence>() }
        };

        _violationRepoMock.Setup(r => r.GetViolationsAsync(null, studentUserId, null, null))
            .ReturnsAsync(violations);

        // Act
        var result = await sut.GetStudentHistoryAsync(studentUserId, term: null, status: null);

        // Assert
        result.Should().HaveCount(2);
        result.Select(r => r.Code).Should().Contain(new[] { "VIO-1", "VIO-2" });
    }
}
