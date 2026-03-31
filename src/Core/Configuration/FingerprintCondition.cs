using System.Text.RegularExpressions;
using SystemFitnessHelper.Fingerprinting;

namespace SystemFitnessHelper.Configuration;

public sealed class FingerprintCondition
{
    public string Field { get; init; } = string.Empty;
    public string Op { get; init; } = "eq";
    public string Value { get; init; } = string.Empty;

    public bool Evaluate(ProcessFingerprint fp)
    {
        var fieldValue = GetFieldValue(fp);
        return Op.ToLowerInvariant() switch
        {
            "eq"    => string.Equals(fieldValue, Value, StringComparison.OrdinalIgnoreCase),
            "neq"   => !string.Equals(fieldValue, Value, StringComparison.OrdinalIgnoreCase),
            "regex" => fieldValue != null && Regex.IsMatch(fieldValue, Value, RegexOptions.IgnoreCase),
            "gt"    => CompareNumeric(fieldValue, Value) > 0,
            "lt"    => CompareNumeric(fieldValue, Value) < 0,
            _       => false,
        };
    }

    private string? GetFieldValue(ProcessFingerprint fp) =>
        Field.ToLowerInvariant() switch
        {
            "processname"        => fp.ProcessName,
            "executablepath"     => fp.ExecutablePath,
            "commandline"        => fp.CommandLine,
            "publisher"          => fp.Publisher,
            "parentprocessname"  => fp.ParentProcessName,
            "servicename"        => fp.ServiceName,
            "servicedisplayname" => fp.ServiceDisplayName,
            "workingsetbytes"    => fp.WorkingSetBytes.ToString(),
            "servicestatus"      => fp.ServiceStatus?.ToString(),
            "isservice"          => fp.IsService.ToString(),
            _                    => null,
        };

    private static int CompareNumeric(string? fieldValue, string conditionValue)
    {
        if (!long.TryParse(fieldValue, out var fv) || !long.TryParse(conditionValue, out var cv))
            return 0;
        return fv.CompareTo(cv);
    }
}
