namespace Sloth.Domain;

internal sealed record RunOptions(string InputPath, string? OutputPath, bool OverwriteOutput);
