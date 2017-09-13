namespace Khala.Processes.Sql
{
    using System;
    using System.Linq;
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
    }
}
