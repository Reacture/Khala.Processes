namespace Khala.Processes.Sql
{
    using System;
    using System.Data.Entity;

    public interface IProcessManagerDbContext<T> : IDisposable
        where T : class
    {
        DbSet<T> ProcessManagers { get; }
    }
}
