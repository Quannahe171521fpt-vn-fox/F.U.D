using System;
using System.Collections.Generic;

namespace FUD.DTOs.Utilities
{
    public class StudentEwReportDto
    {
        public int TermId { get; set; }
        public string TermName { get; set; } = string.Empty;
        public string AcademicYear { get; set; } = string.Empty;
        public int BlockId { get; set; }
        public string BlockCode { get; set; } = string.Empty;
        public string BlockName { get; set; } = string.Empty;
        public string RoomNumber { get; set; } = string.Empty;
        public int NumberOfCheckedInResidents { get; set; }
        public List<string> Roommates { get; set; } = new();

        // Danh sách các dòng hiển thị từng tháng
        public List<StudentEwMonthlyRowDto> Rows { get; set; } = new();

        // Dòng tổng kết điện
        public decimal TotalElectricUsage { get; set; }
        public decimal ElectricQuota { get; set; } = 50m;
        public decimal TotalElectricOverUsage { get; set; }
        public decimal ElectricUnitPrice { get; set; } = 2500m;
        public decimal ElectricOverUsagePayment { get; set; }

        // Dòng tổng kết nước
        public decimal TotalWaterUsage { get; set; }
        public decimal WaterQuota { get; set; } = 3m;
        public decimal TotalWaterOverUsage { get; set; }
        public decimal WaterUnitPrice { get; set; } = 10000m;
        public decimal WaterOverUsagePayment { get; set; }
    }

    public class StudentEwMonthlyRowDto
    {
        public string Block { get; set; } = string.Empty;
        public string RoomNumber { get; set; } = string.Empty;
        public string MonthYear { get; set; } = string.Empty; // "9 - 2026"
        public int TargetMonth { get; set; }
        public int TargetYear { get; set; }
        public DateTime? RecordDate { get; set; }
        public string Type { get; set; } = string.Empty; // "Điện" hoặc "Nước"
        public string RawType { get; set; } = string.Empty; // "Electricity" hoặc "Water"
        public decimal RoomTotalUsage { get; set; }
        public decimal YourUsage { get; set; }
        public decimal QuotaMonthly { get; set; }
        public decimal YourOverUsage { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal YourOverUsagePayment { get; set; }
    }

    public class StudentTermOptionDto
    {
        public int TermId { get; set; }
        public string TermName { get; set; } = string.Empty;
        public string AcademicYear { get; set; } = string.Empty;
        public bool IsCurrent { get; set; }
    }
}

