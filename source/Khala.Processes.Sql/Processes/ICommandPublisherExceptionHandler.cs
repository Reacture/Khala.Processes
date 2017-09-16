namespace Khala.Processes
{
    using System.Threading.Tasks;

    public interface ICommandPublisherExceptionHandler
    {
        Task Handle(CommandPublisherExceptionContext context);
    }
}