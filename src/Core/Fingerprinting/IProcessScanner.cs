namespace SystemFitnessHelper.Fingerprinting;

public interface IProcessScanner
{
    IReadOnlyList<ProcessFingerprint> Scan();
}
