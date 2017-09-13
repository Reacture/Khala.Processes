namespace Khala.Processes.Sql
{
    using System;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;

    public abstract class ProcessManager
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode", Justification = "Entity Framework calls the private setter.")]
        [Key]
        public long SequenceId { get; private set; }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode", Justification = "Entity Framework calls the private setter.")]
        [Index(IsUnique = true)]
        public Guid Id { get; private set; } = Guid.NewGuid();
    }
}
