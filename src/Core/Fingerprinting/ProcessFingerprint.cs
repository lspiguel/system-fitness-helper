using System.ServiceProcess;

namespace SystemFitnessHelper.Fingerprinting;

/// <summary>
/// Immutable snapshot (record) of a running process captured by <see cref="IProcessScanner"/>.
/// Aggregates identity, path, command-line, publisher, resource usage, parent process,
/// and optional Windows Service metadata for use by the rule-matching pipeline.
/// </summary>
public sealed record ProcessFingerprint(
    int ProcessId,
    string ProcessName,
    string? ExecutablePath,
    string? CommandLine,
    string? Publisher,
    long WorkingSetBytes,
    string? ParentProcessName,
    bool IsService,
    string? ServiceName,
    string? ServiceDisplayName,
    ServiceControllerStatus? ServiceStatus);
