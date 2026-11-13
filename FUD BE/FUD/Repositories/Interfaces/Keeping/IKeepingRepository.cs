using FUD.Models;

namespace FUD.Repositories.Interfaces.Keeping
{
    /// <summary>Truy vấn dữ liệu cho module Giữ phòng (UC02).</summary>
    public interface IKeepingRepository
    {
        /// <summary>
        /// Lấy cư trú đang hiệu lực (Residency Active) của sinh viên, kèm Bed → Room → Block → Dorm.
        /// Dùng cho bước kiểm tra điều kiện (GB-06) và để lấy giường hiện tại.
        /// </summary>
        Task<Residency?> GetActiveResidencyAsync(int studentId);

        /// <summary>
        /// Lấy kỳ Giữ phòng (Registration_Period loại Keeping) đang mở của học kỳ kế tiếp.
        /// Kỳ kế tiếp được xác định theo Registration_Period đang Active, sau đó kiểm tra
        /// Term của kỳ đó nằm sau Term hiện tại (GB-04).
        /// </summary>
        Task<(RegistrationPeriod? Period, Term? NextTerm, Term? CurrentTerm)>
            GetOpenKeepingPeriodAsync(int studentId, DateOnly today);

        /// <summary>Kiểm tra Term kế tiếp đã có bảng giá Published hay chưa (GB-16).</summary>
        Task<bool> IsPriceListPublishedAsync(int termId);

        /// <summary>Kiểm tra sinh viên còn hóa đơn Overdue hay không (GB-05, GB-11).</summary>
        Task<bool> HasOverdueInvoiceAsync(int studentId);

        /// <summary>Kiểm tra sinh viên còn hóa đơn Unpaid (chưa quá hạn) hay không (GB-05, GB-11).</summary>
        Task<bool> HasUnpaidInvoiceAsync(int studentId);

        /// <summary>Kiểm tra sinh viên đã có yêu cầu giữ phòng Completed cho học kỳ kế tiếp hay chưa.</summary>
        Task<bool> HasCompletedKeepingRequestAsync(int studentId, int termId);

        /// <summary>Lấy yêu cầu giữ phòng Completed của sinh viên cho học kỳ kế tiếp (dùng cho idempotent).</summary>
        Task<Request?> GetCompletedKeepingRequestAsync(int studentId, int termId);

        /// <summary>Lấy một yêu cầu giữ phòng theo Id kèm quan hệ.</summary>
        Task<Request?> GetKeepingRequestByIdAsync(int requestId);

        /// <summary>Lấy giá giữ giường (Bed_Price) của giường trong học kỳ kế tiếp để hiển thị.</summary>
        Task<decimal?> GetKeepingBedPriceAsync(int bedId, int termId);

        /// <summary>Lấy số tháng tính phí giữ phòng từ Bed_Price (TargetMonth).</summary>
        Task<int> GetKeepingMonthsAsync(int bedId, int termId);

        /// <summary>Kiểm tra giường có còn trống cho học kỳ kế tiếp hay không (chống race GB-03).</summary>
        Task<bool> IsBedOccupiedForTermAsync(int bedId, int termId);

        /// <summary>Kiểm tra sinh viên đã có cư trú Active/Reserved cho học kỳ kế tiếp hay chưa (GB-03).</summary>
        Task<bool> HasResidencyForTermAsync(int studentId, int termId);

        /// <summary>Lấy danh sách cư dân Active của kỳ hiện tại kèm giường (dùng cho job đóng kỳ giữ phòng).</summary>
        Task<List<Residency>> GetActiveResidenciesForTermAsync(int termId);

        /// <summary>Lấy danh sách sinh viên đã giữ phòng (Request Keeping Completed) cho kỳ kế tiếp.</summary>
        Task<HashSet<int>> GetStudentsWithCompletedKeepingAsync(int termId);

        /// <summary>Lấy cư trú của sinh viên theo học kỳ kèm Bed → Room → Block → Dorm.</summary>
        Task<Residency?> GetResidencyByStudentAndTermAsync(int studentId, int termId);

        /// <summary>Bắt đầu transaction để thao tác tạo yêu cầu + cư trú an toàn.</summary>
        Task<IAccountTransaction> BeginTransactionAsync();

        /// <summary>Lưu thay đổi.</summary>
        Task SaveChangesAsync();
    }
}
