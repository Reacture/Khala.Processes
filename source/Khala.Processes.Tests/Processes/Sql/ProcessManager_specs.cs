namespace Khala.Processes.Sql
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Linq;
    using FluentAssertions;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

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
    }
}
