using System.ComponentModel.DataAnnotations;

namespace FUD.DTOs.Facilities.Block
{
    /// <summary>
    /// Query parameters cho GET danh sách Block.
    /// Khớp với form search UI: DormName (text), BlockName (text), GenderType (dropdown)
    /// </summary>
    public class BlockQueryDto
    {
        public string? DormName { get; set; }       // ô tìm "Dorm Name" (text search)
        public string? BlockName { get; set; }      // ô tìm "Block Name" (text search)
        public string? GenderType { get; set; }     // dropdown "Tất cả" / "Male" / "Female"
        public string? Status { get; set; }         // "Active" | "Maintenance"

        [Range(1, int.MaxValue)]
        public int Page { get; set; } = 1;

        [Range(1, 100)]
        public int PageSize { get; set; } = 10;
    }
}
