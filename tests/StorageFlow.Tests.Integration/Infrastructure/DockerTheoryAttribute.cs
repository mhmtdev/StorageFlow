namespace StorageFlow.Tests.Integration.Infrastructure;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
internal sealed class DockerTheoryAttribute : TheoryAttribute
{
    public DockerTheoryAttribute()
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable(DockerFactAttribute.EnabledVariable),
                "true",
                StringComparison.OrdinalIgnoreCase))
        {
            Skip = $"Set {DockerFactAttribute.EnabledVariable}=true to run Docker integration tests.";
        }
    }
}
