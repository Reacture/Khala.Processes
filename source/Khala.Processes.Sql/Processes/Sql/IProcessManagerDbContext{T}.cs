namespace Khala.Processes.Sql
{
    using System;
    using System.Data.Entity;

    public interface IProcessManagerDbContext<T> : IDisposable
        where T : ProcessManager
    {
        DbSet<T> ProcessManagers { get; }
    }
}
