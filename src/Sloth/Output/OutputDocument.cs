namespace Sloth.Output;

internal sealed record OutputDocument(
    string InputPath,
    bool IsSuccess,
    int ParsedRequestCount,
    int ExecutedRequestCount,
    int SucceededCount,
    int FailedCount,
    IReadOnlyList<OutputOutcomeDocument> Outcomes,
    IReadOnlyList<OutputFailureDocument> Failures,
    IReadOnlyList<string> ParseErrors);

internal sealed record OutputOutcomeDocument(
    int RequestIndex,
    string Method,
    string Url,
    int StatusCode,
    IReadOnlyDictionary<string, IReadOnlyList<string>> Headers,
    string Body);

internal sealed record OutputFailureDocument(
    int RequestIndex,
    string Method,
    string Url,
    string Error,
    bool IsTimeout,
    bool IsCanceled);
