using Sloth.Domain;
using Sloth.Execution;
using Sloth.HttpFileParsing;
using Sloth.Output;

namespace Sloth.Cli;

internal interface IRunService
{
    Task<int> RunAsync(RunOptions options, CancellationToken cancellationToken);
}

internal sealed class RunService : IRunService
{
    private readonly IHttpFileParser _httpFileParser;
    private readonly IRequestExecutor _requestExecutor;
    private readonly IOutputFormatterRegistry _outputFormatterRegistry;
    private readonly IOutputWriterFactory _outputWriterFactory;

    public RunService(
        IHttpFileParser httpFileParser,
        IRequestExecutor requestExecutor,
        IOutputFormatterRegistry outputFormatterRegistry,
        IOutputWriterFactory outputWriterFactory)
    {
        _httpFileParser = httpFileParser;
        _requestExecutor = requestExecutor;
        _outputFormatterRegistry = outputFormatterRegistry;
        _outputWriterFactory = outputWriterFactory;
    }

    public async Task<int> RunAsync(RunOptions options, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            await using var outputWriter = _outputWriterFactory.Create(options);
            var outputFormatter = _outputFormatterRegistry.Resolve(options.OutputPath);
            var useConsoleProgress = string.IsNullOrWhiteSpace(options.OutputPath);

            var parseResult = _httpFileParser.ParseFile(options.InputPath);
            if (!parseResult.IsSuccess)
            {
                var parseFailureDocument = new OutputDocument(
                    InputPath: options.InputPath,
                    IsSuccess: false,
                    ParsedRequestCount: 0,
                    ExecutedRequestCount: 0,
                    SucceededCount: 0,
                    FailedCount: parseResult.Errors.Count,
                    Outcomes: [],
                    Failures: [],
                    ParseErrors: parseResult.Errors);

                await outputWriter.WriteAsync(outputFormatter.Format(parseFailureDocument), cancellationToken);
                return 1;
            }

            var document = parseResult.Document!;
            ConsoleExecutionProgressReporter? progressReporter = null;
            var executionOptions = RequestExecutionOptions.Default;
            if (useConsoleProgress)
            {
                progressReporter = new ConsoleExecutionProgressReporter();
                progressReporter.Start(document.Requests.Count);
                executionOptions = executionOptions with
                {
                    Progress = progressReporter
                };
            }

            var executionResult = await _requestExecutor.ExecuteAsync(
                document,
                executionOptions,
                cancellationToken);

            progressReporter?.Complete();

            var outputDocument = new OutputDocument(
                InputPath: options.InputPath,
                IsSuccess: executionResult.IsSuccess,
                ParsedRequestCount: document.Requests.Count,
                ExecutedRequestCount: executionResult.Outcomes.Count + executionResult.Failures.Count,
                SucceededCount: executionResult.Outcomes.Count,
                FailedCount: executionResult.Failures.Count,
                Outcomes: executionResult.Outcomes.Select(outcome => new OutputOutcomeDocument(
                    RequestIndex: outcome.RequestIndex,
                    Method: outcome.Request.Method,
                    Url: outcome.Request.Url,
                    StatusCode: (int)outcome.StatusCode,
                    Headers: outcome.Headers,
                    Body: outcome.Body)).ToArray(),
                Failures: executionResult.Failures.Select(failure => new OutputFailureDocument(
                    RequestIndex: failure.RequestIndex,
                    Method: failure.Request.Method,
                    Url: failure.Request.Url,
                    Error: failure.Error,
                    IsTimeout: failure.IsTimeout,
                    IsCanceled: failure.IsCanceled)).ToArray(),
                ParseErrors: []);

            await outputWriter.WriteAsync(outputFormatter.Format(outputDocument), cancellationToken);
            return executionResult.IsSuccess ? 0 : 1;
        }
        catch (InvalidOperationException ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }
}

internal sealed class ConsoleExecutionProgressReporter : IProgress<RequestExecutionProgress>
{
    private const int BarWidth = 32;
    private const int ClearWidth = 180;
    private readonly Lock _lock = new();
    private int _totalCount;

    public void Report(RequestExecutionProgress progress)
    {
        var progressBar = BuildProgressBar(progress.CompletedCount, progress.TotalCount);
        lock (_lock)
        {
            _totalCount = progress.TotalCount;
            ClearCurrentLine();

            if (progress.Outcome is not null)
            {
                var outcome = progress.Outcome;
                Console.WriteLine($"  ✓ [{outcome.RequestIndex + 1}] {outcome.Request.Method} {outcome.Request.Url} -> {(int)outcome.StatusCode} {outcome.StatusCode}");
            }
            else if (progress.Failure is not null)
            {
                var failure = progress.Failure;
                Console.WriteLine($"  ✗ [{failure.RequestIndex + 1}] {failure.Request.Method} {failure.Request.Url} -> {failure.Error}");
            }

            Console.Write(progressBar);
        }
    }

    public void Start(int totalCount)
    {
        lock (_lock)
        {
            _totalCount = totalCount;
            Console.Write(BuildProgressBar(0, totalCount));
        }
    }

    public void Complete()
    {
        lock (_lock)
        {
            if (_totalCount == 0)
            {
                Console.Write(BuildProgressBar(0, 0));
            }

            Console.WriteLine();
        }
    }

    private static string BuildProgressBar(int completedCount, int totalCount)
    {
        if (totalCount <= 0)
        {
            return "[--------------------------------] 0/0 (0%)";
        }

        var progressRatio = (double)completedCount / totalCount;
        var filledWidth = (int)Math.Round(progressRatio * BarWidth, MidpointRounding.AwayFromZero);
        filledWidth = Math.Clamp(filledWidth, 0, BarWidth);
        var emptyWidth = BarWidth - filledWidth;
        var percent = progressRatio * 100;

        return $"[{new string('#', filledWidth)}{new string('-', emptyWidth)}] {completedCount}/{totalCount} ({percent,5:0.0}%)";
    }

    private static void ClearCurrentLine()
    {
        Console.Write('\r');
        Console.Write(new string(' ', ClearWidth));
        Console.Write('\r');
    }
}
