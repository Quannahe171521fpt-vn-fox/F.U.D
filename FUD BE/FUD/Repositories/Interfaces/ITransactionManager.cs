using FUD.Models;

namespace FUD.Repositories.Interfaces
{
    /// <summary>
    /// Quản lý transaction của EF Core cho các thao tác nhiều bước
    /// (đổi trạng thái + ghi lịch sử + thu hồi token).
    /// </summary>
    public interface ITransactionManager
    {
        Task<ITransactionScope> BeginTransactionAsync(CancellationToken cancellationToken = default);
    }

    public interface ITransactionScope : IAsyncDisposable
    {
        Task CommitAsync(CancellationToken cancellationToken = default);
        Task RollbackAsync(CancellationToken cancellationToken = default);
    }
}
