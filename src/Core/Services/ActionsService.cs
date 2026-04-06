// <copyright file="ActionsService.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using SystemFitnessHelper.Actions;
using SystemFitnessHelper.Configuration;
using SystemFitnessHelper.Fingerprinting;
using SystemFitnessHelper.Matching;
using SystemFitnessHelper.Safety;

namespace SystemFitnessHelper.Services;

/// <summary>
/// Default implementation of <see cref="IActionsService"/>.
/// </summary>
public sealed class ActionsService : IActionsService
{
    private readonly IProcessScanner scanner;
    private readonly IRuleMatcher matcher;

    /// <summary>
    /// Initializes a new instance of the <see cref="ActionsService"/> class.
    /// </summary>
    /// <param name="scanner">The process scanner.</param>
    /// <param name="matcher">The rule matcher.</param>
    public ActionsService(IProcessScanner scanner, IRuleMatcher matcher)
    {
        this.scanner = scanner;
        this.matcher = matcher;
    }

    /// <inheritdoc/>
    public ActionsResult GetActions(string? configPath, string? ruleSetName)
    {
        var path = ConfigurationLoader.DiscoverPath(configPath);
        if (path is null)
        {
            return new ActionsResult(
                Plans: [],
                ResolvedRuleSetName: null,
                ErrorMessage: "No rules.json found. Use --config to specify a path.",
                ExitCode: 2);
        }

        var (config, validation) = ConfigurationLoader.Load(path);
        if (!validation.IsValid || config is null)
        {
            return new ActionsResult(
                Plans: [],
                ResolvedRuleSetName: null,
                ErrorMessage: string.Join("; ", validation.Errors),
                ExitCode: 2);
        }

        var (ruleSet, resolvedName, resolveError) = ConfigurationLoader.ResolveRuleSet(config, ruleSetName);
        if (resolveError is not null || ruleSet is null)
        {
            return new ActionsResult(
                Plans: [],
                ResolvedRuleSetName: null,
                ErrorMessage: resolveError,
                ExitCode: 2);
        }

        var guard = new SafetyGuard(
            new HashSet<string>(ruleSet.Protected, StringComparer.OrdinalIgnoreCase));

        var plans = this.matcher
            .Match(this.scanner.Scan(), ruleSet)
            .Select(m =>
            {
                var plan = new ActionPlan(m.Fingerprint, m.Rule.Action, m.Rule.Id);
                var (allowed, reason) = guard.IsAllowed(plan);
                return new ActionPlanView(
                    ProcessName: m.Fingerprint.ProcessName,
                    ProcessId: m.Fingerprint.ProcessId,
                    ServiceName: m.Fingerprint.ServiceName,
                    RuleId: m.Rule.Id,
                    Action: m.Rule.Action,
                    Blocked: !allowed,
                    BlockReason: reason);
            })
            .ToList();

        return new ActionsResult(Plans: plans, ResolvedRuleSetName: resolvedName, ErrorMessage: null, ExitCode: 0);
    }
}
