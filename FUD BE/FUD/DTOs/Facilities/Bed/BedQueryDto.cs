using System.ComponentModel.DataAnnotations;

namespace FUD.DTOs.Facilities.Bed
{
    /// <summary>
    /// Query parameters cho GET danh sách Bed có filter + phân trang.
    /// </summary>
    public class BedQueryDto
    {
        public int?    RoomId   { get; set; }   // lọc theo phòng
        public string? BedCode  { get; set; }   // tìm theo mã giường
        public string? Status   { get; set; }   // "Free"|"Holding"|"Pending Transfer"|"Occupied"|"Maintenance"

        [Range(1, int.MaxValue)]
        public int Page     { get; set; } = 1;

        [Range(1, 100)]
        public int PageSize { get; set; } = 10;
    }
}

