namespace Khala.Processes
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using FluentAssertions;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Ploeh.AutoFixture;
    using Ploeh.AutoFixture.AutoMoq;
    using Ploeh.AutoFixture.Idioms;

    [TestClass]
    public class CompositeCommandPublisherExceptionHandler_specs
    {
        [TestMethod]
        public void sut_implements_ICommandPublisherExceptionHandler()
        {
            typeof(CompositeCommandPublisherExceptionHandler).Should().Implement<ICommandPublisherExceptionHandler>();
        }

        [TestMethod]
        public async Task Handle_invokes_all_inner_handler_functions()
        {
            // Arrange
            ICommandPublisherExceptionHandler[] handlers = new[]
            {
                Mock.Of<ICommandPublisherExceptionHandler>(),
                Mock.Of<ICommandPublisherExceptionHandler>(),
                Mock.Of<ICommandPublisherExceptionHandler>()
            };
            var fixture = new Fixture();
            var context = fixture.Create<CommandPublisherExceptionContext>();
            var sut = new CompositeCommandPublisherExceptionHandler(handlers);

            // Act
            await sut.Handle(context);

            // Assert
            foreach (ICommandPublisherExceptionHandler handler in handlers)
            {
                Mock.Get(handler).Verify(x => x.Handle(context), Times.Once());
            }
        }

        [TestMethod]
        public void sut_has_guard_clauses()
        {
            var builder = new Fixture().Customize(new AutoMoqCustomization());
            new GuardClauseAssertion(builder).Verify(typeof(CompositeCommandPublisherExceptionHandler));
        }

        [TestMethod]
        public void constructor_has_guard_clause_against_null_handler()
        {
            ICommandPublisherExceptionHandler[] handlers = new[]
            {
                default(ICommandPublisherExceptionHandler),
                Mock.Of<ICommandPublisherExceptionHandler>(),
                Mock.Of<ICommandPublisherExceptionHandler>()
            };
            var random = new Random();

            Action action = () =>
            new CompositeCommandPublisherExceptionHandler(handlers.OrderBy(h => random.Next()).ToArray());

            action.ShouldThrow<ArgumentException>().Where(x => x.ParamName == "handlers");
        }

        [TestMethod]
        public void Handle_absorbs_handler_exception()
        {
            // Arrange
            ICommandPublisherExceptionHandler[] handlers = new[]
            {
                Mock.Of<ICommandPublisherExceptionHandler>(),
                Mock.Of<ICommandPublisherExceptionHandler>(),
                Mock.Of<ICommandPublisherExceptionHandler>()
            };

            var fixture = new Fixture();
            var context = fixture.Create<CommandPublisherExceptionContext>();
            Mock.Get(handlers[0])
                .Setup(x => x.Handle(context))
                .ThrowsAsync(new InvalidOperationException());

            var random = new Random();
            var sut = new CompositeCommandPublisherExceptionHandler(handlers.OrderBy(h => random.Next()).ToArray());

            // Act
            Func<Task> action = () => sut.Handle(context);

            // Assert
            action.ShouldNotThrow();
        }
    }
}
