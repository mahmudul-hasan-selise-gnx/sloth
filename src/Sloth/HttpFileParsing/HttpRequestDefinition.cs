namespace Sloth.HttpFileParsing;

internal sealed record HttpRequestDefinition(
    string Method,
    Uri Url,
    IReadOnlyDictionary<string, string> Headers,
    string? Body);
