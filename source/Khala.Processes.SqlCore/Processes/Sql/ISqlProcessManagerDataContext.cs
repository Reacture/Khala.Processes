namespace Khala.Processes.Sql
{
    using System;
    using System.Linq.Expressions;
    using System.Threading;
    using System.Threading.Tasks;

    public interface ISqlProcessManagerDataContext<T> : IDisposable
        where T : ProcessManager
    {
        Task<T> FindProcessManager(
            Expression<Func<T, bool>> predicate,
            CancellationToken cancellationToken = default);

        Task SaveProcessManagerAndPublishCommands(
            T processManager,
            Guid? correlationId = default,
            CancellationToken cancellationToken = default);
    }
}
