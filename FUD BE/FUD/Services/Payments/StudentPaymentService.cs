using System.Security.Cryptography;
using FUD.DTOs.Booking;
using FUD.DTOs.Payments;
using FUD.Models;
using FUD.Services.Interfaces.Payments;
using Microsoft.EntityFrameworkCore;

namespace FUD.Services.Payments
{
    /// <summary>
    /// Triển khai nghiệp vụ thanh toán của Sinh viên.
    ///
    /// Mọi thao tác ghi đều bọc trong transaction (xem <see cref="IPaymentTransaction"/>)
    /// để đảm bảo tính nguyên tử: trạng thái giao dịch, trạng thái hóa đơn,
    /// side-effects Booking/Transfer/Handover và thông báo đều hoặc thành công trọn vẹn,
    /// hoặc rollback toàn bộ.
    /// </summary>
    public class StudentPaymentService : IStudentPaymentService
    {
        private const int QrExpiryMinutes = 20;
        private const string VietQrBankCode = "970436-FUD-compact";

        private readonly FudContext _context;
        private readonly IPaymentTransactionFactory _transactionFactory;
        private readonly ILogger<StudentPaymentService> _logger;

        public StudentPaymentService(
            FudContext context,
            IPaymentTransactionFactory transactionFactory,
            ILogger<StudentPaymentService> logger)
        {
            _context = context;
            _transactionFactory = transactionFactory;
            _logger = logger;
        }

        // ============================================================
        //  1. Danh sách hóa đơn đang nợ
        // ============================================================
        public async Task<StudentInvoiceListResponseDto> GetPayableInvoicesAsync(int studentId)
        {
            var now = DateTime.Now;
            var today = DateOnly.FromDateTime(now);

            var invoices = await _context.Invoices
                .AsNoTracking()
                .Where(i => i.StudentId == studentId
                            && i.DeletedAt == null
                            && (i.Status == InvoiceStatuses.Unpaid
                                || i.Status == InvoiceStatuses.Overdue
                                || i.Status == InvoiceStatuses.Processing))
                .Include(i => i.InvoiceItems)
                .OrderByDescending(i => i.Status == InvoiceStatuses.Overdue)
                .ThenBy(i => i.DueDate)
                .ThenByDescending(i => i.IssuedTime)
                .ToListAsync();

            // Tự động xử lý các phiên QR đã hết hạn trước: chuyển sang 'Expired' và mở khóa hóa đơn,
            // tránh việc hóa đơn bị kẹt ở trạng thái 'Processing' vô thời hạn.
            await ExpireStaleTransactionsAsync(studentId, now);

            // Phiên QR đang chạy của sinh viên (Status = Processing, chưa quá hạn).
            var activeTransactions = await _context.PaymentTransactions
                .AsNoTracking()
                .Where(t => t.StudentId == studentId
                            && t.Status == PaymentTransactionStatuses.Processing
                            && t.DeletedAt == null)
                .Include(t => t.TransactionInvoices)
                .ToListAsync();

            // BookingKind để phân biệt Booking / Keeping khi sinh query param sourceType.
            var bookingKinds = await _context.BookingRequests
                .AsNoTracking()
                .Where(b => b.StudentId == studentId)
                .ToDictionaryAsync(b => b.Id, b => b.BookingKind);

            var result = new StudentInvoiceListResponseDto
            {
                ServerTime = now
            };

            foreach (var invoice in invoices)
            {
                var dto = new StudentInvoiceDto
                {
                    Id = invoice.Id,
                    StudentId = invoice.StudentId,
                    TermId = invoice.TermId,
                    InvoiceNumber = invoice.InvoiceNumber,
                    InvoiceType = invoice.InvoiceType,
                    AdjustmentKind = invoice.AdjustmentKind,
                    Amount = invoice.Amount,
                    IssuedTime = invoice.IssuedTime,
                    DueDate = invoice.DueDate,
                    PaidTime = invoice.PaidTime,
                    Status = invoice.Status,
                    SourceType = ResolveSourceType(invoice, invoice.BookingRequestId.HasValue
                        && bookingKinds.TryGetValue(invoice.BookingRequestId.Value, out var kind)
                            ? kind
                            : null),
                    SourceId = ResolveSourceId(invoice),
                    Items = invoice.InvoiceItems
                        .OrderBy(it => it.Id)
                        .Select(it => new InvoiceItemDto
                        {
                            Id = it.Id,
                            Description = it.Description,
                            Quantity = it.Quantity,
                            UnitPrice = it.UnitPrice,
                            Amount = it.Amount
                        })
                        .ToList()
                };

                if (invoice.DueDate < today)
                {
                    dto.IsOverdue = true;
                    dto.OverdueDays = today.DayNumber - invoice.DueDate.DayNumber;
                }

                // Gắn phiên QR đang xử lý (nếu có) vào hóa đơn tương ứng.
                if (invoice.Status == InvoiceStatuses.Processing)
                {
                    var tx = activeTransactions.FirstOrDefault(t =>
                        t.TransactionInvoices.Any(ti => ti.InvoiceId == invoice.Id));

                    if (tx != null)
                    {
                        dto.HasActiveTransaction = true;
                        dto.ActiveTransactionId = tx.Id;
                        dto.ActiveTransactionExpiryTime = tx.ExpiryTime;
                        dto.ActiveTransactionNotExpired = tx.ExpiryTime > now;
                    }
                }

                result.Items.Add(dto);

                if (invoice.Status == InvoiceStatuses.Unpaid || invoice.Status == InvoiceStatuses.Overdue)
                {
                    result.TotalOutstanding += invoice.Amount;
                }

                if (dto.IsOverdue)
                {
                    result.OverdueCount++;
                }
            }

            return result;
        }

        // ============================================================
        //  1b. Tìm hóa đơn theo nguồn phát sinh (Booking/Keeping/Utility/Transfer/Checkout)
        // ============================================================
        public async Task<StudentInvoiceDto?> FindInvoiceBySourceAsync(int studentId, string sourceType, int sourceId)
        {
            if (string.IsNullOrWhiteSpace(sourceType))
            {
                return null;
            }

            var normalized = sourceType.Trim().ToLowerInvariant();

            var invoice = await _context.Invoices
                .AsNoTracking()
                .Include(i => i.InvoiceItems)
                .Where(i => i.StudentId == studentId
                            && i.DeletedAt == null
                            && (i.Status == InvoiceStatuses.Unpaid
                                || i.Status == InvoiceStatuses.Overdue
                                || i.Status == InvoiceStatuses.Processing))
                .FirstOrDefaultAsync(i =>
                    (normalized == "booking" && i.InvoiceType == InvoiceTypes.RoomFee && i.BookingRequestId == sourceId)
                    || (normalized == "keeping" && i.InvoiceType == InvoiceTypes.RoomFee && i.BookingRequestId == sourceId)
                    || (normalized == "utility" && i.InvoiceType == InvoiceTypes.Utility && i.UtilityBillId == sourceId)
                    || (normalized == "bedtransfer" && i.InvoiceType == InvoiceTypes.BedTransfer && i.TransferRequestId == sourceId)
                    || (normalized == "checkout" && i.HandoverId == sourceId));

            if (invoice == null)
            {
                return null;
            }

            var today = DateOnly.FromDateTime(DateTime.Now);
            var dto = new StudentInvoiceDto
            {
                Id = invoice.Id,
                StudentId = invoice.StudentId,
                TermId = invoice.TermId,
                InvoiceNumber = invoice.InvoiceNumber,
                InvoiceType = invoice.InvoiceType,
                AdjustmentKind = invoice.AdjustmentKind,
                Amount = invoice.Amount,
                IssuedTime = invoice.IssuedTime,
                DueDate = invoice.DueDate,
                PaidTime = invoice.PaidTime,
                Status = invoice.Status,
                SourceId = ResolveSourceId(invoice),
                Items = invoice.InvoiceItems
                    .OrderBy(it => it.Id)
                    .Select(it => new InvoiceItemDto
                    {
                        Id = it.Id,
                        Description = it.Description,
                        Quantity = it.Quantity,
                        UnitPrice = it.UnitPrice,
                        Amount = it.Amount
                    })
                    .ToList()
            };

            if (invoice.DueDate < today)
            {
                dto.IsOverdue = true;
                dto.OverdueDays = today.DayNumber - invoice.DueDate.DayNumber;
            }

            return dto;
        }

        // ============================================================
        //  2. Tạo giao dịch QR
        // ============================================================
        public async Task<PaymentTransactionDto> CreateTransactionAsync(int studentId, CreatePaymentTransactionDto dto)
        {
            var invoiceIds = dto.InvoiceIds.Distinct().ToList();

            if (invoiceIds.Count == 0)
            {
                throw new ArgumentException("PAY_400: Vui lòng chọn ít nhất một hóa đơn để thanh toán.");
            }

            await using var dbTransaction = await _transactionFactory.BeginAsync();
            try
            {
                var invoices = await _context.Invoices
                    .Where(i => invoiceIds.Contains(i.Id))
                    .ToListAsync();

                if (invoices.Count != invoiceIds.Count)
                {
                    throw new KeyNotFoundException(
                        "PAY_404: Một hoặc nhiều hóa đơn không tồn tại hoặc đã bị xóa.");
                }

                // Mọi hóa đơn phải thuộc về sinh viên đang đăng nhập.
                var foreignInvoice = invoices.FirstOrDefault(i => i.StudentId != studentId);
                if (foreignInvoice != null)
                {
                    throw new ArgumentException(
                        $"PAY_403: Hóa đơn {foreignInvoice.InvoiceNumber} không thuộc về tài khoản của bạn.");
                }

                // Hóa đơn đã bị xóa mềm -> không được thanh toán.
                var deletedInvoice = invoices.FirstOrDefault(i => i.DeletedAt != null);
                if (deletedInvoice != null)
                {
                    throw new ArgumentException(
                        $"PAY_400: Hóa đơn {deletedInvoice.InvoiceNumber} đã bị vô hiệu và không thể thanh toán.");
                }

                // Chỉ cho phép thanh toán hóa đơn đang Unpaid hoặc Overdue.
                var invalidStatus = invoices.FirstOrDefault(i =>
                    i.Status != InvoiceStatuses.Unpaid && i.Status != InvoiceStatuses.Overdue);
                if (invalidStatus != null)
                {
                    throw new ArgumentException(
                        $"PAY_409: Hóa đơn {invalidStatus.InvoiceNumber} hiện đang ở trạng thái '{invalidStatus.Status}' " +
                        "nên không thể thanh toán. Vui lòng tải lại danh sách.");
                }

                var totalAmount = invoices.Sum(i => i.Amount);
                if (totalAmount <= 0)
                {
                    throw new ArgumentException("PAY_400: Tổng số tiền thanh toán phải lớn hơn 0.");
                }

                var now = DateTime.Now;
                var expiryTime = now.AddMinutes(QrExpiryMinutes);
                var gatewayReference = await GenerateGatewayReferenceAsync();

                var transaction = new PaymentTransaction
                {
                    StudentId = studentId,
                    Method = "VietQR",
                    GatewayReference = gatewayReference,
                    QrLink = BuildQrLink(totalAmount, gatewayReference),
                    TotalAmount = totalAmount,
                    // CHK_Payment_Status chỉ cho phép 'Processing' khi mới tạo.
                    Status = PaymentTransactionStatuses.Processing,
                    CreatedTime = now,
                    ExpiryTime = expiryTime
                };

                _context.PaymentTransactions.Add(transaction);

                foreach (var invoice in invoices)
                {
                    _context.TransactionInvoices.Add(new TransactionInvoice
                    {
                        Transaction = transaction,
                        Invoice = invoice,
                        AllocatedAmount = invoice.Amount
                    });

                    // Khóa hóa đơn để chống thanh toán trùng.
                    invoice.Status = InvoiceStatuses.Processing;
                }

                // Nạp Invoice_Items để trả về chi tiết khoản phí cho màn hình QR.
                var invoiceItems = await _context.InvoiceItems
                    .AsNoTracking()
                    .Where(it => invoiceIds.Contains(it.InvoiceId))
                    .ToListAsync();

                await _context.SaveChangesAsync();
                await dbTransaction.CommitAsync();

                return new PaymentTransactionDto
                {
                    Id = transaction.Id,
                    StudentId = transaction.StudentId,
                    Method = transaction.Method,
                    GatewayReference = transaction.GatewayReference,
                    QrLink = transaction.QrLink,
                    TotalAmount = transaction.TotalAmount,
                    Status = transaction.Status,
                    ReceiptNumber = transaction.ReceiptNumber,
                    CreatedTime = transaction.CreatedTime,
                    ExpiryTime = transaction.ExpiryTime,
                    PaidTime = transaction.PaidTime,
                    CallbackTime = transaction.CallbackTime,
                    RemainingSeconds = Math.Max(0, (int)Math.Ceiling((expiryTime - now).TotalSeconds)),
                    Invoices = invoices.Select(i => new TransactionInvoiceDto
                    {
                        InvoiceId = i.Id,
                        InvoiceNumber = i.InvoiceNumber,
                        InvoiceType = i.InvoiceType,
                        AllocatedAmount = i.Amount,
                        DueDate = i.DueDate,
                        Status = i.Status,
                        Items = invoiceItems
                            .Where(it => it.InvoiceId == i.Id)
                            .OrderBy(it => it.Id)
                            .Select(it => new InvoiceItemDto
                            {
                                Id = it.Id,
                                Description = it.Description,
                                Quantity = it.Quantity,
                                UnitPrice = it.UnitPrice,
                                Amount = it.Amount
                            })
                            .ToList()
                    }).ToList()
                };
            }
            catch
            {
                await dbTransaction.RollbackAsync();
                throw;
            }
        }

        // ============================================================
        //  3. Giả lập xác nhận thanh toán (+ side-effects)
        // ============================================================
        public async Task<PaymentSuccessDto> MockConfirmAsync(int studentId, int transactionId, MockConfirmPaymentDto? dto)
        {
            await using var dbTransaction = await _transactionFactory.BeginAsync();
            try
            {
                var transaction = await _context.PaymentTransactions
                    .Include(t => t.TransactionInvoices)
                    .FirstOrDefaultAsync(t => t.Id == transactionId && t.DeletedAt == null);

                if (transaction == null)
                {
                    throw new KeyNotFoundException($"PAY_404: Không tìm thấy giao dịch thanh toán với mã {transactionId}.");
                }

                if (transaction.StudentId != studentId)
                {
                    throw new UnauthorizedAccessException("PAY_403: Giao dịch này không thuộc về tài khoản của bạn.");
                }

                if (transaction.Status != PaymentTransactionStatuses.Processing)
                {
                    throw new InvalidOperationException(
                        $"PAY_409: Giao dịch hiện đang ở trạng thái '{transaction.Status}' nên không thể xác nhận thanh toán.");
                }

                // Đối soát mã tham chiếu (nếu client gửi kèm).
                if (!string.IsNullOrWhiteSpace(dto?.GatewayReference)
                    && !string.Equals(dto.GatewayReference, transaction.GatewayReference, StringComparison.OrdinalIgnoreCase))
                {
                    throw new ArgumentException("PAY_400: Mã tham chiếu không khớp với giao dịch hiện tại.");
                }

                var now = DateTime.Now;

                transaction.Status = PaymentTransactionStatuses.Success;
                transaction.PaidTime = now;
                transaction.CallbackTime = now;
                transaction.ReceiptNumber = "RC-" + transaction.GatewayReference.Replace("VQR-", string.Empty);
                transaction.GatewayPayload = "{\"code\":\"00\",\"desc\":\"Mock payment success\"}";

                var invoiceIds = transaction.TransactionInvoices.Select(ti => ti.InvoiceId).ToList();

                var invoices = await _context.Invoices
                    .Include(i => i.InvoiceItems)
                    .Where(i => invoiceIds.Contains(i.Id))
                    .ToListAsync();

                var sideEffectNotes = new List<string>();

                // Tập các hóa đơn sắp được đánh dấu Paid trong phiên làm việc này.
                // Dùng để kiểm tra "đã trả đủ công nợ" cho Handover/Checkout mà không phụ thuộc
                // vào việc đã SaveChanges hay chưa.
                var newlyPaidInvoiceIds = invoices.Select(i => i.Id).ToHashSet();

                foreach (var invoice in invoices)
                {
                    // Trigger trg_Invoices_PaidImmutable chỉ cho sửa Status/PaidTime khi đã Paid.
                    invoice.Status = InvoiceStatuses.Paid;
                    invoice.PaidTime = now;

                    await ApplyInvoiceSideEffectsAsync(invoice, studentId, now, sideEffectNotes, newlyPaidInvoiceIds);

                    _context.Notifications.Add(new Notification
                    {
                        UserId = studentId,
                        Channel = "InApp",
                        Title = "Thanh toán thành công",
                        Content = $"Hóa đơn {invoice.InvoiceNumber} ({invoice.Amount:N0} đồng) đã được thanh toán thành công.",
                        ReferenceType = "Invoice",
                        ReferenceId = invoice.Id.ToString(),
                        ReadFlag = false,
                        SentTime = now
                    });
                }

                await _context.SaveChangesAsync();
                await dbTransaction.CommitAsync();

                return new PaymentSuccessDto
                {
                    TransactionId = transaction.Id,
                    ReceiptNumber = transaction.ReceiptNumber ?? string.Empty,
                    GatewayReference = transaction.GatewayReference,
                    TotalAmount = transaction.TotalAmount,
                    PaidTime = transaction.PaidTime ?? now,
                    CallbackTime = transaction.CallbackTime ?? now,
                    SideEffectNotes = sideEffectNotes,
                    Invoices = invoices.Select(i => new TransactionInvoiceDto
                    {
                        InvoiceId = i.Id,
                        InvoiceNumber = i.InvoiceNumber,
                        InvoiceType = i.InvoiceType,
                        AllocatedAmount = i.Amount,
                        DueDate = i.DueDate,
                        Status = i.Status,
                        Items = i.InvoiceItems
                            .OrderBy(it => it.Id)
                            .Select(it => new InvoiceItemDto
                            {
                                Id = it.Id,
                                Description = it.Description,
                                Quantity = it.Quantity,
                                UnitPrice = it.UnitPrice,
                                Amount = it.Amount
                            })
                            .ToList()
                    }).ToList()
                };
            }
            catch
            {
                await dbTransaction.RollbackAsync();
                throw;
            }
        }

        // ============================================================
        //  4. Hủy giao dịch (mở khóa hóa đơn)
        // ============================================================
        public async Task<PaymentCancelResultDto> CancelTransactionAsync(int studentId, int transactionId, CancelPaymentDto? dto)
        {
            await using var dbTransaction = await _transactionFactory.BeginAsync();
            try
            {
                var transaction = await _context.PaymentTransactions
                    .Include(t => t.TransactionInvoices)
                    .FirstOrDefaultAsync(t => t.Id == transactionId && t.DeletedAt == null);

                if (transaction == null)
                {
                    throw new KeyNotFoundException($"PAY_404: Không tìm thấy giao dịch thanh toán với mã {transactionId}.");
                }

                if (transaction.StudentId != studentId)
                {
                    throw new UnauthorizedAccessException("PAY_403: Giao dịch này không thuộc về tài khoản của bạn.");
                }

                if (transaction.Status != PaymentTransactionStatuses.Processing)
                {
                    throw new InvalidOperationException(
                        $"PAY_409: Giao dịch hiện đang ở trạng thái '{transaction.Status}' nên không thể hủy.");
                }

                var now = DateTime.Now;
                var today = DateOnly.FromDateTime(now);

                transaction.Status = PaymentTransactionStatuses.Cancelled;

                var invoiceIds = transaction.TransactionInvoices.Select(ti => ti.InvoiceId).ToList();
                var invoices = await _context.Invoices
                    .Where(i => invoiceIds.Contains(i.Id))
                    .ToListAsync();

                var unlocked = new List<UnlockedInvoiceDto>();

                foreach (var invoice in invoices)
                {
                    var previousStatus = invoice.Status;

                    // Trả hóa đơn về trạng thái trước khi khóa: Overdue nếu quá hạn, ngược lại Unpaid.
                    invoice.Status = invoice.DueDate < today
                        ? InvoiceStatuses.Overdue
                        : InvoiceStatuses.Unpaid;

                    unlocked.Add(new UnlockedInvoiceDto
                    {
                        InvoiceId = invoice.Id,
                        InvoiceNumber = invoice.InvoiceNumber,
                        PreviousStatus = previousStatus,
                        CurrentStatus = invoice.Status
                    });
                }

                if (!string.IsNullOrWhiteSpace(dto?.Reason))
                {
                    _logger.LogInformation("Giao dịch {GatewayReference} bị hủy bởi sinh viên {StudentId}. Lý do: {Reason}",
                        transaction.GatewayReference, studentId, dto.Reason);
                }

                await _context.SaveChangesAsync();
                await dbTransaction.CommitAsync();

                return new PaymentCancelResultDto
                {
                    TransactionId = transaction.Id,
                    Status = transaction.Status,
                    CancelledTime = now,
                    UnlockedInvoices = unlocked
                };
            }
            catch
            {
                await dbTransaction.RollbackAsync();
                throw;
            }
        }

        // ============================================================
        //  Helpers
        // ============================================================

        /// <summary>
        /// Sinh mã tham chiếu gateway duy nhất: VQR-yyyyMMddHHmmss-xxxx
        /// (UQ__Payment___GatewayReference là UNIQUE nên phải bảo đảm không trùng).
        /// </summary>
        private async Task<string> GenerateGatewayReferenceAsync()
        {
            for (int attempt = 0; attempt < 5; attempt++)
            {
                var random = RandomNumberGenerator.GetInt32(0, 10000);
                var reference = "VQR-" + DateTime.Now.ToString("yyyyMMddHHmmss") + "-" + random.ToString("D4");

                var exists = await _context.PaymentTransactions
                    .AsNoTracking()
                    .AnyAsync(t => t.GatewayReference == reference);

                if (!exists)
                {
                    return reference;
                }
            }

            // Fallback: thêm mili giây để chắc chắn khác trong cùng giây.
            return "VQR-" + DateTime.Now.ToString("yyyyMMddHHmmssfff") + "-" +
                   RandomNumberGenerator.GetInt32(0, 10000).ToString("D4");
        }

        private static string BuildQrLink(decimal totalAmount, string gatewayReference)
        {
            return $"https://img.vietqr.io/image/{VietQrBankCode}.png?amount={(int)totalAmount}&addInfo={gatewayReference}";
        }

        /// <summary>
        /// Tự động hủy các phiên QR đã hết hạn (ExpiryTime &lt;= now):
        /// giao dịch -> 'Expired' và mở khóa hóa đơn về 'Unpaid'/'Overdue'.
        /// Nhờ đó hóa đơn không bị kẹt vô thời hạn ở trạng thái 'Processing'.
        /// </summary>
        private async Task ExpireStaleTransactionsAsync(int studentId, DateTime now)
        {
            var staleTransactions = await _context.PaymentTransactions
                .Include(t => t.TransactionInvoices)
                .Where(t => t.StudentId == studentId
                            && t.Status == PaymentTransactionStatuses.Processing
                            && t.DeletedAt == null
                            && t.ExpiryTime <= now)
                .ToListAsync();

            if (staleTransactions.Count == 0)
            {
                return;
            }

            var today = DateOnly.FromDateTime(now);
            var staleInvoiceIds = staleTransactions
                .SelectMany(t => t.TransactionInvoices)
                .Select(ti => ti.InvoiceId)
                .ToList();

            var lockedInvoices = await _context.Invoices
                .Where(i => staleInvoiceIds.Contains(i.Id) && i.Status == InvoiceStatuses.Processing)
                .ToListAsync();

            foreach (var transaction in staleTransactions)
            {
                transaction.Status = PaymentTransactionStatuses.Expired;
            }

            foreach (var invoice in lockedInvoices)
            {
                invoice.Status = invoice.DueDate < today
                    ? InvoiceStatuses.Overdue
                    : InvoiceStatuses.Unpaid;
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Đã tự động hết hạn {Count} giao dịch QR quá hạn của sinh viên {StudentId}.",
                staleTransactions.Count, studentId);
        }

        /// <summary>
        /// Xác định nguồn phát sinh để Frontend gắn nút "Thanh toán ngay" đúng ngữ cảnh.
        /// Giá trị trả về dùng được làm query param <c>sourceType</c> của màn hình Choose Paid Item.
        /// </summary>
        private static string? ResolveSourceType(Invoice invoice, string? bookingKind)
        {
            // Ưu tiên nguồn theo loại hóa đơn để tương thích với query param sourceType của Frontend.
            return invoice.InvoiceType switch
            {
                InvoiceTypes.RoomFee => string.Equals(bookingKind, "Keeping", StringComparison.OrdinalIgnoreCase)
                    ? "Keeping"
                    : "Booking",
                InvoiceTypes.BedTransfer => "BedTransfer",
                InvoiceTypes.Utility => invoice.HandoverId.HasValue ? "Checkout" : "Utility",
                InvoiceTypes.Compensation => "Checkout",
                _ => null
            };
        }

        private static int? ResolveSourceId(Invoice invoice)
        {
            return invoice.InvoiceType switch
            {
                InvoiceTypes.RoomFee => invoice.BookingRequestId,
                InvoiceTypes.BedTransfer => invoice.TransferRequestId,
                InvoiceTypes.Compensation => invoice.HandoverId,
                // Utility gắn với Utility_Bills (chung cả phòng) hoặc hóa đơn cuối lúc Checkout.
                InvoiceTypes.Utility => invoice.HandoverId ?? invoice.UtilityBillId,
                _ => null
            };
        }

        /// <summary>
        /// Side-effects sau khi hóa đơn được thanh toán:
        /// - Booking/Keeping: Booking_Request 'Pending Payment' -> 'Approved' (+ ProcessedTime).
        /// - BedTransfer: ghi Request_Logs.
        /// - Handover/Checkout: nếu tất cả hóa đơn của Handover đã Paid -> Request 'Approved' -> 'Completed'.
        /// </summary>
        private async Task ApplyInvoiceSideEffectsAsync(
            Invoice invoice,
            int studentId,
            DateTime now,
            List<string> sideEffectNotes,
            IReadOnlySet<int> newlyPaidInvoiceIds)
        {
            // ── 1. Booking / Keeping ─────────────────────────────────────────
            if (invoice.BookingRequestId.HasValue)
            {
                var booking = await _context.BookingRequests
                    .FirstOrDefaultAsync(b => b.Id == invoice.BookingRequestId.Value);

                if (booking != null && booking.Status == BookingStatusConstants.PendingPayment)
                {
                    // Đã thanh toán -> chuyển sang 'Pending' để Ban Quản lý xét duyệt.
                    // (Manager duyệt sau đó mới tạo Residency và chuyển 'Approved'.)
                    // Áp dụng cho cả 2 loại: Booking (BookingKind='Booking') và Keeping (BookingKind='Keeping').
                    booking.Status = BookingStatusConstants.Pending;

                    sideEffectNotes.Add(
                        $"Yêu cầu đặt/giữ phòng #{booking.Id} đã được thanh toán và chuyển sang chờ Ban Quản lý duyệt.");
                }
            }

            // ── 2. Bed Transfer ───────────────────────────────────────────────
            if (invoice.TransferRequestId.HasValue)
            {
                var transferRequest = await _context.Requests
                    .FirstOrDefaultAsync(r => r.Id == invoice.TransferRequestId.Value);

                if (transferRequest != null)
                {
                    // Capture trạng thái gốc trước khi thay đổi để ghi log chính xác.
                    var previousStatus = transferRequest.Status;

                    // Nếu Manager chưa duyệt -> chuyển Pending sang Approved.
                    if (transferRequest.Status == "Pending")
                    {
                        transferRequest.Status = "Approved";
                        transferRequest.ProcessedTime = now;
                    }

                    _context.RequestLogs.Add(new RequestLog
                    {
                        RequestId = transferRequest.Id,
                        FromStatus = previousStatus,
                        ToStatus = transferRequest.Status,
                        Note = "Sinh viên đã thanh toán phí chuyển giường.",
                        ChangeTime = now,
                        ChangedById = studentId
                    });

                    sideEffectNotes.Add(
                        $"Yêu cầu chuyển giường #{transferRequest.Id} đã ghi nhận thanh toán phí.");
                }
            }

            // ── 3. Handover / Checkout ────────────────────────────────────────
            if (invoice.HandoverId.HasValue)
            {
                var handover = await _context.Handovers
                    .FirstOrDefaultAsync(h => h.Id == invoice.HandoverId.Value);

                if (handover?.RequestId != null)
                {
                    // Kiểm tra tất cả hóa đơn gắn với Handover này đã Paid hay chưa.
                    // Lưu ý: các hóa đơn vừa được đánh dấu Paid trong phiên làm việc này
                    // chưa có trong DB, nên phải loại trừ chúng khỏi điều kiện "chưa thanh toán".
                    var hasUnpaid = await _context.Invoices
                        .Where(i => i.HandoverId == handover.Id
                                    && i.DeletedAt == null
                                    && !newlyPaidInvoiceIds.Contains(i.Id))
                        .AnyAsync(i => i.Status != InvoiceStatuses.Paid);

                    if (!hasUnpaid)
                    {
                        var checkoutRequest = await _context.Requests
                            .FirstOrDefaultAsync(r => r.Id == handover.RequestId.Value);

                        if (checkoutRequest != null && checkoutRequest.Status == "Approved")
                        {
                            var previousStatus = checkoutRequest.Status;
                            checkoutRequest.Status = "Completed";
                            checkoutRequest.ProcessedTime = now;

                            _context.RequestLogs.Add(new RequestLog
                            {
                                RequestId = checkoutRequest.Id,
                                FromStatus = previousStatus,
                                ToStatus = "Completed",
                                Note = "Sinh viên đã hoàn tất thanh toán phí bàn giao/trả phòng.",
                                ChangeTime = now,
                                ChangedById = studentId
                            });

                            sideEffectNotes.Add(
                                $"Yêu cầu trả phòng #{checkoutRequest.Id} đã hoàn tất (Completed) sau khi trả đủ công nợ.");
                        }
                    }
                }
            }
        }
    }
}
