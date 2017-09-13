namespace Khala.Processes.Sql
{
    using System.Data.Entity;

    public interface IProcessManagerDbContext<T> : IProcessManagerDbContext
        where T : ProcessManager
    {
        DbSet<T> ProcessManagers { get; }
    }
}
