namespace Khala.Processes.Sql
{
    using System;
    using System.Data.Entity;
    using System.Data.Entity.Infrastructure;
    using System.Threading;
    using System.Threading.Tasks;

    public interface IProcessManagerDbContext : IDisposable
    {
        DbSet<PendingCommand> PendingCommands { get; }

        DbSet<PendingScheduledCommand> PendingScheduledCommands { get; }

        DbEntityEntry Entry(object entity);

        Task<int> SaveChangesAsync(CancellationToken cancellationToken);
    }
}
