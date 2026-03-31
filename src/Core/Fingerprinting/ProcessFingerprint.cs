using System.ServiceProcess;

namespace SystemFitnessHelper.Fingerprinting;

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
