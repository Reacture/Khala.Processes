namespace Khala.Processes
{
    using System;

    public class ScheduledCommand
    {
        public ScheduledCommand(object command, DateTime scheduledTimeUtc)
        {
            Command = command ?? throw new ArgumentNullException(nameof(command));
            ScheduledTimeUtc = scheduledTimeUtc;
        }

        public object Command { get; }

        public DateTime ScheduledTimeUtc { get; }
    }
}
