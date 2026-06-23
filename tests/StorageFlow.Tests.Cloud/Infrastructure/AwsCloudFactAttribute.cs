namespace StorageFlow.Tests.Cloud.Infrastructure;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
internal sealed class AwsCloudFactAttribute : FactAttribute
{
    internal const string EnabledVariable = "STORAGEFLOW_TEST_AWS_ENABLED";

    public AwsCloudFactAttribute()
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable(EnabledVariable),
                "true",
                StringComparison.OrdinalIgnoreCase))
        {
            Skip = $"Set {EnabledVariable}=true to run AWS cloud tests.";
        }
    }
}
