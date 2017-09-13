namespace Khala.Processes.Sql
{
    using System;

    public sealed class SqlProcessManagerDataContext<T> : IDisposable
        where T : ProcessManager
    {
        private IProcessManagerDbContext<T> _dbContext;

        public SqlProcessManagerDataContext(IProcessManagerDbContext<T> dbContext)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        public void Dispose() => _dbContext.Dispose();
    }
}
