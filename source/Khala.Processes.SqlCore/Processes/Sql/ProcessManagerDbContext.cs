namespace Khala.Processes.Sql
{
    using Microsoft.EntityFrameworkCore;

    public class ProcessManagerDbContext : DbContext
    {
        public ProcessManagerDbContext(DbContextOptions options)
            : base(options)
        {
        }

        public DbSet<PendingCommand> PendingCommands { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<PendingCommand>().HasIndex(e => e.ProcessManagerId);
            modelBuilder.Entity<PendingCommand>().HasIndex(e => e.MessageId).IsUnique();
        }
    }
}
