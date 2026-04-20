using System.Collections.ObjectModel;
using Sloth.Execution;
using Sloth.HttpFileParsing;
using Sloth.Output;

return await new CompositionRoot().RunAsync(args, CancellationToken.None);

internal sealed class CompositionRoot
{
    private readonly ICommandLineParser _parser;
    private readonly IRunService _runService;

    public CompositionRoot()
    {
        var optionRegistry = new OptionRegistry(new[]
        {
            OptionDefinition.Flag("-h", "--help"),
            OptionDefinition.Flag("-v", "--version"),
            OptionDefinition.Value("-f", "--file"),
            OptionDefinition.Value("-o", "--output"),
            OptionDefinition.Value("-ow", "--outputw")
        });

        _parser = new CommandLineParser(optionRegistry);
        var formatterRegistry = new OutputFormatterRegistry(
            new Dictionary<string, IOutputFormatter>(StringComparer.OrdinalIgnoreCase)
            {
                [".json"] = new JsonOutputFormatter(),
                [".txt"] = new TextOutputFormatter()
            },
            defaultFormatter: new TextOutputFormatter());

        var outputWriterFactory = new OutputWriterFactory(new OutputPathPolicy());
        _runService = new RunService(
            new HttpFileParser(),
            new HttpClientRequestExecutor(new HttpClient()),
            formatterRegistry,
            outputWriterFactory);
    }

    public async Task<int> RunAsync(string[] args, CancellationToken cancellationToken)
    {
        var parseResult = _parser.Parse(args);

        return parseResult.Status switch
        {
            ParseStatus.HelpRequested => ExitWithMessage(0, parseResult.Message),
            ParseStatus.VersionRequested => ExitWithMessage(0, parseResult.Message),
            ParseStatus.Error => ExitWithMessage(1, parseResult.Message),
            ParseStatus.Success when parseResult.Options is not null => await _runService.RunAsync(parseResult.Options, cancellationToken),
            _ => ExitWithMessage(1, "Unable to process command line arguments.")
        };
    }

    private static int ExitWithMessage(int exitCode, string? message)
    {
        if (!string.IsNullOrWhiteSpace(message))
        {
            Console.WriteLine(message);
        }

        return exitCode;
    }
}

internal interface ICommandLineParser
{
    ParseResult Parse(string[] args);
}

internal sealed class CommandLineParser : ICommandLineParser
{
    private static readonly string HelpText =
        """
        Usage:
          sloth <file.http> [options]
          sloth -f|--file <file.http> [options]

        Options:
          -f, --file <path>      Path to .http file.
          -o, --output <path>    Write output to file (no overwrite).
          -ow, --outputw <path>  Write output to file and overwrite existing file.
          -v, --version          Print version.
          -h, --help             Show help.
        """;

    private readonly OptionRegistry _optionRegistry;

    public CommandLineParser(OptionRegistry optionRegistry)
    {
        _optionRegistry = optionRegistry;
    }

    public ParseResult Parse(string[] args)
    {
        if (args.Length == 0)
        {
            return ParseResult.Error("No input specified. Provide a .http file path.", HelpText);
        }

        string? positionalPath = null;
        string? filePath = null;
        string? outputPath = null;
        string? overwriteOutputPath = null;
        bool helpRequested = false;
        bool versionRequested = false;

        for (var index = 0; index < args.Length; index++)
        {
            var token = args[index];

            if (!_optionRegistry.TryGet(token, out var option))
            {
                if (token.StartsWith("-", StringComparison.Ordinal))
                {
                    return ParseResult.Error($"Unknown option '{token}'.", HelpText);
                }

                if (positionalPath is not null)
                {
                    return ParseResult.Error("Only one positional input file is allowed.", HelpText);
                }

                positionalPath = token;
                continue;
            }

            if (!option.RequiresValue)
            {
                if (option.HasAlias("-h", "--help"))
                {
                    helpRequested = true;
                }
                else if (option.HasAlias("-v", "--version"))
                {
                    versionRequested = true;
                }

                continue;
            }

            if (index + 1 >= args.Length)
            {
                return ParseResult.Error($"Option '{token}' expects a value.", HelpText);
            }

            var value = args[++index];
            if (value.StartsWith("-", StringComparison.Ordinal))
            {
                return ParseResult.Error($"Option '{token}' expects a value.", HelpText);
            }

            if (option.HasAlias("-f", "--file"))
            {
                filePath = value;
            }
            else if (option.HasAlias("-o", "--output"))
            {
                outputPath = value;
            }
            else if (option.HasAlias("-ow", "--outputw"))
            {
                overwriteOutputPath = value;
            }
        }

        if (helpRequested)
        {
            return ParseResult.Help(HelpText);
        }

        if (versionRequested)
        {
            var version = typeof(Program).Assembly.GetName().Version?.ToString() ?? "unknown";
            return ParseResult.Version($"sloth {version}");
        }

        if (outputPath is not null && overwriteOutputPath is not null)
        {
            return ParseResult.Error("Options '-o/--output' and '-ow/--outputw' cannot be used together.", HelpText);
        }

        if (filePath is not null && positionalPath is not null)
        {
            return ParseResult.Error("Use either positional .http path or '-f/--file', not both.", HelpText);
        }

        var normalizedInputPath = filePath ?? positionalPath;
        if (string.IsNullOrWhiteSpace(normalizedInputPath))
        {
            return ParseResult.Error("No input specified. Provide a .http file path.", HelpText);
        }

        if (!normalizedInputPath.EndsWith(".http", StringComparison.OrdinalIgnoreCase))
        {
            return ParseResult.Error("Input file must have a .http extension.", HelpText);
        }

        var options = new RunOptions(
            InputPath: normalizedInputPath,
            OutputPath: outputPath ?? overwriteOutputPath,
            OverwriteOutput: overwriteOutputPath is not null);

        return ParseResult.Success(options);
    }
}

internal sealed record OptionDefinition(string[] Aliases, bool RequiresValue)
{
    public static OptionDefinition Flag(params string[] aliases) => new(aliases, RequiresValue: false);

    public static OptionDefinition Value(params string[] aliases) => new(aliases, RequiresValue: true);

    public bool HasAlias(params string[] aliases) => aliases.Any(alias => Aliases.Contains(alias, StringComparer.Ordinal));
}

internal sealed class OptionRegistry
{
    private readonly IReadOnlyDictionary<string, OptionDefinition> _optionsByAlias;

    public OptionRegistry(IEnumerable<OptionDefinition> options)
    {
        var optionsByAlias = new Dictionary<string, OptionDefinition>(StringComparer.Ordinal);

        foreach (var option in options)
        {
            foreach (var alias in option.Aliases)
            {
                optionsByAlias[alias] = option;
            }
        }

        _optionsByAlias = new ReadOnlyDictionary<string, OptionDefinition>(optionsByAlias);
    }

    public bool TryGet(string token, out OptionDefinition option) => _optionsByAlias.TryGetValue(token, out option!);
}

internal sealed record RunOptions(string InputPath, string? OutputPath, bool OverwriteOutput);

internal enum ParseStatus
{
    Success,
    HelpRequested,
    VersionRequested,
    Error
}

internal sealed record ParseResult(ParseStatus Status, RunOptions? Options, string? Message)
{
    public static ParseResult Success(RunOptions options) => new(ParseStatus.Success, options, null);

    public static ParseResult Help(string helpText) => new(ParseStatus.HelpRequested, null, helpText);

    public static ParseResult Version(string message) => new(ParseStatus.VersionRequested, null, message);

    public static ParseResult Error(string error, string? helpText = null)
    {
        var message = helpText is null ? $"Error: {error}" : $"Error: {error}{Environment.NewLine}{Environment.NewLine}{helpText}";
        return new ParseResult(ParseStatus.Error, null, message);
    }
}

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
