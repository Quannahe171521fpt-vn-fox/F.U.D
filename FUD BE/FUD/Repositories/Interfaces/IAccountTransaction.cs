using System;
using System.Threading.Tasks;

namespace FUD.Repositories.Interfaces;

/// <summary>Trừu tượng giao dịch DB cho Service Layer (ẩn chi tiết EF Core).</summary>
public interface IAccountTransaction : IAsyncDisposable
{
    Task CommitAsync();
    Task RollbackAsync();
}
