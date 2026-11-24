using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using ClosedXML.Excel;
using FluentAssertions;
using FUD.Infrastructure.Excel;
using FUD.Infrastructure.Storage;
using FUD.Settings.pattern;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Xunit;

namespace FUD.Tests.Infrastructure;

public class UploadValidationTests
{
    private static CloudinaryStorage CreateStorage()
    {
        var options = Options.Create(new CloudinarySettings());
        var config = new ConfigurationBuilder().Build();
        return new CloudinaryStorage(options, config);
    }

    private static IFormFile CreateFormFile(byte[] content, string fileName = "test.jpg", string contentType = "image/jpeg")
    {
        var stream = new MemoryStream(content);
        return new FormFile(stream, 0, content.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
    }

    [Fact]
    public async Task UploadImageAsync_WhenPayloadIsFakeJpgText_ThrowsArgumentException()
    {
        // Arrange
        var storage = CreateStorage();
        var fakeBytes = Encoding.UTF8.GetBytes("<html><script>alert(1)</script></html>");
        var file = CreateFormFile(fakeBytes, "exploit.jpg", "image/jpeg");

        // Act
        var act = () => storage.UploadImageAsync(file, "violations");

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Chỉ chấp nhận tệp ảnh hợp lệ*");
    }

    [Fact]
    public async Task UploadImageAsync_WhenPayloadExceeds10MB_ThrowsArgumentException()
    {
        // Arrange
        var storage = CreateStorage();
        var oversized = new byte[CloudinaryStorage.MaxImageBytes + 1];
        oversized[0] = 0xFF;
        oversized[1] = 0xD8;
        oversized[2] = 0xFF;
        var file = CreateFormFile(oversized, "huge.jpg", "image/jpeg");

        // Act
        var act = () => storage.UploadImageAsync(file, "violations");

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*10MB*");
    }

    [Fact]
    public async Task UploadImagesAsync_WhenMoreThan5Files_ThrowsArgumentException()
    {
        // Arrange
        var storage = CreateStorage();
        var validJpgHeader = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10 };
        var files = new List<IFormFile>
        {
            CreateFormFile(validJpgHeader, "1.jpg"),
            CreateFormFile(validJpgHeader, "2.jpg"),
            CreateFormFile(validJpgHeader, "3.jpg"),
            CreateFormFile(validJpgHeader, "4.jpg"),
            CreateFormFile(validJpgHeader, "5.jpg"),
            CreateFormFile(validJpgHeader, "6.jpg")
        };

        // Act
        var act = () => storage.UploadImagesAsync(files, "violations");

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Tối đa 5 ảnh*");
    }

    [Fact]
    public async Task UploadImageAsync_WhenFolderHasTraversal_ThrowsArgumentException()
    {
        // Arrange
        var storage = CreateStorage();
        var validJpgHeader = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10 };
        var file = CreateFormFile(validJpgHeader, "1.jpg");

        // Act
        var act = () => storage.UploadImageAsync(file, "../outside-folder");

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Thư mục lưu trữ ảnh không hợp lệ*");
    }

    [Fact]
    public void EwExcelParser_WhenRowCountExceedsLimit_ThrowsArgumentException()
    {
        // Arrange
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Sheet1");
        ws.Cell(1, 1).Value = "Dorm";
        ws.Cell(1, 2).Value = "Block";
        ws.Cell(1, 3).Value = "Type";
        ws.Cell(1, 4).Value = "Date";
        ws.Cell(1, 5).Value = "L";
        ws.Cell(1, 6).Value = "R";

        for (int r = 2; r <= EwExcelParser.MaxAllowedRows + 2; r++)
        {
            ws.Cell(r, 1).Value = "Dom A";
            ws.Cell(r, 2).Value = "A101";
            ws.Cell(r, 3).Value = "E";
            ws.Cell(r, 5).Value = 100;
        }

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        ms.Position = 0;

        // Act
        var act = () => EwExcelParser.ParseExcelFile(ms);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage($"*vượt quá giới hạn tối đa {EwExcelParser.MaxAllowedRows} dòng*");
    }
}
