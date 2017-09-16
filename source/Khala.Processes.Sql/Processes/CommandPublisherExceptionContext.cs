namespace Khala.Processes
{
    using System;

    /// <summary>
    /// Encapsulates information about an error that occur while publishing commands associated with a process manager.
    /// </summary>
    public class CommandPublisherExceptionContext
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CommandPublisherExceptionContext"/> class.
        /// </summary>
        /// <param name="processManagerType">The type of the process manager.</param>
        /// <param name="processManagerId">The identifier of the process manager.</param>
        /// <param name="exception">The caught exception.</param>
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

        /// <summary>
        /// Gets the type of the process manager.
        /// </summary>
        /// <value>The type of the process manager.</value>
        public Type ProcessManagerType { get; }

        /// <summary>
        /// Gets the identifier of the process manager.
        /// </summary>
        /// <value>The identifier of the process manager.</value>
        public Guid ProcessManagerId { get; }

        /// <summary>
        /// Gets the caught exception.
        /// </summary>
        /// <value>The caught exception.</value>
        public Exception Exception { get; }

        /// <summary>
        /// Gets or sets a value indicating whether the caught exception have been handled so it may not be rethrown.
        /// </summary>
        /// <value>A value indicating whether the caught exception have been handled so it may not be rethrown.</value>
        public bool Handled { get; set; } = false;
    }
}
