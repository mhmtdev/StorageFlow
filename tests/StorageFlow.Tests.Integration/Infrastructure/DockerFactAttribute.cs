namespace StorageFlow.Tests.Integration.Infrastructure;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
internal sealed class DockerFactAttribute : FactAttribute
{
    internal const string EnabledVariable = "STORAGEFLOW_TEST_DOCKER";

    public DockerFactAttribute()
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable(EnabledVariable),
                "true",
                StringComparison.OrdinalIgnoreCase))
        {
            Skip = $"Set {EnabledVariable}=true to run Docker integration tests.";
        }
    }
}
