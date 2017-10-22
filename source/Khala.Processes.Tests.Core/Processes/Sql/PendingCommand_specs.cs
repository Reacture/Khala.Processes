namespace Khala.Processes.Sql
{
    using System;
    using System.ComponentModel.DataAnnotations;
    using FluentAssertions;
    using Khala.FakeDomain;
    using Khala.Messaging;
    using Xunit;

    public class PendingCommand_specs
    {
        [Fact]
        public void sut_has_Id_property()
        {
            typeof(PendingCommand)
                .Should()
                .HaveProperty<long>("Id")
                .Which.SetMethod.IsPrivate.Should().BeTrue();
        }

        [Fact]
        public void sut_has_ProcessManagerType_property()
        {
            typeof(PendingCommand)
                .Should()
                .HaveProperty<string>("ProcessManagerType")
                .Which.SetMethod.IsPrivate.Should().BeTrue();
        }

        [Fact]
        public void ProcessManagerType_is_decorated_with_Required()
        {
            typeof(PendingCommand)
                .GetProperty("ProcessManagerType")
                .Should()
                .BeDecoratedWith<RequiredAttribute>();
        }

        [Fact]
        public void sut_has_ProcessManagerId_property()
        {
            typeof(PendingCommand)
                .Should()
                .HaveProperty<Guid>("ProcessManagerId")
                .Which.SetMethod.IsPrivate.Should().BeTrue();
        }

        [Fact]
        public void sut_has_MessageId_property()
        {
            typeof(PendingCommand)
                .Should()
                .HaveProperty<Guid>("MessageId")
                .Which.SetMethod.IsPrivate.Should().BeTrue();
        }

        [Fact]
        public void sut_has_CorrelationId_property()
        {
            typeof(PendingCommand)
                .Should()
                .HaveProperty<Guid?>("CorrelationId")
                .Which.SetMethod.IsPrivate.Should().BeTrue();
        }

        [Fact]
        public void sut_has_CommandJson_property()
        {
            typeof(PendingCommand)
                .Should()
                .HaveProperty<string>("CommandJson")
                .Which.SetMethod.IsPrivate.Should().BeTrue();
        }

        [Fact]
        public void CommandJson_is_decorated_with_Required()
        {
            typeof(PendingCommand)
                .GetProperty("CommandJson")
                .Should()
                .BeDecoratedWith<RequiredAttribute>();
        }

        [Fact]
        public void FromEnvelope_generates_PendingCommand_correctly()
        {
            // Arrange
            var processManager = new FakeProcessManager();
            var messageId = Guid.NewGuid();
            var correlationId = Guid.NewGuid();
            var message = new FakeCommand
            {
                Int32Value = new Random().Next(),
                StringValue = Guid.NewGuid().ToString()
            };
            var envelope = new Envelope(messageId, correlationId, message);
            var serializer = new JsonMessageSerializer();

            // Act
            var actual = PendingCommand.FromEnvelope(processManager, envelope, serializer);

            // Assert
            actual.ProcessManagerType.Should().Be(typeof(FakeProcessManager).FullName);
            actual.ProcessManagerId.Should().Be(processManager.Id);
            actual.MessageId.Should().Be(messageId);
            actual.CorrelationId.Should().Be(correlationId);
            actual.CommandJson.Should().Be(serializer.Serialize(message));
        }
    }
}
