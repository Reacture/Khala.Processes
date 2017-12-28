namespace Khala.Processes.Sql
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using FluentAssertions;
    using Khala.FakeDomain;
    using Khala.Messaging;
    using Khala.TransientFaultHandling;
    using Microsoft.EntityFrameworkCore;
    using Moq;
    using Xunit;

    public class SqlCommandPublisher_specs
    {
        private readonly DbContextOptions<ProcessManagerDbContext> _dbContextOptions;

        public SqlCommandPublisher_specs()
        {
            _dbContextOptions = new DbContextOptionsBuilder<ProcessManagerDbContext>()
                .UseInMemoryDatabase(nameof(ProcessManagerDbContext_specs))
                .Options;
        }

        [Fact]
        public void sut_implements_ICommandPublisher()
        {
            typeof(SqlCommandPublisher).Should().Implement<ICommandPublisher>();
        }

        [Fact]
        public async Task FlushCommands_deletes_all_commands_associated_with_specified_process_manager()
        {
            // Arrange
            var serializer = new JsonMessageSerializer();

            var processManager = new FakeProcessManager();
            var noiseProcessManager = new FakeProcessManager();

            const int noiseCommandCount = 3;

            using (var db = new ProcessManagerDbContext(_dbContextOptions))
            {
                var commands = new List<PendingCommand>(
                    from command in Enumerable.Repeat(new FakeCommand(), 3)
                    let envelope = new Envelope(command)
                    select PendingCommand.FromEnvelope(processManager, envelope, serializer));

                commands.AddRange(
                    from command in Enumerable.Repeat(new FakeCommand(), noiseCommandCount)
                    let envelope = new Envelope(command)
                    select PendingCommand.FromEnvelope(noiseProcessManager, envelope, serializer));

                var random = new Random();
                db.PendingCommands.AddRange(
                    from command in commands
                    orderby random.Next()
                    select command);

                await db.SaveChangesAsync();
            }

            var sut = new SqlCommandPublisher(
                () => new ProcessManagerDbContext(_dbContextOptions),
                serializer,
                Mock.Of<IMessageBus>(),
                Mock.Of<IScheduledMessageBus>());

            // Act
            await sut.FlushCommands(processManager.Id, CancellationToken.None);

            // Assert
            using (var db = new ProcessManagerDbContext(_dbContextOptions))
            {
                (await db.PendingCommands.AnyAsync(c => c.ProcessManagerId == processManager.Id)).Should().BeFalse();
                (await db.PendingCommands.CountAsync(c => c.ProcessManagerId == noiseProcessManager.Id)).Should().Be(noiseCommandCount);
            }
        }

        [Fact]
        public async Task FlushCommands_sends_all_commands_associated_with_specified_process_manager_sequentially()
        {
            // Arrange
            var serializer = new JsonMessageSerializer();

            var processManager = new FakeProcessManager();
            var noiseProcessManager = new FakeProcessManager();

            var random = new Random();

            var envelopes = new List<Envelope>(
                from command in new[]
                {
                    new FakeCommand { Int32Value = random.Next(), StringValue = Guid.NewGuid().ToString() },
                    new FakeCommand { Int32Value = random.Next(), StringValue = Guid.NewGuid().ToString() },
                    new FakeCommand { Int32Value = random.Next(), StringValue = Guid.NewGuid().ToString() },
                }
                select new Envelope(Guid.NewGuid(), Guid.NewGuid(), command));

            using (var db = new ProcessManagerDbContext(_dbContextOptions))
            {
                db.PendingCommands.AddRange(from envelope in envelopes
                                            select PendingCommand.FromEnvelope(processManager, envelope, serializer));

                db.PendingCommands.AddRange(from envelope in new[]
                                            {
                                                new Envelope(new object()),
                                                new Envelope(new object()),
                                                new Envelope(new object()),
                                            }
                                            select PendingCommand.FromEnvelope(noiseProcessManager, envelope, serializer));

                await db.SaveChangesAsync();
            }

            var messageBus = new MessageBus();

            var sut = new SqlCommandPublisher(
                () => new ProcessManagerDbContext(_dbContextOptions),
                serializer,
                messageBus,
                Mock.Of<IScheduledMessageBus>());

            // Act
            await sut.FlushCommands(processManager.Id, CancellationToken.None);

            // Assert
            messageBus.Sent.ShouldAllBeEquivalentTo(envelopes, opts => opts.WithStrictOrdering().RespectingRuntimeTypes());
        }

        [Fact]
        public async Task given_message_bus_fails_FlushCommands_deletes_no_command()
        {
            // Arrange
            var serializer = new JsonMessageSerializer();
            var processManager = new FakeProcessManager();
            var random = new Random();
            var commands = new List<PendingCommand>(
                from command in new[]
                {
                    new FakeCommand { Int32Value = random.Next(), StringValue = Guid.NewGuid().ToString() },
                    new FakeCommand { Int32Value = random.Next(), StringValue = Guid.NewGuid().ToString() },
                    new FakeCommand { Int32Value = random.Next(), StringValue = Guid.NewGuid().ToString() },
                }
                let envelope = new Envelope(command)
                select PendingCommand.FromEnvelope(processManager, envelope, serializer));

            using (var db = new ProcessManagerDbContext(_dbContextOptions))
            {
                db.PendingCommands.AddRange(commands);
                await db.SaveChangesAsync();
            }

            IMessageBus messageBus = Mock.Of<IMessageBus>();
            var exception = new InvalidOperationException();
            Mock.Get(messageBus)
                .Setup(x => x.Send(It.IsAny<IEnumerable<Envelope>>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(exception);

            var sut = new SqlCommandPublisher(
                () => new ProcessManagerDbContext(_dbContextOptions),
                serializer,
                messageBus,
                Mock.Of<IScheduledMessageBus>());

            // Act
            Func<Task> action = () => sut.FlushCommands(processManager.Id, CancellationToken.None);

            // Assert
            action.ShouldThrow<InvalidOperationException>().Which.Should().BeSameAs(exception);
            using (var db = new ProcessManagerDbContext(_dbContextOptions))
            {
                IQueryable<PendingCommand> query = from c in db.PendingCommands
                                                   where c.ProcessManagerId == processManager.Id
                                                   select c;
                List<PendingCommand> actual = await query.ToListAsync();
                actual.ShouldAllBeEquivalentTo(commands, opts => opts.RespectingRuntimeTypes());
            }
        }

        [Fact]
        public async Task given_no_command_FlushCommands_does_not_try_to_send()
        {
            // Arrange
            IMessageBus messageBus = Mock.Of<IMessageBus>();

            var sut = new SqlCommandPublisher(
                () => new ProcessManagerDbContext(_dbContextOptions),
                new JsonMessageSerializer(),
                messageBus,
                Mock.Of<IScheduledMessageBus>());

            var processManagerId = Guid.NewGuid();

            // Act
            await sut.FlushCommands(processManagerId, CancellationToken.None);

            // Assert
            Mock.Get(messageBus).Verify(
                x =>
                x.Send(
                    It.IsAny<IEnumerable<Envelope>>(),
                    It.IsAny<CancellationToken>()),
                Times.Never());
        }

        [Fact]
        public async Task FlushCommands_absorbs_exception_caused_by_that_some_pending_command_already_deleted_since_loaded()
        {
            // Arrange
            var messageBus = new CompletableMessageBus();
            var serializer = new JsonMessageSerializer();
            var sut = new SqlCommandPublisher(
                () => new ProcessManagerDbContext(_dbContextOptions),
                serializer,
                messageBus,
                Mock.Of<IScheduledMessageBus>());

            var processManager = new FakeProcessManager();

            using (var db = new ProcessManagerDbContext(_dbContextOptions))
            {
                db.PendingCommands.AddRange(
                    new[]
                    {
                        new FakeCommand(),
                        new FakeCommand(),
                        new FakeCommand(),
                    }
                    .Select(c => new Envelope(c))
                    .Select(e => PendingCommand.FromEnvelope(processManager, e, serializer)));
                await db.SaveChangesAsync();
            }

            // Act
            Func<Task> action = async () =>
            {
                Task flushTask = sut.FlushCommands(processManager.Id, CancellationToken.None);
                using (var db = new ProcessManagerDbContext(_dbContextOptions))
                {
                    List<PendingCommand> pendingCommands = await db
                        .PendingCommands
                        .Where(c => c.ProcessManagerId == processManager.Id)
                        .OrderByDescending(c => c.Id)
                        .Take(1)
                        .ToListAsync();
                    db.PendingCommands.RemoveRange(pendingCommands);
                    await db.SaveChangesAsync();
                }

                messageBus.Complete();
                await flushTask;
            };

            // Assert
            action.ShouldNotThrow();
        }

        [Fact]
        public async Task FlushCommands_deletes_all_scheduled_commands_associated_with_specified_process_manager()
        {
            // Arrange
            var serializer = new JsonMessageSerializer();

            var processManager = new FakeProcessManager();
            var noiseProcessManager = new FakeProcessManager();

            const int noiseCommandCount = 3;

            using (var db = new ProcessManagerDbContext(_dbContextOptions))
            {
                var commands = new List<PendingScheduledCommand>(
                    from command in Enumerable.Repeat(new FakeCommand(), 3)
                    let scheduledEnvelope = new ScheduledEnvelope(new Envelope(command), DateTimeOffset.Now)
                    select PendingScheduledCommand.FromScheduledEnvelope(processManager, scheduledEnvelope, serializer));

                commands.AddRange(
                    from command in Enumerable.Repeat(new FakeCommand(), noiseCommandCount)
                    let scheduledEnvelope = new ScheduledEnvelope(new Envelope(command), DateTimeOffset.Now)
                    select PendingScheduledCommand.FromScheduledEnvelope(noiseProcessManager, scheduledEnvelope, serializer));

                var random = new Random();
                db.PendingScheduledCommands.AddRange(
                    from command in commands
                    orderby random.Next()
                    select command);

                await db.SaveChangesAsync();
            }

            var sut = new SqlCommandPublisher(
                () => new ProcessManagerDbContext(_dbContextOptions),
                serializer,
                Mock.Of<IMessageBus>(),
                Mock.Of<IScheduledMessageBus>());

            // Act
            await sut.FlushCommands(processManager.Id, CancellationToken.None);

            // Assert
            using (var db = new ProcessManagerDbContext(_dbContextOptions))
            {
                (await db.PendingScheduledCommands.AnyAsync(c => c.ProcessManagerId == processManager.Id)).Should().BeFalse();
                (await db.PendingScheduledCommands.CountAsync(c => c.ProcessManagerId == noiseProcessManager.Id)).Should().Be(noiseCommandCount);
            }
        }

        [Fact]
        public async Task FlushCommands_sends_all_scheduled_commands_associated_with_specified_process_manager_sequentially()
        {
            // Arrange
            var serializer = new JsonMessageSerializer();
            var processManager = new FakeProcessManager();
            var noiseProcessManager = new FakeProcessManager();

            var random = new Random();

            var scheduledEnvelopes = new List<ScheduledEnvelope>(
                from command in new[]
                {
                    new FakeCommand { Int32Value = random.Next(), StringValue = Guid.NewGuid().ToString() },
                    new FakeCommand { Int32Value = random.Next(), StringValue = Guid.NewGuid().ToString() },
                    new FakeCommand { Int32Value = random.Next(), StringValue = Guid.NewGuid().ToString() },
                }
                let envelope = new Envelope(Guid.NewGuid(), Guid.NewGuid(), command)
                select new ScheduledEnvelope(envelope, DateTimeOffset.Now.AddTicks(random.Next())));

            using (var db = new ProcessManagerDbContext(_dbContextOptions))
            {
                db.PendingScheduledCommands.AddRange(
                    from scheduledEnvelope in scheduledEnvelopes
                    select PendingScheduledCommand.FromScheduledEnvelope(processManager, scheduledEnvelope, serializer));

                db.PendingScheduledCommands.AddRange(
                    from scheduledEnvelope in new[]
                    {
                        new ScheduledEnvelope(new Envelope(new object()), DateTimeOffset.Now),
                        new ScheduledEnvelope(new Envelope(new object()), DateTimeOffset.Now),
                        new ScheduledEnvelope(new Envelope(new object()), DateTimeOffset.Now),
                    }
                    select PendingScheduledCommand.FromScheduledEnvelope(noiseProcessManager, scheduledEnvelope, serializer));

                await db.SaveChangesAsync();
            }

            var scheduledMessageBus = new ScheduledMessageBus();

            var sut = new SqlCommandPublisher(
                () => new ProcessManagerDbContext(_dbContextOptions),
                serializer,
                Mock.Of<IMessageBus>(),
                scheduledMessageBus);

            // Act
            await sut.FlushCommands(processManager.Id, CancellationToken.None);

            // Assert
            scheduledMessageBus.Sent.ShouldAllBeEquivalentTo(scheduledEnvelopes, opts => opts.WithStrictOrdering().RespectingRuntimeTypes());
        }

        [Fact]
        public async Task given_scheduled_message_bus_fails_FlushCommands_deletes_no_scheduled_command()
        {
            // Arrange
            var serializer = new JsonMessageSerializer();
            var processManager = new FakeProcessManager();
            var random = new Random();
            var scheduledCommands = new List<PendingScheduledCommand>(
                from command in new[]
                {
                    new FakeCommand { Int32Value = random.Next(), StringValue = Guid.NewGuid().ToString() },
                    new FakeCommand { Int32Value = random.Next(), StringValue = Guid.NewGuid().ToString() },
                    new FakeCommand { Int32Value = random.Next(), StringValue = Guid.NewGuid().ToString() },
                }
                let envelope = new Envelope(command)
                let scheduledEnvelope = new ScheduledEnvelope(envelope, DateTimeOffset.Now.AddTicks(random.Next()))
                select PendingScheduledCommand.FromScheduledEnvelope(processManager, scheduledEnvelope, serializer));

            using (var db = new ProcessManagerDbContext(_dbContextOptions))
            {
                db.PendingScheduledCommands.AddRange(scheduledCommands);
                await db.SaveChangesAsync();
            }

            Guid poisonedMessageId = (from c in scheduledCommands
                                      orderby c.GetHashCode()
                                      select c.MessageId).First();

            IScheduledMessageBus scheduledMessageBus = Mock.Of<IScheduledMessageBus>();
            var exception = new InvalidOperationException();
            Mock.Get(scheduledMessageBus)
                .Setup(x => x.Send(It.Is<ScheduledEnvelope>(p => p.Envelope.MessageId == poisonedMessageId), CancellationToken.None))
                .ThrowsAsync(exception);

            var sut = new SqlCommandPublisher(
                () => new ProcessManagerDbContext(_dbContextOptions),
                serializer,
                Mock.Of<IMessageBus>(),
                scheduledMessageBus);

            // Act
            Func<Task> action = () => sut.FlushCommands(processManager.Id, CancellationToken.None);

            // Assert
            action.ShouldThrow<InvalidOperationException>().Which.Should().BeSameAs(exception);
            using (var db = new ProcessManagerDbContext(_dbContextOptions))
            {
                IQueryable<PendingScheduledCommand> query = from c in db.PendingScheduledCommands
                                                            where c.ProcessManagerId == processManager.Id
                                                            select c;
                List<PendingScheduledCommand> actual = await query.ToListAsync();
                actual.ShouldAllBeEquivalentTo(scheduledCommands, opts => opts.RespectingRuntimeTypes());
            }
        }

        [Fact]
        public async Task FlushCommands_absorbs_exception_caused_by_that_some_pending_scheduled_command_already_deleted_since_loaded()
        {
            // Arrange
            var scheduledMessageBus = new CompletableScheduledMessageBus();
            var serializer = new JsonMessageSerializer();
            var sut = new SqlCommandPublisher(
                () => new ProcessManagerDbContext(_dbContextOptions),
                serializer,
                Mock.Of<IMessageBus>(),
                scheduledMessageBus);

            var processManager = new FakeProcessManager();

            using (var db = new ProcessManagerDbContext(_dbContextOptions))
            {
                db.PendingScheduledCommands.AddRange(
                    from command in new[]
                    {
                        new FakeCommand(),
                        new FakeCommand(),
                        new FakeCommand(),
                    }
                    let envelope = new Envelope(command)
                    let scheduledEnvelope = new ScheduledEnvelope(envelope, DateTimeOffset.Now)
                    select PendingScheduledCommand.FromScheduledEnvelope(processManager, scheduledEnvelope, serializer));
                await db.SaveChangesAsync();
            }

            // Act
            Func<Task> action = async () =>
            {
                Task flushTask = sut.FlushCommands(processManager.Id, CancellationToken.None);
                using (var db = new ProcessManagerDbContext(_dbContextOptions))
                {
                    List<PendingScheduledCommand> pendingScheduledCommands = await db
                        .PendingScheduledCommands
                        .Where(c => c.ProcessManagerId == processManager.Id)
                        .OrderByDescending(c => c.Id)
                        .Take(1)
                        .ToListAsync();
                    db.PendingScheduledCommands.RemoveRange(pendingScheduledCommands);
                    await db.SaveChangesAsync();
                }

                scheduledMessageBus.Complete();
                await flushTask;
            };

            // Assert
            action.ShouldNotThrow();
        }

        [Fact]
        public async Task EnqueueAll_publishes_all_pending_commands_asynchronously()
        {
            // Arrange
            var serializer = new JsonMessageSerializer();

            using (var db = new ProcessManagerDbContext(_dbContextOptions))
            {
                for (int i = 0; i < 3; i++)
                {
                    var processManager = new FakeProcessManager();
                    db.PendingCommands.AddRange(from command in Enumerable.Repeat(new FakeCommand(), 3)
                                                let envelope = new Envelope(command)
                                                select PendingCommand.FromEnvelope(processManager, envelope, serializer));
                }

                await db.SaveChangesAsync();
            }

            var sut = new SqlCommandPublisher(
                () => new ProcessManagerDbContext(_dbContextOptions),
                serializer,
                Mock.Of<IMessageBus>(),
                Mock.Of<IScheduledMessageBus>());

            // Act
            sut.EnqueueAll(CancellationToken.None);

            // Assert
            using (var db = new ProcessManagerDbContext(_dbContextOptions))
            {
                int maximumRetryCount = 5;
                var retryPolicy = new RetryPolicy<bool>(
                    maximumRetryCount,
                    new DelegatingTransientFaultDetectionStrategy<bool>(any => any == true),
                    new ConstantRetryIntervalStrategy(TimeSpan.FromSeconds(1.0)));
                (await retryPolicy.Run(db.PendingCommands.AnyAsync, CancellationToken.None)).Should().BeFalse();
            }
        }

        [Fact]
        public async Task EnqueueAll_publishes_all_pending_scheduled_commands_asynchronously()
        {
            // Arrange
            var serializer = new JsonMessageSerializer();

            using (var db = new ProcessManagerDbContext(_dbContextOptions))
            {
                for (int i = 0; i < 3; i++)
                {
                    var processManager = new FakeProcessManager();
                    db.PendingScheduledCommands.AddRange(from command in Enumerable.Repeat(new FakeCommand(), 3)
                                                         let envelope = new Envelope(command)
                                                         let scheduledEnvelope = new ScheduledEnvelope(envelope, DateTimeOffset.Now)
                                                         select PendingScheduledCommand.FromScheduledEnvelope(processManager, scheduledEnvelope, serializer));
                }

                await db.SaveChangesAsync();
            }

            var sut = new SqlCommandPublisher(
                () => new ProcessManagerDbContext(_dbContextOptions),
                serializer,
                Mock.Of<IMessageBus>(),
                Mock.Of<IScheduledMessageBus>());

            // Act
            sut.EnqueueAll(CancellationToken.None);

            // Assert
            using (var db = new ProcessManagerDbContext(_dbContextOptions))
            {
                int maximumRetryCount = 5;
                var retryPolicy = new RetryPolicy<bool>(
                    maximumRetryCount,
                    new DelegatingTransientFaultDetectionStrategy<bool>(any => any == true),
                    new ConstantRetryIntervalStrategy(TimeSpan.FromSeconds(1.0)));
                (await retryPolicy.Run(db.PendingScheduledCommands.AnyAsync, CancellationToken.None)).Should().BeFalse();
            }
        }

        private class MessageBus : IMessageBus
        {
            private readonly ConcurrentQueue<Envelope> _sent = new ConcurrentQueue<Envelope>();

            public IReadOnlyCollection<Envelope> Sent => _sent;

            public Task Send(Envelope envelope, CancellationToken cancellationToken)
            {
                _sent.Enqueue(envelope);
                return Task.CompletedTask;
            }

            public Task Send(IEnumerable<Envelope> envelopes, CancellationToken cancellationToken)
            {
                foreach (Envelope envelope in envelopes)
                {
                    _sent.Enqueue(envelope);
                }

                return Task.CompletedTask;
            }
        }

        private class CompletableMessageBus : IMessageBus
        {
            private readonly TaskCompletionSource<bool> _completionSource = new TaskCompletionSource<bool>();

            public void Complete() => _completionSource.SetResult(true);

            public Task Send(Envelope envelope, CancellationToken cancellationToken) => _completionSource.Task;

            public Task Send(IEnumerable<Envelope> envelopes, CancellationToken cancellationToken) => _completionSource.Task;
        }

        private class ScheduledMessageBus : IScheduledMessageBus
        {
            private readonly ConcurrentQueue<ScheduledEnvelope> _sent = new ConcurrentQueue<ScheduledEnvelope>();

            public IReadOnlyCollection<ScheduledEnvelope> Sent => _sent;

            public Task Send(ScheduledEnvelope envelope, CancellationToken cancellationToken)
            {
                _sent.Enqueue(envelope);
                return Task.CompletedTask;
            }
        }

        private class CompletableScheduledMessageBus : IScheduledMessageBus
        {
            private readonly TaskCompletionSource<bool> _completionSource = new TaskCompletionSource<bool>();

            public void Complete() => _completionSource.SetResult(true);

            public Task Send(ScheduledEnvelope envelope, CancellationToken cancellationToken) => _completionSource.Task;
        }
    }
}
