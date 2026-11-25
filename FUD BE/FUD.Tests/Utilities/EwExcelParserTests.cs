using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClosedXML.Excel;
using FluentAssertions;
using FUD.DTOs.Utilities;
using FUD.Infrastructure.Excel;
using Xunit;

namespace FUD.Tests.Utilities;

/// <summary>
/// Unit tests cho EwExcelParser (Đọc & sinh file Excel mẫu Điện – Nước).
/// Bao phủ Happy Path, Boundary (2000 dòng) và Error Handling theo chuẩn AAA.
/// </summary>
public class EwExcelParserTests
{
    private static MemoryStream BuildCustomWorkbookStream(Action<IXLWorksheet> configureSheet)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Sheet1");
        ws.Cell(1, 1).Value = "Dorm";
        ws.Cell(1, 2).Value = "Block";
        ws.Cell(1, 3).Value = "Type";
        ws.Cell(1, 4).Value = "Date";
        ws.Cell(1, 5).Value = "L";
        ws.Cell(1, 6).Value = "R";
        ws.Cell(1, 7).Value = "Term";

        configureSheet(ws);

        var ms = new MemoryStream();
        wb.SaveAs(ms);
        ms.Position = 0;
        return ms;
    }

    private static MemoryStream BuildBulkRowsStream(int dataRowCount)
    {
        return BuildCustomWorkbookStream(ws =>
        {
            foreach (var i in Enumerable.Range(0, dataRowCount))
            {
                int row = i + 2;
                ws.Cell(row, 1).Value = "A";
                ws.Cell(row, 2).Value = $"A{100 + i}";
                ws.Cell(row, 3).Value = "E";
                ws.Cell(row, 4).Value = "30/11/2026";
                ws.Cell(row, 5).Value = 120m;
                ws.Cell(row, 6).Value = 130m;
                ws.Cell(row, 7).Value = 1;
            }
        });
    }

    [Fact]
    public void ParseExcelFile_WhenRowsContainValidElectricityAndWater_ParsesNormalizedMeterTypesAndValues()
    {
        // Arrange
        using var stream = BuildCustomWorkbookStream(ws =>
        {
            ws.Cell(2, 1).Value = " A ";
            ws.Cell(2, 2).Value = " A101 ";
            ws.Cell(2, 3).Value = "e";
            ws.Cell(2, 4).Value = "30/11/2026";
            ws.Cell(2, 5).Value = 245.5m;
            ws.Cell(2, 6).Value = 310m;

            ws.Cell(3, 1).Value = "B";
            ws.Cell(3, 2).Value = "B202";
            ws.Cell(3, 3).Value = "W";
            ws.Cell(3, 4).Value = "30/11/2026";
            ws.Cell(3, 5).Value = 18.25m;
        });

        // Act
        var rows = EwExcelParser.ParseExcelFile(stream);

        // Assert
        rows.Should().HaveCount(2);
        rows[0].RowIndex.Should().Be(2);
        rows[0].Dorm.Should().Be("A");
        rows[0].BlockCode.Should().Be("A101");
        rows[0].MeterType.Should().Be("Electricity");
        rows[0].LeftValue.Should().Be(245.5m);
        rows[0].RightValue.Should().Be(310m);
        rows[0].Status.Should().Be("Valid");

        rows[1].MeterType.Should().Be("Water");
        rows[1].LeftValue.Should().Be(18.25m);
        rows[1].RightValue.Should().BeNull();
    }

    [Fact]
    public void ParseExcelFile_WhenBlockCodeEmptyOrBothLeftAndRightBlank_SkipsUnfilledTemplateRows()
    {
        // Arrange
        using var stream = BuildCustomWorkbookStream(ws =>
        {
            // Row 2: empty BlockCode -> skipped
            ws.Cell(2, 1).Value = "A";
            ws.Cell(2, 5).Value = 100m;

            // Row 3: template row where Guard hasn't entered L or R yet -> skipped
            ws.Cell(3, 1).Value = "A";
            ws.Cell(3, 2).Value = "A101";
            ws.Cell(3, 3).Value = "E";

            // Row 4: valid filled row
            ws.Cell(4, 1).Value = "A";
            ws.Cell(4, 2).Value = "A102";
            ws.Cell(4, 3).Value = "W";
            ws.Cell(4, 5).Value = 25m;
        });

        // Act
        var rows = EwExcelParser.ParseExcelFile(stream);

        // Assert
        rows.Should().ContainSingle();
        rows[0].RowIndex.Should().Be(4);
        rows[0].BlockCode.Should().Be("A102");
    }

    [Fact]
    public void ParseExcelFile_WhenLeftAndRightContainNonNumericText_MarksRowAsErrorWithCombinedMessage()
    {
        // Arrange
        using var stream = BuildCustomWorkbookStream(ws =>
        {
            ws.Cell(2, 1).Value = "A";
            ws.Cell(2, 2).Value = "A101";
            ws.Cell(2, 3).Value = "E";
            ws.Cell(2, 5).Value = "abc";
            ws.Cell(2, 6).Value = "xyz";
        });

        // Act
        var rows = EwExcelParser.ParseExcelFile(stream);

        // Assert
        rows.Should().ContainSingle();
        rows[0].Status.Should().Be("Error");
        rows[0].ErrorMessage.Should().Contain("Chỉ số L không hợp lệ");
        rows[0].ErrorMessage.Should().Contain("Chỉ số R không hợp lệ");
    }

    [Theory]
    [InlineData(-1.0, 50.0)]
    [InlineData(50.0, -0.01)]
    public void ParseExcelFile_WhenLeftOrRightValueIsNegative_MarksRowAsError(double left, double right)
    {
        // Arrange
        using var stream = BuildCustomWorkbookStream(ws =>
        {
            ws.Cell(2, 1).Value = "A";
            ws.Cell(2, 2).Value = "A101";
            ws.Cell(2, 3).Value = "E";
            ws.Cell(2, 5).Value = (decimal)left;
            ws.Cell(2, 6).Value = (decimal)right;
        });

        // Act
        var rows = EwExcelParser.ParseExcelFile(stream);

        // Assert
        rows.Should().ContainSingle();
        rows[0].Status.Should().Be("Error");
        rows[0].ErrorMessage.Should().Contain("số âm");
    }

    [Fact]
    public void ParseExcelFile_WhenRowCountEqualsMaxAllowedBoundary2000_ParsesAllRowsSuccessfully()
    {
        // Arrange
        using var stream = BuildBulkRowsStream(EwExcelParser.MaxAllowedRows);

        // Act
        var rows = EwExcelParser.ParseExcelFile(stream);

        // Assert
        rows.Should().HaveCount(EwExcelParser.MaxAllowedRows);
    }

    [Fact]
    public void ParseExcelFile_WhenRowCountExceedsMaxAllowedBoundary2000_ThrowsArgumentException()
    {
        // Arrange
        using var stream = BuildBulkRowsStream(EwExcelParser.MaxAllowedRows + 1);

        // Act
        var act = () => EwExcelParser.ParseExcelFile(stream);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage($"*{EwExcelParser.MaxAllowedRows}*");
    }

    [Fact]
    public void GenerateTemplateFromData_WhenRowsProvided_ProducesValidWorkbookRoundTrippableByParser()
    {
        // Arrange
        var templateRows = new List<ExcelTemplateRowModel>
        {
            new() { Dorm = "A", Block = "A101", Type = "E", Date = "30/11/2026", L = 210m, R = null, Term = 2 },
            new() { Dorm = "A", Block = "A101", Type = "W", Date = "30/11/2026", L = null, R = null, Term = 2 }
        };

        // Act
        var bytes = EwExcelParser.GenerateTemplateFromData(templateRows);
        var parsedBack = EwExcelParser.ParseExcelFile(new MemoryStream(bytes));

        // Assert
        bytes.Should().NotBeEmpty();
        parsedBack.Should().ContainSingle(); // Second row has blank L & R so skipped
        parsedBack[0].BlockCode.Should().Be("A101");
        parsedBack[0].LeftValue.Should().Be(210m);
    }

    [Fact]
    public void GenerateTemplate_WhenCalled_CreatesSevenStandardHeaderColumns()
    {
        // Arrange & Act
        var bytes = EwExcelParser.GenerateTemplate();

        // Assert
        using var wb = new XLWorkbook(new MemoryStream(bytes));
        var ws = wb.Worksheet("Sheet1");
        ws.Cell(1, 1).GetString().Should().Be("Dorm");
        ws.Cell(1, 2).GetString().Should().Be("Block");
        ws.Cell(1, 3).GetString().Should().Be("Type");
        ws.Cell(1, 4).GetString().Should().Be("Date");
        ws.Cell(1, 5).GetString().Should().Be("L");
        ws.Cell(1, 6).GetString().Should().Be("R");
        ws.Cell(1, 7).GetString().Should().Be("Term");
    }
}
