namespace Khala.Processes.Sql
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

#if NETCOREAPP2_0
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.ChangeTracking;
#else
    using System.Data.Entity;
    using System.Data.Entity.Infrastructure;
#endif

    public interface IProcessManagerDbContext : IDisposable
    {
        DbSet<PendingCommand> PendingCommands { get; }

        DbSet<PendingScheduledCommand> PendingScheduledCommands { get; }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1716:IdentifiersShouldNotMatchKeywords", MessageId = "Set", Justification = "The name follows Entity Framework or Entity Framework Core.")]
        DbSet<TEntity> Set<TEntity>()
            where TEntity : class;

#if NETCOREAPP2_0
        EntityEntry Entry(object entity);
#else
        DbEntityEntry Entry(object entity);
#endif

        Task<int> SaveChangesAsync(CancellationToken cancellationToken);
    }
}
