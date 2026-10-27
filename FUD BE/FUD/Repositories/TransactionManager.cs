using FUD.Repositories.Interfaces;
using FUD.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace FUD.Repositories.Accounts
{
    public class TransactionManager : ITransactionManager
    {
        private readonly FudContext _context;

        public TransactionManager(FudContext context)
        {
            _context = context;
        }

        public async Task<ITransactionScope> BeginTransactionAsync(CancellationToken cancellationToken = default)
        {
            var tx = await _context.Database.BeginTransactionAsync(cancellationToken);
            return new TransactionScope(tx);
        }

        private sealed class TransactionScope : ITransactionScope
        {
            private readonly IDbContextTransaction _transaction;

            public TransactionScope(IDbContextTransaction transaction)
            {
                _transaction = transaction;
            }

            public async Task CommitAsync(CancellationToken cancellationToken = default)
                => await _transaction.CommitAsync(cancellationToken);

            public async Task RollbackAsync(CancellationToken cancellationToken = default)
                => await _transaction.RollbackAsync(cancellationToken);

            public async ValueTask DisposeAsync()
                => await _transaction.DisposeAsync();
        }
    }
}
