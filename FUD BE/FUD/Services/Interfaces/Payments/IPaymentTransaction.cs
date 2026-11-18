namespace FUD.Services.Interfaces.Payments
{
    /// <summary>
    /// Wrapper quanh <c>IDbContextTransaction</c> để tầng Service không phụ thuộc trực tiếp vào EF Core.
    ///
    /// Lưu ý: EF Core InMemory provider (dùng cho unit test) không hỗ trợ transaction,
    /// nên khi chạy trên provider đó sẽ trả về no-op transaction.
    /// </summary>
    public interface IPaymentTransaction : IAsyncDisposable
    {
        Task CommitAsync();
        Task RollbackAsync();
    }
}
