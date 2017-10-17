namespace Khala.Processes.Sql
{
    using System;

    public abstract class ProcessManager
    {
        public Guid Id { get; private set; } = Guid.NewGuid();
    }
}
