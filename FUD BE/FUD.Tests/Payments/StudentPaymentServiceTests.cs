using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using FUD.Controllers.Payments;
using FUD.DTOs.Payments;
using FUD.Models;
using FUD.Services.Interfaces.Payments;
using FUD.Services.Payments;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace FUD.Tests.Payments;

/// <summary>
/// Unit tests cho nghiệp vụ Thanh toán của Sinh viên (UC-Payment) tầng Service.
/// Bao phủ: lấy danh sách hóa đơn nợ, tạo giao dịch QR, xác nhận thanh toán (kèm side-effects)
/// và hủy giao dịch (mở khóa hóa đơn).
/// </summary>
public class StudentPaymentServiceTests
{
    private const int StudentId = 10;
    private const int OtherStudentId = 20;

    private static FudContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<FudContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new FudContext(options);
    }

    private static StudentPaymentService CreateSut(FudContext context, IConfiguration? config = null)
    {
        return new StudentPaymentService(
            context,
            new PaymentTransactionFactory(context),
            NullLogger<StudentPaymentService>.Instance,
            config);
    }

    private static Invoice CreateInvoice(
        int id,
        int studentId,
        string invoiceNumber,
        string invoiceType,
        decimal amount,
        string status,
        DateOnly dueDate,
        int? bookingRequestId = null,
        int? transferRequestId = null,
        int? utilityBillId = null,
        int? handoverId = null)
    {
        return new Invoice
        {
            Id = id,
            StudentId = studentId,
            TermId = 1,
            InvoiceNumber = invoiceNumber,
            InvoiceType = invoiceType,
            BookingRequestId = bookingRequestId,
            TransferRequestId = transferRequestId,
            UtilityBillId = utilityBillId,
            HandoverId = handoverId,
            Amount = amount,
            IssuedTime = DateTime.Now.AddDays(-5),
            DueDate = dueDate,
            Status = status
        };
    }

    private static Term CreateTerm() => new Term
    {
        Id = 1,
        Name = "Fall 2026",
        AcademicYear = "2026-2027",
        StartDate = new DateOnly(2026, 9, 1),
        EndDate = new DateOnly(2026, 12, 31)
    };

    #region GetPayableInvoicesAsync

    [Fact]
    public async Task GetPayableInvoicesAsync_ReturnsOnlyPayableInvoices_OfCurrentStudent()
    {
        // Arrange
        var context = CreateInMemoryContext();
        context.Terms.Add(CreateTerm());
        context.Students.Add(new Student { UserId = StudentId });

        var today = DateOnly.FromDateTime(DateTime.Now);
        context.Invoices.AddRange(
            CreateInvoice(1, StudentId, "INV-001", "RoomFee", 4_000_000m, "Unpaid", today.AddDays(3)),
            CreateInvoice(2, StudentId, "INV-002", "Utility", 350_000m, "Overdue", today.AddDays(-2)),
            CreateInvoice(3, StudentId, "INV-003", "BedTransfer", 180_329m, "Processing", today.AddDays(1)),
            CreateInvoice(4, StudentId, "INV-004", "Compensation", 100_000m, "Paid", today.AddDays(1)),
            CreateInvoice(5, StudentId, "INV-005", "RoomFee", 900_000m, "Cancelled", today.AddDays(1)),
            CreateInvoice(6, OtherStudentId, "INV-006", "RoomFee", 700_000m, "Unpaid", today.AddDays(1)));

        // Hóa đơn đã xóa mềm -> không được hiển thị.
        var softDeleted = CreateInvoice(7, StudentId, "INV-007", "RoomFee", 800_000m, "Unpaid", today.AddDays(1));
        softDeleted.DeletedAt = DateTime.Now;
        context.Invoices.Add(softDeleted);

        await context.SaveChangesAsync();
        var sut = CreateSut(context);

        // Act
        var result = await sut.GetPayableInvoicesAsync(StudentId);

        // Assert
        result.Items.Should().HaveCount(3);
        result.Items.Select(i => i.Id).Should().BeEquivalentTo(new[] { 1, 2, 3 });
        result.TotalOutstanding.Should().Be(4_350_000m);
        result.OverdueCount.Should().Be(1);
    }

    [Fact]
    public async Task GetPayableInvoicesAsync_MarksOverdueInvoices_WithOverdueDays()
    {
        // Arrange
        var context = CreateInMemoryContext();
        context.Terms.Add(CreateTerm());
        context.Students.Add(new Student { UserId = StudentId });

        var today = DateOnly.FromDateTime(DateTime.Now);
        context.Invoices.Add(CreateInvoice(1, StudentId, "INV-001", "Utility", 100_000m, "Overdue", today.AddDays(-3)));

        await context.SaveChangesAsync();
        var sut = CreateSut(context);

        // Act
        var result = await sut.GetPayableInvoicesAsync(StudentId);

        // Assert
        result.Items.Should().ContainSingle();
        result.Items[0].IsOverdue.Should().BeTrue();
        result.Items[0].OverdueDays.Should().Be(3);
    }

    [Fact]
    public async Task GetPayableInvoicesAsync_IncludesInvoiceItems_AsDetail()
    {
        // Arrange
        var context = CreateInMemoryContext();
        context.Terms.Add(CreateTerm());
        context.Students.Add(new Student { UserId = StudentId });

        var invoice = CreateInvoice(1, StudentId, "INV-001", "RoomFee", 4_000_000m, "Unpaid", DateOnly.FromDateTime(DateTime.Now));
        context.Invoices.Add(invoice);
        context.InvoiceItems.Add(new InvoiceItem
        {
            Id = 1,
            InvoiceId = 1,
            Description = "Phí phòng học kỳ Fall 2026",
            Quantity = 1m,
            UnitPrice = 4_000_000m,
            Amount = 4_000_000m
        });

        await context.SaveChangesAsync();
        var sut = CreateSut(context);

        // Act
        var result = await sut.GetPayableInvoicesAsync(StudentId);

        // Assert
        result.Items.Should().ContainSingle();
        result.Items[0].Items.Should().ContainSingle();
        result.Items[0].Items[0].Description.Should().Be("Phí phòng học kỳ Fall 2026");
    }

    #endregion

    #region ExpireStaleTransactions

    [Fact]
    public async Task GetPayableInvoicesAsync_ExpiresStaleTransaction_AndUnlocksInvoice()
    {
        // Arrange
        var context = CreateInMemoryContext();
        context.Terms.Add(CreateTerm());
        context.Students.Add(new Student { UserId = StudentId });

        var invoice = CreateInvoice(1, StudentId, "INV-001", "RoomFee", 4_000_000m, "Processing",
            DateOnly.FromDateTime(DateTime.Now).AddDays(3));
        context.Invoices.Add(invoice);

        var transaction = new PaymentTransaction
        {
            StudentId = StudentId,
            Method = "VietQR",
            GatewayReference = "VQR-20260101000000-0009",
            TotalAmount = 4_000_000m,
            Status = PaymentTransactionStatuses.Processing,
            CreatedTime = DateTime.Now.AddMinutes(-30),
            ExpiryTime = DateTime.Now.AddMinutes(-10) // đã hết hạn
        };
        context.PaymentTransactions.Add(transaction);
        context.TransactionInvoices.Add(new TransactionInvoice
        {
            TransactionId = transaction.Id,
            InvoiceId = invoice.Id,
            AllocatedAmount = 4_000_000m
        });

        await context.SaveChangesAsync();
        var sut = CreateSut(context);

        // Act
        var result = await sut.GetPayableInvoicesAsync(StudentId);

        // Assert
        var updatedTransaction = await context.PaymentTransactions.FirstAsync();
        updatedTransaction.Status.Should().Be(PaymentTransactionStatuses.Expired);

        var updatedInvoice = await context.Invoices.FirstAsync();
        updatedInvoice.Status.Should().Be(InvoiceStatuses.Unpaid);

        result.Items.Should().ContainSingle();
        result.Items[0].HasActiveTransaction.Should().BeFalse();
    }

    #endregion

    #region FindInvoiceBySourceAsync

    [Fact]
    public async Task FindInvoiceBySourceAsync_ReturnsInvoice_ForBookingSource()
    {
        // Arrange
        var context = CreateInMemoryContext();
        context.Terms.Add(CreateTerm());
        context.Students.Add(new Student { UserId = StudentId });

        context.Invoices.Add(CreateInvoice(1, StudentId, "INV-001", "RoomFee", 4_000_000m, "Unpaid",
            DateOnly.FromDateTime(DateTime.Now), bookingRequestId: 5));
        context.Invoices.Add(CreateInvoice(2, StudentId, "INV-002", "RoomFee", 9_000_000m, "Unpaid",
            DateOnly.FromDateTime(DateTime.Now), bookingRequestId: 6));

        await context.SaveChangesAsync();
        var sut = CreateSut(context);

        // Act
        var result = await sut.FindInvoiceBySourceAsync(StudentId, "Booking", 5);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(1);
        result.InvoiceNumber.Should().Be("INV-001");
    }

    [Fact]
    public async Task FindInvoiceBySourceAsync_ReturnsNull_WhenNoMatchingInvoice()
    {
        // Arrange
        var context = CreateInMemoryContext();
        context.Terms.Add(CreateTerm());
        context.Students.Add(new Student { UserId = StudentId });
        await context.SaveChangesAsync();
        var sut = CreateSut(context);

        // Act
        var result = await sut.FindInvoiceBySourceAsync(StudentId, "Booking", 999);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task FindInvoiceBySourceAsync_IgnoresInvoice_OfAnotherStudent()
    {
        // Arrange
        var context = CreateInMemoryContext();
        context.Terms.Add(CreateTerm());
        context.Students.Add(new Student { UserId = StudentId });
        context.Invoices.Add(CreateInvoice(1, OtherStudentId, "INV-X", "RoomFee", 100_000m, "Unpaid",
            DateOnly.FromDateTime(DateTime.Now), bookingRequestId: 5));

        await context.SaveChangesAsync();
        var sut = CreateSut(context);

        // Act
        var result = await sut.FindInvoiceBySourceAsync(StudentId, "Booking", 5);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region CreateTransactionAsync

    [Fact]
    public async Task CreateTransactionAsync_CreatesProcessingTransaction_AndLocksInvoices()
    {
        // Arrange
        var context = CreateInMemoryContext();
        context.Terms.Add(CreateTerm());
        context.Students.Add(new Student { UserId = StudentId });

        var today = DateOnly.FromDateTime(DateTime.Now);
        context.Invoices.AddRange(
            CreateInvoice(1, StudentId, "INV-001", "RoomFee", 4_000_000m, "Unpaid", today.AddDays(3)),
            CreateInvoice(2, StudentId, "INV-002", "Utility", 350_000m, "Overdue", today.AddDays(-1)));

        await context.SaveChangesAsync();
        var sut = CreateSut(context);

        // Act
        var result = await sut.CreateTransactionAsync(StudentId, new CreatePaymentTransactionDto
        {
            InvoiceIds = new List<int> { 1, 2 }
        });

        // Assert
        result.Id.Should().BeGreaterThan(0);
        result.Status.Should().Be(PaymentTransactionStatuses.Processing);
        result.Method.Should().Be("VietQR");
        result.TotalAmount.Should().Be(4_350_000m);
        result.GatewayReference.Should().StartWith("VQR-");
        result.QrLink.Should().Contain("970436-FUD-compact.png");
        result.ExpiryTime.Should().BeAfter(DateTime.Now.AddMinutes(19));
        result.Invoices.Should().HaveCount(2);

        var transaction = await context.PaymentTransactions.Include(t => t.TransactionInvoices).FirstAsync();
        transaction.TransactionInvoices.Should().HaveCount(2);
        transaction.TransactionInvoices.Should().OnlyContain(ti => ti.AllocatedAmount > 0);

        var invoices = await context.Invoices.ToListAsync();
        invoices.Should().OnlyContain(i => i.Status == InvoiceStatuses.Processing);
    }

    [Fact]
    public async Task CreateTransactionAsync_RejectsInvoice_OfAnotherStudent()
    {
        // Arrange
        var context = CreateInMemoryContext();
        context.Terms.Add(CreateTerm());
        context.Students.Add(new Student { UserId = StudentId });
        context.Invoices.Add(CreateInvoice(1, OtherStudentId, "INV-X", "RoomFee", 100_000m, "Unpaid", DateOnly.FromDateTime(DateTime.Now)));

        await context.SaveChangesAsync();
        var sut = CreateSut(context);

        // Act
        var act = async () => await sut.CreateTransactionAsync(StudentId, new CreatePaymentTransactionDto
        {
            InvoiceIds = new List<int> { 1 }
        });

        // Assert
        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*không thuộc về tài khoản của bạn*");
    }

    [Fact]
    public async Task CreateTransactionAsync_RejectsInvoice_AlreadyProcessing()
    {
        // Arrange
        var context = CreateInMemoryContext();
        context.Terms.Add(CreateTerm());
        context.Students.Add(new Student { UserId = StudentId });
        context.Invoices.Add(CreateInvoice(1, StudentId, "INV-001", "RoomFee", 100_000m, "Processing", DateOnly.FromDateTime(DateTime.Now)));

        await context.SaveChangesAsync();
        var sut = CreateSut(context);

        // Act
        var act = async () => await sut.CreateTransactionAsync(StudentId, new CreatePaymentTransactionDto
        {
            InvoiceIds = new List<int> { 1 }
        });

        // Assert
        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*trạng thái*");
    }

    [Fact]
    public async Task CreateTransactionAsync_RejectsSoftDeletedInvoice()
    {
        // Arrange
        var context = CreateInMemoryContext();
        context.Terms.Add(CreateTerm());
        context.Students.Add(new Student { UserId = StudentId });

        var invoice = CreateInvoice(1, StudentId, "INV-001", "RoomFee", 100_000m, "Unpaid", DateOnly.FromDateTime(DateTime.Now));
        invoice.DeletedAt = DateTime.Now;
        context.Invoices.Add(invoice);

        await context.SaveChangesAsync();
        var sut = CreateSut(context);

        // Act
        var act = async () => await sut.CreateTransactionAsync(StudentId, new CreatePaymentTransactionDto
        {
            InvoiceIds = new List<int> { 1 }
        });

        // Assert
        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*vô hiệu*");
    }

    [Fact]
    public async Task CreateTransactionAsync_Throws_WhenInvoiceNotFound()
    {
        // Arrange
        var context = CreateInMemoryContext();
        context.Terms.Add(CreateTerm());
        context.Students.Add(new Student { UserId = StudentId });
        await context.SaveChangesAsync();
        var sut = CreateSut(context);

        // Act
        var act = async () => await sut.CreateTransactionAsync(StudentId, new CreatePaymentTransactionDto
        {
            InvoiceIds = new List<int> { 999 }
        });

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    #endregion

    #region MockConfirmAsync

    [Fact]
    public async Task MockConfirmAsync_MarksTransactionSuccess_AndInvoicesPaid()
    {
        // Arrange
        var context = CreateInMemoryContext();
        context.Terms.Add(CreateTerm());
        context.Students.Add(new Student { UserId = StudentId });
        context.Invoices.Add(CreateInvoice(1, StudentId, "INV-001", "RoomFee", 4_000_000m, "Unpaid", DateOnly.FromDateTime(DateTime.Now)));

        await context.SaveChangesAsync();
        var sut = CreateSut(context);

        var created = await sut.CreateTransactionAsync(StudentId, new CreatePaymentTransactionDto
        {
            InvoiceIds = new List<int> { 1 }
        });

        // Act
        var result = await sut.MockConfirmAsync(StudentId, created.Id, null);

        // Assert
        result.ReceiptNumber.Should().StartWith("RC-");
        result.TotalAmount.Should().Be(4_000_000m);
        result.Invoices.Should().ContainSingle();

        var transaction = await context.PaymentTransactions.FirstAsync();
        transaction.Status.Should().Be(PaymentTransactionStatuses.Success);
        transaction.PaidTime.Should().NotBeNull();
        transaction.CallbackTime.Should().NotBeNull();
        transaction.GatewayPayload.Should().Contain("Mock payment success");

        var invoice = await context.Invoices.FirstAsync();
        invoice.Status.Should().Be(InvoiceStatuses.Paid);
        invoice.PaidTime.Should().NotBeNull();

        var notification = await context.Notifications.FirstAsync();
        notification.UserId.Should().Be(StudentId);
        notification.Channel.Should().Be("InApp");
        notification.ReferenceType.Should().Be("Invoice");
        notification.ReferenceId.Should().Be("1");
        notification.ReadFlag.Should().BeFalse();
    }

    [Fact]
    public async Task MockConfirmAsync_MovesBookingRequest_ToPending_AfterPayment()
    {
        // Arrange
        var context = CreateInMemoryContext();
        context.Terms.Add(CreateTerm());
        context.Students.Add(new Student { UserId = StudentId });

        var booking = new BookingRequest
        {
            Id = 5,
            StudentId = StudentId,
            TermId = 1,
            BedId = 1,
            BookingKind = "Booking",
            AgreementToRegulations = true,
            PaymentDeadline = DateTime.Now.AddDays(2),
            Status = "Pending Payment",
            CreatedTime = DateTime.Now
        };
        context.BookingRequests.Add(booking);
        context.Invoices.Add(CreateInvoice(1, StudentId, "INV-001", "RoomFee", 4_000_000m, "Unpaid",
            DateOnly.FromDateTime(DateTime.Now), bookingRequestId: 5));

        await context.SaveChangesAsync();
        var sut = CreateSut(context);

        var created = await sut.CreateTransactionAsync(StudentId, new CreatePaymentTransactionDto
        {
            InvoiceIds = new List<int> { 1 }
        });

        // Act
        var result = await sut.MockConfirmAsync(StudentId, created.Id, null);

        // Assert
        // Sau khi thanh toán, đơn chuyển sang 'Pending' để CHỜ Ban Quản lý duyệt
        // (Manager duyệt sau đó mới tạo Residency và chuyển 'Approved').
        var updatedBooking = await context.BookingRequests.FirstAsync();
        updatedBooking.Status.Should().Be("Pending");
        result.SideEffectNotes.Should().NotBeEmpty();
    }

    [Fact]
    public async Task MockConfirmAsync_MovesKeepingBookingRequest_ToPending_AfterPayment()
    {
        // Arrange
        var context = CreateInMemoryContext();
        context.Terms.Add(CreateTerm());
        context.Students.Add(new Student { UserId = StudentId });

        var booking = new BookingRequest
        {
            Id = 6,
            StudentId = StudentId,
            TermId = 1,
            BedId = 2,
            BookingKind = "Keeping",
            AgreementToRegulations = true,
            PaymentDeadline = DateTime.Now.AddDays(2),
            Status = "Pending Payment",
            CreatedTime = DateTime.Now
        };
        context.BookingRequests.Add(booking);
        context.Invoices.Add(CreateInvoice(1, StudentId, "INV-KEEP-001", "RoomFee", 2_000_000m, "Unpaid",
            DateOnly.FromDateTime(DateTime.Now), bookingRequestId: 6));

        await context.SaveChangesAsync();
        var sut = CreateSut(context);

        var created = await sut.CreateTransactionAsync(StudentId, new CreatePaymentTransactionDto
        {
            InvoiceIds = new List<int> { 1 }
        });

        // Act
        await sut.MockConfirmAsync(StudentId, created.Id, null);

        // Assert
        // Giữ phòng: Booking_Request (BookingKind='Keeping') -> 'Pending' (chờ QL duyệt).
        // Khi QL duyệt mới chuyển 'Approved' và tạo Residency kỳ kế tiếp.
        var updatedBooking = await context.BookingRequests.FirstAsync();
        updatedBooking.Status.Should().Be("Pending");
    }

    [Fact]
    public async Task MockConfirmAsync_LogsRequestLog_ForBedTransferInvoice()
    {
        // Arrange
        var context = CreateInMemoryContext();
        context.Terms.Add(CreateTerm());
        context.Students.Add(new Student { UserId = StudentId });

        var request = new Request
        {
            Id = 7,
            StudentId = StudentId,
            TermId = 1,
            RequestType = "BedTransfer",
            Status = "Pending",
            CreatedTime = DateTime.Now
        };
        context.Requests.Add(request);
        context.Invoices.Add(CreateInvoice(1, StudentId, "INV-TRF-001", "BedTransfer", 1_803_299m, "Unpaid",
            DateOnly.FromDateTime(DateTime.Now), transferRequestId: 7));

        await context.SaveChangesAsync();
        var sut = CreateSut(context);

        var created = await sut.CreateTransactionAsync(StudentId, new CreatePaymentTransactionDto
        {
            InvoiceIds = new List<int> { 1 }
        });

        // Act
        await sut.MockConfirmAsync(StudentId, created.Id, null);

        // Assert
        var updatedRequest = await context.Requests.FirstAsync();
        updatedRequest.Status.Should().Be("Approved");

        var log = await context.RequestLogs.FirstAsync();
        log.RequestId.Should().Be(7);
        log.FromStatus.Should().Be("Pending");
        log.ToStatus.Should().Be("Approved");
        log.Note.Should().Contain("đã thanh toán phí chuyển giường");
        log.ChangedById.Should().Be(StudentId);
    }

    [Fact]
    public async Task MockConfirmAsync_CompletesCheckoutRequest_WhenAllHandoverInvoicesPaid()
    {
        // Arrange
        var context = CreateInMemoryContext();
        context.Terms.Add(CreateTerm());
        context.Students.Add(new Student { UserId = StudentId });

        var checkoutRequest = new Request
        {
            Id = 9,
            StudentId = StudentId,
            TermId = 1,
            RequestType = "Checkout",
            Status = "Approved",
            CreatedTime = DateTime.Now
        };
        context.Requests.Add(checkoutRequest);

        var handover = new Handover
        {
            Id = 3,
            HandoverType = "Checkout",
            RequestId = 9,
            RoomId = 1,
            GuardId = 1,
            Status = "Draft",
            CreatedTime = DateTime.Now
        };
        context.Handovers.Add(handover);

        context.Invoices.Add(CreateInvoice(1, StudentId, "INV-CMP-001", "Compensation", 350_000m, "Unpaid",
            DateOnly.FromDateTime(DateTime.Now), handoverId: 3));

        await context.SaveChangesAsync();
        var sut = CreateSut(context);

        var created = await sut.CreateTransactionAsync(StudentId, new CreatePaymentTransactionDto
        {
            InvoiceIds = new List<int> { 1 }
        });

        // Act
        await sut.MockConfirmAsync(StudentId, created.Id, null);

        // Assert
        var updatedRequest = await context.Requests.FirstAsync();
        updatedRequest.Status.Should().Be("Completed");

        var log = await context.RequestLogs.FirstAsync();
        log.Note.Should().Contain("hoàn tất thanh toán phí bàn giao/trả phòng");
    }

    [Fact]
    public async Task MockConfirmAsync_KeepsCheckoutRequestApproved_WhenOtherHandoverInvoiceStillUnpaid()
    {
        // Arrange
        var context = CreateInMemoryContext();
        context.Terms.Add(CreateTerm());
        context.Students.Add(new Student { UserId = StudentId });

        var checkoutRequest = new Request
        {
            Id = 9,
            StudentId = StudentId,
            TermId = 1,
            RequestType = "Checkout",
            Status = "Approved",
            CreatedTime = DateTime.Now
        };
        context.Requests.Add(checkoutRequest);

        var handover = new Handover
        {
            Id = 3,
            HandoverType = "Checkout",
            RequestId = 9,
            RoomId = 1,
            GuardId = 1,
            Status = "Draft",
            CreatedTime = DateTime.Now
        };
        context.Handovers.Add(handover);

        // Hóa đơn 1 sẽ được thanh toán, hóa đơn 2 vẫn còn Unpaid.
        context.Invoices.AddRange(
            CreateInvoice(1, StudentId, "INV-CMP-001", "Compensation", 350_000m, "Unpaid",
                DateOnly.FromDateTime(DateTime.Now), handoverId: 3),
            CreateInvoice(2, StudentId, "INV-UTL-002", "Utility", 120_000m, "Unpaid",
                DateOnly.FromDateTime(DateTime.Now), handoverId: 3));

        await context.SaveChangesAsync();
        var sut = CreateSut(context);

        var created = await sut.CreateTransactionAsync(StudentId, new CreatePaymentTransactionDto
        {
            InvoiceIds = new List<int> { 1 }
        });

        // Act
        await sut.MockConfirmAsync(StudentId, created.Id, null);

        // Assert
        var updatedRequest = await context.Requests.FirstAsync();
        updatedRequest.Status.Should().Be("Approved");
        (await context.RequestLogs.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task MockConfirmAsync_Throws_WhenTransactionNotProcessing()
    {
        // Arrange
        var context = CreateInMemoryContext();
        context.Terms.Add(CreateTerm());
        context.Students.Add(new Student { UserId = StudentId });

        var transaction = new PaymentTransaction
        {
            StudentId = StudentId,
            Method = "VietQR",
            GatewayReference = "VQR-20260101000000-0001",
            TotalAmount = 100_000m,
            Status = PaymentTransactionStatuses.Success,
            CreatedTime = DateTime.Now,
            ExpiryTime = DateTime.Now.AddMinutes(20)
        };
        context.PaymentTransactions.Add(transaction);
        await context.SaveChangesAsync();

        var sut = CreateSut(context);

        // Act
        var act = async () => await sut.MockConfirmAsync(StudentId, transaction.Id, null);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*trạng thái*");
    }

    [Fact]
    public async Task MockConfirmAsync_Throws_WhenTransactionBelongsToAnotherStudent()
    {
        // Arrange
        var context = CreateInMemoryContext();
        context.Terms.Add(CreateTerm());
        context.Students.Add(new Student { UserId = StudentId });

        var transaction = new PaymentTransaction
        {
            StudentId = OtherStudentId,
            Method = "VietQR",
            GatewayReference = "VQR-20260101000000-0002",
            TotalAmount = 100_000m,
            Status = PaymentTransactionStatuses.Processing,
            CreatedTime = DateTime.Now,
            ExpiryTime = DateTime.Now.AddMinutes(20)
        };
        context.PaymentTransactions.Add(transaction);
        await context.SaveChangesAsync();

        var sut = CreateSut(context);

        // Act
        var act = async () => await sut.MockConfirmAsync(StudentId, transaction.Id, null);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    #endregion

    #region CancelTransactionAsync

    [Fact]
    public async Task CancelTransactionAsync_CancelsTransaction_AndUnlocksInvoices()
    {
        // Arrange
        var context = CreateInMemoryContext();
        context.Terms.Add(CreateTerm());
        context.Students.Add(new Student { UserId = StudentId });

        var today = DateOnly.FromDateTime(DateTime.Now);
        context.Invoices.AddRange(
            CreateInvoice(1, StudentId, "INV-001", "RoomFee", 4_000_000m, "Unpaid", today.AddDays(5)),
            CreateInvoice(2, StudentId, "INV-002", "Utility", 100_000m, "Overdue", today.AddDays(-1)));

        await context.SaveChangesAsync();
        var sut = CreateSut(context);

        var created = await sut.CreateTransactionAsync(StudentId, new CreatePaymentTransactionDto
        {
            InvoiceIds = new List<int> { 1, 2 }
        });

        // Act
        var result = await sut.CancelTransactionAsync(StudentId, created.Id, null);

        // Assert
        result.Status.Should().Be(PaymentTransactionStatuses.Cancelled);
        result.UnlockedInvoices.Should().HaveCount(2);

        var transaction = await context.PaymentTransactions.FirstAsync();
        transaction.Status.Should().Be(PaymentTransactionStatuses.Cancelled);

        var invoices = await context.Invoices.OrderBy(i => i.Id).ToListAsync();
        invoices[0].Status.Should().Be(InvoiceStatuses.Unpaid);   // chưa quá hạn
        invoices[1].Status.Should().Be(InvoiceStatuses.Overdue);  // đã quá hạn
    }

    [Fact]
    public async Task CancelTransactionAsync_Throws_WhenTransactionAlreadySucceeded()
    {
        // Arrange
        var context = CreateInMemoryContext();
        context.Terms.Add(CreateTerm());
        context.Students.Add(new Student { UserId = StudentId });

        var transaction = new PaymentTransaction
        {
            StudentId = StudentId,
            Method = "VietQR",
            GatewayReference = "VQR-20260101000000-0003",
            TotalAmount = 100_000m,
            Status = PaymentTransactionStatuses.Success,
            CreatedTime = DateTime.Now,
            ExpiryTime = DateTime.Now.AddMinutes(20)
        };
        context.PaymentTransactions.Add(transaction);
        await context.SaveChangesAsync();

        var sut = CreateSut(context);

        // Act
        var act = async () => await sut.CancelTransactionAsync(StudentId, transaction.Id, null);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    #endregion

    #region SePayWebhookAndMockConfirmSecurity

    [Fact]
    public async Task ProcessSePayWebhookAsync_WhenWebhookApiKeyNotConfigured_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var context = CreateInMemoryContext();
        var config = new ConfigurationBuilder().Build();
        var sut = CreateSut(context, config);
        var payload = new SePayWebhookPayloadDto
        {
            TransferType = "in",
            TransferAmount = 100_000m,
            Content = "VQR-20260101000000-0001"
        };

        // Act
        var act = () => sut.ProcessSePayWebhookAsync(payload, "Apikey any-key");

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*chưa được cấu hình*");
    }

    [Fact]
    public async Task ProcessSePayWebhookAsync_WhenApiKeyInvalid_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var context = CreateInMemoryContext();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SePay:WebhookApiKey"] = "test-sepay-secret-key"
            })
            .Build();
        var sut = CreateSut(context, config);
        var payload = new SePayWebhookPayloadDto
        {
            TransferType = "in",
            TransferAmount = 100_000m,
            Content = "VQR-20260101000000-0001"
        };

        // Act
        var act = () => sut.ProcessSePayWebhookAsync(payload, "Apikey wrong-key");

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*không hợp lệ*");
    }

    [Fact]
    public async Task ProcessSePayWebhookAsync_WhenValidApikeyHeader_AcceptsAuth()
    {
        // Arrange
        var context = CreateInMemoryContext();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SePay:WebhookApiKey"] = "test-sepay-secret-key"
            })
            .Build();
        var sut = CreateSut(context, config);
        var payload = new SePayWebhookPayloadDto
        {
            TransferType = "in",
            TransferAmount = 100_000m,
            Content = "VQR-20260101000000-0001"
        };

        // Act
        var (handled, message, receipt) = await sut.ProcessSePayWebhookAsync(payload, "Apikey test-sepay-secret-key");

        // Assert
        handled.Should().BeFalse();
        receipt.Should().BeNull();
        message.Should().Contain("Processing");
    }

    [Fact]
    public async Task MockConfirm_WhenProductionEnvironment_ReturnsNotFound()
    {
        // Arrange
        var serviceMock = new Mock<IStudentPaymentService>();
        var envMock = new Mock<IWebHostEnvironment>();
        envMock.SetupGet(e => e.EnvironmentName).Returns("Production");
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Payments:EnableMockConfirm"] = "true"
            })
            .Build();

        var controller = new StudentPaymentsController(serviceMock.Object, envMock.Object, config);

        // Act
        var result = await controller.MockConfirm(1, null);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
        serviceMock.Verify(
            s => s.MockConfirmAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<MockConfirmPaymentDto?>()),
            Times.Never);
    }

    [Fact]
    public async Task MockConfirm_WhenDevelopmentAndMockDisabled_ReturnsNotFound()
    {
        // Arrange
        var serviceMock = new Mock<IStudentPaymentService>();
        var envMock = new Mock<IWebHostEnvironment>();
        envMock.SetupGet(e => e.EnvironmentName).Returns("Development");
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Payments:EnableMockConfirm"] = "false"
            })
            .Build();

        var controller = new StudentPaymentsController(serviceMock.Object, envMock.Object, config);

        // Act
        var result = await controller.MockConfirm(1, null);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
        serviceMock.Verify(
            s => s.MockConfirmAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<MockConfirmPaymentDto?>()),
            Times.Never);
    }

    private static IConfiguration CreateSePayWebhookConfig(string apiKey = "test-sepay-secret-key")
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SePay:WebhookApiKey"] = apiKey
            })
            .Build();
    }

    [Fact]
    public async Task ProcessSePayWebhookAsync_WhenTransferAmountLessThanTotalAmount_RejectsAndLeavesInvoiceProcessing()
    {
        // Arrange
        var context = CreateInMemoryContext();
        context.Terms.Add(CreateTerm());
        context.Students.Add(new Student { UserId = StudentId });
        context.Invoices.Add(CreateInvoice(1, StudentId, "INV-001", "RoomFee", 4_000_000m, "Unpaid", DateOnly.FromDateTime(DateTime.Now).AddDays(3)));
        await context.SaveChangesAsync();

        var sut = CreateSut(context, CreateSePayWebhookConfig());
        var created = await sut.CreateTransactionAsync(StudentId, new CreatePaymentTransactionDto { InvoiceIds = new List<int> { 1 } });

        var payload = new SePayWebhookPayloadDto
        {
            Id = 1001,
            TransferType = "in",
            TransferAmount = 3_999_999m,
            Content = created.GatewayReference
        };

        // Act
        var (handled, message, receipt) = await sut.ProcessSePayWebhookAsync(payload, "Apikey test-sepay-secret-key");

        // Assert
        handled.Should().BeFalse();
        receipt.Should().BeNull();
        message.Should().Contain("Số tiền chuyển khoản không khớp");

        var tx = await context.PaymentTransactions.FirstAsync(t => t.Id == created.Id);
        tx.Status.Should().Be(PaymentTransactionStatuses.Processing);

        var invoice = await context.Invoices.FirstAsync(i => i.Id == 1);
        invoice.Status.Should().Be(InvoiceStatuses.Processing);
    }

    [Fact]
    public async Task ProcessSePayWebhookAsync_WhenTransferAmountGreaterThanTotalAmount_RejectsAndLeavesInvoiceProcessing()
    {
        // Arrange
        var context = CreateInMemoryContext();
        context.Terms.Add(CreateTerm());
        context.Students.Add(new Student { UserId = StudentId });
        context.Invoices.Add(CreateInvoice(1, StudentId, "INV-001", "RoomFee", 4_000_000m, "Unpaid", DateOnly.FromDateTime(DateTime.Now).AddDays(3)));
        await context.SaveChangesAsync();

        var sut = CreateSut(context, CreateSePayWebhookConfig());
        var created = await sut.CreateTransactionAsync(StudentId, new CreatePaymentTransactionDto { InvoiceIds = new List<int> { 1 } });

        var payload = new SePayWebhookPayloadDto
        {
            Id = 1002,
            TransferType = "in",
            TransferAmount = 4_500_000m,
            Content = created.GatewayReference
        };

        // Act
        var (handled, message, receipt) = await sut.ProcessSePayWebhookAsync(payload, "Apikey test-sepay-secret-key");

        // Assert
        handled.Should().BeFalse();
        receipt.Should().BeNull();
        message.Should().Contain("Số tiền chuyển khoản không khớp");

        var tx = await context.PaymentTransactions.FirstAsync(t => t.Id == created.Id);
        tx.Status.Should().Be(PaymentTransactionStatuses.Processing);

        var invoice = await context.Invoices.FirstAsync(i => i.Id == 1);
        invoice.Status.Should().Be(InvoiceStatuses.Processing);
    }

    [Fact]
    public async Task ProcessSePayWebhookAsync_WhenReferenceMissingButAmountMatches_DoesNotMatchUnrelatedTransaction()
    {
        // Arrange
        var context = CreateInMemoryContext();
        context.Terms.Add(CreateTerm());
        context.Students.Add(new Student { UserId = StudentId });
        context.Invoices.Add(CreateInvoice(1, StudentId, "INV-001", "RoomFee", 4_000_000m, "Unpaid", DateOnly.FromDateTime(DateTime.Now).AddDays(3)));
        await context.SaveChangesAsync();

        var sut = CreateSut(context, CreateSePayWebhookConfig());
        var created = await sut.CreateTransactionAsync(StudentId, new CreatePaymentTransactionDto { InvoiceIds = new List<int> { 1 } });

        var payload = new SePayWebhookPayloadDto
        {
            Id = 1003,
            TransferType = "in",
            TransferAmount = 4_000_000m,
            Content = "CHUYEN KHOAN TIEN PHONG KHONG CO MA VQR"
        };

        // Act
        var (handled, _, receipt) = await sut.ProcessSePayWebhookAsync(payload, "Apikey test-sepay-secret-key");

        // Assert
        handled.Should().BeFalse();
        receipt.Should().BeNull();

        var tx = await context.PaymentTransactions.FirstAsync(t => t.Id == created.Id);
        tx.Status.Should().Be(PaymentTransactionStatuses.Processing);
    }

    [Fact]
    public async Task ProcessSePayWebhookAsync_WhenReferenceMissingOnTestAccount_DoesNotSettleArbitraryTransaction()
    {
        // Arrange
        var context = CreateInMemoryContext();
        context.Terms.Add(CreateTerm());
        context.Students.Add(new Student { UserId = StudentId });
        context.Invoices.Add(CreateInvoice(1, StudentId, "INV-001", "RoomFee", 4_000_000m, "Unpaid", DateOnly.FromDateTime(DateTime.Now).AddDays(3)));
        await context.SaveChangesAsync();

        var sut = CreateSut(context, CreateSePayWebhookConfig());
        var created = await sut.CreateTransactionAsync(StudentId, new CreatePaymentTransactionDto { InvoiceIds = new List<int> { 1 } });

        var payload = new SePayWebhookPayloadDto
        {
            Id = 1004,
            TransferType = "in",
            TransferAmount = 1_000m,
            AccountNumber = "0000000001",
            SubAccount = "96247FUD26",
            Content = "NO REF"
        };

        // Act
        var (handled, _, receipt) = await sut.ProcessSePayWebhookAsync(payload, "Apikey test-sepay-secret-key");

        // Assert
        handled.Should().BeFalse();
        receipt.Should().BeNull();

        var tx = await context.PaymentTransactions.FirstAsync(t => t.Id == created.Id);
        tx.Status.Should().Be(PaymentTransactionStatuses.Processing);
    }

    [Fact]
    public async Task ProcessSePayWebhookAsync_WhenSameSePayIdReplayed_IsIdempotentAndDoesNotRerunSideEffects()
    {
        // Arrange
        var context = CreateInMemoryContext();
        context.Terms.Add(CreateTerm());
        context.Students.Add(new Student { UserId = StudentId });
        context.Invoices.Add(CreateInvoice(1, StudentId, "INV-001", "RoomFee", 4_000_000m, "Unpaid", DateOnly.FromDateTime(DateTime.Now).AddDays(3)));
        await context.SaveChangesAsync();

        var sut = CreateSut(context, CreateSePayWebhookConfig());
        var created = await sut.CreateTransactionAsync(StudentId, new CreatePaymentTransactionDto { InvoiceIds = new List<int> { 1 } });

        var payload = new SePayWebhookPayloadDto
        {
            Id = 999888,
            TransferType = "in",
            TransferAmount = 4_000_000m,
            Content = created.GatewayReference
        };

        // Act 1: First processing
        var first = await sut.ProcessSePayWebhookAsync(payload, "Apikey test-sepay-secret-key");
        // Act 2: Replayed webhook with same SePay Id
        var second = await sut.ProcessSePayWebhookAsync(payload, "Apikey test-sepay-secret-key");

        // Assert
        first.Handled.Should().BeTrue();
        first.Receipt.Should().NotBeNull();

        second.Handled.Should().BeTrue();
        second.Receipt.Should().BeNull();
        second.Message.Should().Contain("idempotent");

        (await context.Notifications.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task ProcessSePayWebhookAsync_WhenTransactionExpired_DoesNotConfirm()
    {
        // Arrange
        var context = CreateInMemoryContext();
        context.Terms.Add(CreateTerm());
        context.Students.Add(new Student { UserId = StudentId });
        context.Invoices.Add(CreateInvoice(1, StudentId, "INV-001", "RoomFee", 4_000_000m, "Processing", DateOnly.FromDateTime(DateTime.Now).AddDays(3)));

        var tx = new PaymentTransaction
        {
            Id = 55,
            StudentId = StudentId,
            Method = "VietQR",
            GatewayReference = "VQR-20260101000000-0055",
            TotalAmount = 4_000_000m,
            Status = PaymentTransactionStatuses.Processing,
            CreatedTime = DateTime.Now.AddMinutes(-30),
            ExpiryTime = DateTime.Now.AddMinutes(-1)
        };
        context.PaymentTransactions.Add(tx);
        context.TransactionInvoices.Add(new TransactionInvoice
        {
            TransactionId = 55,
            InvoiceId = 1,
            AllocatedAmount = 4_000_000m
        });
        await context.SaveChangesAsync();

        var sut = CreateSut(context, CreateSePayWebhookConfig());
        var payload = new SePayWebhookPayloadDto
        {
            Id = 1006,
            TransferType = "in",
            TransferAmount = 4_000_000m,
            Content = "VQR-20260101000000-0055"
        };

        // Act
        var (handled, message, receipt) = await sut.ProcessSePayWebhookAsync(payload, "Apikey test-sepay-secret-key");

        // Assert
        handled.Should().BeFalse();
        receipt.Should().BeNull();
        message.Should().Contain("hết hạn");

        var reloadedTx = await context.PaymentTransactions.FirstAsync(t => t.Id == 55);
        reloadedTx.Status.Should().Be(PaymentTransactionStatuses.Processing);
    }

    [Fact]
    public async Task MockConfirmAsync_WhenTransactionExpired_ThrowsInvalidOperationException()
    {
        // Arrange
        var context = CreateInMemoryContext();
        context.Terms.Add(CreateTerm());
        context.Students.Add(new Student { UserId = StudentId });
        context.Invoices.Add(CreateInvoice(1, StudentId, "INV-001", "RoomFee", 4_000_000m, "Processing", DateOnly.FromDateTime(DateTime.Now).AddDays(3)));

        var tx = new PaymentTransaction
        {
            Id = 56,
            StudentId = StudentId,
            Method = "VietQR",
            GatewayReference = "VQR-20260101000000-0056",
            TotalAmount = 4_000_000m,
            Status = PaymentTransactionStatuses.Processing,
            CreatedTime = DateTime.Now.AddMinutes(-30),
            ExpiryTime = DateTime.Now.AddMinutes(-1)
        };
        context.PaymentTransactions.Add(tx);
        context.TransactionInvoices.Add(new TransactionInvoice
        {
            TransactionId = 56,
            InvoiceId = 1,
            AllocatedAmount = 4_000_000m
        });
        await context.SaveChangesAsync();

        var sut = CreateSut(context);

        // Act
        var act = () => sut.MockConfirmAsync(StudentId, 56, null);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*hết hạn*");
    }

    [Fact]
    public async Task ProcessSePayWebhookAsync_WhenExactReferenceAndExactAmount_ConfirmsAndPersistsGatewayPayload()
    {
        // Arrange
        var context = CreateInMemoryContext();
        context.Terms.Add(CreateTerm());
        context.Students.Add(new Student { UserId = StudentId });
        context.Invoices.Add(CreateInvoice(1, StudentId, "INV-001", "RoomFee", 4_000_000m, "Unpaid", DateOnly.FromDateTime(DateTime.Now).AddDays(3)));
        await context.SaveChangesAsync();

        var sut = CreateSut(context, CreateSePayWebhookConfig());
        var created = await sut.CreateTransactionAsync(StudentId, new CreatePaymentTransactionDto { InvoiceIds = new List<int> { 1 } });

        var payload = new SePayWebhookPayloadDto
        {
            Id = 777111,
            TransferType = "in",
            TransferAmount = 4_000_000m,
            Content = $"Thanh toan {created.GatewayReference}"
        };

        // Act
        var (handled, _, receipt) = await sut.ProcessSePayWebhookAsync(payload, "Apikey test-sepay-secret-key");

        // Assert
        handled.Should().BeTrue();
        receipt.Should().NotBeNull();
        receipt!.TotalAmount.Should().Be(4_000_000m);

        var tx = await context.PaymentTransactions.FirstAsync(t => t.Id == created.Id);
        tx.Status.Should().Be(PaymentTransactionStatuses.Success);
        tx.GatewayPayload.Should().Contain("777111");

        var invoice = await context.Invoices.FirstAsync(i => i.Id == 1);
        invoice.Status.Should().Be(InvoiceStatuses.Paid);
    }

    #endregion

    #region BedTransfer Invoice Source & SideEffects

    [Fact]
    public async Task GetPayableInvoicesAsync_MapsBedTransferUtilityAndCompensationSourceCorrectly()
    {
        // Arrange
        var context = CreateInMemoryContext();
        context.Terms.Add(CreateTerm());
        context.Students.Add(new Student { UserId = StudentId });

        var today = DateOnly.FromDateTime(DateTime.Now);
        context.Handovers.Add(new Handover
        {
            Id = 300,
            HandoverType = "BedTransfer",
            RequestId = 77,
            RoomId = 1,
            GuardId = 5,
            Status = "Completed",
            TotalCompensation = 120_000m
        });

        context.Invoices.AddRange(
            CreateInvoice(1, StudentId, "INV-TRF-77-A", InvoiceTypes.BedTransfer, 500_000m, InvoiceStatuses.Unpaid, today.AddDays(7), transferRequestId: 77),
            CreateInvoice(2, StudentId, "INV-TRF-78-U", InvoiceTypes.Utility, 60_000m, InvoiceStatuses.Unpaid, today.AddDays(7), transferRequestId: 78),
            CreateInvoice(3, StudentId, "INV-CMP-77-C", InvoiceTypes.Compensation, 120_000m, InvoiceStatuses.Unpaid, today.AddDays(7), handoverId: 300));
        await context.SaveChangesAsync();

        var sut = CreateSut(context);

        // Act
        var result = await sut.GetPayableInvoicesAsync(StudentId);

        // Assert
        result.Items.Should().HaveCount(3);
        result.Items.Single(i => i.Id == 1).Should().Match<StudentInvoiceDto>(d => d.SourceType == "BedTransfer" && d.SourceId == 77);
        result.Items.Single(i => i.Id == 2).Should().Match<StudentInvoiceDto>(d => d.SourceType == "BedTransfer" && d.SourceId == 78);
        result.Items.Single(i => i.Id == 3).Should().Match<StudentInvoiceDto>(d => d.SourceType == "BedTransfer" && d.SourceId == 77);
    }

    [Fact]
    public async Task FindInvoiceBySourceAsync_BedTransfer_MatchesUtilityOnlyAndCompensationInvoices()
    {
        // Arrange
        var context = CreateInMemoryContext();
        context.Terms.Add(CreateTerm());
        context.Students.Add(new Student { UserId = StudentId });

        var today = DateOnly.FromDateTime(DateTime.Now);
        context.Handovers.Add(new Handover
        {
            Id = 310,
            HandoverType = "BedTransfer",
            RequestId = 88,
            RoomId = 1,
            GuardId = 5,
            Status = "Completed",
            TotalCompensation = 150_000m
        });

        context.Invoices.AddRange(
            CreateInvoice(10, StudentId, "INV-TRF-87-U", InvoiceTypes.Utility, 45_000m, InvoiceStatuses.Unpaid, today.AddDays(7), transferRequestId: 87),
            CreateInvoice(11, StudentId, "INV-CMP-88-C", InvoiceTypes.Compensation, 150_000m, InvoiceStatuses.Unpaid, today.AddDays(7), handoverId: 310));
        await context.SaveChangesAsync();

        var sut = CreateSut(context);

        // Act
        var utilMatch = await sut.FindInvoiceBySourceAsync(StudentId, "BedTransfer", 87);
        var compMatch = await sut.FindInvoiceBySourceAsync(StudentId, "BedTransfer", 88);

        // Assert
        utilMatch.Should().NotBeNull();
        utilMatch!.Id.Should().Be(10);
        utilMatch.SourceType.Should().Be("BedTransfer");
        utilMatch.SourceId.Should().Be(87);

        compMatch.Should().NotBeNull();
        compMatch!.Id.Should().Be(11);
        compMatch.SourceType.Should().Be("BedTransfer");
        compMatch.SourceId.Should().Be(88);
    }

    [Fact]
    public async Task MockConfirmAsync_BedTransferCompensationInvoice_LogsRequestLogAndSideEffect()
    {
        // Arrange
        var context = CreateInMemoryContext();
        context.Terms.Add(CreateTerm());
        context.Students.Add(new Student { UserId = StudentId });
        context.Requests.Add(new Request
        {
            Id = 77,
            StudentId = StudentId,
            TermId = 1,
            RequestType = "BedTransfer",
            Status = "Completed",
            CreatedTime = DateTime.Now.AddDays(-2)
        });
        context.Handovers.Add(new Handover
        {
            Id = 300,
            HandoverType = "BedTransfer",
            RequestId = 77,
            RoomId = 1,
            GuardId = 5,
            Status = "Completed",
            TotalCompensation = 120_000m
        });
        context.Invoices.Add(CreateInvoice(
            1,
            StudentId,
            "INV-CMP-77",
            InvoiceTypes.Compensation,
            120_000m,
            InvoiceStatuses.Unpaid,
            DateOnly.FromDateTime(DateTime.Now).AddDays(7),
            handoverId: 300));
        await context.SaveChangesAsync();

        var sut = CreateSut(context);
        var created = await sut.CreateTransactionAsync(StudentId, new CreatePaymentTransactionDto { InvoiceIds = new List<int> { 1 } });

        // Act
        var receipt = await sut.MockConfirmAsync(StudentId, created.Id, null);

        // Assert
        receipt.SideEffectNotes.Should().Contain(n => n.Contains("#77") && n.Contains("bồi thường"));
        var log = await context.RequestLogs.FirstOrDefaultAsync(l => l.RequestId == 77);
        log.Should().NotBeNull();
        log!.Note.Should().Contain("bồi thường thiết bị chuyển giường");
    }

    #endregion
}
