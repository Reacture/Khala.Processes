namespace Khala.Processes.Sql
{
    using System;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using Khala.Messaging;

    public class PendingScheduledCommand
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode", Justification = "Entity Framework calls the private setter.")]
        public long Id { get; private set; }

        [Required]
        public string ProcessManagerType { get; private set; }

        [Index]
        public Guid ProcessManagerId { get; private set; }

        [Index(IsUnique = true)]
        public Guid MessageId { get; private set; }

        public Guid? CorrelationId { get; private set; }

        [Required]
        public string CommandJson { get; private set; }

        public DateTimeOffset ScheduledTime { get; private set; }

        public static PendingScheduledCommand FromScheduledEnvelope<T>(
            T processManager,
            ScheduledEnvelope scheduledEnvelope,
            IMessageSerializer serializer)
            where T : ProcessManager
        {
            if (processManager == null)
            {
                throw new ArgumentNullException(nameof(processManager));
            }

            if (scheduledEnvelope == null)
            {
                throw new ArgumentNullException(nameof(scheduledEnvelope));
            }

            if (serializer == null)
            {
                throw new ArgumentNullException(nameof(serializer));
            }

            return new PendingScheduledCommand
            {
                ProcessManagerType = typeof(T).FullName,
                ProcessManagerId = processManager.Id,
                MessageId = scheduledEnvelope.Envelope.MessageId,
                CorrelationId = scheduledEnvelope.Envelope.CorrelationId,
                CommandJson = serializer.Serialize(scheduledEnvelope.Envelope.Message),
                ScheduledTime = scheduledEnvelope.ScheduledTime
            };
        }
    }
}
