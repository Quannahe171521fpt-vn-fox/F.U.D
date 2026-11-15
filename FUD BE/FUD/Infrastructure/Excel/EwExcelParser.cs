using ClosedXML.Excel;
using FUD.DTOs.Utilities;
using System.Globalization;

namespace FUD.Infrastructure.Excel
{
    public class EwExcelParser
    {
        public static List<ExcelPreviewRowDto> ParseExcelFile(Stream fileStream)
        {
            var results = new List<ExcelPreviewRowDto>();

            using var workbook = new XLWorkbook(fileStream);
            var worksheet = workbook.Worksheets.FirstOrDefault() 
                ?? throw new InvalidOperationException("File Excel không có sheet nào");

            var rows = worksheet.RowsUsed().Skip(1); // Bỏ qua header
            int index = 2;

            foreach (var row in rows)
            {
                var dorm = row.Cell(1).GetString().Trim();
                var blockCode = row.Cell(2).GetString().Trim();
                var typeStr = row.Cell(3).GetString().Trim().ToUpper();
                var dateStr = row.Cell(4).GetString().Trim();

                if (string.IsNullOrEmpty(blockCode))
                {
                    index++;
                    continue;
                }

                // Parse Left and Right values (chấp nhận ô trống cho các phòng không có chỉ số)
                decimal? leftVal = null;
                decimal? rightVal = null;
                string? errorMsg = null;

                var cellL = row.Cell(5);
                var cellR = row.Cell(6);

                if (!cellL.IsEmpty() && !string.IsNullOrWhiteSpace(cellL.GetString()))
                {
                    if (cellL.TryGetValue(out decimal lv))
                    {
                        leftVal = lv;
                    }
                    else if (decimal.TryParse(cellL.GetString().Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal plv))
                    {
                        leftVal = plv;
                    }
                    else
                    {
                        errorMsg = "Chỉ số L không hợp lệ (phải là số)";
                    }
                }

                if (!cellR.IsEmpty() && !string.IsNullOrWhiteSpace(cellR.GetString()))
                {
                    if (cellR.TryGetValue(out decimal rv))
                    {
                        rightVal = rv;
                    }
                    else if (decimal.TryParse(cellR.GetString().Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal prv))
                    {
                        rightVal = prv;
                    }
                    else
                    {
                        errorMsg = (errorMsg != null ? errorMsg + "; " : "") + "Chỉ số R không hợp lệ (phải là số)";
                    }
                }

                var item = new ExcelPreviewRowDto
                {
                    RowIndex = index,
                    Dorm = dorm,
                    BlockCode = blockCode,
                    MeterType = (typeStr == "E" || typeStr == "ELECTRICITY") ? "Electricity" : "Water",
                    DateStr = dateStr,
                    LeftValue = leftVal,
                    RightValue = rightVal,
                    Status = "Valid"
                };

                if (errorMsg != null)
                {
                    item.Status = "Error";
                    item.ErrorMessage = errorMsg;
                    results.Add(item);
                }
                else if ((leftVal.HasValue && leftVal.Value < 0) || (rightVal.HasValue && rightVal.Value < 0))
                {
                    item.Status = "Error";
                    item.ErrorMessage = "Chỉ số L hoặc R không được là số âm";
                    results.Add(item);
                }
                else if (!leftVal.HasValue && !rightVal.HasValue)
                {
                    // Dòng template Guard chưa nhập chỉ số → bỏ qua (skip), không báo lỗi
                    index++;
                    continue;
                }
                else
                {
                    results.Add(item);
                }

                index++;
            }

            return results;
        }

        /// <summary>
        /// Sinh file Excel template với các cột: Dorm | Block | Type | Date | L | R | Term
        /// Cột L và R:
        ///   - Nếu Guard đã nhập chỉ số kỳ này rồi (AlreadyRecorded=true) → hiển thị chỉ số đó (đã ghi nhận)
        ///   - Ngược lại → để trống để Guard nhập chỉ số MỚI vào
        /// PreviousValue (chỉ số cũ) KHÔNG có trên template, hệ thống tự tra DB khi import.
        /// </summary>
        public static byte[] GenerateTemplateFromData(List<ExcelTemplateRowModel> rows)
        {
            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Sheet1");

            // Header đúng y chang mẫu ảnh
            worksheet.Cell(1, 1).Value = "Dorm";
            worksheet.Cell(1, 2).Value = "Block";
            worksheet.Cell(1, 3).Value = "Type";
            worksheet.Cell(1, 4).Value = "Date";
            worksheet.Cell(1, 5).Value = "L";
            worksheet.Cell(1, 6).Value = "R";
            worksheet.Cell(1, 7).Value = "Term";

            var headerRange = worksheet.Range(1, 1, 1, 7);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Font.FontName = "Calibri";
            headerRange.Style.Font.FontSize = 11;
            headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#E2E8F0");
            headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            headerRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            headerRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            headerRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

            int rIndex = 2;
            foreach (var r in rows)
            {
                worksheet.Cell(rIndex, 1).Value = r.Dorm;
                worksheet.Cell(rIndex, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                worksheet.Cell(rIndex, 2).Value = r.Block;
                worksheet.Cell(rIndex, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                worksheet.Cell(rIndex, 3).Value = r.Type;
                worksheet.Cell(rIndex, 3).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                worksheet.Cell(rIndex, 4).Value = r.Date;
                worksheet.Cell(rIndex, 4).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                if (r.L.HasValue)
                {
                    worksheet.Cell(rIndex, 5).Value = r.L.Value;
                }
                else
                {
                    worksheet.Cell(rIndex, 5).Value = Blank.Value;
                }
                worksheet.Cell(rIndex, 5).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

                if (r.R.HasValue)
                {
                    worksheet.Cell(rIndex, 6).Value = r.R.Value;
                }
                else
                {
                    worksheet.Cell(rIndex, 6).Value = Blank.Value;
                }
                worksheet.Cell(rIndex, 6).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

                worksheet.Cell(rIndex, 7).Value = r.Term;
                worksheet.Cell(rIndex, 7).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                var rowRange = worksheet.Range(rIndex, 1, rIndex, 7);
                rowRange.Style.Font.FontName = "Calibri";
                rowRange.Style.Font.FontSize = 11;
                rowRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                rowRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

                rIndex++;
            }

            worksheet.Columns().AdjustToContents();
            worksheet.Column(1).Width = Math.Max(worksheet.Column(1).Width, 10);
            worksheet.Column(2).Width = Math.Max(worksheet.Column(2).Width, 12);
            worksheet.Column(3).Width = Math.Max(worksheet.Column(3).Width, 10);
            worksheet.Column(4).Width = Math.Max(worksheet.Column(4).Width, 14);
            worksheet.Column(5).Width = Math.Max(worksheet.Column(5).Width, 10);
            worksheet.Column(6).Width = Math.Max(worksheet.Column(6).Width, 10);
            worksheet.Column(7).Width = Math.Max(worksheet.Column(7).Width, 10);

            using var ms = new MemoryStream();
            workbook.SaveAs(ms);
            return ms.ToArray();
        }

        public static byte[] GenerateTemplate()
        {
            // Mẫu trống cơ bản nếu không có dữ liệu DB truyền vào
            var defaultRows = new List<ExcelTemplateRowModel>();
            return GenerateTemplateFromData(defaultRows);
        }
    }
}
