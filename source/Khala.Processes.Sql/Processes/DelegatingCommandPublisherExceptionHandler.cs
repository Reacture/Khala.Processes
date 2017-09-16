namespace Khala.Processes
{
    using System;
    using System.Threading.Tasks;

    public class DelegatingCommandPublisherExceptionHandler : ICommandPublisherExceptionHandler
    {
        private Func<CommandPublisherExceptionContext, Task> _action;

        public DelegatingCommandPublisherExceptionHandler(
            Func<CommandPublisherExceptionContext, Task> action)
        {
            _action = action ?? throw new ArgumentNullException(nameof(action));
        }

        public DelegatingCommandPublisherExceptionHandler(
            Action<CommandPublisherExceptionContext> action)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            _action = context =>
            {
                action.Invoke(context);
                return Task.FromResult(true);
            };
        }

        public Task Handle(CommandPublisherExceptionContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            return _action.Invoke(context);
        }
    }
}
