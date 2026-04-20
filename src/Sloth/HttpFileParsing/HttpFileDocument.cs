namespace Sloth.HttpFileParsing;

internal sealed record HttpFileDocument(IReadOnlyList<HttpRequestDefinition> Requests);
