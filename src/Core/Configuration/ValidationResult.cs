namespace SystemFitnessHelper.Configuration;

public sealed class ValidationResult
{
    private readonly List<string> _errors = [];
    private readonly List<string> _warnings = [];

    public IReadOnlyList<string> Errors => _errors;
    public IReadOnlyList<string> Warnings => _warnings;
    public bool IsValid => _errors.Count == 0;

    internal void AddError(string message) => _errors.Add(message);
    internal void AddWarning(string message) => _warnings.Add(message);
}
