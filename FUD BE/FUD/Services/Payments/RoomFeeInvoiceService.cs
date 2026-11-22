using FUD.DTOs.Payments;
using FUD.Models;
using FUD.Services.Interfaces.Payments;
using Microsoft.EntityFrameworkCore;

namespace FUD.Services.Payments
{
    /// <summary>
    /// Hiện thực tạo hóa đơn phí phòng dùng chung cho Booking / Keeping.
    /// </summary>
    public class RoomFeeInvoiceService : IRoomFeeInvoiceService
    {
        private readonly FudContext _context;

        public RoomFeeInvoiceService(FudContext context)
        {
            _context = context;
        }

        public async Task<Invoice> CreateRoomFeeInvoiceAsync(
            BookingRequest bookingRequest,
            decimal amount,
            string description,
            DateOnly dueDate,
            DateTime issuedTime)
        {
            if (amount < 0)
            {
                throw new ArgumentException("Số tiền hóa đơn không được âm.");
            }

            var invoiceNumber = await GenerateInvoiceNumberAsync(
                bookingRequest.BookingKind == "Keeping" ? "K" : "B");

            var invoice = new Invoice
            {
                StudentId = bookingRequest.StudentId,
                TermId = bookingRequest.TermId,
                InvoiceNumber = invoiceNumber,
                InvoiceType = InvoiceTypes.RoomFee,
                // CHK_Invoice_OneSource: RoomFee bắt buộc gắn đúng 1 nguồn = BookingRequest.
                BookingRequestId = bookingRequest.Id,
                Amount = amount,
                IssuedTime = issuedTime,
                DueDate = dueDate,
                // Hóa đơn mới luôn ở trạng thái 'Unpaid' để sinh viên thanh toán.
                Status = InvoiceStatuses.Unpaid
            };

            _context.Invoices.Add(invoice);

            // Chi tiết hóa đơn. Phải thêm khi hóa đơn còn 'Unpaid'
            // (trigger trg_InvoiceItems_LockedAfterUnpaid khóa chi tiết khi hóa đơn rời 'Unpaid').
            _context.InvoiceItems.Add(new InvoiceItem
            {
                Invoice = invoice,
                Description = description,
                Quantity = 1m,
                UnitPrice = amount,
                Amount = amount
            });

            return invoice;
        }

        public async Task<string> GenerateInvoiceNumberAsync(string prefix)
        {
            for (int attempt = 0; attempt < 5; attempt++)
            {
                var today = DateTime.Now;
                var baseNumber = $"INV-ROOM-{prefix}{today:yy}";

                // Lấy số thứ tự lớn nhất đã dùng cho prefix này rồi cộng 1.
                var lastNumber = await _context.Invoices
                    .AsNoTracking()
                    .Where(i => i.InvoiceNumber.StartsWith(baseNumber))
                    .OrderByDescending(i => i.InvoiceNumber)
                    .Select(i => i.InvoiceNumber)
                    .FirstOrDefaultAsync();

                var nextSeq = 1;
                if (!string.IsNullOrEmpty(lastNumber))
                {
                    var tail = lastNumber[(baseNumber.Length + 1)..];
                    if (int.TryParse(tail, out var parsed))
                    {
                        nextSeq = parsed + 1;
                    }
                }

                var candidate = $"{baseNumber}-{nextSeq:000}";

                var exists = await _context.Invoices
                    .AsNoTracking()
                    .AnyAsync(i => i.InvoiceNumber == candidate);

                if (!exists)
                {
                    return candidate;
                }
            }

            // Fallback: thêm hậu tố timestamp để chắc chắn không trùng (InvoiceNumber là UNIQUE).
            return $"INV-ROOM-{prefix}{DateTime.Now:yy}-{DateTime.Now:HHmmssfff}";
        }

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }
    }
}
