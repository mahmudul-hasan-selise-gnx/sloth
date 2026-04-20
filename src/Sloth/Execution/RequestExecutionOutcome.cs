using System.Net;
using Sloth.HttpFileParsing;

namespace Sloth.Execution;

internal sealed record RequestExecutionOutcome(
    int RequestIndex,
    HttpRequestDefinition Request,
    HttpStatusCode StatusCode,
    IReadOnlyDictionary<string, IReadOnlyList<string>> Headers,
    string Body);
