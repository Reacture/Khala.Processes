namespace Khala.Processes.Sql
{
    using FluentAssertions;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Metadata;
    using Xunit;

    public class ProcessManagerDbContext_specs
    {
        private readonly DbContextOptions<ProcessManagerDbContext> _dbContextOptions;

        public ProcessManagerDbContext_specs()
        {
            _dbContextOptions = new DbContextOptionsBuilder<ProcessManagerDbContext>()
                .UseInMemoryDatabase(nameof(ProcessManagerDbContext_specs))
                .Options;
        }

        [Fact]
        public void sut_inherits_DbContext()
        {
            typeof(ProcessManagerDbContext).BaseType.Should().Be(typeof(DbContext));
        }

        [Fact]
        public void model_has_PendingCommand_entity()
        {
            var sut = new ProcessManagerDbContext(_dbContextOptions);
            IEntityType actual = sut.Model.FindEntityType(typeof(PendingCommand));
            actual.Should().NotBeNull();
        }

        [Fact]
        public void PendingCommand_entity_has_index_for_ProcessManagerId()
        {
            var context = new ProcessManagerDbContext(_dbContextOptions);
            IEntityType sut = context.Model.FindEntityType(typeof(PendingCommand));
            IProperty property = sut.FindProperty("ProcessManagerId");
            property.GetContainingIndexes().Should().ContainSingle(index => index.IsUnique == false);
        }

        [Fact]
        public void PendingCommand_entity_has_index_for_MessageId()
        {
            var context = new ProcessManagerDbContext(_dbContextOptions);
            IEntityType sut = context.Model.FindEntityType(typeof(PendingCommand));
            IProperty property = sut.FindProperty("MessageId");
            property.GetContainingIndexes().Should().ContainSingle(index => index.IsUnique);
        }
    }
}
