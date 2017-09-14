namespace Khala.Processes.Sql
{
    using System;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using Khala.Messaging;

    public class PendingCommand
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
                CorrelationId = envelope.CorrelationId,
                CommandJson = serializer.Serialize(envelope.Message)
            };
        }

        internal static Envelope ToEnvelope(
            PendingCommand command,
            IMessageSerializer serializer)
        {
            return new Envelope(
                command.MessageId,
                command.CorrelationId,
                serializer.Deserialize(command.CommandJson));
        }
    }
}
