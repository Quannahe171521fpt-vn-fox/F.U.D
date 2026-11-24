using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using FUD.DTOs.Facilities.News;
using FUD.Models;
using FUD.Repositories.Interfaces.Facilities;
using FUD.Services.Facilities;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace FUD.Tests.Facilities;

public class NewsServiceTests
{
    private readonly Mock<INewsRepository> _newsRepoMock = new();

    private FudContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<FudContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new FudContext(options);
    }

    private NewsService CreateSut(FudContext context) => new(_newsRepoMock.Object, context);

    [Fact]
    public async Task GetAllAsync_ReturnsPagedResultWithCorrectAuditLogTimestamps()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var sut = CreateSut(context);

        var news1 = new News
        {
            Id = 1,
            AuthorId = 2,
            Title = "Thông báo bảo trì KTX",
            Content = "Nội dung thông báo bảo trì...",
            Category = "News",
            Status = "Draft",
            PublishedTime = null
        };
        var news2 = new News
        {
            Id = 2,
            AuthorId = 2,
            Title = "Chào đón tân sinh viên K20",
            Content = "Nội dung chào đón tân sinh viên...",
            Category = "Event",
            Status = "Published",
            PublishedTime = DateTime.UtcNow.AddDays(-2)
        };

        var query = new NewsQueryDto { Page = 1, PageSize = 10 };
        _newsRepoMock.Setup(r => r.GetAllAsync(query))
            .ReturnsAsync(((IEnumerable<News>)new List<News> { news1, news2 }, 2));

        var createLog = new AuditLog
        {
            Id = 1,
            UserId = 2,
            ActionType = "CREATE",
            TargetEntity = "News",
            TargetId = "1",
            CreatedAt = DateTime.UtcNow.AddDays(-5)
        };
        var unpublishLog = new AuditLog
        {
            Id = 2,
            UserId = 2,
            ActionType = "UNPUBLISH",
            TargetEntity = "News",
            TargetId = "1",
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        };
        await context.AuditLogs.AddRangeAsync(createLog, unpublishLog);
        await context.SaveChangesAsync();

        // Act
        var result = await sut.GetAllAsync(query);

        // Assert
        result.Should().NotBeNull();
        result.TotalCount.Should().Be(2);
        result.Items.Should().HaveCount(2);

        var item1 = result.Items.First(n => n.Id == 1);
        item1.Title.Should().Be("Thông báo bảo trì KTX");
        item1.CreatedAt.Should().BeCloseTo(createLog.CreatedAt, TimeSpan.FromSeconds(1));
        item1.HiddenAt.Should().BeCloseTo(unpublishLog.CreatedAt, TimeSpan.FromSeconds(1));

        var item2 = result.Items.First(n => n.Id == 2);
        item2.Title.Should().Be("Chào đón tân sinh viên K20");
        item2.Status.Should().Be("Published");
    }

    [Fact]
    public async Task GetByIdAsync_WhenNewsExists_ReturnsNewsDto()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var sut = CreateSut(context);

        var news = new News
        {
            Id = 5,
            AuthorId = 2,
            Title = "Quy định giờ giấc ký túc xá",
            Content = "Sinh viên cần về trước 23h...",
            Category = "Rule",
            Status = "Published",
            PublishedTime = DateTime.UtcNow.AddDays(-10),
            NewsAttachments = new List<NewsAttachment>
            {
                new() { Id = 1, NewsId = 5, FileUrl = "https://example.com/rules.pdf", FileType = "pdf" }
            }
        };
        _newsRepoMock.Setup(r => r.GetByIdAsync(5)).ReturnsAsync(news);

        // Act
        var result = await sut.GetByIdAsync(5);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(5);
        result.Title.Should().Be("Quy định giờ giấc ký túc xá");
        result.Attachments.Should().HaveCount(1);
        result.Attachments.First().FileType.Should().Be("pdf");
    }

    [Fact]
    public async Task GetByIdAsync_WhenNewsDoesNotExist_ReturnsNull()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var sut = CreateSut(context);

        _newsRepoMock.Setup(r => r.GetByIdAsync(999)).ReturnsAsync((News?)null);

        // Act
        var result = await sut.GetByIdAsync(999);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task CreateAsync_ValidInputWithPublishAction_CreatesPublishedNewsAndSendsNotifications()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var sut = CreateSut(context);

        var studentUser = new User
        {
            Id = 101,
            Code = "SE101",
            FullName = "Sinh vien A",
            Email = "se101@fpt.edu.vn",
            Gender = "Male",
            AccountStatus = "Active"
        };
        var student = new Student { UserId = 101, User = studentUser };
        await context.Users.AddAsync(studentUser);
        await context.Students.AddAsync(student);
        await context.SaveChangesAsync();

        var dto = new CreateNewsDto
        {
            Title = "Thông báo đăng ký nội trú mới",
            Content = "Nội dung đăng ký nội trú kỳ Fall...",
            Category = "News",
            Action = "Publish",
            AttachmentUrls = new List<string> { "https://cdn.example.com/file1.png" }
        };

        var createdNews = new News
        {
            Id = 10,
            AuthorId = 2,
            Title = dto.Title,
            Content = dto.Content,
            Category = dto.Category,
            Status = "Published",
            PublishedTime = DateTime.UtcNow
        };

        _newsRepoMock.Setup(r => r.AddAsync(It.IsAny<News>()))
            .ReturnsAsync(createdNews);
        _newsRepoMock.Setup(r => r.AddAttachmentAsync(It.IsAny<NewsAttachment>()))
            .Returns(Task.CompletedTask);
        _newsRepoMock.Setup(r => r.GetByIdAsync(10))
            .ReturnsAsync(createdNews);

        // Act
        var result = await sut.CreateAsync(dto, userId: 2);

        // Assert
        result.Should().NotBeNull();
        result.Title.Should().Be(dto.Title);
        result.Status.Should().Be("Published");

        _newsRepoMock.Verify(r => r.AddAsync(It.Is<News>(n =>
            n.Title == dto.Title &&
            n.Status == "Published" &&
            n.PublishedTime != null)), Times.Once);

        _newsRepoMock.Verify(r => r.AddAttachmentAsync(It.Is<NewsAttachment>(a =>
            a.NewsId == 10 &&
            a.FileUrl == "https://cdn.example.com/file1.png" &&
            a.FileType == "png")), Times.Once);

        var notifications = await context.Notifications.ToListAsync();
        notifications.Should().ContainSingle();
        notifications.First().UserId.Should().Be(101);
        notifications.First().ReferenceType.Should().Be("News");

        var auditLogs = await context.AuditLogs.ToListAsync();
        auditLogs.Should().Contain(a => a.TargetEntity == "News" && a.ActionType == "CREATE" && a.TargetId == "10");
    }

    [Theory]
    [InlineData("", "Nội dung có thực")]
    [InlineData("   ", "Nội dung có thực")]
    [InlineData("Tiêu đề hợp lệ", "")]
    [InlineData("Tiêu đề hợp lệ", "   ")]
    public async Task CreateAsync_InvalidEmptyTitleOrContent_ThrowsArgumentException(string title, string content)
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var sut = CreateSut(context);

        var dto = new CreateNewsDto
        {
            Title = title,
            Content = content,
            Category = "News",
            Action = "Draft"
        };

        // Act
        var act = async () => await sut.CreateAsync(dto, userId: 2);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*không được để trống*");
        _newsRepoMock.Verify(r => r.AddAsync(It.IsAny<News>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_ValidInput_UpdatesNewsAndAuditLog()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var sut = CreateSut(context);

        var existingNews = new News
        {
            Id = 3,
            AuthorId = 2,
            Title = "Tiêu đề cũ",
            Content = "Nội dung cũ",
            Category = "News",
            Status = "Draft",
            PublishedTime = null
        };

        _newsRepoMock.Setup(r => r.GetByIdAsync(3)).ReturnsAsync(existingNews);
        _newsRepoMock.Setup(r => r.UpdateAsync(It.IsAny<News>())).Returns(Task.CompletedTask);
        _newsRepoMock.Setup(r => r.RemoveAttachmentsByNewsIdAsync(3)).Returns(Task.CompletedTask);
        _newsRepoMock.Setup(r => r.AddAttachmentAsync(It.IsAny<NewsAttachment>())).Returns(Task.CompletedTask);

        var updateDto = new UpdateNewsDto
        {
            Title = "Tiêu đề mới đã chỉnh sửa",
            Content = "Nội dung mới chi tiết",
            Category = "Event",
            Action = "Publish",
            AttachmentUrls = new List<string> { "https://example.com/doc.pdf" }
        };

        // Act
        var result = await sut.UpdateAsync(3, updateDto, userId: 2);

        // Assert
        result.Should().NotBeNull();
        result.Title.Should().Be("Tiêu đề mới đã chỉnh sửa");
        result.Status.Should().Be("Published");

        _newsRepoMock.Verify(r => r.UpdateAsync(It.Is<News>(n =>
            n.Id == 3 &&
            n.Title == "Tiêu đề mới đã chỉnh sửa" &&
            n.Status == "Published")), Times.Once);
        _newsRepoMock.Verify(r => r.RemoveAttachmentsByNewsIdAsync(3), Times.Once);

        var auditLogs = await context.AuditLogs.ToListAsync();
        auditLogs.Should().Contain(a => a.ActionType == "UPDATE" && a.TargetId == "3");
    }

    [Fact]
    public async Task UpdateAsync_WhenNewsNotFound_ThrowsKeyNotFoundException()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var sut = CreateSut(context);

        _newsRepoMock.Setup(r => r.GetByIdAsync(404)).ReturnsAsync((News?)null);

        var updateDto = new UpdateNewsDto
        {
            Title = "Bản tin không tồn tại",
            Content = "Nội dung test"
        };

        // Act
        var act = async () => await sut.UpdateAsync(404, updateDto, userId: 2);

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("*không tồn tại*");
    }

    [Fact]
    public async Task DeleteAsync_ExistingNews_SetsStatusToDraftSoftDelete()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var sut = CreateSut(context);

        var news = new News
        {
            Id = 8,
            AuthorId = 2,
            Title = "Tin tức cần gỡ",
            Status = "Published",
            PublishedTime = DateTime.UtcNow
        };

        _newsRepoMock.Setup(r => r.GetByIdAsync(8)).ReturnsAsync(news);
        _newsRepoMock.Setup(r => r.UpdateAsync(It.IsAny<News>())).Returns(Task.CompletedTask);

        // Act
        await sut.DeleteAsync(8, userId: 2);

        // Assert
        news.Status.Should().Be("Draft");
        news.PublishedTime.Should().BeNull();
        _newsRepoMock.Verify(r => r.UpdateAsync(It.Is<News>(n => n.Id == 8 && n.Status == "Draft")), Times.Once);

        var auditLogs = await context.AuditLogs.ToListAsync();
        auditLogs.Should().Contain(a => a.ActionType == "UNPUBLISH" && a.TargetId == "8");
    }

    [Fact]
    public async Task PublishAsync_ExistingDraftNews_ChangesStatusToPublishedAndNotifiesStudents()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var sut = CreateSut(context);

        var studentUser = new User
        {
            Id = 202,
            Code = "SE202",
            FullName = "Sinh vien B",
            Email = "se202@fpt.edu.vn",
            Gender = "Female",
            AccountStatus = "Active"
        };
        var student = new Student { UserId = 202, User = studentUser };
        await context.Students.AddAsync(student);
        await context.SaveChangesAsync();

        var news = new News
        {
            Id = 12,
            AuthorId = 2,
            Title = "Bản tin chuyển sang xuất bản",
            Content = "Nội dung bản tin đã duyệt...",
            Status = "Draft",
            PublishedTime = null
        };

        _newsRepoMock.Setup(r => r.GetByIdAsync(12)).ReturnsAsync(news);
        _newsRepoMock.Setup(r => r.UpdateAsync(It.IsAny<News>())).Returns(Task.CompletedTask);

        // Act
        await sut.PublishAsync(12, userId: 2);

        // Assert
        news.Status.Should().Be("Published");
        news.PublishedTime.Should().NotBeNull();
        _newsRepoMock.Verify(r => r.UpdateAsync(It.Is<News>(n => n.Id == 12 && n.Status == "Published")), Times.Once);

        var notis = await context.Notifications.ToListAsync();
        notis.Should().Contain(n => n.UserId == 202 && n.ReferenceId == "12");

        var auditLogs = await context.AuditLogs.ToListAsync();
        auditLogs.Should().Contain(a => a.ActionType == "PUBLISH" && a.TargetId == "12");
    }

    [Fact]
    public async Task UnpublishAsync_ExistingPublishedNews_ChangesStatusToDraft()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var sut = CreateSut(context);

        var news = new News
        {
            Id = 15,
            AuthorId = 2,
            Title = "Bản tin cần ẩn",
            Status = "Published",
            PublishedTime = DateTime.UtcNow
        };

        _newsRepoMock.Setup(r => r.GetByIdAsync(15)).ReturnsAsync(news);
        _newsRepoMock.Setup(r => r.UpdateAsync(It.IsAny<News>())).Returns(Task.CompletedTask);

        // Act
        await sut.UnpublishAsync(15, userId: 2);

        // Assert
        news.Status.Should().Be("Draft");
        news.PublishedTime.Should().BeNull();
        _newsRepoMock.Verify(r => r.UpdateAsync(It.Is<News>(n => n.Id == 15 && n.Status == "Draft")), Times.Once);

        var auditLogs = await context.AuditLogs.ToListAsync();
        auditLogs.Should().Contain(a => a.ActionType == "UNPUBLISH" && a.TargetId == "15");
    }

    [Fact]
    public async Task CreateAsync_WhenAttachmentUrlHasJavascriptScheme_ThrowsArgumentException()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var sut = CreateSut(context);

        var dto = new CreateNewsDto
        {
            Title = "Bản tin thử nghiệm",
            Content = "Nội dung bản tin",
            Category = "News",
            Action = "Draft",
            AttachmentUrls = new List<string> { "javascript:alert(1)" }
        };

        // Act
        var act = async () => await sut.CreateAsync(dto, userId: 2);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*https://*");
        _newsRepoMock.Verify(r => r.AddAsync(It.IsAny<News>()), Times.Never);
        _newsRepoMock.Verify(r => r.AddAttachmentAsync(It.IsAny<NewsAttachment>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_WhenAttachmentUrlIsValidHttps_SavesNormalizedExtension()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var sut = CreateSut(context);

        var dto = new CreateNewsDto
        {
            Title = "Bản tin đính kèm PDF",
            Content = "Nội dung bản tin",
            Category = "News",
            Action = "Draft",
            AttachmentUrls = new List<string> { "https://res.cloudinary.com/fud/raw/upload/v1/Notice.PDF?dl=1" }
        };

        var createdNews = new News
        {
            Id = 99,
            AuthorId = 2,
            Title = dto.Title,
            Content = dto.Content,
            Category = "News",
            Status = "Draft"
        };

        _newsRepoMock.Setup(r => r.AddAsync(It.IsAny<News>())).ReturnsAsync(createdNews);
        _newsRepoMock.Setup(r => r.AddAttachmentAsync(It.IsAny<NewsAttachment>())).Returns(Task.CompletedTask);
        _newsRepoMock.Setup(r => r.GetByIdAsync(99)).ReturnsAsync(createdNews);

        // Act
        await sut.CreateAsync(dto, userId: 2);

        // Assert
        _newsRepoMock.Verify(r => r.AddAttachmentAsync(It.Is<NewsAttachment>(a =>
            a.NewsId == 99 &&
            a.FileUrl == "https://res.cloudinary.com/fud/raw/upload/v1/Notice.PDF?dl=1" &&
            a.FileType == "pdf")), Times.Once);
    }
}
