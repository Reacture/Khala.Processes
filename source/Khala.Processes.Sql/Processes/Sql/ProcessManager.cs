namespace Khala.Processes.Sql
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Linq;

    public abstract class ProcessManager
    {
        private readonly List<object> _pendingCommands = new List<object>();
        private readonly List<object> _pendingScheduledCommands = new List<object>();

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode", Justification = "Entity Framework calls the private setter.")]
        [Key]
        public long SequenceId { get; private set; }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode", Justification = "Entity Framework calls the private setter.")]
        [Index(IsUnique = true)]
        public Guid Id { get; private set; } = Guid.NewGuid();

        protected void AddCommand(object command)
        {
            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            _pendingCommands.Add(command);
        }

        public IEnumerable<object> FlushPendingCommands()
        {
            List<object> commands = _pendingCommands.ToList();
            try
            {
                return commands;
            }
            finally
            {
                _pendingCommands.Clear();
            }
        }

        protected void AddScheduledCommand(object command)
        {
            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            _pendingScheduledCommands.Add(command);
        }

        public IEnumerable<object> FlushPendingScheduledCommands()
        {
            List<object> scheduledCommands = _pendingScheduledCommands.ToList();
            try
            {
                return scheduledCommands;
            }
            finally
            {
                _pendingScheduledCommands.Clear();
            }
        }
    }
}
