namespace Sloth.HttpFileParsing;

internal sealed record HttpFileParseResult(HttpFileDocument? Document, IReadOnlyList<HttpParseError> Errors)
{
    public bool IsSuccess => Errors.Count == 0 && Document is not null;

    public static HttpFileParseResult Success(HttpFileDocument document) => new(document, []);

    public static HttpFileParseResult Failure(IReadOnlyList<HttpParseError> errors) => new(null, errors);
}
