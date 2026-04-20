namespace Sloth.Execution;

internal sealed record RequestExecutionProgress(
    int CompletedCount,
    int TotalCount,
    RequestExecutionOutcome? Outcome,
    RequestExecutionFailure? Failure);
