using SystemFitnessHelper.Actions;

namespace SystemFitnessHelper.Safety;

public sealed class SafetyGuard
{
    private readonly IReadOnlySet<string> _userProtectedServiceNames;

    public SafetyGuard()
        : this(new HashSet<string>(StringComparer.OrdinalIgnoreCase)) { }

    public SafetyGuard(IReadOnlySet<string> userProtectedServiceNames)
    {
        _userProtectedServiceNames = userProtectedServiceNames;
    }

    /// <summary>
    /// Returns (true, null) if the action is allowed, or (false, reason) if it must be blocked.
    /// </summary>
    public (bool Allowed, string? Reason) IsAllowed(ActionPlan plan)
    {
        var fp = plan.Fingerprint;

        if (fp.IsService && fp.ServiceName is not null)
        {
            if (ProtectedServices.HardCodedServiceNames.Contains(fp.ServiceName))
                return (false, $"'{fp.ServiceName}' is a hard-coded protected service.");

            if (_userProtectedServiceNames.Contains(fp.ServiceName))
                return (false, $"'{fp.ServiceName}' is in the user-defined protected list.");
        }

        if (ProtectedServices.HardCodedProcessNames.Contains(fp.ProcessName))
            return (false, $"'{fp.ProcessName}' is a protected Windows system process.");

        return (true, null);
    }
}
