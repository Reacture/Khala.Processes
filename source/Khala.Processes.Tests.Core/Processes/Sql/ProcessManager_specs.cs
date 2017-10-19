﻿namespace Khala.Processes.Sql
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using FluentAssertions;
    using Moq;
    using Xunit;

    public class ProcessManager_specs
    {
        [Fact]
        public void sut_is_abstract()
        {
            typeof(ProcessManager).IsAbstract.Should().BeTrue();
        }

        [Fact]
        public void sut_has_SequenceId_property()
        {
            typeof(ProcessManager)
                .Should()
                .HaveProperty<long>("SequenceId")
                .Which.SetMethod.IsPrivate.Should().BeTrue();
        }

        [Fact]
        public void sut_has_Id_property()
        {
            typeof(ProcessManager)
                .Should()
                .HaveProperty<Guid>("Id")
                .Which.SetMethod.IsPrivate.Should().BeTrue();
        }

        [Fact]
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

        [Fact]
        public void sut_has_AddCommand_protected_method()
        {
            typeof(ProcessManager)
                .Should()
                .HaveMethod("AddCommand", new[] { typeof(object) })
                .Which.IsFamily.Should().BeTrue();
        }

        [Fact]
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

        [Fact]
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

        [Fact]
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

        [Fact]
        public void sut_has_AddScheduledCommand_protected_method()
        {
            typeof(ProcessManager)
                .Should()
                .HaveMethod("AddScheduledCommand", new[] { typeof(ScheduledCommand) })
                .Which.IsFamily.Should().BeTrue();
        }

        [Fact]
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

        [Fact]
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

        [Fact]
        public void FlushPendingScheduledCommands_clears_pending_scheduled_commands()
        {
            var sut = Mock.Of<ProcessManager>();
            MethodInfo mut = typeof(ProcessManager).GetMethod(
                "AddScheduledCommand", BindingFlags.NonPublic | BindingFlags.Instance);
            mut.Invoke(sut, new[] { new ScheduledCommand(new object(), DateTimeOffset.Now) });
            mut.Invoke(sut, new[] { new ScheduledCommand(new object(), DateTimeOffset.Now) });
            mut.Invoke(sut, new[] { new ScheduledCommand(new object(), DateTimeOffset.Now) });

            sut.FlushPendingScheduledCommands();

            IEnumerable<ScheduledCommand> actual = sut.FlushPendingScheduledCommands();
            actual.Should().NotBeNull().And.BeEmpty();
        }
    }
}