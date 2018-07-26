namespace Khala.Processes.Sql
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Threading;
    using System.Threading.Tasks;
    using Khala.Messaging;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Storage;

    public sealed class SqlProcessManagerDataContext<T> : ISqlProcessManagerDataContext<T>
        where T : ProcessManager
    {
        private readonly ProcessManagerDbContext _dbContext;
        private readonly IMessageSerializer _serializer;
        private readonly ICommandPublisher _commandPublisher;
        private readonly ICommandPublisherExceptionHandler _commandPublisherExceptionHandler;

        public SqlProcessManagerDataContext(
            ProcessManagerDbContext dbContext,
            IMessageSerializer serializer,
            ICommandPublisher commandPublisher,
            ICommandPublisherExceptionHandler commandPublisherExceptionHandler)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
            _commandPublisher = commandPublisher ?? throw new ArgumentNullException(nameof(commandPublisher));
            _commandPublisherExceptionHandler = commandPublisherExceptionHandler ?? throw new ArgumentNullException(nameof(commandPublisherExceptionHandler));
        }

        public SqlProcessManagerDataContext(
            ProcessManagerDbContext dbContext,
            IMessageSerializer serializer,
            ICommandPublisher commandPublisher)
            : this(
                dbContext,
                serializer,
                commandPublisher,
                DefaultCommandPublisherExceptionHandler.Instance)
        {
        }

        public void Dispose() => _dbContext.Dispose();

        public Task<T> FindProcessManager(
            Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
        {
            if (predicate == null)
            {
                throw new ArgumentNullException(nameof(predicate));
            }

            return RunFindProcessManager(predicate, cancellationToken);
        }

        private async Task<T> RunFindProcessManager(
            Expression<Func<T, bool>> predicate, CancellationToken cancellationToken)
        {
            T processManager = await FindSingleProcessManager(predicate, cancellationToken).ConfigureAwait(false);
            await FlushCommandsIfProcessManagerExists(processManager, cancellationToken).ConfigureAwait(false);
            return processManager;
        }

        private Task<T> FindSingleProcessManager(
            Expression<Func<T, bool>> predicate, CancellationToken cancellationToken)
        {
            return _dbContext
                .Set<T>()
                .Where(predicate)
                .SingleOrDefaultAsync(cancellationToken);
        }

        private Task FlushCommandsIfProcessManagerExists(
            T processManager, CancellationToken cancellationToken)
        {
            return processManager == null
                ? Task.FromResult(true)
                : _commandPublisher.FlushCommands(processManager.Id, cancellationToken);
        }

        public Task SaveProcessManagerAndPublishCommands(
            T processManager,
            string operationId = default,
            Guid? correlationId = default,
            string contributor = default,
            CancellationToken cancellationToken = default)
        {
            if (processManager == null)
            {
                throw new ArgumentNullException(nameof(processManager));
            }

            return RunSaveProcessManagerAndPublishCommands(processManager, operationId, correlationId, contributor, cancellationToken);
        }

        private async Task RunSaveProcessManagerAndPublishCommands(
            T processManager,
            string operationId,
            Guid? correlationId,
            string contributor,
            CancellationToken cancellationToken)
        {
            await SaveProcessManagerAndCommands(processManager, operationId, correlationId, contributor, cancellationToken).ConfigureAwait(false);
            await TryFlushCommands(processManager, cancellationToken).ConfigureAwait(false);
        }

        private Task SaveProcessManagerAndCommands(
            T processManager,
            string operationId,
            Guid? correlationId,
            string contributor,
            CancellationToken cancellationToken)
        {
            UpsertProcessManager(processManager);
            InsertPendingCommands(processManager, operationId, correlationId, contributor);
            InsertPendingScheduledCommands(processManager, operationId, correlationId, contributor);
            return Commit(cancellationToken);
        }

        private void UpsertProcessManager(T processManager)
        {
            if (_dbContext.Entry(processManager).State == EntityState.Detached)
            {
                _dbContext.Set<T>().Add(processManager);
            }
        }

        private void InsertPendingCommands(T processManager, string operationId, Guid? correlationId, string contributor)
        {
            IEnumerable<PendingCommand> pendingCommands = processManager
                .FlushPendingCommands()
                .Select(command => new Envelope(Guid.NewGuid(), command, operationId, correlationId, contributor))
                .Select(envelope => PendingCommand.FromEnvelope(processManager, envelope, _serializer));

            _dbContext.PendingCommands.AddRange(pendingCommands);
        }

        private void InsertPendingScheduledCommands(T processManager, string operationId, Guid? correlationId, string contributor)
        {
            IEnumerable<PendingScheduledCommand> pendingScheduledCommands =
                from scheduledCommand in processManager.FlushPendingScheduledCommands()
                let scheduledEnvelope =
                    new ScheduledEnvelope(
                        new Envelope(
                            Guid.NewGuid(),
                            scheduledCommand.Command,
                            operationId,
                            correlationId,
                            contributor),
                        scheduledCommand.ScheduledTimeUtc)
                select PendingScheduledCommand.FromScheduledEnvelope(processManager, scheduledEnvelope, _serializer);

            _dbContext.PendingScheduledCommands.AddRange(pendingScheduledCommands);
        }

        private async Task Commit(CancellationToken cancellationToken)
        {
            using (IDbContextTransaction transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false))
            {
                await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                transaction.Commit();
            }
        }

        private async Task TryFlushCommands(
            T processManager,
            CancellationToken cancellationToken)
        {
            try
            {
                await _commandPublisher.FlushCommands(processManager.Id, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                var context = new CommandPublisherExceptionContext(typeof(T), processManager.Id, exception);
                await HandleCommandPublisherException(context).ConfigureAwait(false);
                if (context.Handled == false)
                {
                    throw;
                }
            }
        }

        private async Task HandleCommandPublisherException(CommandPublisherExceptionContext context)
        {
            try
            {
                await _commandPublisherExceptionHandler.Handle(context).ConfigureAwait(false);
            }
            catch (Exception unhandleable)
            {
                Trace.TraceError(unhandleable.ToString());
            }
        }
    }
}
