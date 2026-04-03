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
/// <remarks>
/// The primary purpose of <see cref="SafetyGuard"/> is to veto actions that would
/// stop or manipulate critical system processes or services. Use <see cref="IsAllowed"/>
/// to determine whether an <see cref="ActionPlan"/> should proceed. The method returns
/// a tuple where <c>Allowed</c> is <c>true</c> when the action may proceed and <c>false</c>
/// when it must be blocked, with <c>Reason</c> explaining why.
/// </remarks>
public sealed class SafetyGuard
{
    private readonly IReadOnlySet<string> userProtectedServiceNames;

    /// <summary>
    /// Initializes a new instance of the <see cref="SafetyGuard"/> class with the specified set of user-protected service names.
    /// </summary>
    /// <param name="userProtectedServiceNames">
    /// A read-only set containing the names of services that are protected by the user.
    /// This collection may be empty but must not be <c>null</c>.
    /// </param>
    public SafetyGuard(IReadOnlySet<string> userProtectedServiceNames)
    {
        this.userProtectedServiceNames = userProtectedServiceNames;
    }

    /// <summary>
    /// Determines whether the provided <paramref name="plan"/> is allowed to run.
    /// </summary>
    /// <param name="plan">The <see cref="ActionPlan"/> describing the intended action and its target fingerprint.</param>
    /// <returns>
    /// A tuple where:
    /// - <c>Allowed</c> is <c>true</c> if the action may proceed; otherwise <c>false</c>.
    /// - <c>Reason</c> contains a human-readable explanation when the action is blocked; otherwise <c>null</c>.
    /// </returns>
    /// <remarks>
    /// The method performs checks in this order:
    /// 1. If the target is a service and has a service name, it checks against the hard-coded protected service names,
    ///    then against the user-provided protected service names. If matched, the action is blocked.
    /// 2. It checks the target process name against the hard-coded protected process names and blocks if matched.
    /// If no protection rule matches, the action is allowed.
    /// </remarks>
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
