namespace Khala.Processes.Sql
{
    using System;
    using System.ComponentModel.DataAnnotations;
    using FluentAssertions;
    using Khala.FakeDomain;
    using Khala.Messaging;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class PendingScheduledCommand_specs
    {
        [TestMethod]
        public void sut_has_Id_property()
        {
            typeof(PendingScheduledCommand)
                .Should()
                .HaveProperty<long>("Id")
                .Which.SetMethod.IsPrivate.Should().BeTrue();
        }

        [TestMethod]
        public void sut_has_ProcessManagerType_property()
        {
            typeof(PendingScheduledCommand)
                .Should()
                .HaveProperty<string>("ProcessManagerType")
                .Which.SetMethod.IsPrivate.Should().BeTrue();
        }

        [TestMethod]
        public void ProcessManagerType_is_decorated_with_Required()
        {
            typeof(PendingScheduledCommand)
                .GetProperty("ProcessManagerType")
                .Should()
                .BeDecoratedWith<RequiredAttribute>();
        }

        [TestMethod]
        public void sut_has_ProcessManagerId_property()
        {
            typeof(PendingScheduledCommand)
                .Should()
                .HaveProperty<Guid>("ProcessManagerId")
                .Which.SetMethod.IsPrivate.Should().BeTrue();
        }

        [TestMethod]
        public void sut_has_MessageId_property()
        {
            typeof(PendingScheduledCommand)
                .Should()
                .HaveProperty<Guid>("MessageId")
                .Which.SetMethod.IsPrivate.Should().BeTrue();
        }

        [TestMethod]
        public void sut_has_CorrelationId_property()
        {
            typeof(PendingScheduledCommand)
                .Should()
                .HaveProperty<Guid?>("CorrelationId")
                .Which.SetMethod.IsPrivate.Should().BeTrue();
        }

        [TestMethod]
        public void sut_has_CommandJson_property()
        {
            typeof(PendingScheduledCommand)
                .Should()
                .HaveProperty<string>("CommandJson")
                .Which.SetMethod.IsPrivate.Should().BeTrue();
        }

        [TestMethod]
        public void CommandJson_is_decorated_with_Required()
        {
            typeof(PendingScheduledCommand)
                .GetProperty("CommandJson")
                .Should()
                .BeDecoratedWith<RequiredAttribute>();
        }

        [TestMethod]
        public void sut_has_ScheduledTimeUtc_property()
        {
            typeof(PendingScheduledCommand)
                .Should()
                .HaveProperty<DateTime>("ScheduledTimeUtc")
                .Which.SetMethod.IsPrivate.Should().BeTrue();
        }

        [TestMethod]
        public void FromScheduledEnvelope_generates_PendingScheduledCommand_correctly()
        {
            // Arrange
            var random = new Random();
            var processManager = new FakeProcessManager();
            var messageId = Guid.NewGuid();
            var correlationId = Guid.NewGuid();
            var message = new FakeCommand
            {
                Int32Value = random.Next(),
                StringValue = Guid.NewGuid().ToString(),
            };
            var scheduledEnvelope =
                new ScheduledEnvelope(
                    new Envelope(messageId, message, correlationId: correlationId),
                    DateTime.Now.AddTicks(random.Next()));
            var serializer = new JsonMessageSerializer();

            // Act
            var actual = PendingScheduledCommand.FromScheduledEnvelope(processManager, scheduledEnvelope, serializer);

            // Assert
            actual.ProcessManagerType.Should().Be(typeof(FakeProcessManager).FullName);
            actual.ProcessManagerId.Should().Be(processManager.Id);
            actual.MessageId.Should().Be(messageId);
            actual.CorrelationId.Should().Be(correlationId);
            actual.CommandJson.Should().Be(serializer.Serialize(message));
            actual.ScheduledTimeUtc.Should().Be(scheduledEnvelope.ScheduledTimeUtc);
        }
    }
}
