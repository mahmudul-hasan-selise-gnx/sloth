using System.Text;

namespace Sloth.Output;

internal sealed class OutputWriterFactory : IOutputWriterFactory
{
    private readonly IOutputPathPolicy _outputPathPolicy;

    public OutputWriterFactory(IOutputPathPolicy outputPathPolicy)
    {
        _outputPathPolicy = outputPathPolicy;
    }

    public IOutputWriter Create(RunOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.OutputPath))
        {
            return new ConsoleOutputWriter();
        }

        var outputPath = _outputPathPolicy.ValidateAndPrepare(options.OutputPath);
        return new FileOutputWriter(outputPath, options.OverwriteOutput);
    }
}

internal sealed class ConsoleOutputWriter : IOutputWriter
{
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    public Task WriteAsync(string content, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Console.WriteLine(content);
        return Task.CompletedTask;
    }
}

internal sealed class FileOutputWriter : IOutputWriter
{
    private readonly StreamWriter _writer;

    public FileOutputWriter(string outputPath, bool overwrite)
    {
        var fileMode = overwrite ? FileMode.Create : FileMode.Append;
        var stream = new FileStream(outputPath, fileMode, FileAccess.Write, FileShare.Read);
        _writer = new StreamWriter(stream, Encoding.UTF8);
    }

    public async Task WriteAsync(string content, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await _writer.WriteLineAsync(content);
        await _writer.FlushAsync(cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _writer.DisposeAsync();
    }
}

internal interface IOutputPathPolicy
{
    string ValidateAndPrepare(string outputPath);
}

internal sealed class OutputPathPolicy : IOutputPathPolicy
{
    public string ValidateAndPrepare(string outputPath)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            throw new InvalidOperationException("Output path is required when output mode is enabled.");
        }

        var fullPath = Path.GetFullPath(outputPath);
        var directoryPath = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directoryPath))
        {
            if (File.Exists(directoryPath))
            {
                throw new InvalidOperationException($"Output directory '{directoryPath}' points to an existing file.");
            }

            Directory.CreateDirectory(directoryPath);
        }

        return fullPath;
    }
}
