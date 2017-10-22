namespace Khala.FakeDomain
{
    using System;
    using System.Collections.Generic;
    using Khala.Processes;
    using Khala.Processes.Sql;

    public class FakeProcessManager : ProcessManager
    {
        public FakeProcessManager()
        {
        }

        public FakeProcessManager(IEnumerable<object> commands)
        {
            foreach (object command in commands)
            {
                AddCommand(command);
            }
        }

        public FakeProcessManager(IEnumerable<ScheduledCommand> scheduledCommands)
        {
            foreach (ScheduledCommand scheduledCommand in scheduledCommands)
            {
                AddScheduledCommand(scheduledCommand);
            }
        }

        public Guid AggregateId { get; set; } = Guid.NewGuid();

        public string StatusValue { get; set; } = Guid.NewGuid().ToString();
    }
}
