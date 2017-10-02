namespace Khala.Processes
{
    using System.Threading.Tasks;

    /// <summary>
    /// Represents an exception handler handling exceptions that occur while publishing process manager commands.
    /// </summary>
    public interface ICommandPublisherExceptionHandler
    {
        /// <summary>
        /// Handles an exception that occurred while publishing process manager commands.
        /// </summary>
        /// <param name="context">A <see cref="CommandPublisherExceptionContext"/> contains information about the exception that occurred.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task Handle(CommandPublisherExceptionContext context);
    }
}
