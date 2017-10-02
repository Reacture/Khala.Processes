namespace Khala.Processes
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public class CompositeCommandPublisherExceptionHandler : ICommandPublisherExceptionHandler
    {
        private readonly IEnumerable<ICommandPublisherExceptionHandler> _handlers;

        public CompositeCommandPublisherExceptionHandler(params ICommandPublisherExceptionHandler[] handlers)
        {
            if (handlers == null)
            {
                throw new ArgumentNullException(nameof(handlers));
            }

            var handlerList = new List<ICommandPublisherExceptionHandler>(handlers);
            for (int i = 0; i < handlerList.Count; i++)
            {
                ICommandPublisherExceptionHandler handler = handlerList[i];
                if (handler == null)
                {
                    throw new ArgumentException(
                        $"{nameof(handlers)} cannot contain null.",
                        nameof(handlers));
                }
            }

            _handlers = handlerList;
        }

        public Task Handle(CommandPublisherExceptionContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            return RunHandle(context);
        }

        private async Task RunHandle(CommandPublisherExceptionContext context)
        {
            foreach (ICommandPublisherExceptionHandler handler in _handlers)
            {
                try
                {
                    await handler.Handle(context).ConfigureAwait(false);
                }
                catch
                {
                }
            }
        }
    }
}
