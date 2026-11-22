using System.Net;
using System.Security.Cryptography;
using FUD.DTOs.Booking;
using FUD.DTOs.Payments;
using FUD.Infrastructure.Email;
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
        private const int UpcomingDueReminderDays = 3;

        private readonly FudContext _context;
        private readonly IPaymentTransactionFactory _transactionFactory;
        private readonly ILogger<StudentPaymentService> _logger;
        private readonly IConfiguration? _config;
        private readonly IEmailSender? _emailSender;

        public StudentPaymentService(
            FudContext context,
            IPaymentTransactionFactory transactionFactory,
            ILogger<StudentPaymentService> logger,
            IConfiguration? config = null,
            IEmailSender? emailSender = null)
        {
            _context = context;
            _transactionFactory = transactionFactory;
            _logger = logger;
            _config = config;
            _emailSender = emailSender;
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

            // Kiểm tra và gửi email nhắc hạn cho các hóa đơn sắp đến hạn trong vòng 3 ngày (idempotent)
            await TrySendUpcomingDueRemindersForStudentAsync(studentId, invoices, today);

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

                if (_context.Database.IsRelational())
                {
                    var lockedCount = await _context.Invoices
                        .Where(i => invoiceIds.Contains(i.Id)
                                    && i.StudentId == studentId
                                    && i.DeletedAt == null
                                    && (i.Status == InvoiceStatuses.Unpaid || i.Status == InvoiceStatuses.Overdue))
                        .ExecuteUpdateAsync(s => s.SetProperty(i => i.Status, InvoiceStatuses.Processing));

                    if (lockedCount != invoiceIds.Count)
                    {
                        throw new InvalidOperationException(
                            "PAY_409: Một hoặc nhiều hóa đơn vừa được xử lý ở phiên khác. Vui lòng tải lại danh sách.");
                    }
                }

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

                var sePayBank = _config?["SePay:BankCode"];
                if (string.IsNullOrWhiteSpace(sePayBank)) sePayBank = "BIDV";
                var sePayAcc = _config?["SePay:AccountNumber"];
                if (string.IsNullOrWhiteSpace(sePayAcc)) sePayAcc = "96247FUD26";
                var sePayName = _config?["SePay:AccountName"];
                if (string.IsNullOrWhiteSpace(sePayName)) sePayName = "LE ANH QUANG (TK: 4271030639)";
                var sePayQr = $"https://qr.sepay.vn/img?acc={Uri.EscapeDataString(sePayAcc)}&bank={Uri.EscapeDataString(sePayBank)}&amount={(int)totalAmount}&des={Uri.EscapeDataString(gatewayReference)}&template=compact";

                return new PaymentTransactionDto
                {
                    Id = transaction.Id,
                    StudentId = transaction.StudentId,
                    Method = transaction.Method,
                    GatewayReference = transaction.GatewayReference,
                    QrLink = transaction.QrLink,
                    SePayQrLink = sePayQr,
                    SePayBankCode = sePayBank,
                    SePayAccountNumber = sePayAcc,
                    SePayAccountName = sePayName,
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
        public Task<PaymentSuccessDto> MockConfirmAsync(int studentId, int transactionId, MockConfirmPaymentDto? dto)
        {
            return ConfirmTransactionInternalAsync(studentId, transactionId, dto, null);
        }

        private async Task<PaymentSuccessDto> ConfirmTransactionInternalAsync(
            int studentId,
            int transactionId,
            MockConfirmPaymentDto? dto,
            string? gatewayPayload = null)
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
                if (transaction.ExpiryTime <= now)
                {
                    throw new InvalidOperationException("PAY_409: Phiên giao dịch QR đã hết hạn.");
                }

                var receiptNumber = "RC-" + transaction.GatewayReference.Replace("VQR-", string.Empty);
                var effectivePayload = string.IsNullOrWhiteSpace(gatewayPayload)
                    ? "{\"code\":\"00\",\"desc\":\"Mock payment success\"}"
                    : gatewayPayload;

                if (_context.Database.IsRelational())
                {
                    var affected = await _context.PaymentTransactions
                        .Where(t => t.Id == transactionId
                                    && t.DeletedAt == null
                                    && t.Status == PaymentTransactionStatuses.Processing
                                    && t.ExpiryTime > now)
                        .ExecuteUpdateAsync(s => s
                            .SetProperty(t => t.Status, PaymentTransactionStatuses.Success)
                            .SetProperty(t => t.PaidTime, now)
                            .SetProperty(t => t.CallbackTime, now)
                            .SetProperty(t => t.ReceiptNumber, receiptNumber)
                            .SetProperty(t => t.GatewayPayload, effectivePayload));

                    if (affected != 1)
                    {
                        throw new InvalidOperationException(
                            "PAY_409: Giao dịch đã được xử lý ở phiên khác hoặc đã hết hạn.");
                    }
                }

                transaction.Status = PaymentTransactionStatuses.Success;
                transaction.PaidTime = now;
                transaction.CallbackTime = now;
                transaction.ReceiptNumber = receiptNumber;
                transaction.GatewayPayload = effectivePayload;

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

                var successResult = new PaymentSuccessDto
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

                // Gửi email biên lai xác nhận thanh toán thành công cho sinh viên (không chặn luồng chính nếu SMTP lỗi)
                await TrySendPaymentSuccessEmailAsync(studentId, successResult);

                return successResult;
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

                if (_context.Database.IsRelational())
                {
                    var affected = await _context.PaymentTransactions
                        .Where(t => t.Id == transactionId
                                    && t.DeletedAt == null
                                    && t.Status == PaymentTransactionStatuses.Processing)
                        .ExecuteUpdateAsync(s => s.SetProperty(t => t.Status, PaymentTransactionStatuses.Cancelled));

                    if (affected != 1)
                    {
                        throw new InvalidOperationException(
                            "PAY_409: Giao dịch đã thay đổi trạng thái ở phiên khác nên không thể hủy.");
                    }
                }

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
        //  5. Kiểm tra trạng thái giao dịch (Frontend Polling chờ SePay Webhook)
        // ============================================================
        public async Task<PaymentTransactionStatusDto> GetTransactionStatusAsync(int studentId, int transactionId)
        {
            var transaction = await _context.PaymentTransactions
                .AsNoTracking()
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

            var now = DateTime.Now;
            var remainingSeconds = transaction.Status == PaymentTransactionStatuses.Processing
                ? Math.Max(0, (int)Math.Ceiling((transaction.ExpiryTime - now).TotalSeconds))
                : 0;

            var statusDto = new PaymentTransactionStatusDto
            {
                TransactionId = transaction.Id,
                Status = transaction.Status,
                RemainingSeconds = remainingSeconds
            };

            if (transaction.Status == PaymentTransactionStatuses.Success)
            {
                var invoiceIds = transaction.TransactionInvoices.Select(ti => ti.InvoiceId).ToList();
                var invoices = await _context.Invoices
                    .AsNoTracking()
                    .Include(i => i.InvoiceItems)
                    .Where(i => invoiceIds.Contains(i.Id))
                    .ToListAsync();

                statusDto.Receipt = new PaymentSuccessDto
                {
                    TransactionId = transaction.Id,
                    ReceiptNumber = transaction.ReceiptNumber ?? ("RC-" + transaction.GatewayReference.Replace("VQR-", string.Empty)),
                    GatewayReference = transaction.GatewayReference,
                    TotalAmount = transaction.TotalAmount,
                    PaidTime = transaction.PaidTime ?? now,
                    CallbackTime = transaction.CallbackTime ?? now,
                    SideEffectNotes = new List<string>
                    {
                        "Đã xác nhận thanh toán tự động qua cổng SePay VietQR Webhook."
                    },
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

            return statusDto;
        }

        // ============================================================
        //  6. Xử lý Webhook từ SePay (Test Mode & Live Mode)
        // ============================================================
        public async Task<(bool Handled, string Message, PaymentSuccessDto? Receipt)> ProcessSePayWebhookAsync(
            SePayWebhookPayloadDto payload,
            string? authorizationHeader = null)
        {
            var expectedApiKey = _config?["SePay:WebhookApiKey"];
            if (string.IsNullOrWhiteSpace(expectedApiKey))
            {
                throw new UnauthorizedAccessException("SePay Webhook API Key chưa được cấu hình trên máy chủ.");
            }

            var provided = (authorizationHeader ?? string.Empty).Trim();
            if (provided.StartsWith("Apikey ", StringComparison.OrdinalIgnoreCase))
            {
                provided = provided.Substring("Apikey ".Length).Trim();
            }
            else if (provided.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                provided = provided.Substring("Bearer ".Length).Trim();
            }

            var providedBytes = System.Text.Encoding.UTF8.GetBytes(provided);
            var expectedBytes = System.Text.Encoding.UTF8.GetBytes(expectedApiKey.Trim());
            if (providedBytes.Length != expectedBytes.Length ||
                !CryptographicOperations.FixedTimeEquals(providedBytes, expectedBytes))
            {
                throw new UnauthorizedAccessException("SePay Webhook API Key không hợp lệ.");
            }

            if (!string.IsNullOrWhiteSpace(payload.TransferType) &&
                !string.Equals(payload.TransferType, "in", StringComparison.OrdinalIgnoreCase))
            {
                return (false, "Bỏ qua giao dịch không phải tiền vào (transferType != in).", null);
            }

            if (payload.Id > 0)
            {
                var idMarkerPascal = $"\"Id\":{payload.Id}";
                var idMarkerCamel = $"\"id\":{payload.Id}";
                var alreadyProcessed = await _context.PaymentTransactions
                    .AsNoTracking()
                    .AnyAsync(t => t.DeletedAt == null
                                   && t.Status == PaymentTransactionStatuses.Success
                                   && t.GatewayPayload != null
                                   && (t.GatewayPayload.Contains(idMarkerPascal)
                                       || t.GatewayPayload.Contains(idMarkerCamel)));

                if (alreadyProcessed)
                {
                    return (true, "Webhook đã được xử lý trước đó (idempotent).", null);
                }
            }

            var rawText = $"{payload.Code} {payload.Content} {payload.Description}".Trim();
            var normalizedText = NormalizeRef(rawText);

            var activeTransactions = await _context.PaymentTransactions
                .Where(t => t.Status == PaymentTransactionStatuses.Processing && t.DeletedAt == null)
                .OrderByDescending(t => t.CreatedTime)
                .ToListAsync();

            if (activeTransactions.Count == 0)
            {
                return (false, "Không có giao dịch nào đang ở trạng thái chờ thanh toán (Processing).", null);
            }

            // Chỉ khớp nghiêm ngặt theo mã GatewayReference (có hoặc không có dấu gạch ngang '-').
            PaymentTransaction? matched = activeTransactions.FirstOrDefault(t =>
                (!string.IsNullOrWhiteSpace(rawText) &&
                 rawText.Contains(t.GatewayReference, StringComparison.OrdinalIgnoreCase))
                || (!string.IsNullOrWhiteSpace(normalizedText) &&
                    normalizedText.Contains(NormalizeRef(t.GatewayReference), StringComparison.OrdinalIgnoreCase)));

            if (matched == null)
            {
                return (false, "Không tìm thấy giao dịch khớp với nội dung hoặc số tiền chuyển khoản.", null);
            }

            if (matched.ExpiryTime <= DateTime.Now)
            {
                return (false, "Giao dịch QR đã hết hạn.", null);
            }

            if (payload.TransferAmount != matched.TotalAmount)
            {
                _logger.LogWarning(
                    "SePay Webhook bị từ chối do lệch số tiền cho giao dịch #{TxId}: TotalAmount={TotalAmount}, TransferAmount={TransferAmount}.",
                    matched.Id, matched.TotalAmount, payload.TransferAmount);
                return (false, "Số tiền chuyển khoản không khớp với tổng tiền giao dịch.", null);
            }

            var serializedPayload = System.Text.Json.JsonSerializer.Serialize(payload);
            var receipt = await ConfirmTransactionInternalAsync(
                matched.StudentId,
                matched.Id,
                new MockConfirmPaymentDto { GatewayReference = matched.GatewayReference },
                serializedPayload);

            _logger.LogInformation(
                "SePay Webhook đã xác nhận thành công giao dịch #{TxId} ({GatewayRef}), số tiền {Amount}.",
                matched.Id, matched.GatewayReference, payload.TransferAmount);

            return (true, $"Đã xác nhận thanh toán giao dịch {matched.GatewayReference} qua SePay.", receipt);
        }

        private static string NormalizeRef(string? input)
        {
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;
            var chars = input.Where(char.IsLetterOrDigit).ToArray();
            return new string(chars).ToUpperInvariant();
        }

        // ============================================================
        //  7. Lịch sử giao dịch & hóa đơn đã thanh toán
        // ============================================================
        public async Task<List<PaymentHistoryItemDto>> GetPaymentHistoryAsync(int studentId)
        {
            var transactions = await _context.PaymentTransactions
                .AsNoTracking()
                .Include(t => t.TransactionInvoices)
                    .ThenInclude(ti => ti.Invoice)
                        .ThenInclude(i => i.InvoiceItems)
                .Where(t => t.StudentId == studentId
                            && t.DeletedAt == null
                            && t.Status == PaymentTransactionStatuses.Success)
                .OrderByDescending(t => t.PaidTime ?? t.CreatedTime)
                .ToListAsync();

            var history = new List<PaymentHistoryItemDto>();
            var coveredInvoiceIds = new HashSet<int>();

            foreach (var tx in transactions)
            {
                var invList = new List<TransactionInvoiceDto>();
                foreach (var ti in tx.TransactionInvoices)
                {
                    coveredInvoiceIds.Add(ti.InvoiceId);
                    var inv = ti.Invoice;
                    if (inv == null) continue;
                    invList.Add(new TransactionInvoiceDto
                    {
                        InvoiceId = inv.Id,
                        InvoiceNumber = inv.InvoiceNumber,
                        InvoiceType = inv.InvoiceType,
                        AllocatedAmount = ti.AllocatedAmount,
                        DueDate = inv.DueDate,
                        Status = inv.Status,
                        Items = inv.InvoiceItems
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
                    });
                }

                history.Add(new PaymentHistoryItemDto
                {
                    TransactionId = tx.Id,
                    ReceiptNumber = tx.ReceiptNumber ?? ("RC-" + tx.GatewayReference.Replace("VQR-", string.Empty)),
                    GatewayReference = tx.GatewayReference,
                    Method = tx.Method,
                    TotalAmount = tx.TotalAmount,
                    Status = tx.Status,
                    PaidTime = tx.PaidTime ?? tx.CreatedTime,
                    Invoices = invList
                });
            }

            // Bổ sung các hóa đơn Paid trực tiếp trong DB (nếu không qua bảng Payment_Transactions)
            var standalonePaidInvoices = await _context.Invoices
                .AsNoTracking()
                .Include(i => i.InvoiceItems)
                .Where(i => i.StudentId == studentId
                            && i.DeletedAt == null
                            && i.Status == InvoiceStatuses.Paid
                            && !coveredInvoiceIds.Contains(i.Id))
                .OrderByDescending(i => i.PaidTime ?? i.IssuedTime)
                .ToListAsync();

            foreach (var inv in standalonePaidInvoices)
            {
                history.Add(new PaymentHistoryItemDto
                {
                    TransactionId = inv.Id,
                    ReceiptNumber = $"RC-INV-{inv.Id}",
                    GatewayReference = inv.InvoiceNumber,
                    Method = "VietQR",
                    TotalAmount = inv.Amount,
                    Status = "Success",
                    PaidTime = inv.PaidTime ?? inv.IssuedTime,
                    Invoices = new List<TransactionInvoiceDto>
                    {
                        new TransactionInvoiceDto
                        {
                            InvoiceId = inv.Id,
                            InvoiceNumber = inv.InvoiceNumber,
                            InvoiceType = inv.InvoiceType,
                            AllocatedAmount = inv.Amount,
                            DueDate = inv.DueDate,
                            Status = inv.Status,
                            Items = inv.InvoiceItems
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
                        }
                    }
                });
            }

            return history.OrderByDescending(h => h.PaidTime).ToList();
        }

        // ============================================================
        //  8. Gửi Email biên lai thanh toán thành công & Email nhắc hạn 3 ngày
        // ============================================================
        private async Task TrySendPaymentSuccessEmailAsync(int studentId, PaymentSuccessDto receipt)
        {
            if (_emailSender == null) return;

            try
            {
                var user = await _context.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.Id == studentId && u.DeletedAt == null);

                if (user == null || string.IsNullOrWhiteSpace(user.Email))
                {
                    return;
                }

                var rowsHtml = string.Join("", receipt.Invoices.Select(inv =>
                {
                    var detail = inv.Items != null && inv.Items.Count > 0
                        ? string.Join("; ", inv.Items.Select(it => WebUtility.HtmlEncode(it.Description)))
                        : WebUtility.HtmlEncode(inv.InvoiceType);
                    return $@"
                        <tr>
                            <td style=""padding:8px 12px;border:1px solid #e2e8f0;font-weight:700;color:#c2410c"">{WebUtility.HtmlEncode(inv.InvoiceNumber)}</td>
                            <td style=""padding:8px 12px;border:1px solid #e2e8f0"">{WebUtility.HtmlEncode(inv.InvoiceType)}</td>
                            <td style=""padding:8px 12px;border:1px solid #e2e8f0"">{detail}</td>
                            <td style=""padding:8px 12px;border:1px solid #e2e8f0;text-align:right;font-weight:700"">{inv.AllocatedAmount:N0} đ</td>
                        </tr>";
                }));

                var sideEffectsHtml = receipt.SideEffectNotes != null && receipt.SideEffectNotes.Count > 0
                    ? $@"<div style=""margin-top:14px;padding:12px 14px;background:#f0fdf4;border:1px solid #86efac;border-radius:8px;color:#166534"">
                            <strong>Cập nhật tự động từ hệ thống:</strong>
                            <ul style=""margin:6px 0 0;padding-left:18px"">
                                {string.Join("", receipt.SideEffectNotes.Select(n => $"<li>{WebUtility.HtmlEncode(n)}</li>"))}
                            </ul>
                         </div>"
                    : string.Empty;

                var subject = $"[KTX FUD] Biên lai xác nhận thanh toán thành công - {receipt.ReceiptNumber}";
                var htmlBody = $@"
                    <div style=""font-family:Segoe UI,Arial,sans-serif;font-size:14px;color:#1e293b;max-width:640px;margin:0 auto;border:1px solid #e2e8f0;border-radius:12px;padding:24px"">
                        <h2 style=""color:#15803d;margin:0 0 12px"">✓ Xác nhận thanh toán thành công</h2>
                        <p>Xin chào <strong>{WebUtility.HtmlEncode(user.FullName)}</strong> ({WebUtility.HtmlEncode(user.Code)}),</p>
                        <p>Hệ thống Quản lý Ký túc xá Đại học FPT (FUD) xác nhận đã nhận được khoản thanh toán của bạn:</p>
                        <div style=""background:#f8fafc;border:1px solid #e2e8f0;border-radius:8px;padding:14px;margin:14px 0;line-height:1.7"">
                            <div><strong>Số biên lai:</strong> <span style=""color:#15803d;font-weight:700"">{WebUtility.HtmlEncode(receipt.ReceiptNumber)}</span></div>
                            <div><strong>Mã giao dịch VietQR:</strong> {WebUtility.HtmlEncode(receipt.GatewayReference)}</div>
                            <div><strong>Thời gian thanh toán:</strong> {receipt.PaidTime:dd/MM/yyyy HH:mm:ss}</div>
                            <div><strong>Tổng số tiền đã thanh toán:</strong> <span style=""color:#c2410c;font-weight:700;font-size:16px"">{receipt.TotalAmount:N0} đ</span></div>
                        </div>
                        <table style=""width:100%;border-collapse:collapse;font-size:13px;margin-top:12px"">
                            <thead>
                                <tr style=""background:#f1f5f9;text-align:left"">
                                    <th style=""padding:8px 12px;border:1px solid #e2e8f0"">Mã hóa đơn</th>
                                    <th style=""padding:8px 12px;border:1px solid #e2e8f0"">Loại phí</th>
                                    <th style=""padding:8px 12px;border:1px solid #e2e8f0"">Chi tiết</th>
                                    <th style=""padding:8px 12px;border:1px solid #e2e8f0;text-align:right"">Số tiền</th>
                                </tr>
                            </thead>
                            <tbody>{rowsHtml}</tbody>
                        </table>
                        {sideEffectsHtml}
                        <p style=""margin-top:20px;color:#64748b;font-size:12px"">Trân trọng,<br/><strong>Ban Quản lý Ký túc xá FUD - Đại học FPT</strong></p>
                    </div>";

                await _emailSender.SendEmailAsync(user.Email, subject, htmlBody);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Không thể gửi email biên lai thanh toán cho sinh viên {StudentId}", studentId);
            }
        }

        private async Task TrySendUpcomingDueRemindersForStudentAsync(
            int studentId,
            List<Invoice> invoices,
            DateOnly today)
        {
            if (_emailSender == null || invoices.Count == 0) return;

            try
            {
                var deadlineLimit = today.AddDays(UpcomingDueReminderDays);
                var dueSoonInvoices = invoices
                    .Where(i => i.Status == InvoiceStatuses.Unpaid
                                && i.DeletedAt == null
                                && i.DueDate >= today
                                && i.DueDate <= deadlineLimit)
                    .ToList();

                if (dueSoonInvoices.Count == 0) return;

                var user = await _context.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.Id == studentId && u.DeletedAt == null);

                if (user == null || string.IsNullOrWhiteSpace(user.Email)) return;

                var targetIds = dueSoonInvoices.Select(i => i.Id.ToString()).ToList();
                var alreadyRemindedIds = await _context.AuditLogs
                    .AsNoTracking()
                    .Where(a => a.TargetEntity == "InvoiceDueReminder3Days"
                                && a.TargetId != null
                                && targetIds.Contains(a.TargetId))
                    .Select(a => a.TargetId!)
                    .ToListAsync();

                var remindedSet = alreadyRemindedIds.ToHashSet();
                var now = DateTime.Now;
                var addedAny = false;

                foreach (var invoice in dueSoonInvoices)
                {
                    var key = invoice.Id.ToString();
                    if (remindedSet.Contains(key)) continue;

                    var daysLeft = invoice.DueDate.DayNumber - today.DayNumber;
                    var daysText = daysLeft == 0 ? "hôm nay" : $"còn {daysLeft} ngày";
                    var subject = $"[KTX FUD] Nhắc hạn thanh toán hóa đơn {invoice.InvoiceNumber} ({daysText})";
                    var htmlBody = $@"
                        <div style=""font-family:Segoe UI,Arial,sans-serif;font-size:14px;color:#1e293b;max-width:620px;margin:0 auto;border:1px solid #fed7aa;border-radius:12px;padding:24px"">
                            <h2 style=""color:#c2410c;margin:0 0 12px"">⏰ Nhắc nhở hóa đơn sắp đến hạn thanh toán</h2>
                            <p>Xin chào <strong>{WebUtility.HtmlEncode(user.FullName)}</strong> ({WebUtility.HtmlEncode(user.Code)}),</p>
                            <p>Bạn có hóa đơn tại Ký túc xá FUD sắp đến hạn thanh toán (<strong>{daysText}</strong>):</p>
                            <div style=""background:#fff7ed;border:1px solid #fdba74;border-radius:8px;padding:14px;margin:14px 0;line-height:1.7"">
                                <div><strong>Mã hóa đơn:</strong> <span style=""color:#c2410c;font-weight:700"">{WebUtility.HtmlEncode(invoice.InvoiceNumber)}</span></div>
                                <div><strong>Loại khoản thu:</strong> {WebUtility.HtmlEncode(invoice.InvoiceType)}</div>
                                <div><strong>Số tiền cần thanh toán:</strong> <span style=""color:#b91c1c;font-weight:700;font-size:16px"">{invoice.Amount:N0} đ</span></div>
                                <div><strong>Hạn chót thanh toán:</strong> <strong>{invoice.DueDate:dd/MM/yyyy}</strong></div>
                            </div>
                            <p>Vui lòng đăng nhập Cổng Sinh viên KTX FUD mục <strong>Thanh toán hóa đơn</strong> để quét mã VietQR thanh toán đúng hạn, tránh phát sinh nợ quá hạn làm gián đoạn quyền gửi đơn từ và đăng ký phòng.</p>
                            <p style=""margin-top:20px;color:#64748b;font-size:12px"">Trân trọng,<br/><strong>Ban Quản lý Ký túc xá FUD - Đại học FPT</strong></p>
                        </div>";

                    await _emailSender.SendEmailAsync(user.Email, subject, htmlBody);

                    _context.Notifications.Add(new Notification
                    {
                        UserId = studentId,
                        Channel = "InApp",
                        Title = "Hóa đơn sắp đến hạn thanh toán",
                        Content = $"Hóa đơn {invoice.InvoiceNumber} ({invoice.Amount:N0} đồng) sẽ đến hạn vào ngày {invoice.DueDate:dd/MM/yyyy} ({daysText}). Vui lòng thanh toán đúng hạn.",
                        ReferenceType = "Invoice",
                        ReferenceId = key,
                        ReadFlag = false,
                        SentTime = now
                    });

                    _context.AuditLogs.Add(new AuditLog
                    {
                        UserId = studentId,
                        ActionType = "REMIND_3DAYS",
                        TargetEntity = "InvoiceDueReminder3Days",
                        TargetId = key,
                        NewValue = $"Reminded {user.Email} for Invoice {invoice.InvoiceNumber} due {invoice.DueDate:yyyy-MM-dd}",
                        CreatedAt = now
                    });

                    addedAny = true;
                }

                if (addedAny)
                {
                    await _context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi gửi email nhắc hạn 3 ngày cho sinh viên {StudentId}", studentId);
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
