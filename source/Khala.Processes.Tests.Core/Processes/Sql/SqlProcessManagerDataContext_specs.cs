namespace Khala.Processes.Sql
{
    using System;
    using System.Collections.Generic;
    using System.Data.SqlClient;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Threading;
    using System.Threading.Tasks;
    using FluentAssertions;
    using Khala.Messaging;
    using Microsoft.EntityFrameworkCore;
    using Moq;
    using Xunit;

    public class SqlProcessManagerDataContext_specs
    {
        private readonly DbContextOptions<ProcessManagerDbContext> _dbContextOptions;

        public SqlProcessManagerDataContext_specs()
        {
            _dbContextOptions = new DbContextOptionsBuilder<ProcessManagerDbContext>()
                .UseInMemoryDatabase(nameof(ProcessManagerDbContext_specs))
                .Options;
        }

        [Fact]
        public void sut_implements_IDisposable()
        {
            typeof(SqlProcessManagerDataContext<>).Should().Implement<IDisposable>();
        }

        [Fact]
        public void Dispose_disposes_db_context()
        {
            var disposable = Mock.Of<IDisposable>();
            var sut = new SqlProcessManagerDataContext<FooProcessManager>(
                new FooProcessManagerDbContext(_dbContextOptions) { DisposableResource = disposable },
                new JsonMessageSerializer(),
                Mock.Of<ICommandPublisher>());

            sut.Dispose();

            Mock.Get(disposable).Verify(x => x.Dispose(), Times.Once());
        }

        [Fact]
        public void T_has_ProcessManager_constraint()
        {
            typeof(SqlProcessManagerDataContext<>)
                .GetGenericArguments().Single()
                .GetGenericParameterConstraints()
                .Should().Contain(typeof(ProcessManager));
        }

        [Fact]
        public async Task FindProcessManager_returns_null_if_process_manager_that_satisfies_predicate_not_found()
        {
            // Arrange
            var sut = new SqlProcessManagerDataContext<FooProcessManager>(
                new FooProcessManagerDbContext(_dbContextOptions),
                new JsonMessageSerializer(),
                Mock.Of<ICommandPublisher>());
            using (sut)
            {
                Expression<Func<FooProcessManager, bool>> predicate = x => x.Id == Guid.NewGuid();

                // Act
                FooProcessManager actual = await sut.FindProcessManager(predicate, CancellationToken.None);

                // Assert
                actual.Should().BeNull();
            }
        }

        [Fact]
        public async Task FindProcessManager_returns_process_manager_that_satisfies_predicate()
        {
            // Arrange
            List<FooProcessManager> processManagers = Enumerable
                .Repeat<Func<FooProcessManager>>(() => new FooProcessManager { AggregateId = Guid.NewGuid() }, 10)
                .Select(f => f.Invoke())
                .ToList();

            FooProcessManager expected = processManagers.First();

            using (var db = new FooProcessManagerDbContext(_dbContextOptions))
            {
                var random = new Random();
                foreach (FooProcessManager processManager in from p in processManagers
                                                             orderby random.Next()
                                                             select p)
                {
                    db.FooProcessManagers.Add(processManager);
                }

                await db.SaveChangesAsync(default);
            }

            var sut = new SqlProcessManagerDataContext<FooProcessManager>(
                new FooProcessManagerDbContext(_dbContextOptions),
                new JsonMessageSerializer(),
                Mock.Of<ICommandPublisher>());
            using (sut)
            {
                Expression<Func<FooProcessManager, bool>> predicate = x => x.AggregateId == expected.AggregateId;

                // Act
                FooProcessManager actual = await sut.FindProcessManager(predicate, default);

                // Assert
                actual.Should().NotBeNull();
                actual.Id.Should().Be(expected.Id);
            }
        }

        [Fact]
        public async Task FindProcessManager_flushes_pending_commands()
        {
            // Arrange
            var publisher = Mock.Of<ICommandPublisher>();
            var processManager = new FooProcessManager();
            var sut = new SqlProcessManagerDataContext<FooProcessManager>(
                new FooProcessManagerDbContext(_dbContextOptions),
                new JsonMessageSerializer(),
                publisher);
            using (var db = new FooProcessManagerDbContext(_dbContextOptions))
            {
                db.FooProcessManagers.Add(processManager);
                await db.SaveChangesAsync(default);
            }

            // Act
            await sut.FindProcessManager(p => p.Id == processManager.Id, default);

            // Assert
            Mock.Get(publisher).Verify(x => x.FlushCommands(processManager.Id, default), Times.Once());
        }

        [Fact]
        public async Task SaveProcessManagerAndPublishCommands_inserts_new_process_manager()
        {
            // Arrange
            var processManager = new FooProcessManager { AggregateId = Guid.NewGuid() };
            var sut = new SqlProcessManagerDataContext<FooProcessManager>(
                new FooProcessManagerDbContext(_dbContextOptions),
                new JsonMessageSerializer(),
                Mock.Of<ICommandPublisher>());
            using (sut)
            {
                // Act
                var cancellationToken = CancellationToken.None;
                var correlationId = default(Guid?);
                await sut.SaveProcessManagerAndPublishCommands(processManager, correlationId, cancellationToken);
            }

            // Assert
            using (var db = new FooProcessManagerDbContext(_dbContextOptions))
            {
                FooProcessManager actual = await
                    db.FooProcessManagers.SingleOrDefaultAsync(x => x.Id == processManager.Id);
                actual.Should().NotBeNull();
                actual.AggregateId.Should().Be(processManager.AggregateId);
            }
        }

        [Fact]
        public async Task SaveProcessManagerAndPublishCommands_updates_existing_process_manager()
        {
            // Arrange
            var random = new Random();
            var processManager = new FooProcessManager
            {
                AggregateId = Guid.NewGuid(),
                StatusValue = Guid.NewGuid().ToString()
            };
            using (var db = new FooProcessManagerDbContext(_dbContextOptions))
            {
                db.FooProcessManagers.Add(processManager);
                await db.SaveChangesAsync(default);
            }

            string statusValue = Guid.NewGuid().ToString();

            var sut = new SqlProcessManagerDataContext<FooProcessManager>(
                new FooProcessManagerDbContext(_dbContextOptions),
                new JsonMessageSerializer(),
                Mock.Of<ICommandPublisher>());
            using (sut)
            {
                processManager = await sut.FindProcessManager(x => x.Id == processManager.Id, default);
                processManager.StatusValue = statusValue;
                var correlationId = default(Guid?);

                // Act
                await sut.SaveProcessManagerAndPublishCommands(processManager, correlationId, default);
            }

            // Assert
            using (var db = new FooProcessManagerDbContext(_dbContextOptions))
            {
                FooProcessManager actual = await
                    db.FooProcessManagers.SingleOrDefaultAsync(x => x.Id == processManager.Id);
                actual.StatusValue.Should().Be(statusValue);
            }
        }

        [Fact]
        public async Task SaveProcessManagerAndPublishCommands_commits_once()
        {
            // Arrange
            var cancellationToken = CancellationToken.None;
            var context = new FooProcessManagerDbContext(_dbContextOptions);
            var sut = new SqlProcessManagerDataContext<FooProcessManager>(
                context,
                new JsonMessageSerializer(),
                Mock.Of<ICommandPublisher>());
            var processManager = new FooProcessManager();
            var correlationId = default(Guid?);

            // Act
            await sut.SaveProcessManagerAndPublishCommands(processManager, correlationId, cancellationToken);

            // Assert
            context.CommitCount.Should().Be(1);
        }

        [Fact]
        public async Task SaveProcessManagerAndPublishCommands_inserts_pending_commands_sequentially()
        {
            // Arrange
            var random = new Random();
            IEnumerable<FooCommand> commands = new[]
            {
                new FooCommand { Int32Value = random.Next(), StringValue = Guid.NewGuid().ToString() },
                new FooCommand { Int32Value = random.Next(), StringValue = Guid.NewGuid().ToString() },
                new FooCommand { Int32Value = random.Next(), StringValue = Guid.NewGuid().ToString() }
            };
            var processManager = new FooProcessManager(commands);
            var correlationId = Guid.NewGuid();

            var serializer = new JsonMessageSerializer();

            var sut = new SqlProcessManagerDataContext<FooProcessManager>(
                new FooProcessManagerDbContext(_dbContextOptions),
                serializer,
                Mock.Of<ICommandPublisher>());

            // Act
            using (sut)
            {
                await sut.SaveProcessManagerAndPublishCommands(processManager, correlationId, CancellationToken.None);
            }

            // Assert
            using (var db = new FooProcessManagerDbContext(_dbContextOptions))
            {
                IQueryable<PendingCommand> query =
                    from c in db.PendingCommands
                    where c.ProcessManagerId == processManager.Id
                    orderby c.Id
                    select c;

                List<PendingCommand> pendingCommands = await query.ToListAsync();
                pendingCommands.Should().HaveCount(commands.Count());
                foreach (var t in commands.Zip(pendingCommands, (expected, actual) => new { expected, actual }))
                {
                    t.actual.ProcessManagerType.Should().Be(typeof(FooProcessManager).FullName);
                    t.actual.ProcessManagerId.Should().Be(processManager.Id);
                    t.actual.MessageId.Should().NotBeEmpty();
                    t.actual.CorrelationId.Should().Be(correlationId);
                    serializer.Deserialize(t.actual.CommandJson).ShouldBeEquivalentTo(t.expected, opts => opts.RespectingRuntimeTypes());
                }
            }
        }

        [Fact]
        public async Task SaveProcessManagerAndPublishCommands_inserts_pending_scheduled_commands_sequentially()
        {
            // Arrange
            var random = new Random();
#pragma warning disable SA1009 // Disable warning SA1009(Closing parenthesis must be spaced correctly) for generic types of tuples
            IEnumerable<(FooCommand command, DateTimeOffset scheduledTime)> scheduledCommands = new[]
            {
                (new FooCommand { Int32Value = random.Next(), StringValue = Guid.NewGuid().ToString() }, DateTimeOffset.Now.AddTicks(random.Next())),
                (new FooCommand { Int32Value = random.Next(), StringValue = Guid.NewGuid().ToString() }, DateTimeOffset.Now.AddTicks(random.Next())),
                (new FooCommand { Int32Value = random.Next(), StringValue = Guid.NewGuid().ToString() }, DateTimeOffset.Now.AddTicks(random.Next()))
            };
#pragma warning restore SA1009 // Disable warning SA1009(Closing parenthesis must be spaced correctly) for generic types of tuples
            var processManager = new FooProcessManager(
                from e in scheduledCommands
                select new ScheduledCommand(e.command, e.scheduledTime));
            var correlationId = Guid.NewGuid();
            var serializer = new JsonMessageSerializer();

            // Act
            using (var sut = new SqlProcessManagerDataContext<FooProcessManager>(
                                 new FooProcessManagerDbContext(_dbContextOptions),
                                 serializer,
                                 Mock.Of<ICommandPublisher>()))
            {
                await sut.SaveProcessManagerAndPublishCommands(processManager, correlationId, CancellationToken.None);
            }

            // Assert
            using (var db = new FooProcessManagerDbContext(_dbContextOptions))
            {
                IQueryable<PendingScheduledCommand> query =
                    from c in db.PendingScheduledCommands
                    where c.ProcessManagerId == processManager.Id
                    orderby c.Id
                    select c;

                List<PendingScheduledCommand> pendingScheduledCommands = query.ToList();
                pendingScheduledCommands.Should().HaveCount(scheduledCommands.Count());
#pragma warning disable SA1008 // Disable warning SA1008(Opening parenthesis must be spaced correctly) for generic types of tuples
                foreach (var t in scheduledCommands.Zip(pendingScheduledCommands, (expected, actual) => (expected, actual)))
#pragma warning restore SA1008 // Disable warning SA1008(Opening parenthesis must be spaced correctly) for generic types of tuples
                {
                    t.actual.ProcessManagerType.Should().Be(typeof(FooProcessManager).FullName);
                    t.actual.ProcessManagerId.Should().Be(processManager.Id);
                    t.actual.MessageId.Should().NotBeEmpty();
                    t.actual.CorrelationId.Should().Be(correlationId);
                    serializer.Deserialize(t.actual.CommandJson).ShouldBeEquivalentTo(t.expected.command, opts => opts.RespectingRuntimeTypes());
                    t.actual.ScheduledTime.Should().Be(t.expected.scheduledTime);
                }
            }
        }

        [Fact]
        public async Task SaveProcessManagerAndPublishCommands_publishes_commands()
        {
            // Arrange
            var processManager = new FooProcessManager();
            var publisher = Mock.Of<ICommandPublisher>();
            var sut = new SqlProcessManagerDataContext<FooProcessManager>(
                new FooProcessManagerDbContext(_dbContextOptions),
                new JsonMessageSerializer(),
                publisher);

            // Act
            await sut.SaveProcessManagerAndPublishCommands(processManager, null, CancellationToken.None);

            // Assert
            Mock.Get(publisher).Verify(x => x.FlushCommands(processManager.Id, CancellationToken.None), Times.Once());
        }

        [Fact]
        public void given_fails_to_commit_SaveProcessManagerAndPublishCommands_does_not_publish_commands()
        {
            // Arrange
            var publisher = Mock.Of<ICommandPublisher>();
            var processManager = new FooProcessManager();
            var sut = new SqlProcessManagerDataContext<FooProcessManager>(
                new FooProcessManagerDbContext(
                    new DbContextOptionsBuilder()
                        .UseSqlServer($"Data Source=(localdb)\\mssqllocaldb;Initial Catalog=nonexistent;")
                        .Options),
                new JsonMessageSerializer(),
                publisher);

            // Act
            Func<Task> action = () => sut.SaveProcessManagerAndPublishCommands(processManager, null, CancellationToken.None);

            // Assert
            action.ShouldThrow<SqlException>();
            Mock.Get(publisher).Verify(x => x.FlushCommands(processManager.Id, CancellationToken.None), Times.Never());
        }

        [Fact]
        public void given_command_publisher_fails_SaveProcessManagerAndPublishCommands_invokes_exception_handler()
        {
            // Arrange
            var processManager = new FooProcessManager();
            var cancellationToken = CancellationToken.None;
            Exception exception = new InvalidOperationException();
            var commandPublisher = Mock.Of<ICommandPublisher>();
            Mock.Get(commandPublisher)
                .Setup(x => x.FlushCommands(processManager.Id, cancellationToken))
                .ThrowsAsync(exception);

            var commandPublisherExceptionHandler = Mock.Of<ICommandPublisherExceptionHandler>();

            var sut = new SqlProcessManagerDataContext<FooProcessManager>(
                new FooProcessManagerDbContext(_dbContextOptions),
                new JsonMessageSerializer(),
                commandPublisher,
                commandPublisherExceptionHandler);

            // Act
            Func<Task> action = () =>
            sut.SaveProcessManagerAndPublishCommands(processManager, null, cancellationToken);

            // Assert
            action.ShouldThrow<InvalidOperationException>().Which.Should().BeSameAs(exception);
            Mock.Get(commandPublisherExceptionHandler).Verify(
                x =>
                x.Handle(It.Is<CommandPublisherExceptionContext>(
                    p =>
                    p.ProcessManagerType == typeof(FooProcessManager) &&
                    p.ProcessManagerId == processManager.Id &&
                    p.Exception == exception)),
                Times.Once());
        }

        [Fact]
        public void given_command_publisher_exception_handled_SaveProcessManagerAndPublishCommands_does_not_throw()
        {
            // Arrange
            var processManager = new FooProcessManager();
            var cancellationToken = CancellationToken.None;
            Exception exception = new InvalidOperationException();
            var commandPublisher = Mock.Of<ICommandPublisher>();
            Mock.Get(commandPublisher)
                .Setup(x => x.FlushCommands(processManager.Id, cancellationToken))
                .ThrowsAsync(exception);

            var sut = new SqlProcessManagerDataContext<FooProcessManager>(
                new FooProcessManagerDbContext(_dbContextOptions),
                new JsonMessageSerializer(),
                commandPublisher,
                new DelegatingCommandPublisherExceptionHandler(
                    context =>
                    context.Handled =
                    context.ProcessManagerType == typeof(FooProcessManager) &&
                    context.ProcessManagerId == processManager.Id &&
                    context.Exception == exception));

            // Act
            Func<Task> action = () =>
            sut.SaveProcessManagerAndPublishCommands(processManager, null, cancellationToken);

            // Assert
            action.ShouldNotThrow();
        }

        [Fact]
        public void SaveProcessManagerAndPublishCommands_absorbs_command_publisher_exception_handler_exception()
        {
            // Arrange
            var processManager = new FooProcessManager();
            var cancellationToken = CancellationToken.None;
            var commandPublisher = Mock.Of<ICommandPublisher>();
            Mock.Get(commandPublisher)
                .Setup(x => x.FlushCommands(processManager.Id, cancellationToken))
                .ThrowsAsync(new InvalidOperationException());

            var sut = new SqlProcessManagerDataContext<FooProcessManager>(
                new FooProcessManagerDbContext(_dbContextOptions),
                new JsonMessageSerializer(),
                commandPublisher,
                new DelegatingCommandPublisherExceptionHandler(
                    context =>
                    {
                        context.Handled = true;
                        throw new InvalidOperationException();
                    }));

            // Act
            Func<Task> action = () =>
            sut.SaveProcessManagerAndPublishCommands(processManager, null, cancellationToken);

            // Assert
            action.ShouldNotThrow();
        }

        public class FooProcessManager : ProcessManager
        {
            private string _validationError = null;

            public FooProcessManager()
            {
            }

            public FooProcessManager(IEnumerable<object> commands)
            {
                foreach (object command in commands)
                {
                    AddCommand(command);
                }
            }

            public FooProcessManager(IEnumerable<ScheduledCommand> scheduledCommands)
            {
                foreach (ScheduledCommand scheduledCommand in scheduledCommands)
                {
                    AddScheduledCommand(scheduledCommand);
                }
            }

            public Guid AggregateId { get; set; }

            public string StatusValue { get; set; }
        }

        public class FooCommand
        {
            public int Int32Value { get; set; }

            public string StringValue { get; set; }
        }

        public class FooProcessManagerDbContext : ProcessManagerDbContext
        {
            private int _commitCount = 0;

            public FooProcessManagerDbContext(DbContextOptions options)
                : base(options)
            {
            }

            public DbSet<FooProcessManager> FooProcessManagers { get; set; }

            public int CommitCount => _commitCount;

            public IDisposable DisposableResource { get; set; }

            public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken)
            {
                try
                {
                    return await base.SaveChangesAsync(cancellationToken);
                }
                finally
                {
                    Interlocked.Increment(ref _commitCount);
                }
            }

            public override void Dispose()
            {
                base.Dispose();

                DisposableResource?.Dispose();
            }
        }
    }
}
