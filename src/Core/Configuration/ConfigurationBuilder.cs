// <copyright file="ConfigurationBuilder.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using SystemFitnessHelper.Fingerprinting;

namespace SystemFitnessHelper.Configuration;

/// <summary>
/// Builds a <see cref="RuleSet"/> template from a snapshot of running processes and services,
/// producing one disabled <see cref="Rule"/> per fingerprint as a starting point for user configuration.
/// </summary>
public static class ConfigurationBuilder
{
    /// <summary>
    /// Converts a list of <see cref="ProcessFingerprint"/> entries into a <see cref="RuleSet"/>
    /// where each rule targets the corresponding process or service, is disabled by default,
    /// and has the appropriate action (<see cref="ActionType.Stop"/> for services,
    /// <see cref="ActionType.Kill"/> for plain processes).
    /// <para>
    /// For services, one rule is produced per fingerprint (ServiceName is already unique).
    /// For plain processes, fingerprints are deduplicated by the combination of
    /// ProcessName, ExecutablePath, Publisher, and ParentProcessName before building rules;
    /// each of those non-null fields is emitted as a separate <see cref="FingerprintCondition"/>
    /// joined with <c>ConditionLogic = "And"</c>.
    /// </para>
    /// </summary>
    /// <param name="fingerprints">List of process fingerprints, usually the result of a scan.</param>
    /// <returns>A <see cref="RuleSet"/> with one disabled rule per unique process/service fingerprint.</returns>
    public static RuleSet Build(IReadOnlyList<ProcessFingerprint> fingerprints)
    {
        var services = fingerprints.Where(fp => fp.IsService);
        var processes = fingerprints
            .Where(fp => !fp.IsService)
            .GroupBy(fp => (fp.ProcessName, fp.ExecutablePath, fp.Publisher, fp.ParentProcessName))
            .Select(g => g.First());

        var ordered = services.Cast<ProcessFingerprint>().Concat(processes);

        var rules = ordered
            .Select((fp, index) => new Rule
            {
                Id = $"rule-{index + 1:D4}",
                Description = fp.IsService
                    ? (fp.ServiceDisplayName ?? fp.ServiceName ?? fp.ProcessName)
                    : fp.ProcessName,
                Enabled = false,
                Conditions = BuildConditions(fp),
                ConditionLogic = "And",
                Action = fp.IsService ? ActionType.Stop : ActionType.Kill,
            })
            .ToList();

        return new RuleSet { Rules = rules };
    }

    private static List<FingerprintCondition> BuildConditions(ProcessFingerprint fp)
    {
        if (fp.IsService)
        {
            return
            [
                new FingerprintCondition
                {
                    Field = "ServiceName",
                    Op = "eq",
                    Value = fp.ServiceName ?? fp.ProcessName,
                },
            ];
        }

        var conditions = new List<FingerprintCondition>
        {
            new () { Field = "ProcessName", Op = "eq", Value = fp.ProcessName },
        };

        if (fp.ExecutablePath is not null)
        {
            conditions.Add(new FingerprintCondition { Field = "ExecutablePath", Op = "eq", Value = fp.ExecutablePath });
        }

        if (fp.Publisher is not null)
        {
            conditions.Add(new FingerprintCondition { Field = "Publisher", Op = "eq", Value = fp.Publisher });
        }

        if (fp.ParentProcessName is not null)
        {
            conditions.Add(new FingerprintCondition { Field = "ParentProcessName", Op = "eq", Value = fp.ParentProcessName });
        }

        return conditions;
    }
}
