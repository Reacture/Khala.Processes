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

#if NETSTANDARD2_0
    using Microsoft.EntityFrameworkCore;
#else
    using System.Data.Entity;
#endif

    public sealed class SqlProcessManagerDataContext<T> : IDisposable
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

        [Obsolete("Use FindProcessManager() instead. This method will be removed in version 1.0.0.")]
        public Task<T> Find(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken)
            => FindProcessManager(predicate, cancellationToken);

        public Task<T> FindProcessManager(
            Expression<Func<T, bool>> predicate, CancellationToken cancellationToken)
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
            Guid? correlationId,
            CancellationToken cancellationToken)
        {
            if (processManager == null)
            {
                throw new ArgumentNullException(nameof(processManager));
            }

            async Task Run()
            {
                await SaveProcessManagerAndCommands(processManager, correlationId, cancellationToken).ConfigureAwait(false);
                await TryFlushCommands(processManager, cancellationToken).ConfigureAwait(false);
            }

            return Run();
        }

        private Task SaveProcessManagerAndCommands(
            T processManager,
            Guid? correlationId,
            CancellationToken cancellationToken)
        {
            UpsertProcessManager(processManager);
            InsertPendingCommands(processManager, correlationId);
            InsertPendingScheduledCommands(processManager, correlationId);
            return Commit(cancellationToken);
        }

        private void UpsertProcessManager(T processManager)
        {
            if (_dbContext.Entry(processManager).State == EntityState.Detached)
            {
                _dbContext.Set<T>().Add(processManager);
            }
        }

        private void InsertPendingCommands(T processManager, Guid? correlationId)
        {
            IEnumerable<PendingCommand> pendingCommands = processManager
                .FlushPendingCommands()
                .Select(command => new Envelope(Guid.NewGuid(), correlationId, command))
                .Select(envelope => PendingCommand.FromEnvelope(processManager, envelope, _serializer));

            _dbContext.PendingCommands.AddRange(pendingCommands);
        }

        private void InsertPendingScheduledCommands(T processManager, Guid? correlationId)
        {
            IEnumerable<PendingScheduledCommand> pendingScheduledCommands =
                from scheduledCommand in processManager.FlushPendingScheduledCommands()
                let scheduledEnvelope =
                    new ScheduledEnvelope(
                        new Envelope(
                            Guid.NewGuid(),
                            correlationId,
                            scheduledCommand.Command),
                        scheduledCommand.ScheduledTime)
                select PendingScheduledCommand.FromScheduledEnvelope(processManager, scheduledEnvelope, _serializer);

            _dbContext.PendingScheduledCommands.AddRange(pendingScheduledCommands);
        }

        private Task Commit(CancellationToken cancellationToken)
        {
            return _dbContext.SaveChangesAsync(cancellationToken);
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
