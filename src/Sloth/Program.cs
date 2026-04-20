using Sloth.Cli;
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
