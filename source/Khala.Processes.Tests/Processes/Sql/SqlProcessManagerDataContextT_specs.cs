namespace Khala.Processes.Sql
{
    using System;
    using System.Collections.Generic;
    using System.Data.Entity;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Threading;
    using System.Threading.Tasks;
    using FluentAssertions;
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
            var sut = new SqlProcessManagerDataContext<FooProcessManager>(context);

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
        public async Task Find_returns_null_if_process_manager_that_satisfies_predicate_not_found()
        {
            // Arrange
            using (var sut = new SqlProcessManagerDataContext<FooProcessManager>(new ProcessManagerDbContext()))
            {
                Expression<Func<FooProcessManager, bool>> predicate = x => x.Id == Guid.NewGuid();

                // Act
                FooProcessManager actual = await sut.Find(predicate, CancellationToken.None);

                // Assert
                actual.Should().BeNull();
            }
        }

        [TestMethod]
        public async Task Find_returns_process_manager_that_satisfies_predicate()
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

            using (var sut = new SqlProcessManagerDataContext<FooProcessManager>(new ProcessManagerDbContext()))
            {
                Expression<Func<FooProcessManager, bool>> predicate = x => x.AggregateId == expected.AggregateId;

                // Act
                FooProcessManager actual = await sut.Find(predicate, CancellationToken.None);

                // Assert
                actual.Should().NotBeNull();
                actual.Id.Should().Be(expected.Id);
            }
        }

        [TestMethod]
        public async Task Save_inserts_new_process_manager()
        {
            // Arrange
            var processManager = new FooProcessManager { AggregateId = Guid.NewGuid() };
            using (var sut = new SqlProcessManagerDataContext<FooProcessManager>(new ProcessManagerDbContext()))
            {
                // Act
                var cancellationToken = CancellationToken.None;
                await sut.Save(processManager, cancellationToken);
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
        public async Task Save_updates_existing_process_manager()
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

            using (var sut = new SqlProcessManagerDataContext<FooProcessManager>(new ProcessManagerDbContext()))
            {
                var cancellationToken = CancellationToken.None;
                processManager = await sut.Find(x => x.Id == processManager.Id, cancellationToken);
                processManager.StatusValue = statusValue;

                // Act
                await sut.Save(processManager, cancellationToken);
            }

            // Assert
            using (var db = new ProcessManagerDbContext())
            {
                FooProcessManager actual = await
                    db.ProcessManagers.SingleOrDefaultAsync(x => x.Id == processManager.Id);
                actual.StatusValue.Should().Be(statusValue);
            }
        }

        public class FooProcessManager : ProcessManager
        {
            public Guid AggregateId { get; set; }

            public string StatusValue { get; set; }
        }

        public class ProcessManagerDbContext : DbContext, IProcessManagerDbContext<FooProcessManager>
        {
            public DbSet<FooProcessManager> ProcessManagers { get; set; }

            public DbSet<PendingCommand> PendingCommands { get; set; }
        }
    }
}
