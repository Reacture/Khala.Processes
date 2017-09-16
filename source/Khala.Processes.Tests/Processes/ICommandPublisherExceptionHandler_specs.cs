namespace Khala.Processes
{
    using System.Threading.Tasks;
    using FluentAssertions;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class ICommandPublisherExceptionHandler_specs
    {
        [TestMethod]
        public void sut_has_Handle_method()
        {
            typeof(ICommandPublisherExceptionHandler)
                .Should()
                .HaveMethod("Handle", new[] { typeof(CommandPublisherExceptionContext) });
        }

        [TestMethod]
        public void Handle_is_asynchronous()
        {
            typeof(ICommandPublisherExceptionHandler)
                .GetMethod("Handle").ReturnType.Should().Be(typeof(Task));
        }
    }
}
