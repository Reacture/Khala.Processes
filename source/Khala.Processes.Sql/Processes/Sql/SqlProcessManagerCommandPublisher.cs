namespace Khala.Processes.Sql
{
    using System;
    using System.Collections.Generic;
    using System.Data.Entity;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Khala.Messaging;

    public class SqlProcessManagerCommandPublisher : ISqlProcessManagerCommandPublisher
    {
        private readonly Func<IProcessManagerDbContext> _dbContextFactory;
        private readonly IMessageSerializer _serializer;
        private readonly IMessageBus _messageBus;

        public SqlProcessManagerCommandPublisher(
            Func<IProcessManagerDbContext> dbContextFactory,
            IMessageSerializer serializer,
            IMessageBus messageBus)
        {
            _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
            _messageBus = messageBus ?? throw new ArgumentNullException(nameof(messageBus));
        }

        public virtual Task PublishCommands(Guid processManagerId, CancellationToken cancellationToken)
        {
            if (processManagerId == Guid.Empty)
            {
                throw new ArgumentException("Value cannot be empty.", nameof(processManagerId));
            }

            async Task Run()
            {
                using (IProcessManagerDbContext context = _dbContextFactory.Invoke())
                {
                    List<PendingCommand> commands = await LoadCommands(processManagerId, context, cancellationToken).ConfigureAwait(false);
                    await SendCommands(commands, cancellationToken).ConfigureAwait(false);
                    await RemoveCommands(context, commands, cancellationToken).ConfigureAwait(false);
                }
            }

            return Run();
        }

        private static Task<List<PendingCommand>> LoadCommands(
            Guid processManagerId,
            IProcessManagerDbContext dbContext,
            CancellationToken cancellationToken)
        {
            IQueryable<PendingCommand> query =
                from c in dbContext.PendingCommands
                where c.ProcessManagerId == processManagerId
                orderby c.Id
                select c;

            return query.ToListAsync(cancellationToken);
        }

        private static Task RemoveCommands(
            IProcessManagerDbContext dbContext,
            List<PendingCommand> commands,
            CancellationToken cancellationToken)
        {
            dbContext.PendingCommands.RemoveRange(commands);
            return dbContext.SaveChangesAsync(cancellationToken);
        }

        private Task SendCommands(
            IEnumerable<PendingCommand> commands,
            CancellationToken cancellationToken)
        {
            IEnumerable<Envelope> envelopes =
                from command in commands
                select PendingCommand.ToEnvelope(command, _serializer);

            return _messageBus.SendBatch(envelopes, cancellationToken);
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

                Task[] tasks = source.Select(processManagerId => PublishCommands(processManagerId, cancellationToken)).ToArray();
                await Task.WhenAll(tasks).ConfigureAwait(false);

                if (source.Any())
                {
                    goto Loop;
                }
            }
        }
    }
}
