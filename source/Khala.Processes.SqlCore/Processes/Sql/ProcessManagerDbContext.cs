namespace Khala.Processes.Sql
{
#if NETSTANDARD2_0
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Metadata;
    using Microsoft.EntityFrameworkCore.Metadata.Builders;
#else
    using System.Data.Entity;
#endif

    public class ProcessManagerDbContext : DbContext
    {
#if NETSTANDARD2_0
        public ProcessManagerDbContext(DbContextOptions options)
            : base(options)
        {
        }
#else
        public ProcessManagerDbContext()
        {
        }

        public ProcessManagerDbContext(string nameOrConnectionString)
            : base(nameOrConnectionString)
        {
        }
#endif

        public DbSet<PendingCommand> PendingCommands { get; set; }

        public DbSet<PendingScheduledCommand> PendingScheduledCommands { get; set; }

#if NETSTANDARD2_0
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<PendingCommand>().HasIndex(e => e.ProcessManagerId);
            modelBuilder.Entity<PendingCommand>().HasIndex(e => e.MessageId).IsUnique();

            modelBuilder.Entity<PendingScheduledCommand>().HasIndex(e => e.ProcessManagerId);
            modelBuilder.Entity<PendingScheduledCommand>().HasIndex(e => e.MessageId).IsUnique();

            IEnumerable<IMutableEntityType> processManagerEntityTypes =
                from entityType in modelBuilder.Model.GetEntityTypes()
                where typeof(ProcessManager).IsAssignableFrom(entityType.ClrType)
                select entityType;

            foreach (IMutableEntityType entityType in processManagerEntityTypes)
            {
                EntityTypeBuilder entity = modelBuilder.Entity(entityType.ClrType);
                entity.HasKey("SequenceId");
                entity.HasIndex("Id").IsUnique();
            }
        }
#endif
    }
}
