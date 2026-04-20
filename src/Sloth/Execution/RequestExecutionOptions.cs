namespace Sloth.Execution;

internal sealed record RequestExecutionOptions(TimeSpan RequestTimeout)
{
    public static RequestExecutionOptions Default { get; } = new(TimeSpan.FromSeconds(100));
}
