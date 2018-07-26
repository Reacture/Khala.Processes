namespace Khala.Processes.Sql
{
    using System;
    using System.ComponentModel.DataAnnotations;
    using Khala.Messaging;

    public class PendingScheduledCommand
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode", Justification = "Entity Framework calls the private setter.")]
        public long Id { get; private set; }

        [Required]
        public string ProcessManagerType { get; private set; }

        public Guid ProcessManagerId { get; private set; }

        public Guid MessageId { get; private set; }

        public string OperationId { get; private set; }

        public Guid? CorrelationId { get; private set; }

        public string Contributor { get; private set; }

        [Required]
        public string CommandJson { get; private set; }

        public DateTime ScheduledTimeUtc { get; private set; }

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
                OperationId = scheduledEnvelope.Envelope.OperationId,
                CorrelationId = scheduledEnvelope.Envelope.CorrelationId,
                Contributor = scheduledEnvelope.Envelope.Contributor,
                CommandJson = serializer.Serialize(scheduledEnvelope.Envelope.Message),
                ScheduledTimeUtc = scheduledEnvelope.ScheduledTimeUtc,
            };
        }
    }
}
