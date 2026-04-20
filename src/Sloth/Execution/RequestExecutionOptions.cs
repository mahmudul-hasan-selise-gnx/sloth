namespace Sloth.Execution;

internal sealed record RequestExecutionOptions(
    TimeSpan RequestTimeout,
    IProgress<RequestExecutionProgress>? Progress = null)
{
    public static RequestExecutionOptions Default { get; } = new(TimeSpan.FromSeconds(100));
}
