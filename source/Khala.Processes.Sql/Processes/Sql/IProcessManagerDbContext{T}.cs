namespace Khala.Processes.Sql
{
    using System;
    using System.Data.Entity;
    using System.Data.Entity.Infrastructure;
    using System.Threading;
    using System.Threading.Tasks;

    public interface IProcessManagerDbContext<T> : IDisposable
        where T : ProcessManager
    {
        DbSet<T> ProcessManagers { get; }

        DbSet<PendingCommand> PendingCommands { get; }

        DbEntityEntry Entry(object entity);

        Task<int> SaveChangesAsync(CancellationToken cancellationToken);
    }
}
