namespace Khala.Processes.Sql
{
    using System;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using FluentAssertions;
    using Khala.Messaging;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Ploeh.AutoFixture;
    using Ploeh.AutoFixture.AutoMoq;
    using Ploeh.AutoFixture.Idioms;

    [TestClass]
    public class PendingCommand_specs
    {
        public TestContext TestContext { get; set; }

        [TestMethod]
        public void sut_has_Id_property()
        {
            typeof(PendingCommand).Should().HaveProperty<long>("Id");
        }

        [TestMethod]
        public void Id_setter_is_private()
        {
            typeof(PendingCommand)
                .GetProperty("Id")
                .GetSetMethod(nonPublic: true)
                .IsPrivate.Should().BeTrue();
        }

        [TestMethod]
        public void sut_has_ProcessManagerType_property()
        {
            typeof(PendingCommand).Should().HaveProperty<string>("ProcessManagerType");
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1702:CompoundWordsShouldBeCasedCorrectly", MessageId = "Typesetter", Justification = "It's not a compound word.")]
        [TestMethod]
        public void ProcessManagerType_setter_is_private()
        {
            typeof(PendingCommand)
                .GetProperty("ProcessManagerType")
                .GetSetMethod(nonPublic: true)
                .IsPrivate.Should().BeTrue();
        }

        [TestMethod]
        public void ProcessManagerType_is_decorated_with_Required()
        {
            typeof(PendingCommand)
                .GetProperty("ProcessManagerType")
                .Should()
                .BeDecoratedWith<RequiredAttribute>(a => a.AllowEmptyStrings == false);
        }

        [TestMethod]
        public void sut_has_ProcessManagerId_property()
        {
            typeof(PendingCommand).Should().HaveProperty<Guid>("ProcessManagerId");
        }

        [TestMethod]
        public void ProcessManagerId_setter_is_private()
        {
            typeof(PendingCommand)
                .GetProperty("ProcessManagerId")
                .GetSetMethod(nonPublic: true)
                .IsPrivate.Should().BeTrue();
        }

        [TestMethod]
        public void sut_has_MessageId_property()
        {
            typeof(PendingCommand).Should().HaveProperty<Guid>("MessageId");
        }

        [TestMethod]
        public void MessageId_setter_is_private()
        {
            typeof(PendingCommand)
                .GetProperty("MessageId")
                .GetSetMethod(nonPublic: true)
                .IsPrivate.Should().BeTrue();
        }

        [TestMethod]
        public void MessageId_is_decorated_with_Index()
        {
            typeof(PendingCommand)
                .GetProperty("MessageId")
                .Should()
                .BeDecoratedWith<IndexAttribute>(a => a.IsUnique);
        }

        [TestMethod]
        public void sut_has_CorrelationId_property()
        {
            typeof(PendingCommand).Should().HaveProperty<Guid?>("CorrelationId");
        }

        [TestMethod]
        public void CorrelationId_setter_is_private()
        {
            typeof(PendingCommand)
                .GetProperty("CorrelationId")
                .GetSetMethod(nonPublic: true)
                .IsPrivate.Should().BeTrue();
        }

        [TestMethod]
        public void sut_has_CommandJson_property()
        {
            typeof(PendingCommand).Should().HaveProperty<string>("CommandJson");
        }

        [TestMethod]
        public void CommandJson_setter_is_private()
        {
            typeof(PendingCommand)
                .GetProperty("CommandJson")
                .GetSetMethod(nonPublic: true)
                .IsPrivate.Should().BeTrue();
        }

        [TestMethod]
        public void CommandJson_is_decorated_with_Required()
        {
            typeof(PendingCommand)
                .GetProperty("CommandJson")
                .Should()
                .BeDecoratedWith<RequiredAttribute>(a => a.AllowEmptyStrings == false);
        }

        [TestMethod]
        public void sut_has_guard_clauses()
        {
            var builder = new Fixture().Customize(new AutoMoqCustomization());
            new GuardClauseAssertion(builder).Verify(typeof(PendingCommand));
        }

        [TestMethod]
        public void FromEnvelope_sets_ProcessManagerType_correctly()
        {
            var fixture = new Fixture();
            var envelope = new Envelope(fixture.Create<FooCommand>());
            var processManager = new FooProcessManager();
            var serializer = new JsonMessageSerializer();

            var actual = PendingCommand.FromEnvelope(processManager, envelope, serializer);

            TestContext.WriteLine(actual.ProcessManagerType);
            actual.ProcessManagerType.Should().Be(typeof(FooProcessManager).FullName);
        }

        [TestMethod]
        public void FromEnvelope_sets_ProcessManagerId_correctly()
        {
            var fixture = new Fixture();
            var envelope = new Envelope(fixture.Create<FooCommand>());
            var processManager = new FooProcessManager();
            var serializer = new JsonMessageSerializer();

            var actual = PendingCommand.FromEnvelope(processManager, envelope, serializer);

            TestContext.WriteLine($"{actual.ProcessManagerId}");
            actual.ProcessManagerId.Should().Be(processManager.Id);
        }

        [TestMethod]
        public void FromEnvelope_sets_MessageId_correctly()
        {
            var fixture = new Fixture();
            var envelope = new Envelope(fixture.Create<FooCommand>());
            var processManager = new FooProcessManager();
            var serializer = new JsonMessageSerializer();

            var actual = PendingCommand.FromEnvelope(processManager, envelope, serializer);

            actual.MessageId.Should().Be(envelope.MessageId);
        }

        [TestMethod]
        public void FromEnvelope_sets_CorrelationId_correctly()
        {
            var fixture = new Fixture();
            var correlationId = Guid.NewGuid();
            var envelope = new Envelope(correlationId, fixture.Create<FooCommand>());
            var processManager = new FooProcessManager();
            var serializer = new JsonMessageSerializer();

            var actual = PendingCommand.FromEnvelope(processManager, envelope, serializer);

            actual.CorrelationId.Should().Be(correlationId);
        }

        [TestMethod]
        public void FromEnvelope_sets_CommandJson_correctly()
        {
            var fixture = new Fixture();
            var command = fixture.Create<FooCommand>();
            var envelope = new Envelope(command);
            var processManager = new FooProcessManager();
            var serializer = new JsonMessageSerializer();

            var actual = PendingCommand.FromEnvelope(processManager, envelope, serializer);

            serializer.Deserialize(actual.CommandJson)
                .Should().BeOfType<FooCommand>()
                .Subject.ShouldBeEquivalentTo(command);
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
