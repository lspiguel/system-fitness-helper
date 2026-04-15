// <copyright file="ExecuteService.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using Serilog;
using SystemFitnessHelper.Actions;
using SystemFitnessHelper.Configuration;
using SystemFitnessHelper.Fingerprinting;
using SystemFitnessHelper.Matching;
using SystemFitnessHelper.Safety;

namespace SystemFitnessHelper.Services;

/// <summary>
/// Default implementation of <see cref="IExecuteService"/>.
/// </summary>
public sealed class ExecuteService : IExecuteService
{
    private readonly IProcessScanner scanner;
    private readonly IRuleMatcher matcher;
    private readonly IActionExecutor executor;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExecuteService"/> class.
    /// </summary>
    /// <param name="scanner">The process scanner.</param>
    /// <param name="matcher">The rule matcher.</param>
    /// <param name="executor">The action executor.</param>
    public ExecuteService(IProcessScanner scanner, IRuleMatcher matcher, IActionExecutor executor)
    {
        this.scanner = scanner;
        this.matcher = matcher;
        this.executor = executor;
    }

    /// <inheritdoc/>
    public ExecuteResult Execute(string? configPath, string? ruleSetName)
    {
        var path = ConfigurationLoader.DiscoverPath(configPath);
        if (path is null)
        {
            if (configPath is not null)
            {
                return new ExecuteResult(
                    Results: [],
                    AnyFailed: false,
                    ResolvedRuleSetName: null,
                    ErrorMessage: $"Config file not found: {configPath}",
                    ExitCode: 2);
            }

            if (ruleSetName is not null)
            {
                return new ExecuteResult(
                    Results: [],
                    AnyFailed: false,
                    ResolvedRuleSetName: null,
                    ErrorMessage: $"No rules.json found. Cannot resolve ruleset '{ruleSetName}'.",
                    ExitCode: 2);
            }

            // No config file found during auto-discovery — treat as an uninitialized installation.
            return new ExecuteResult(
                Results: [],
                AnyFailed: false,
                ResolvedRuleSetName: null,
                ErrorMessage: null,
                ExitCode: 0);
        }

        var (config, validation) = ConfigurationLoader.Load(path);
        if (!validation.IsValid || config is null)
        {
            return new ExecuteResult(
                Results: [],
                AnyFailed: false,
                ResolvedRuleSetName: null,
                ErrorMessage: string.Join("; ", validation.Errors),
                ExitCode: 2);
        }

        var (ruleSet, resolvedName, resolveError) = ConfigurationLoader.ResolveRuleSet(config, ruleSetName);
        if (resolveError is not null || ruleSet is null)
        {
            return new ExecuteResult(
                Results: [],
                AnyFailed: false,
                ResolvedRuleSetName: null,
                ErrorMessage: resolveError,
                ExitCode: 2);
        }

        var guard = new SafetyGuard(
            new HashSet<string>(ruleSet.Protected, StringComparer.OrdinalIgnoreCase));

        var plans = this.matcher
            .Match(this.scanner.Scan(), ruleSet)
            .Select(m => new ActionPlan(m.Fingerprint, m.Rule.Action, m.Rule.Id))
            .ToList();

        var results = new List<ActionResultView>(plans.Count);
        foreach (var plan in plans)
        {
            var (allowed, reason) = guard.IsAllowed(plan);
            ActionResultView view;

            if (!allowed)
            {
                view = new ActionResultView(
                    ProcessName: plan.Fingerprint.ProcessName,
                    ProcessId: plan.Fingerprint.ProcessId,
                    ServiceName: plan.Fingerprint.ServiceName,
                    RuleId: plan.RuleId,
                    Action: plan.Action,
                    Success: false,
                    Message: $"Blocked: {reason}");
            }
            else
            {
                var result = this.executor.Execute(plan);
                Log.Information(
                    "Action {Action} on {Process} (rule: {Rule}): {Outcome}",
                    plan.Action,
                    plan.Fingerprint.ProcessName,
                    plan.RuleId,
                    result.Success ? "Success" : $"Failed - {result.Message}");
                view = new ActionResultView(
                    ProcessName: plan.Fingerprint.ProcessName,
                    ProcessId: plan.Fingerprint.ProcessId,
                    ServiceName: plan.Fingerprint.ServiceName,
                    RuleId: plan.RuleId,
                    Action: plan.Action,
                    Success: result.Success,
                    Message: result.Message);
            }

            results.Add(view);
        }

        var anyFailed = results.Any(r => !r.Success);
        return new ExecuteResult(
            Results: results,
            AnyFailed: anyFailed,
            ResolvedRuleSetName: resolvedName,
            ErrorMessage: null,
            ExitCode: anyFailed ? 1 : 0);
    }
}
