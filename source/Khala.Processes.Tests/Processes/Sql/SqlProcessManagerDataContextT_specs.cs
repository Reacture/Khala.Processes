namespace Khala.Processes.Sql
{
    using System;
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
            var context = Mock.Of<IProcessManagerDbContext<ProcessManager>>();
            var sut = new SqlProcessManagerDataContext<ProcessManager>(context);

            sut.Dispose();

            Mock.Get(context).Verify(x => x.Dispose(), Times.Once());
        }

        [TestMethod]
        public void sut_has_guard_clauses()
        {
            var builder = new Fixture().Customize(new AutoMoqCustomization());
            new GuardClauseAssertion(builder).Verify(typeof(SqlProcessManagerDataContext<>));
        }

        public class ProcessManager
        {
        }
    }
}
