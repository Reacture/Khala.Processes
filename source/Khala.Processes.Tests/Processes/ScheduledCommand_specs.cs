namespace Khala.Processes
{
    using System;
    using FluentAssertions;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Ploeh.AutoFixture;
    using Ploeh.AutoFixture.Idioms;

    [TestClass]
    public class ScheduledCommand_specs
    {
        [TestMethod]
        public void sut_has_Command_property()
        {
            typeof(ScheduledCommand)
                .Should()
                .HaveProperty<object>("Command")
                .Which.Should().NotBeWritable();
        }

        [TestMethod]
        public void sut_has_ScheduledTime_property()
        {
            typeof(ScheduledCommand)
                .Should()
                .HaveProperty<DateTimeOffset>("ScheduledTime")
                .Which.Should().NotBeWritable();
        }

        [TestMethod]
        public void constructor_sets_properties_correctly()
        {
            var fixture = new Fixture();
            object command = fixture.Create<object>();
            DateTimeOffset scheduledTime = fixture.Create<DateTimeOffset>();

            var sut = new ScheduledCommand(command, scheduledTime);

            sut.Command.Should().BeSameAs(command);
            sut.ScheduledTime.Should().Be(scheduledTime);
        }

        [TestMethod]
        public void sut_has_guard_clauses()
        {
            var builder = new Fixture();
            new GuardClauseAssertion(builder).Verify(typeof(ScheduledCommand));
        }
    }
}
