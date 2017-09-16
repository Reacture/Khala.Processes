namespace Khala.Processes
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    public interface ICommandPublisher
    {
        Task FlushCommands(Guid processManagerId, CancellationToken cancellationToken);

        void EnqueueAll(CancellationToken cancellationToken);
    }
}
