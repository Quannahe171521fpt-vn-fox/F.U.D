namespace FUD.Services.Interfaces.Payments
{
    /// <summary>
    /// Tạo transaction cho các thao tác ghi của luồng thanh toán.
    /// Tự động dùng no-op transaction khi provider không hỗ trợ (EF Core InMemory trong unit test).
    /// </summary>
    public interface IPaymentTransactionFactory
    {
        Task<IPaymentTransaction> BeginAsync();
    }
}
