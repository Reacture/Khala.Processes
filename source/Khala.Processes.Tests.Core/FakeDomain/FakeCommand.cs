namespace Khala.FakeDomain
{
    using System;

    public class FakeCommand
    {
        private static readonly Random _random;

        static FakeCommand()
        {
            _random = new Random(new object().GetHashCode());
        }

        public int Int32Value { get; set; } = _random.Next();

        public string StringValue { get; set; } = Guid.NewGuid().ToString();
    }
}
