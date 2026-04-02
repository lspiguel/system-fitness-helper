namespace SystemFitnessHelper.Fingerprinting;

/// <summary>
/// Enumerates all running processes on the current machine and returns a
/// <see cref="ProcessFingerprint"/> snapshot for each one.
/// </summary>
public interface IProcessScanner
{
    IReadOnlyList<ProcessFingerprint> Scan();
}
