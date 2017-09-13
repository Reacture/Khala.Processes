namespace Khala.Processes.Sql
{
    using System;
    using System.Collections.Generic;
    using System.Data.Entity;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Threading;
    using System.Threading.Tasks;
    using Khala.Messaging;

    public sealed class SqlProcessManagerDataContext<T> : IDisposable
        where T : ProcessManager
    {
        private readonly IProcessManagerDbContext<T> _dbContext;
        private readonly IMessageSerializer _serializer;

        public SqlProcessManagerDataContext(
            IProcessManagerDbContext<T> dbContext,
            IMessageSerializer serializer)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
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

        public Task Save(T processManager, Guid? correlationId, CancellationToken cancellationToken)
        {
            if (processManager == null)
            {
                throw new ArgumentNullException(nameof(processManager));
            }

            if (_dbContext.Entry(processManager).State == EntityState.Detached)
            {
                _dbContext.ProcessManagers.Add(processManager);
            }

            IEnumerable<PendingCommand> pendingCommands = processManager
                .FlushPendingCommands()
                .Select(command => new Envelope(Guid.NewGuid(), correlationId, command))
                .Select(envelope => PendingCommand.FromEnvelope(processManager, envelope, _serializer));

            foreach (PendingCommand command in pendingCommands)
            {
                _dbContext.PendingCommands.Add(command);
            }

            return _dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
