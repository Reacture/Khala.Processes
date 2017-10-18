namespace Khala.Processes.Sql
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using FluentAssertions;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.ChangeTracking;
    using Xunit;

    public class IProcessManagerDbContext_specs
    {
        [Fact]
        public void sut_inherits_IDisposable()
        {
            typeof(IProcessManagerDbContext).Should().Implement<IDisposable>();
        }

        [Fact]
        public void sut_has_PendingCommands_property()
        {
            typeof(IProcessManagerDbContext)
                .Should()
                .HaveProperty<DbSet<PendingCommand>>("PendingCommands")
                .Which.Should().NotBeWritable();
        }

        [Fact]
        public void sut_has_PendingScheduledCommands_property()
        {
            typeof(IProcessManagerDbContext)
                .Should()
                .HaveProperty<DbSet<PendingScheduledCommand>>("PendingScheduledCommands")
                .Which.Should().NotBeWritable();
        }

        [Fact]
        public void sut_has_Entry_method()
        {
            typeof(IProcessManagerDbContext)
                .Should()
                .HaveMethod("Entry", new[] { typeof(object) })
                .Which.ReturnType.Should().Be(typeof(EntityEntry));
        }

        [Fact]
        public void sut_has_SaveChangesAsync_method()
        {
            typeof(IProcessManagerDbContext)
                .Should()
                .HaveMethod("SaveChangesAsync", new[] { typeof(CancellationToken) })
                .Which.ReturnType.Should().Be(typeof(Task<int>));
        }
    }
}
