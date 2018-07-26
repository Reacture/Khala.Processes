namespace Khala.Processes.Sql
{
    using System;
    using System.ComponentModel.DataAnnotations;
    using Khala.Messaging;

    public class PendingCommand
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode", Justification = "Entity Framework calls the private setter.")]
        public long Id { get; private set; }

        [Required]
        public string ProcessManagerType { get; private set; }

        public Guid ProcessManagerId { get; private set; }

        public Guid MessageId { get; private set; }

        public Guid? OperationId { get; private set; }

        public Guid? CorrelationId { get; private set; }

        public string Contributor { get; private set; }

        [Required]
        public string CommandJson { get; private set; }

        public static PendingCommand FromEnvelope<T>(
            T processManager,
            Envelope envelope,
            IMessageSerializer serializer)
            where T : ProcessManager
        {
            if (processManager == null)
            {
                throw new ArgumentNullException(nameof(processManager));
            }

            if (envelope == null)
            {
                throw new ArgumentNullException(nameof(envelope));
            }

            if (serializer == null)
            {
                throw new ArgumentNullException(nameof(serializer));
            }

            return new PendingCommand
            {
                ProcessManagerType = typeof(T).FullName,
                ProcessManagerId = processManager.Id,
                MessageId = envelope.MessageId,
                OperationId = envelope.OperationId,
                CorrelationId = envelope.CorrelationId,
                Contributor = envelope.Contributor,
                CommandJson = serializer.Serialize(envelope.Message),
            };
        }
    }
}
