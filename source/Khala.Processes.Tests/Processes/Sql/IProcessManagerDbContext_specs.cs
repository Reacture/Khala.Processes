namespace Khala.Processes.Sql
{
    using System;
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
    }
}
