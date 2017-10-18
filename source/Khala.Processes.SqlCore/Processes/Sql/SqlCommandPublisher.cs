namespace Khala.Processes.Sql
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Khala.Messaging;

#if NETSTANDARD2_0
    using Microsoft.EntityFrameworkCore;
#else
    using System.Data.Entity;
    using System.Data.Entity.Core;
    using System.Data.Entity.Infrastructure;
#endif

    public class SqlCommandPublisher : ICommandPublisher
    {
        private readonly Func<IProcessManagerDbContext> _dbContextFactory;
        private readonly IMessageSerializer _serializer;
        private readonly IMessageBus _messageBus;
        private readonly IScheduledMessageBus _scheduledMessageBus;

        public SqlCommandPublisher(
            Func<IProcessManagerDbContext> dbContextFactory,
            IMessageSerializer serializer,
            IMessageBus messageBus,
            IScheduledMessageBus scheduledMessageBus)
        {
            _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
            _messageBus = messageBus ?? throw new ArgumentNullException(nameof(messageBus));
            _scheduledMessageBus = scheduledMessageBus ?? throw new ArgumentNullException(nameof(scheduledMessageBus));
        }

        public virtual Task FlushCommands(Guid processManagerId, CancellationToken cancellationToken)
        {
            if (processManagerId == Guid.Empty)
            {
                throw new ArgumentException("Value cannot be empty.", nameof(processManagerId));
            }

            return RunFlushCommands(processManagerId, cancellationToken);
        }

        private async Task RunFlushCommands(Guid processManagerId, CancellationToken cancellationToken)
        {
            using (IProcessManagerDbContext context = _dbContextFactory.Invoke())
            {
                await FlushPendingCommands(context, processManagerId, cancellationToken).ConfigureAwait(false);
                await FlushPendingScheduledCommands(context, processManagerId, cancellationToken).ConfigureAwait(false);
            }
        }

        private async Task FlushPendingCommands(
            IProcessManagerDbContext dbContext,
            Guid processManagerId,
            CancellationToken cancellationToken)
        {
            List<PendingCommand> commands = await LoadCommands(dbContext, processManagerId, cancellationToken).ConfigureAwait(false);
            if (commands.Any())
            {
                await SendCommands(commands, cancellationToken).ConfigureAwait(false);
                await RemoveCommands(dbContext, commands, cancellationToken).ConfigureAwait(false);
            }
        }

        private static Task<List<PendingCommand>> LoadCommands(
            IProcessManagerDbContext dbContext,
            Guid processManagerId,
            CancellationToken cancellationToken)
        {
            IQueryable<PendingCommand> query =
                from c in dbContext.PendingCommands
                where c.ProcessManagerId == processManagerId
                orderby c.Id
                select c;

            return query.ToListAsync(cancellationToken);
        }

        private Task SendCommands(
            IEnumerable<PendingCommand> commands,
            CancellationToken cancellationToken)
        {
            IEnumerable<Envelope> envelopes =
                from command in commands
                select RestoreEnvelope(command);

            return _messageBus.Send(envelopes, cancellationToken);
        }

        private Envelope RestoreEnvelope(PendingCommand command) =>
            new Envelope(
                command.MessageId,
                command.CorrelationId,
                _serializer.Deserialize(command.CommandJson));

        private static async Task RemoveCommands(
            IProcessManagerDbContext dbContext,
            List<PendingCommand> commands,
            CancellationToken cancellationToken)
        {
            foreach (PendingCommand command in commands)
            {
                await RemoveCommand(dbContext, command, cancellationToken).ConfigureAwait(false);
            }
        }

        private static async Task RemoveCommand(
            IProcessManagerDbContext dbContext,
            PendingCommand command,
            CancellationToken cancellationToken)
        {
            try
            {
                dbContext.PendingCommands.Remove(command);
                await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
#if NETSTANDARD2_0
            catch (DbUpdateConcurrencyException)
#else
            catch (DbUpdateConcurrencyException exception)
            when (exception.InnerException is OptimisticConcurrencyException)
#endif
            {
                dbContext.Entry(command).State = EntityState.Detached;
            }
        }

        private async Task FlushPendingScheduledCommands(
            IProcessManagerDbContext dbContext,
            Guid processManagerId,
            CancellationToken cancellationToken)
        {
            List<PendingScheduledCommand> scheduledCommands = await LoadScheduledCommands(dbContext, processManagerId, cancellationToken).ConfigureAwait(false);
            await SendScheduledCommands(scheduledCommands, cancellationToken).ConfigureAwait(false);
            await RemoveScheduledCommands(dbContext, scheduledCommands, cancellationToken).ConfigureAwait(false);
        }

        private static Task<List<PendingScheduledCommand>> LoadScheduledCommands(
            IProcessManagerDbContext dbContext,
            Guid processManagerId,
            CancellationToken cancellationToken)
        {
            IQueryable<PendingScheduledCommand> query =
                from c in dbContext.PendingScheduledCommands
                where c.ProcessManagerId == processManagerId
                orderby c.Id
                select c;

            return query.ToListAsync(cancellationToken);
        }

        private async Task SendScheduledCommands(
            IEnumerable<PendingScheduledCommand> scheduledCommands,
            CancellationToken cancellationToken)
        {
            foreach (PendingScheduledCommand scheduledCommand in scheduledCommands)
            {
                ScheduledEnvelope scheduledEnvelope = RestoreScheduledEnvelope(scheduledCommand);
                await _scheduledMessageBus.Send(scheduledEnvelope, cancellationToken).ConfigureAwait(false);
            }
        }

        private ScheduledEnvelope RestoreScheduledEnvelope(PendingScheduledCommand scheduledCommand) =>
            new ScheduledEnvelope(
                new Envelope(
                    scheduledCommand.MessageId,
                    scheduledCommand.CorrelationId,
                    _serializer.Deserialize(scheduledCommand.CommandJson)),
                scheduledCommand.ScheduledTime);

        private static async Task RemoveScheduledCommands(
            IProcessManagerDbContext dbContext,
            IEnumerable<PendingScheduledCommand> scheduledCommands,
            CancellationToken cancellationToken)
        {
            foreach (PendingScheduledCommand scheduledCommand in scheduledCommands)
            {
                await RemoveScheduledCommand(dbContext, scheduledCommand, cancellationToken).ConfigureAwait(false);
            }
        }

        private static async Task RemoveScheduledCommand(
            IProcessManagerDbContext dbContext,
            PendingScheduledCommand scheduledCommand,
            CancellationToken cancellationToken)
        {
            try
            {
                dbContext.PendingScheduledCommands.Remove(scheduledCommand);
                await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
#if NETSTANDARD2_0
            catch (DbUpdateConcurrencyException)
#else
            catch (DbUpdateConcurrencyException exception)
            when (exception.InnerException is OptimisticConcurrencyException)
#endif
            {
                dbContext.Entry(scheduledCommand).State = EntityState.Detached;
            }
        }

        public async void EnqueueAll(CancellationToken cancellationToken)
        {
            using (IProcessManagerDbContext context = _dbContextFactory.Invoke())
            {
                Loop:

                List<Guid> withPendingCommand = await context
                    .PendingCommands
                    .Take(1)
                    .Select(c => c.ProcessManagerId)
                    .ToListAsync()
                    .ConfigureAwait(false);

                List<Guid> withPendingScheduledCommand = await context
                    .PendingScheduledCommands
                    .Take(1)
                    .Select(c => c.ProcessManagerId)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                IEnumerable<Guid> processManagerIds = withPendingCommand.Union(withPendingScheduledCommand);
                IEnumerable<Task> flushTasks = processManagerIds.Select(processManagerId => FlushCommands(processManagerId, cancellationToken));
                await Task.WhenAll(flushTasks).ConfigureAwait(false);

                if (withPendingCommand.Any() ||
                    withPendingScheduledCommand.Any())
                {
                    goto Loop;
                }
            }
        }
    }
}
