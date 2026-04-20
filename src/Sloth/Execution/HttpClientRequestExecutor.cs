using System.Net.Http.Headers;
using Sloth.HttpFileParsing;

namespace Sloth.Execution;

internal sealed class HttpClientRequestExecutor : IRequestExecutor
{
    private readonly HttpClient _httpClient;

    public HttpClientRequestExecutor(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _httpClient.Timeout = Timeout.InfiniteTimeSpan;
    }

    public async Task<ExecutionResult> ExecuteAsync(
        HttpFileDocument document,
        RequestExecutionOptions options,
        CancellationToken cancellationToken)
    {
        var outcomes = new List<RequestExecutionOutcome>(document.Requests.Count);
        var failures = new List<RequestExecutionFailure>();

        for (var index = 0; index < document.Requests.Count; index++)
        {
            var requestDefinition = document.Requests[index];

            try
            {
                using var request = ToHttpRequestMessage(requestDefinition);
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(options.RequestTimeout);

                using var response = await _httpClient.SendAsync(request, timeoutCts.Token);
                var responseBody = await response.Content.ReadAsStringAsync(timeoutCts.Token);

                outcomes.Add(new RequestExecutionOutcome(
                    RequestIndex: index,
                    Request: requestDefinition,
                    StatusCode: response.StatusCode,
                    Headers: MergeHeaders(response.Headers, response.Content.Headers),
                    Body: responseBody));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                failures.Add(new RequestExecutionFailure(
                    RequestIndex: index,
                    Request: requestDefinition,
                    Error: "Execution cancelled.",
                    IsTimeout: false,
                    IsCanceled: true));

                break;
            }
            catch (OperationCanceledException)
            {
                failures.Add(new RequestExecutionFailure(
                    RequestIndex: index,
                    Request: requestDefinition,
                    Error: $"Request timed out after {options.RequestTimeout.TotalSeconds:0.###}s.",
                    IsTimeout: true,
                    IsCanceled: false));
            }
            catch (Exception ex)
            {
                failures.Add(new RequestExecutionFailure(
                    RequestIndex: index,
                    Request: requestDefinition,
                    Error: ex.Message,
                    IsTimeout: false,
                    IsCanceled: false));
            }
        }

        return new ExecutionResult(outcomes, failures);
    }

    private static HttpRequestMessage ToHttpRequestMessage(HttpRequestDefinition definition)
    {
        var message = new HttpRequestMessage(new HttpMethod(definition.Method), definition.Url);

        if (!string.IsNullOrEmpty(definition.Body))
        {
            message.Content = new StringContent(definition.Body);
        }

        foreach (var (name, value) in definition.Headers)
        {
            if (message.Headers.TryAddWithoutValidation(name, value))
            {
                continue;
            }

            message.Content ??= new ByteArrayContent([]);
            if (!message.Content.Headers.TryAddWithoutValidation(name, value))
            {
                throw new InvalidOperationException($"Unable to map header '{name}' to an HTTP request message.");
            }
        }

        return message;
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<string>> MergeHeaders(
        HttpResponseHeaders responseHeaders,
        HttpContentHeaders contentHeaders)
    {
        var allHeaders = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var (name, values) in responseHeaders)
        {
            allHeaders[name] = values.ToArray();
        }

        foreach (var (name, values) in contentHeaders)
        {
            allHeaders[name] = values.ToArray();
        }

        return allHeaders;
    }
}
