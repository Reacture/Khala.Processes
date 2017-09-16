namespace Khala.Processes
{
    using System;

    public class CommandPublisherExceptionContext
    {
        public CommandPublisherExceptionContext(
            Type processManagerType,
            Guid processManagerId,
            Exception exception)
        {
            if (processManagerId == Guid.Empty)
            {
                throw new ArgumentException("Value cannot be empty.", nameof(processManagerId));
            }

            ProcessManagerType = processManagerType ?? throw new ArgumentNullException(nameof(processManagerType));
            ProcessManagerId = processManagerId;
            Exception = exception ?? throw new ArgumentNullException(nameof(exception));
        }

        public Type ProcessManagerType { get; }

        public Guid ProcessManagerId { get; }

        public Exception Exception { get; }

        public bool Handled { get; set; } = false;
    }
}
