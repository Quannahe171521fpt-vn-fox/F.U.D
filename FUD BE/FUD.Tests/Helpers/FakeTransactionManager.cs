using FUD.Infrastructure.Security;
using FUD.Repositories.Interfaces;
using FUD.Models;
using Moq;

namespace FUD.Tests.Helpers
{
    /// <summary>
    /// Transaction manager giả — không mở transaction thật, chỉ ghi nhận
    /// Commit/Rollback để assert trong unit test.
    /// </summary>
    public class FakeTransactionManager : ITransactionManager
    {
        public int Committed { get; private set; }
        public int RolledBack { get; private set; }

        public Task<ITransactionScope> BeginTransactionAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<ITransactionScope>(new FakeTransactionScope(this));

        private sealed class FakeTransactionScope : ITransactionScope
        {
            private readonly FakeTransactionManager _owner;

            public FakeTransactionScope(FakeTransactionManager owner)
            {
                _owner = owner;
            }

            public Task CommitAsync(CancellationToken cancellationToken = default)
            {
                _owner.Committed++;
                return Task.CompletedTask;
            }

            public Task RollbackAsync(CancellationToken cancellationToken = default)
            {
                _owner.RolledBack++;
                return Task.CompletedTask;
            }

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }

    /// <summary>Password hasher giả — trả về chuỗi cố định, không cần BCrypt thật.</summary>
    public class FakePasswordHasher : IPasswordHasher
    {
        public string Hash(string password) => $"hashed:{password}";

        public bool Verify(string password, string passwordHash)
            => passwordHash == $"hashed:{password}";
    }
}
