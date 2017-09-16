namespace Khala.Processes
{
    using System;
    using FluentAssertions;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Ploeh.AutoFixture;
    using Ploeh.AutoFixture.Idioms;

    [TestClass]
    public class CommandPublisherExceptionContext_specs
    {
        [TestMethod]
        public void sut_has_ProcessManagerType_immutable_property()
        {
            typeof(CommandPublisherExceptionContext)
                .Should().HaveProperty<Type>("ProcessManagerType")
                .Which.Should().NotBeWritable();
        }

        [TestMethod]
        public void sut_has_ProcessManagerId_immutable_property()
        {
            typeof(CommandPublisherExceptionContext)
                .Should().HaveProperty<Guid>("ProcessManagerId")
                .Which.Should().NotBeWritable();
        }

        [TestMethod]
        public void sut_has_Exception_immutable_property()
        {
            typeof(CommandPublisherExceptionContext)
                .Should().HaveProperty<Exception>("Exception")
                .Which.Should().NotBeWritable();
        }

        [TestMethod]
        public void sut_has_Handled_mutable_property()
        {
            typeof(CommandPublisherExceptionContext)
                .Should().HaveProperty<bool>("Handled")
                .Which.CanWrite.Should().BeTrue();
        }

        [TestMethod]
        public void constructor_sets_ProcessManagerType_correctly()
        {
            var processManagerType = typeof(ProcessManager);

            var sut = new CommandPublisherExceptionContext(
                processManagerType,
                Guid.NewGuid(),
                new InvalidOperationException());

            sut.ProcessManagerType.Should().BeSameAs(processManagerType);
        }

        [TestMethod]
        public void constructor_sets_ProcessManagerId_correctly()
        {
            var processManagerId = Guid.NewGuid();

            var sut = new CommandPublisherExceptionContext(
                typeof(ProcessManager),
                processManagerId,
                new InvalidOperationException());

            sut.ProcessManagerId.Should().Be(processManagerId);
        }

        [TestMethod]
        public void constructor_sets_Exception_correctly()
        {
            var exception = new InvalidOperationException();

            var sut = new CommandPublisherExceptionContext(
                typeof(ProcessManager),
                Guid.NewGuid(),
                exception);

            sut.Exception.Should().Be(exception);
        }

        [TestMethod]
        public void sut_has_guard_clauses()
        {
            var builder = new Fixture();
            new GuardClauseAssertion(builder).Verify(typeof(CommandPublisherExceptionContext));
        }

        [TestMethod]
        public void initializer_sets_Handled_to_false()
        {
            var sut = new CommandPublisherExceptionContext(
                typeof(ProcessManager),
                Guid.NewGuid(),
                new InvalidOperationException());
            sut.Handled.Should().BeFalse();
        }

        private class ProcessManager
        {
        }
    }
}
