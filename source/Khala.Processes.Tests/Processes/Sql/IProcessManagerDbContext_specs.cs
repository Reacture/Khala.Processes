namespace Khala.Processes.Sql
{
    using System;
    using System.Data.Entity;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using FluentAssertions;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class IProcessManagerDbContext_specs
    {
        [TestMethod]
        public void sut_inherits_IDisposable()
        {
            typeof(IProcessManagerDbContext<>).Should().Implement<IDisposable>();
        }

        [TestMethod]
        public void T_has_ProcessManager_constraint()
        {
            typeof(IProcessManagerDbContext<>)
                .GetGenericArguments().Single()
                .GetGenericParameterConstraints()
                .Should().Contain(typeof(ProcessManager));
        }

        [TestMethod]
        public void sut_has_SaveChangesAsync_method()
        {
            typeof(IProcessManagerDbContext<>).Should()
                .HaveMethod("SaveChangesAsync", new[] { typeof(CancellationToken) });
        }

        [TestMethod]
        public void SaveChangeAsync_returns_the_number_of_objects_written_asynchronously()
        {
            typeof(IProcessManagerDbContext<>)
                .GetMethod("SaveChangesAsync").ReturnType.Should().Be(typeof(Task<int>));
        }

        [TestMethod]
        public void sut_has_PendingCommands_property()
        {
            typeof(IProcessManagerDbContext<>).Should()
                .HaveProperty<DbSet<PendingCommand>>("PendingCommands");
        }
    }
}
