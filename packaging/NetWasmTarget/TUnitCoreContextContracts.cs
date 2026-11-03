using TUnit.Core.Interfaces;

namespace TUnit.Core
{
    // The running test's context: what the source-generated catalog records for
    // the test, under upstream TUnit's names. TestContext.Current is the test
    // the runner executes, through its test-level hooks and its body, and it
    // flows with the test's own awaits as an AsyncLocal does upstream.
    public class TestContext : ITestMetadata
    {
        private static readonly AsyncLocal<TestContext?> s_current = new();

        internal TestContext(GeneratedTestCase testCase, Type classType, object classInstance)
        {
            DefinitionId = testCase.FullyQualifiedName;
            TestName = testCase.MethodName;
            DisplayName = testCase.DisplayName;
            TestDetails = new TestDetails(testCase, classType, classInstance);
        }

        public static TestContext? Current
        {
            get => s_current.Value;
            internal set => s_current.Value = value;
        }

        public ITestMetadata Metadata => this;

        public string DefinitionId { get; }

        public TestDetails TestDetails { get; }

        public string TestName { get; }

        public string DisplayName { get; }
    }

    public class TestDetails
    {
        internal TestDetails(GeneratedTestCase testCase, Type classType, object classInstance)
        {
            TestId = testCase.StableId;
            TestName = testCase.MethodName;
            ClassType = classType;
            MethodName = testCase.MethodName;
            ClassInstance = classInstance;
            TestMethodArguments = [.. testCase.Arguments];
            TestClassArguments = [.. testCase.ConstructorArguments];
            Timeout = testCase.Timeout;
            RetryLimit = testCase.RetryPolicy.MaxRetries;
            TestFilePath = testCase.FilePath;
            TestLineNumber = testCase.LineNumber;
            Categories = [.. testCase.Categories];
            CustomProperties = [];
            foreach (var property in testCase.Properties)
            {
                // The catalog records each [Property] as name=value.
                var separator = property.IndexOf('=');
                var name = property[..separator];
                if (!CustomProperties.TryGetValue(name, out var values))
                {
                    CustomProperties[name] = values = [];
                }

                values.Add(property[(separator + 1)..]);
            }
        }

        public string TestId { get; }

        public string TestName { get; }

        public Type ClassType { get; }

        public string MethodName { get; }

        public object ClassInstance { get; }

        public object?[] TestMethodArguments { get; }

        public object?[] TestClassArguments { get; }

        public TimeSpan? Timeout { get; }

        public int RetryLimit { get; }

        public string TestFilePath { get; }

        public int TestLineNumber { get; }

        public List<string> Categories { get; }

        public Dictionary<string, List<string>> CustomProperties { get; }
    }
}

namespace TUnit.Core.Interfaces
{
    public interface ITestMetadata
    {
        string DefinitionId { get; }

        TestDetails TestDetails { get; }

        string TestName { get; }

        string DisplayName { get; }
    }
}
