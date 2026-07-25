namespace Test.Xunit
{
    using System.Threading;
    using System.Threading.Tasks;

    using Test.Shared;
    using Touchstone.Core;
    using Touchstone.XunitAdapter;
    using global::Xunit;

    public sealed class CacheTouchstoneTests
    {
        public static TheoryData<TestCaseDescriptor> TestCases()
        {
            return new TouchstoneTheoryData(CacheTestSuites.All);
        }

        [Theory]
        [MemberData(nameof(TestCases))]
        public async Task RunTest(TestCaseDescriptor testCase)
        {
            await testCase.ExecuteAsync(CancellationToken.None);
        }
    }
}
