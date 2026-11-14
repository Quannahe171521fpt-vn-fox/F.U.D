namespace FUD.DTOs.Utilities
{
    public class EwUsageResponseDto
    {
        public int Id { get; set; }
        public string Block { get; set; }
        public string LoaiSuDung { get; set; }
        public DateTime? NgayTao { get; set; }
        public string HocKy { get; set; }
        public string MeterNumberDisplay { get; set; }
        public decimal TotalConsumption { get; set; }
        public string Donvi { get; set; }
    }
}
