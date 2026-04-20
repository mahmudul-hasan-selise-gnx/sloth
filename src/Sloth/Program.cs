using System.Collections.ObjectModel;

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
        _runService = new RunService();
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
                if (token.StartsWith('-', StringComparison.Ordinal))
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
            if (value.StartsWith('-', StringComparison.Ordinal))
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
    public Task<int> RunAsync(RunOptions options, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        Console.WriteLine($"Running with '{options.InputPath}'.");

        if (!string.IsNullOrWhiteSpace(options.OutputPath))
        {
            var overwriteText = options.OverwriteOutput ? "overwrite enabled" : "overwrite disabled";
            Console.WriteLine($"Output: '{options.OutputPath}' ({overwriteText}).");
        }

        return Task.FromResult(0);
    }
}
