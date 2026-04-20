namespace Sloth.HttpFileParsing;

internal sealed record HttpParseError(int LineNumber, string Message)
{
    public override string ToString() => $"Line {LineNumber}: {Message}";
}
