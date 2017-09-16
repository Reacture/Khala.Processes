namespace Khala.Processes
{
    using System;
    using System.Threading.Tasks;
    using FluentAssertions;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Ploeh.AutoFixture;
    using Ploeh.AutoFixture.Idioms;

    [TestClass]
    public class DelegatingCommandPublisherExceptionHandler_specs
    {
        public interface IFunctionProvider
        {
            void Action<T>(T arg);
        }

        [TestMethod]
        public void sut_implements_ICommandPublisherExceptionHandler()
        {
            typeof(DelegatingCommandPublisherExceptionHandler)
                .Should().Implement<ICommandPublisherExceptionHandler>();
        }

        [TestMethod]
        public async Task Handle_relays_to_asynchronous_action_correctly()
        {
            var handler = Mock.Of<ICommandPublisherExceptionHandler>();
            Func<CommandPublisherExceptionContext, Task> action = handler.Handle;
            var sut = new DelegatingCommandPublisherExceptionHandler(action);
            var fixture = new Fixture();
            var context = fixture.Create<CommandPublisherExceptionContext>();

            await sut.Handle(context);

            Mock.Get(handler).Verify(x => x.Handle(context), Times.Once());
        }

        [TestMethod]
        public async Task Handle_relays_to_synchronous_action_correctly()
        {
            var functionProvider = Mock.Of<IFunctionProvider>();
            Action<CommandPublisherExceptionContext> action = functionProvider.Action;
            var sut = new DelegatingCommandPublisherExceptionHandler(action);
            var fixture = new Fixture();
            var context = fixture.Create<CommandPublisherExceptionContext>();

            await sut.Handle(context);

            Mock.Get(functionProvider).Verify(x => x.Action(context), Times.Once());
        }

        [TestMethod]
        public void sut_has_guard_clauses()
        {
            var builder = new Fixture();
            new GuardClauseAssertion(builder).Verify(
                typeof(DelegatingCommandPublisherExceptionHandler));
        }
    }
}
