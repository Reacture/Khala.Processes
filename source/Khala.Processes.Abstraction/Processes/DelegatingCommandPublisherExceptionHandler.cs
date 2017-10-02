namespace Khala.Processes
{
    using System;
    using System.Threading.Tasks;

    /// <summary>
    /// An implementation of <see cref="ICommandPublisherExceptionHandler"/> that delegates responsibility for handling exceptions to a handler function.
    /// </summary>
    public class DelegatingCommandPublisherExceptionHandler : ICommandPublisherExceptionHandler
    {
        private Func<CommandPublisherExceptionContext, Task> _handler;

        /// <summary>
        /// Initializes a new instance of the <see cref="DelegatingCommandPublisherExceptionHandler"/> class with an asynchronous exception handler function.
        /// </summary>
        /// <param name="handler">An asynchronous exception handler function.</param>
        public DelegatingCommandPublisherExceptionHandler(
            Func<CommandPublisherExceptionContext, Task> handler)
        {
            _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DelegatingCommandPublisherExceptionHandler"/> class with a synchronous exception handler function.
        /// </summary>
        /// <param name="handler">A synchronous exception handler function.</param>
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

        /// <summary>
        /// Handles an exception that occurred while publishing process manager commands.
        /// </summary>
        /// <param name="context">A <see cref="CommandPublisherExceptionContext"/> contains information about the exception that occurred.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
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
