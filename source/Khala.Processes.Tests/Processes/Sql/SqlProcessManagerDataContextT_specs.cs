namespace Khala.Processes.Sql
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.Data.Entity;
    using System.Data.Entity.Validation;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Threading;
    using System.Threading.Tasks;
    using FluentAssertions;
    using Khala.Messaging;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Ploeh.AutoFixture;
    using Ploeh.AutoFixture.AutoMoq;
    using Ploeh.AutoFixture.Idioms;

    [TestClass]
    public class SqlProcessManagerDataContextT_specs
    {
        [TestMethod]
        public void sut_implements_IDisposable()
        {
            typeof(SqlProcessManagerDataContext<>).Should().Implement<IDisposable>();
        }

        [TestMethod]
        public void Dispose_disposes_db_context()
        {
            var context = Mock.Of<IProcessManagerDbContext<FooProcessManager>>();
            var sut = new SqlProcessManagerDataContext<FooProcessManager>(
                context,
                new JsonMessageSerializer(),
                Mock.Of<ICommandPublisher>());

            sut.Dispose();

            Mock.Get(context).Verify(x => x.Dispose(), Times.Once());
        }

        [TestMethod]
        public void sut_has_guard_clauses()
        {
            var builder = new Fixture().Customize(new AutoMoqCustomization());
            new GuardClauseAssertion(builder).Verify(typeof(SqlProcessManagerDataContext<>));
        }

        [TestMethod]
        public void T_has_ProcessManager_constraint()
        {
            typeof(SqlProcessManagerDataContext<>)
                .GetGenericArguments().Single()
                .GetGenericParameterConstraints()
                .Should().Contain(typeof(ProcessManager));
        }

        [TestMethod]
        public async Task FindProcessManager_returns_null_if_process_manager_that_satisfies_predicate_not_found()
        {
            // Arrange
            var sut = new SqlProcessManagerDataContext<FooProcessManager>(
                new ProcessManagerDbContext(),
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

        [TestMethod]
        public async Task FindProcessManager_returns_process_manager_that_satisfies_predicate()
        {
            // Arrange
            List<FooProcessManager> processManagers = Enumerable
                .Repeat<Func<FooProcessManager>>(() => new FooProcessManager { AggregateId = Guid.NewGuid() }, 10)
                .Select(f => f.Invoke())
                .ToList();

            FooProcessManager expected = processManagers.First();

            using (var db = new ProcessManagerDbContext())
            {
                var random = new Random();
                foreach (FooProcessManager processManager in from p in processManagers
                                                             orderby random.Next()
                                                             select p)
                {
                    db.ProcessManagers.Add(processManager);
                }

                await db.SaveChangesAsync();
            }

            var sut = new SqlProcessManagerDataContext<FooProcessManager>(
                new ProcessManagerDbContext(),
                new JsonMessageSerializer(),
                Mock.Of<ICommandPublisher>());
            using (sut)
            {
                Expression<Func<FooProcessManager, bool>> predicate = x => x.AggregateId == expected.AggregateId;

                // Act
                FooProcessManager actual = await sut.FindProcessManager(predicate, CancellationToken.None);

                // Assert
                actual.Should().NotBeNull();
                actual.Id.Should().Be(expected.Id);
            }
        }

        [TestMethod]
        public async Task FindProcessManager_flushes_pending_commands()
        {
            // Arrange
            var publisher = Mock.Of<ICommandPublisher>();
            var processManager = new FooProcessManager();
            var sut = new SqlProcessManagerDataContext<FooProcessManager>(
                new ProcessManagerDbContext(),
                new JsonMessageSerializer(),
                publisher);
            using (var db = new ProcessManagerDbContext())
            {
                db.ProcessManagers.Add(processManager);
                await db.SaveChangesAsync();
            }

            // Act
            await sut.FindProcessManager(p => p.Id == processManager.Id, CancellationToken.None);

            // Assert
            Mock.Get(publisher).Verify(x => x.FlushCommands(processManager.Id, CancellationToken.None), Times.Once());
        }

        [TestMethod]
        public async Task SaveProcessManagerAndPublishCommands_inserts_new_process_manager()
        {
            // Arrange
            var processManager = new FooProcessManager { AggregateId = Guid.NewGuid() };
            var sut = new SqlProcessManagerDataContext<FooProcessManager>(
                new ProcessManagerDbContext(),
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
            using (var db = new ProcessManagerDbContext())
            {
                FooProcessManager actual = await
                    db.ProcessManagers.SingleOrDefaultAsync(x => x.Id == processManager.Id);
                actual.Should().NotBeNull();
                actual.AggregateId.Should().Be(processManager.AggregateId);
            }
        }

        [TestMethod]
        public async Task SaveProcessManagerAndPublishCommands_updates_existing_process_manager()
        {
            // Arrange
            var fixture = new Fixture();
            var processManager = fixture.Create<FooProcessManager>();
            using (var db = new ProcessManagerDbContext())
            {
                db.ProcessManagers.Add(processManager);
                await db.SaveChangesAsync();
            }

            string statusValue = fixture.Create(nameof(FooProcessManager.StatusValue));

            var sut = new SqlProcessManagerDataContext<FooProcessManager>(
                new ProcessManagerDbContext(),
                new JsonMessageSerializer(),
                Mock.Of<ICommandPublisher>());
            using (sut)
            {
                var cancellationToken = CancellationToken.None;
                processManager = await sut.FindProcessManager(x => x.Id == processManager.Id, cancellationToken);
                processManager.StatusValue = statusValue;
                var correlationId = default(Guid?);

                // Act
                await sut.SaveProcessManagerAndPublishCommands(processManager, correlationId, cancellationToken);
            }

            // Assert
            using (var db = new ProcessManagerDbContext())
            {
                FooProcessManager actual = await
                    db.ProcessManagers.SingleOrDefaultAsync(x => x.Id == processManager.Id);
                actual.StatusValue.Should().Be(statusValue);
            }
        }

        [TestMethod]
        public async Task SaveProcessManagerAndPublishCommands_commits_once()
        {
            // Arrange
            var cancellationToken = CancellationToken.None;
            var context = new ProcessManagerDbContext();
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

        [TestMethod]
        public async Task SaveProcessManagerAndPublishCommands_inserts_pending_commands_sequentially()
        {
            // Arrange
            var fixture = new Fixture();
            IEnumerable<FooCommand> commands = fixture.CreateMany<FooCommand>();
            var processManager = new FooProcessManager(commands);
            var correlationId = Guid.NewGuid();

            var serializer = new JsonMessageSerializer();

            var sut = new SqlProcessManagerDataContext<FooProcessManager>(
                new ProcessManagerDbContext(),
                serializer,
                Mock.Of<ICommandPublisher>());
            using (sut)
            {
                // Act
                await sut.SaveProcessManagerAndPublishCommands(processManager, correlationId, CancellationToken.None);
            }

            // Assert
            using (var db = new ProcessManagerDbContext())
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

        [TestMethod]
        public async Task SaveProcessManagerAndPublishCommands_publishes_commands()
        {
            // Arrange
            var fixture = new Fixture();
            var processManager = fixture.Create<FooProcessManager>();
            var publisher = Mock.Of<ICommandPublisher>();
            var sut = new SqlProcessManagerDataContext<FooProcessManager>(
                new ProcessManagerDbContext(),
                new JsonMessageSerializer(),
                publisher);

            // Act
            await sut.SaveProcessManagerAndPublishCommands(processManager, null, CancellationToken.None);

            // Assert
            Mock.Get(publisher).Verify(x => x.FlushCommands(processManager.Id, CancellationToken.None), Times.Once());
        }

        [TestMethod]
        public void given_fails_to_commit_SaveProcessManagerAndPublishCommands_does_not_publish_commands()
        {
            // Arrange
            var fixture = new Fixture();
            var publisher = Mock.Of<ICommandPublisher>();
            var processManager = fixture.Create<FooProcessManager>();
            processManager.SetValidationError("invalid process manager state");
            var sut = new SqlProcessManagerDataContext<FooProcessManager>(
                new ProcessManagerDbContext(),
                new JsonMessageSerializer(),
                publisher);

            // Act
            Func<Task> action = () => sut.SaveProcessManagerAndPublishCommands(processManager, null, CancellationToken.None);

            // Assert
            action.ShouldThrow<DbEntityValidationException>();
            Mock.Get(publisher).Verify(x => x.FlushCommands(processManager.Id, CancellationToken.None), Times.Never());
        }

        [TestMethod]
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
                new ProcessManagerDbContext(),
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

        [TestMethod]
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
                new ProcessManagerDbContext(),
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

        [TestMethod]
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
                new ProcessManagerDbContext(),
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

        public class FooProcessManager : ProcessManager, IValidatableObject
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

            public Guid AggregateId { get; set; }

            public string StatusValue { get; set; }

            public void SetValidationError(string validationError) => _validationError = validationError;

            public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
            {
                if (string.IsNullOrEmpty(_validationError) == false)
                {
                    yield return new ValidationResult(_validationError);
                }
            }
        }

        public class FooCommand
        {
            public int Int32Value { get; set; }

            public string StringValue { get; set; }
        }

        public class ProcessManagerDbContext : DbContext, IProcessManagerDbContext<FooProcessManager>
        {
            private int _commitCount = 0;

            public DbSet<FooProcessManager> ProcessManagers { get; set; }

            public DbSet<PendingCommand> PendingCommands { get; set; }

            public int CommitCount => _commitCount;

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
        }
    }
}
