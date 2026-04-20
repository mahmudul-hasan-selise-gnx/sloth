using Sloth.HttpFileParsing;

namespace Sloth.Execution;

internal interface IRequestExecutor
{
    Task<ExecutionResult> ExecuteAsync(
        HttpFileDocument document,
        RequestExecutionOptions options,
        CancellationToken cancellationToken);
}
