// <copyright file="SafetyGuard.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using SystemFitnessHelper.Actions;

namespace SystemFitnessHelper.Safety;

/// <summary>
/// Guards against accidental or malicious targeting of protected processes and services.
/// Checks both the hard-coded lists in <see cref="ProtectedServices"/> and the
/// user-defined protected service names supplied at construction time.
/// </summary>
public sealed class SafetyGuard
{
    private readonly IReadOnlySet<string> userProtectedServiceNames;

    public SafetyGuard(IReadOnlySet<string> userProtectedServiceNames)
    {
        this.userProtectedServiceNames = userProtectedServiceNames;
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
            {
                return (false, $"'{fp.ServiceName}' is a hard-coded protected service.");
            }

            if (this.userProtectedServiceNames.Contains(fp.ServiceName))
            {
                return (false, $"'{fp.ServiceName}' is in the user-defined protected list.");
            }
        }

        if (ProtectedServices.HardCodedProcessNames.Contains(fp.ProcessName))
        {
            return (false, $"'{fp.ProcessName}' is a protected Windows system process.");
        }

        return (true, null);
    }
}
