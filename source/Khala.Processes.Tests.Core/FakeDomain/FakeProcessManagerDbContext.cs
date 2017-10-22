namespace Khala.FakeDomain
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Khala.Processes.Sql;
    using Microsoft.EntityFrameworkCore;

    public class FakeProcessManagerDbContext : ProcessManagerDbContext
    {
        private int _commitCount = 0;

        public FakeProcessManagerDbContext(DbContextOptions options)
            : base(options)
        {
        }

        public FakeProcessManagerDbContext()
            : this(new DbContextOptionsBuilder()
                .UseSqlServer($@"Server=(localdb)\mssqllocaldb;Database={typeof(FakeProcessManagerDbContext).FullName}.Core;Trusted_Connection=True;")
                .Options)
        {
        }

        public DbSet<FakeProcessManager> FakeProcessManagers { get; set; }

        public int CommitCount => _commitCount;

        public IDisposable DisposableResource { get; set; }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken)
        {
            try
            {
                return await base.SaveChangesAsync(cancellationToken);
            }
            finally
            {
                Interlocked.Increment(ref _commitCount);
            }
        }

        public override void Dispose()
        {
            base.Dispose();

            DisposableResource?.Dispose();
        }
    }
}
