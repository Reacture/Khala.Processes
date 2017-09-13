namespace Khala.Processes.Sql
{
    using System;
    using System.Data.Entity;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Threading;
    using System.Threading.Tasks;

    public sealed class SqlProcessManagerDataContext<T> : IDisposable
        where T : ProcessManager
    {
        private IProcessManagerDbContext<T> _dbContext;

        public SqlProcessManagerDataContext(IProcessManagerDbContext<T> dbContext)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        public void Dispose() => _dbContext.Dispose();

        public Task<T> Find(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken)
        {
            if (predicate == null)
            {
                throw new ArgumentNullException(nameof(predicate));
            }

            return _dbContext
                .ProcessManagers
                .Where(predicate)
                .SingleOrDefaultAsync(cancellationToken);
        }

        public Task Save(T processManager, CancellationToken cancellationToken)
        {
            if (processManager == null)
            {
                throw new ArgumentNullException(nameof(processManager));
            }

            if (_dbContext.Entry(processManager).State == EntityState.Detached)
            {
                _dbContext.ProcessManagers.Add(processManager);
            }

            return _dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
