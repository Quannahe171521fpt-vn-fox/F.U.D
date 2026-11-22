using FUD.Models;
using FUD.Services.Interfaces.Payments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace FUD.Services.Payments
{
    /// <summary>
    /// Triển khai mặc định của <see cref="IPaymentTransactionFactory"/> dựa trên <see cref="FudContext"/>.
    /// </summary>
    public class PaymentTransactionFactory : IPaymentTransactionFactory
    {
        private readonly FudContext _context;

        public PaymentTransactionFactory(FudContext context)
        {
            _context = context;
        }

        public async Task<IPaymentTransaction> BeginAsync()
        {
            var providerName = _context.Database.ProviderName;
            if (providerName != null && providerName.Contains("InMemory", StringComparison.OrdinalIgnoreCase))
            {
                return new NoOpPaymentTransaction();
            }

            var tx = await _context.Database.BeginTransactionAsync();
            return new EfPaymentTransaction(tx);
        }

        /// <summary>Transaction no-op cho provider không hỗ trợ transaction (ví dụ EF Core InMemory).</summary>
        private sealed class NoOpPaymentTransaction : IPaymentTransaction
        {
            public Task CommitAsync() => Task.CompletedTask;

            public Task RollbackAsync() => Task.CompletedTask;

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }

        /// <summary>Wrapper cho <see cref="IDbContextTransaction"/>.</summary>
        private sealed class EfPaymentTransaction : IPaymentTransaction
        {
            private readonly IDbContextTransaction _transaction;

            public EfPaymentTransaction(IDbContextTransaction transaction)
            {
                _transaction = transaction;
            }

            public async Task CommitAsync() => await _transaction.CommitAsync();

            public async Task RollbackAsync() => await _transaction.RollbackAsync();

            public async ValueTask DisposeAsync() => await _transaction.DisposeAsync();
        }
    }
}
