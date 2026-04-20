using Sloth.HttpFileParsing;

namespace Sloth.Execution;

internal sealed record RequestExecutionFailure(
    int RequestIndex,
    HttpRequestDefinition Request,
    string Error,
    bool IsTimeout,
    bool IsCanceled);
