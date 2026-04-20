using Sloth.Domain;

namespace Sloth.Output;

internal interface IOutputWriter : IAsyncDisposable
{
    Task WriteAsync(string content, CancellationToken cancellationToken);
}

internal interface IOutputWriterFactory
{
    IOutputWriter Create(RunOptions options);
}
