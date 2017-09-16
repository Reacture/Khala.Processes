namespace Khala.Processes
{
    using System;
    using System.Threading.Tasks;

    public class DelegatingCommandPublisherExceptionHandler : ICommandPublisherExceptionHandler
    {
        private Func<CommandPublisherExceptionContext, Task> _handler;

        public DelegatingCommandPublisherExceptionHandler(
            Func<CommandPublisherExceptionContext, Task> handler)
        {
            _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        }

        public DelegatingCommandPublisherExceptionHandler(
            Action<CommandPublisherExceptionContext> handler)
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            _handler = context =>
            {
                handler.Invoke(context);
                return Task.FromResult(true);
            };
        }

        public Task Handle(CommandPublisherExceptionContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            return _handler.Invoke(context);
        }
    }
}
