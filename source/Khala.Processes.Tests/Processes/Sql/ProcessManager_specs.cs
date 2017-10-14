namespace Khala.Processes.Sql
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Linq;
    using System.Reflection;
    using FluentAssertions;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Ploeh.AutoFixture;
    using Ploeh.AutoFixture.AutoMoq;
    using Ploeh.AutoFixture.Idioms;

    [TestClass]
    public class ProcessManager_specs
    {
        [TestMethod]
        public void sut_is_abstract()
        {
            typeof(ProcessManager).IsAbstract.Should().BeTrue(because: "the class should be abstract");
        }

        [TestMethod]
        public void sut_has_SequenceId_property()
        {
            typeof(ProcessManager).Should().HaveProperty<long>("SequenceId");
        }

        [TestMethod]
        public void SequenceId_setter_is_private()
        {
            typeof(ProcessManager)
                .GetProperty("SequenceId")
                .GetSetMethod(nonPublic: true)
                .Should().NotBeNull()
                .And.Subject.IsPrivate.Should().BeTrue();
        }

        [TestMethod]
        public void SequenceId_is_decorated_with_Key()
        {
            typeof(ProcessManager).GetProperty("SequenceId").Should().BeDecoratedWith<KeyAttribute>();
        }

        [TestMethod]
        public void sut_has_Id_property()
        {
            typeof(ProcessManager).Should().HaveProperty<Guid>("Id");
        }

        [TestMethod]
        public void Id_setter_is_private()
        {
            typeof(ProcessManager)
                .GetProperty("Id")
                .GetSetMethod(nonPublic: true)
                .Should().NotBeNull()
                .And.Subject.IsPrivate.Should().BeTrue();
        }

        [TestMethod]
        public void Id_is_decorated_with_Index()
        {
            typeof(ProcessManager)
                .GetProperty("Id")
                .Should()
                .BeDecoratedWith<IndexAttribute>(a => a.IsUnique && a.IsClustered == false);
        }

        [TestMethod]
        public void sut_initializes_Id_correctly()
        {
            List<Guid> ids = Enumerable
                .Repeat<Func<ProcessManager>>(() => Mock.Of<ProcessManager>(), 100)
                .Select(f => f.Invoke())
                .Select(p => p.Id)
                .ToList();

            ids.Should().NotContain(x => x == Guid.Empty);
            ids.Should().OnlyHaveUniqueItems();
        }

        [TestMethod]
        public void sut_has_AddCommand_protected_method()
        {
            typeof(ProcessManager)
                .Should()
                .HaveMethod("AddCommand", new[] { typeof(object) })
                .Which.IsFamily.Should().BeTrue();
        }

        [TestMethod]
        public void AddCommand_has_guard_clause()
        {
            var builder = new Fixture().Customize(new AutoMoqCustomization());
            MethodInfo mut = typeof(ProcessManager).GetMethod(
                "AddCommand", BindingFlags.NonPublic | BindingFlags.Instance);
            new GuardClauseAssertion(builder).Verify(mut);
        }

        [TestMethod]
        public void AddCommand_adds_envelope()
        {
            var sut = Mock.Of<ProcessManager>();
            MethodInfo mut = typeof(ProcessManager).GetMethod(
                "AddCommand", BindingFlags.NonPublic | BindingFlags.Instance);
            var command = new object();

            mut.Invoke(sut, new[] { command });

            IEnumerable<object> actual = sut.FlushPendingCommands();
            actual.Should().ContainSingle().Which.Should().BeSameAs(command);
        }

        [TestMethod]
        public void AddCommand_appends_envelope()
        {
            var sut = Mock.Of<ProcessManager>();
            MethodInfo mut = typeof(ProcessManager).GetMethod(
                "AddCommand", BindingFlags.NonPublic | BindingFlags.Instance);
            mut.Invoke(sut, new[] { new object() });
            var command = new object();

            mut.Invoke(sut, new[] { command });

            IEnumerable<object> actual = sut.FlushPendingCommands();
            actual.Should().HaveCount(2).And.HaveElementAt(1, command);
        }

        [TestMethod]
        public void FlushPendingCommands_clears_pending_commands()
        {
            var sut = Mock.Of<ProcessManager>();
            MethodInfo mut = typeof(ProcessManager).GetMethod(
                "AddCommand", BindingFlags.NonPublic | BindingFlags.Instance);
            mut.Invoke(sut, new[] { new object() });
            mut.Invoke(sut, new[] { new object() });
            mut.Invoke(sut, new[] { new object() });

            sut.FlushPendingCommands();

            IEnumerable<object> actual = sut.FlushPendingCommands();
            actual.Should().NotBeNull().And.BeEmpty();
        }

        [TestMethod]
        public void AddScheduledCommand_has_guard_clause()
        {
            var builder = new Fixture().Customize(new AutoMoqCustomization());
            MethodInfo mut = typeof(ProcessManager).GetMethod(
                "AddScheduledCommand", BindingFlags.NonPublic | BindingFlags.Instance);
            new GuardClauseAssertion(builder).Verify(mut);
        }

        [TestMethod]
        public void AddScheduledCommand_adds_envelope()
        {
            var sut = Mock.Of<ProcessManager>();
            MethodInfo mut = typeof(ProcessManager).GetMethod(
                "AddScheduledCommand", BindingFlags.NonPublic | BindingFlags.Instance);
            var scheduledCommand = new ScheduledCommand(new object(), DateTimeOffset.Now);

            mut.Invoke(sut, new[] { scheduledCommand });

            IEnumerable<ScheduledCommand> actual = sut.FlushPendingScheduledCommands();
            actual.Should().ContainSingle().Which.Should().BeSameAs(scheduledCommand);
        }

        [TestMethod]
        public void AddScheduledCommand_appends_envelope()
        {
            var sut = Mock.Of<ProcessManager>();
            MethodInfo mut = typeof(ProcessManager).GetMethod(
                "AddScheduledCommand", BindingFlags.NonPublic | BindingFlags.Instance);
            var existingScheduledCommand = new ScheduledCommand(new object(), DateTimeOffset.Now);
            mut.Invoke(sut, new[] { existingScheduledCommand });
            var scheduledCommand = new ScheduledCommand(new object(), DateTimeOffset.Now);

            mut.Invoke(sut, new[] { scheduledCommand });

            IEnumerable<ScheduledCommand> actual = sut.FlushPendingScheduledCommands();
            actual.Should().HaveCount(2).And.HaveElementAt(1, scheduledCommand);
        }

        [TestMethod]
        public void FlushPendingScheduledCommands_clears_pending_scheduled_commands()
        {
            var sut = Mock.Of<ProcessManager>();
            MethodInfo mut = typeof(ProcessManager).GetMethod(
                "AddScheduledCommand", BindingFlags.NonPublic | BindingFlags.Instance);
            var fixture = new Fixture();
            mut.Invoke(sut, new[] { fixture.Create<ScheduledCommand>() });
            mut.Invoke(sut, new[] { fixture.Create<ScheduledCommand>() });
            mut.Invoke(sut, new[] { fixture.Create<ScheduledCommand>() });

            sut.FlushPendingScheduledCommands();

            IEnumerable<ScheduledCommand> actual = sut.FlushPendingScheduledCommands();
            actual.Should().NotBeNull().And.BeEmpty();
        }
    }
}
