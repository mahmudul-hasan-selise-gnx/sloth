using System.Text;
using System.Text.Json;

namespace Sloth.Output;

internal sealed class OutputFormatterRegistry : IOutputFormatterRegistry
{
    private readonly IReadOnlyDictionary<string, IOutputFormatter> _formattersByExtension;
    private readonly IOutputFormatter _defaultFormatter;

    public OutputFormatterRegistry(IEnumerable<KeyValuePair<string, IOutputFormatter>> formattersByExtension, IOutputFormatter defaultFormatter)
    {
        _formattersByExtension = new Dictionary<string, IOutputFormatter>(formattersByExtension, StringComparer.OrdinalIgnoreCase);
        _defaultFormatter = defaultFormatter;
    }

    public IOutputFormatter Resolve(string? outputPath)
    {
        var extension = Path.GetExtension(outputPath ?? string.Empty);
        if (!string.IsNullOrWhiteSpace(extension) && _formattersByExtension.TryGetValue(extension, out var formatter))
        {
            return formatter;
        }

        return _defaultFormatter;
    }
}

internal sealed class TextOutputFormatter : IOutputFormatter
{
    public string Format(OutputDocument document)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Input: {document.InputPath}");

        if (document.ParseErrors.Count > 0)
        {
            builder.AppendLine("Parse errors:");
            foreach (var parseError in document.ParseErrors)
            {
                builder.AppendLine($"  - {parseError}");
            }

            return builder.ToString();
        }

        builder.AppendLine($"Parsed requests: {document.ParsedRequestCount}");
        builder.AppendLine($"Executed: {document.ExecutedRequestCount} (Succeeded: {document.SucceededCount}, Failed: {document.FailedCount})");

        if (document.Outcomes.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("Successful requests:");
            foreach (var outcome in document.Outcomes)
            {
                builder.AppendLine($"  ✓ [{outcome.RequestIndex + 1}] {outcome.Method} {outcome.Url} -> {outcome.StatusCode}");
            }
        }

        if (document.Failures.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("Failed requests:");
            foreach (var failure in document.Failures)
            {
                builder.AppendLine($"  ✗ [{failure.RequestIndex + 1}] {failure.Method} {failure.Url} -> {failure.Error}");
            }
        }

        return builder.ToString();
    }
}

internal sealed class JsonOutputFormatter : IOutputFormatter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public string Format(OutputDocument document) => JsonSerializer.Serialize(document, JsonOptions);
}
