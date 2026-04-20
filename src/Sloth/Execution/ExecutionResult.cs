namespace Sloth.Execution;

internal sealed record ExecutionResult(
    IReadOnlyList<RequestExecutionOutcome> Outcomes,
    IReadOnlyList<RequestExecutionFailure> Failures)
{
    public bool IsSuccess => Failures.Count == 0;
}
