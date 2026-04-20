namespace Sloth.HttpFileParsing;

internal interface IHttpFileParser
{
    HttpFileParseResult ParseFile(string path);

    HttpFileParseResult ParseContent(string content);
}

internal sealed class HttpFileParser : IHttpFileParser
{
    private static readonly HashSet<string> ValidMethods =
    [
        "GET", "POST", "PUT", "PATCH", "DELETE", "HEAD", "OPTIONS", "TRACE", "CONNECT"
    ];

    public HttpFileParseResult ParseFile(string path)
    {
        var errors = new List<HttpParseError>();

        if (string.IsNullOrWhiteSpace(path))
        {
            errors.Add(new HttpParseError(0, "Input path is required."));
            return HttpFileParseResult.Failure(errors);
        }

        if (!path.EndsWith(".http", StringComparison.OrdinalIgnoreCase))
        {
            errors.Add(new HttpParseError(0, "Input file must use the .http extension."));
            return HttpFileParseResult.Failure(errors);
        }

        if (!File.Exists(path))
        {
            errors.Add(new HttpParseError(0, $"Input file '{path}' does not exist."));
            return HttpFileParseResult.Failure(errors);
        }

        var content = File.ReadAllText(path);
        return ParseContent(content);
    }

    public HttpFileParseResult ParseContent(string content)
    {
        var errors = new List<HttpParseError>();
        var requests = new List<HttpRequestDefinition>();
        var lines = SplitLines(content);

        var segmentStart = 1;
        for (var index = 0; index <= lines.Count; index++)
        {
            var isLastLine = index == lines.Count;
            var isSeparator = !isLastLine && IsSeparatorLine(lines[index]);
            if (!isLastLine && !isSeparator)
            {
                continue;
            }

            var segmentLength = index - (segmentStart - 1);
            if (segmentLength > 0)
            {
                ParseRequestSegment(lines, segmentStart, segmentLength, requests, errors);
            }

            segmentStart = index + 2;
        }

        if (requests.Count == 0 && errors.Count == 0)
        {
            errors.Add(new HttpParseError(1, "No request definitions found."));
        }

        return errors.Count == 0
            ? HttpFileParseResult.Success(new HttpFileDocument(requests))
            : HttpFileParseResult.Failure(errors);
    }

    private static void ParseRequestSegment(
        IReadOnlyList<string> lines,
        int segmentStart,
        int segmentLength,
        ICollection<HttpRequestDefinition> requests,
        ICollection<HttpParseError> errors)
    {
        var startIndex = segmentStart - 1;
        var endIndexExclusive = startIndex + segmentLength;

        var requestLineIndex = -1;
        for (var index = startIndex; index < endIndexExclusive; index++)
        {
            if (!string.IsNullOrWhiteSpace(lines[index]) && !IsCommentLine(lines[index]))
            {
                requestLineIndex = index;
                break;
            }
        }

        if (requestLineIndex == -1)
        {
            return;
        }

        var requestLine = lines[requestLineIndex].Trim();
        var parts = requestLine.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
        {
            errors.Add(new HttpParseError(requestLineIndex + 1, "Request line must be in format 'METHOD URL'."));
            return;
        }

        var method = parts[0].Trim().ToUpperInvariant();
        if (!ValidMethods.Contains(method))
        {
            errors.Add(new HttpParseError(requestLineIndex + 1, $"Unsupported HTTP method '{parts[0]}'."));
            return;
        }

        var urlToken = parts[1].Trim();
        if (!Uri.TryCreate(urlToken, UriKind.Absolute, out var uri))
        {
            errors.Add(new HttpParseError(requestLineIndex + 1, $"Invalid absolute URL '{urlToken}'."));
            return;
        }

        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var bodyLines = new List<string>();
        var inBody = false;

        for (var index = requestLineIndex + 1; index < endIndexExclusive; index++)
        {
            var line = lines[index];

            if (!inBody)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    inBody = true;
                    continue;
                }

                if (IsCommentLine(line))
                {
                    continue;
                }

                var separatorIndex = line.IndexOf(':');
                if (separatorIndex <= 0)
                {
                    errors.Add(new HttpParseError(index + 1, "Header line must be in format 'Name: Value'."));
                    return;
                }

                var name = line[..separatorIndex].Trim();
                var value = line[(separatorIndex + 1)..].Trim();
                if (name.Length == 0)
                {
                    errors.Add(new HttpParseError(index + 1, "Header name cannot be empty."));
                    return;
                }

                headers[name] = value;
                continue;
            }

            bodyLines.Add(line);
        }

        var body = bodyLines.Count > 0 ? string.Join(Environment.NewLine, bodyLines) : null;
        requests.Add(new HttpRequestDefinition(method, uri, headers, body));
    }

    private static bool IsCommentLine(string line)
    {
        var trimmed = line.TrimStart();
        return trimmed.StartsWith('#') && !IsSeparatorLine(trimmed);
    }

    private static bool IsSeparatorLine(string line)
    {
        return line.TrimStart().StartsWith("###", StringComparison.Ordinal);
    }

    private static IReadOnlyList<string> SplitLines(string content)
    {
        if (string.IsNullOrEmpty(content))
        {
            return [];
        }

        var normalized = content.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        return normalized.Split('\n');
    }
}
