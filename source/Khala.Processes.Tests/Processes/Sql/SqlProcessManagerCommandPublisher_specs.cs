namespace Khala.Processes.Sql
{
    using System;
    using System.Collections.Generic;
    using System.Data.Entity;
    using System.Linq;
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
    public class SqlProcessManagerCommandPublisher_specs
    {
        [TestMethod]
        public void sut_has_guard_clauses()
        {
            var builder = new Fixture().Customize(new AutoMoqCustomization());
            new GuardClauseAssertion(builder).Verify(typeof(SqlProcessManagerCommandPublisher));
        }

        [TestMethod]
        public void sut_implements_ISqlProcessManagerCommandPublisher()
        {
            typeof(SqlProcessManagerCommandPublisher).Should().Implement<ISqlProcessManagerCommandPublisher>();
        }

        [TestMethod]
        public async Task PublishCommands_deletes_all_commands_associated_with_specified_process_manager()
        {
            // Arrange
            var serializer = new JsonMessageSerializer();

            var processManager = new FooProcessManager();
            var noiseProcessManager = new FooProcessManager();

            const int noiseCommandCount = 3;

            using (var db = new ProcessManagerDbContext())
            {
                var commands = new List<PendingCommand>(
                    from command in Enumerable.Repeat(new FooCommand(), 3)
                    let envelope = new Envelope(command)
                    select PendingCommand.FromEnvelope(processManager, envelope, serializer));

                commands.AddRange(
                    from command in Enumerable.Repeat(new FooCommand(), noiseCommandCount)
                    let envelope = new Envelope(command)
                    select PendingCommand.FromEnvelope(noiseProcessManager, envelope, serializer));

                var random = new Random();
                db.PendingCommands.AddRange(
                    from command in commands
                    orderby random.Next()
                    select command);

                await db.SaveChangesAsync();
            }

            var sut = new SqlProcessManagerCommandPublisher(
                () => new ProcessManagerDbContext(),
                serializer,
                Mock.Of<IMessageBus>());

            // Act
            await sut.PublishCommands(processManager.Id, CancellationToken.None);

            // Assert
            using (var db = new ProcessManagerDbContext())
            {
                (await db.PendingCommands.AnyAsync(c => c.ProcessManagerId == processManager.Id)).Should().BeFalse();
                (await db.PendingCommands.CountAsync(c => c.ProcessManagerId == noiseProcessManager.Id)).Should().Be(noiseCommandCount);
            }
        }

        [TestMethod]
        public async Task PublishCommands_sends_all_commands_associated_with_specified_process_manager_sequentially()
        {
            // Arrange
            var serializer = new JsonMessageSerializer();

            var processManager = new FooProcessManager();
            var noiseProcessManager = new FooProcessManager();

            var fixture = new Fixture();

            var commands = new List<PendingCommand>(
                from i in Enumerable.Range(0, 3)
                let envelope = new Envelope(fixture.Create<FooCommand>())
                select PendingCommand.FromEnvelope(processManager, envelope, serializer));

            using (var db = new ProcessManagerDbContext())
            {
                db.PendingCommands.AddRange(commands);
                db.PendingCommands.AddRange(from command in Enumerable.Repeat(new FooCommand(), 3)
                                            let envelope = new Envelope(command)
                                            select PendingCommand.FromEnvelope(noiseProcessManager, envelope, serializer));

                await db.SaveChangesAsync();
            }

            var messageBus = Mock.Of<IMessageBus>();
            var sent = new List<Envelope>();
            Mock.Get(messageBus)
                .Setup(x => x.SendBatch(It.IsAny<IEnumerable<Envelope>>(), It.IsAny<CancellationToken>()))
                .Callback<IEnumerable<Envelope>, CancellationToken>((envelopes, cancellationToken) => sent.AddRange(envelopes))
                .Returns(Task.FromResult(true));

            var sut = new SqlProcessManagerCommandPublisher(
                () => new ProcessManagerDbContext(),
                serializer,
                messageBus);

            // Act
            await sut.PublishCommands(processManager.Id, CancellationToken.None);

            // Assert
            Mock.Get(messageBus).Verify(x => x.Send(It.IsAny<Envelope>(), It.IsAny<CancellationToken>()), Times.Never());
            Mock.Get(messageBus).Verify(x => x.SendBatch(It.IsAny<IEnumerable<Envelope>>(), It.IsAny<CancellationToken>()), Times.Once());
            sent.ShouldAllBeEquivalentTo(
                commands.Select(c => new Envelope(c.MessageId, c.CorrelationId, serializer.Deserialize(c.CommandJson))),
                opts => opts.WithStrictOrdering().RespectingRuntimeTypes());
        }

        [TestMethod]
        public async Task given_message_bus_fails_PublishCommands_deletes_no_command()
        {
            // Arrange
            var serializer = new JsonMessageSerializer();
            var processManager = new FooProcessManager();
            var fixture = new Fixture();
            var commands = new List<PendingCommand>(
                from i in Enumerable.Range(0, 3)
                let envelope = new Envelope(fixture.Create<FooCommand>())
                select PendingCommand.FromEnvelope(processManager, envelope, serializer));

            using (var db = new ProcessManagerDbContext())
            {
                db.PendingCommands.AddRange(commands);
                await db.SaveChangesAsync();
            }

            var messageBus = Mock.Of<IMessageBus>();
            var exception = new InvalidOperationException();
            Mock.Get(messageBus)
                .Setup(x => x.SendBatch(It.IsAny<IEnumerable<Envelope>>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(exception);

            var sut = new SqlProcessManagerCommandPublisher(
                () => new ProcessManagerDbContext(),
                serializer,
                messageBus);

            // Act
            Func<Task> action = () => sut.PublishCommands(processManager.Id, CancellationToken.None);

            // Assert
            action.ShouldThrow<InvalidOperationException>().Which.Should().BeSameAs(exception);
            using (var db = new ProcessManagerDbContext())
            {
                IQueryable<PendingCommand> query = from c in db.PendingCommands
                                                   where c.ProcessManagerId == processManager.Id
                                                   select c;
                List<PendingCommand> actual = await query.ToListAsync();
                actual.ShouldAllBeEquivalentTo(commands, opts => opts.RespectingRuntimeTypes());
            }
        }

        public class FooProcessManager : ProcessManager
        {
        }

        public class FooCommand
        {
            public int Int32Value { get; set; }

            public string StringValue { get; set; }
        }

        public class ProcessManagerDbContext : DbContext, IProcessManagerDbContext<FooProcessManager>
        {
            public DbSet<FooProcessManager> ProcessManagers { get; set; }

            public DbSet<PendingCommand> PendingCommands { get; set; }
        }
    }
}
