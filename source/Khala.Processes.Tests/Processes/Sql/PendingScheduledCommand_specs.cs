namespace Khala.Processes.Sql
{
    using System;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using AutoFixture;
    using AutoFixture.AutoMoq;
    using AutoFixture.Idioms;
    using FluentAssertions;
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
                .BeDecoratedWith<RequiredAttribute>(a => a.AllowEmptyStrings == false);
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
        public void ProcessManagerId_is_decorated_with_Index()
        {
            typeof(PendingScheduledCommand)
                .GetProperty("ProcessManagerId")
                .Should()
                .BeDecoratedWith<IndexAttribute>(a => a.IsUnique == false && a.IsClustered == false);
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
        public void MessageId_is_decorated_with_Index()
        {
            typeof(PendingScheduledCommand)
                .GetProperty("MessageId")
                .Should()
                .BeDecoratedWith<IndexAttribute>(a => a.IsUnique == true && a.IsClustered == false);
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
                .BeDecoratedWith<RequiredAttribute>(a => a.AllowEmptyStrings == false);
        }

        [TestMethod]
        public void sut_has_ScheduledTime_property()
        {
            typeof(PendingScheduledCommand)
                .Should()
                .HaveProperty<DateTimeOffset>("ScheduledTime")
                .Which.SetMethod.IsPrivate.Should().BeTrue();
        }

        [TestMethod]
        public void FromScheduledEnvelope_sets_ProcessManagerType_correctly()
        {
            ScheduledEnvelope scheduledEnvelope = new Fixture().Create<ScheduledEnvelope>();

            var actual = PendingScheduledCommand.FromScheduledEnvelope(
                new FooProcessManager(),
                scheduledEnvelope,
                new JsonMessageSerializer());

            actual.ProcessManagerType.Should().Be(typeof(FooProcessManager).FullName);
        }

        [TestMethod]
        public void FromScheduledEnvelope_sets_ProcessManagerId_correctly()
        {
            var processManager = new FooProcessManager();

            var actual = PendingScheduledCommand.FromScheduledEnvelope(
                processManager,
                new Fixture().Create<ScheduledEnvelope>(),
                new JsonMessageSerializer());

            actual.ProcessManagerId.Should().Be(processManager.Id);
        }

        [TestMethod]
        public void FromScheduledEnvelope_sets_MessageId_correctly()
        {
            ScheduledEnvelope scheduledEnvelope = new Fixture().Create<ScheduledEnvelope>();

            var actual = PendingScheduledCommand.FromScheduledEnvelope(
                new FooProcessManager(),
                scheduledEnvelope,
                new JsonMessageSerializer());

            actual.MessageId.Should().Be(scheduledEnvelope.Envelope.MessageId);
        }

        [TestMethod]
        public void FromScheduledEnvelope_sets_CorrelationId_correctly()
        {
            var correlationId = Guid.NewGuid();
            var scheduledEnvelope = new ScheduledEnvelope(
                new Envelope(correlationId, new object()),
                DateTimeOffset.Now);

            var actual = PendingScheduledCommand.FromScheduledEnvelope(
                new FooProcessManager(),
                scheduledEnvelope,
                new JsonMessageSerializer());

            actual.CorrelationId.Should().Be(scheduledEnvelope.Envelope.CorrelationId);
        }

        [TestMethod]
        public void FromScheduledEnvelope_sets_CommandJson_correctly()
        {
            FooCommand command = new Fixture().Create<FooCommand>();
            var scheduledEnvelope = new ScheduledEnvelope(
                new Envelope(command),
                DateTimeOffset.Now);
            var serializer = new JsonMessageSerializer();

            var actual = PendingScheduledCommand.FromScheduledEnvelope(
                new FooProcessManager(),
                scheduledEnvelope,
                serializer);

            serializer.Deserialize(actual.CommandJson).ShouldBeEquivalentTo(
                command,
                opts => opts.RespectingRuntimeTypes());
        }

        [TestMethod]
        public void FromScheduledEnvelope_sets_ScheduledTime_correctly()
        {
            ScheduledEnvelope scheduledEnvelope = new Fixture().Create<ScheduledEnvelope>();

            var actual = PendingScheduledCommand.FromScheduledEnvelope(
                new FooProcessManager(),
                scheduledEnvelope,
                new JsonMessageSerializer());

            actual.ScheduledTime.Should().Be(scheduledEnvelope.ScheduledTime);
        }

        [TestMethod]
        public void sut_has_guard_clauses()
        {
            IFixture builder = new Fixture().Customize(new AutoMoqCustomization());
            new GuardClauseAssertion(builder).Verify(typeof(PendingScheduledCommand));
        }

        public class FooProcessManager : ProcessManager
        {
        }

        public class FooCommand
        {
            public int Int32Value { get; set; }

            public string StringValue { get; set; }
        }
    }
}
