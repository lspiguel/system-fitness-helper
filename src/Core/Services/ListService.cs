// <copyright file="ListService.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using SystemFitnessHelper.Configuration;
using SystemFitnessHelper.Fingerprinting;
using SystemFitnessHelper.Matching;

namespace SystemFitnessHelper.Services;

/// <summary>
/// Default implementation of <see cref="IListService"/>.
/// </summary>
public sealed class ListService : IListService
{
    private readonly IProcessScanner scanner;
    private readonly IRuleMatcher matcher;

    /// <summary>
    /// Initializes a new instance of the <see cref="ListService"/> class.
    /// </summary>
    /// <param name="scanner">The process scanner.</param>
    /// <param name="matcher">The rule matcher.</param>
    public ListService(IProcessScanner scanner, IRuleMatcher matcher)
    {
        this.scanner = scanner;
        this.matcher = matcher;
    }

    /// <inheritdoc/>
    public ProcessListResult GetProcessList(string? configPath, string? ruleSetName)
    {
        var path = ConfigurationLoader.DiscoverPath(configPath);
        if (path is null)
        {
            return new ProcessListResult(
                Fingerprints: [],
                Matches: [],
                ResolvedRuleSetName: null,
                ErrorMessage: "No rules.json found. Use --config to specify a path.",
                ExitCode: 2);
        }

        var (config, validation) = ConfigurationLoader.Load(path);
        if (!validation.IsValid || config is null)
        {
            return new ProcessListResult(
                Fingerprints: [],
                Matches: [],
                ResolvedRuleSetName: null,
                ErrorMessage: string.Join("; ", validation.Errors),
                ExitCode: 2);
        }

        var (ruleSet, resolvedName, resolveError) = ConfigurationLoader.ResolveRuleSet(config, ruleSetName);
        if (resolveError is not null || ruleSet is null)
        {
            return new ProcessListResult(
                Fingerprints: [],
                Matches: [],
                ResolvedRuleSetName: null,
                ErrorMessage: resolveError,
                ExitCode: 2);
        }

        var fingerprints = this.scanner.Scan();
        var matches = this.matcher.Match(fingerprints, ruleSet);
        return new ProcessListResult(
            Fingerprints: fingerprints,
            Matches: matches,
            ResolvedRuleSetName: resolvedName,
            ErrorMessage: null,
            ExitCode: 0);
    }

    /// <inheritdoc/>
    public RuleSet BuildTemplate()
    {
        var fingerprints = this.scanner.Scan();
        return ConfigurationBuilder.Build(fingerprints);
    }
}
