using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using FUD.Infrastructure.Email;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace FUD.Tests.Infrastructure;

public class GmailSmtpSenderTests
{
    [Fact]
    public async Task SendEmailAsync_WhenConfigMissing_DoesNotThrowAndLogsWarning()
    {
        // Arrange
        var config = new ConfigurationBuilder().Build();
        var loggerMock = new Mock<ILogger<GmailSmtpSender>>();
        var sender = new GmailSmtpSender(config, loggerMock.Object);

        // Act
        var act = () => sender.SendEmailAsync("student@fpt.edu.vn", "Test", "<p>Hi</p>");

        // Assert
        await act.Should().NotThrowAsync();
        loggerMock.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
        loggerMock.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    [Fact]
    public async Task SendEmailAsync_WhenPlaceholderCredentialsConfigured_DoesNotSend()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Email:SenderEmail"] = "youremail@gmail.com",
                ["Email:SenderPassword"] = "your-gmail-app-password"
            })
            .Build();
        var loggerMock = new Mock<ILogger<GmailSmtpSender>>();
        var sender = new GmailSmtpSender(config, loggerMock.Object);

        // Act
        var act = () => sender.SendEmailAsync("student@fpt.edu.vn", "Test", "<p>Hi</p>");

        // Assert
        await act.Should().NotThrowAsync();
        loggerMock.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
        loggerMock.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    [Fact]
    public async Task SendEmailAsync_WhenRecipientEmpty_ReturnsImmediately()
    {
        // Arrange
        var config = new ConfigurationBuilder().Build();
        var loggerMock = new Mock<ILogger<GmailSmtpSender>>();
        var sender = new GmailSmtpSender(config, loggerMock.Object);

        // Act
        var act = () => sender.SendEmailAsync(string.Empty, "Test", "<p>Hi</p>");

        // Assert
        await act.Should().NotThrowAsync();
        loggerMock.Invocations.Should().BeEmpty();
    }
}
