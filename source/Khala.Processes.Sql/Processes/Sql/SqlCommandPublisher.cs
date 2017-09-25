namespace Khala.Processes.Sql
{
    using System;
    using System.Collections.Generic;
    using System.Data.Entity;
    using System.Data.Entity.Core;
    using System.Data.Entity.Infrastructure;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Khala.Messaging;

    public class SqlCommandPublisher : ICommandPublisher
    {
        private readonly Func<IProcessManagerDbContext> _dbContextFactory;
        private readonly IMessageSerializer _serializer;
        private readonly IMessageBus _messageBus;

        public SqlCommandPublisher(
            Func<IProcessManagerDbContext> dbContextFactory,
            IMessageSerializer serializer,
            IMessageBus messageBus)
        {
            _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
            _messageBus = messageBus ?? throw new ArgumentNullException(nameof(messageBus));
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
                List<PendingCommand> commands = await LoadCommands(context, processManagerId, cancellationToken).ConfigureAwait(false);
                if (commands.Any())
                {
                    await SendCommands(commands, cancellationToken).ConfigureAwait(false);
                    await RemoveCommands(context, commands, cancellationToken).ConfigureAwait(false);
                }
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

            return _messageBus.SendBatch(envelopes, cancellationToken);
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
            catch (DbUpdateConcurrencyException exception)
            when (exception.InnerException is OptimisticConcurrencyException)
            {
                dbContext.Entry(command).State = EntityState.Detached;
            }
        }

        public async void EnqueueAll(CancellationToken cancellationToken)
        {
            using (IProcessManagerDbContext context = _dbContextFactory.Invoke())
            {
                Loop:

                IEnumerable<Guid> source = await context
                    .PendingCommands
                    .OrderBy(c => c.ProcessManagerId)
                    .Select(c => c.ProcessManagerId)
                    .Take(1000)
                    .Distinct()
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                Task[] tasks = source.Select(processManagerId => FlushCommands(processManagerId, cancellationToken)).ToArray();
                await Task.WhenAll(tasks).ConfigureAwait(false);

                if (source.Any())
                {
                    goto Loop;
                }
            }
        }
    }
}
