namespace Sloth.Output;

internal interface IOutputFormatter
{
    string Format(OutputDocument document);
}

internal interface IOutputFormatterRegistry
{
    IOutputFormatter Resolve(string? outputPath);
}
