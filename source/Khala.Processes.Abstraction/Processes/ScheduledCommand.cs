namespace Khala.Processes
{
    using System;

    public class ScheduledCommand
    {
        public ScheduledCommand(object command, DateTimeOffset scheduledTime)
        {
            Command = command ?? throw new ArgumentNullException(nameof(command));
            ScheduledTime = scheduledTime;
        }

        public object Command { get; }

        public DateTimeOffset ScheduledTime { get; }
    }
}
