namespace Khala.Processes.Sql
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
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
    }
}
