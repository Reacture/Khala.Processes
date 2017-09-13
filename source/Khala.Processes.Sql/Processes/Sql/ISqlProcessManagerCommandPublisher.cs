namespace Khala.Processes.Sql
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    public interface ISqlProcessManagerCommandPublisher
    {
        Task PublishCommands(Guid processManagerId, CancellationToken cancellationToken);
    }
}
