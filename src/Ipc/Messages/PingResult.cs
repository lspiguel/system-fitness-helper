namespace SystemFitnessHelper.Ipc.Messages;

/// <summary>
/// Result of <c>sfh.ping</c>: enough to confirm the service is alive and reading the file the
/// caller expects it to read.
/// </summary>
public sealed class PingResult
{
    public bool Ok { get; init; }

    public string Version { get; init; } = string.Empty;

    public string ConfigPath { get; init; } = string.Empty;

    public bool ConfigExists { get; init; }
}
