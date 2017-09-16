namespace Khala.Processes
{
    using System.Threading.Tasks;

    internal class DefaultCommandPublisherExceptionHandler : ICommandPublisherExceptionHandler
    {
        public static readonly DefaultCommandPublisherExceptionHandler Instance = new DefaultCommandPublisherExceptionHandler();

        private DefaultCommandPublisherExceptionHandler()
        {
        }

        public Task Handle(CommandPublisherExceptionContext context) => Task.FromResult(true);
    }
}
