// <copyright file="ConfigurationLoader.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using System.Text.Json;
using System.Text.Json.Serialization;

namespace SystemFitnessHelper.Configuration;

/// <summary>
/// Loads and validates a <see cref="RuleSetsConfig"/> from a JSON file.
/// Discovery order when no explicit path is given: <c>%APPDATA%\SystemFitnessHelper\rules.json</c>,
/// then <c>rules.json</c> next to the executable.
/// </summary>
public static class ConfigurationLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new ()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static (RuleSetsConfig? Config, ValidationResult Validation) Load(string path)
    {
        var validation = new ValidationResult();

        if (!File.Exists(path))
        {
            validation.AddError($"Config file not found: {path}");
            return (null, validation);
        }

        RuleSetsConfig? config;
        try
        {
            var json = File.ReadAllText(path);
            config = JsonSerializer.Deserialize<RuleSetsConfig>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            validation.AddError($"Failed to parse config file: {ex.Message}");
            return (null, validation);
        }

        if (config is null)
        {
            validation.AddError("Config file is empty or null.");
            return (null, validation);
        }

        ValidateConfig(config, validation);
        return (config, validation);
    }

    public static (RuleSet? RuleSet, string? ResolvedName, string? ErrorMessage) ResolveRuleSet(
        RuleSetsConfig config, string? ruleSetName)
    {
        if (ruleSetName is not null)
        {
            if (!config.RuleSets.TryGetValue(ruleSetName, out var named))
            {
                var available = string.Join(", ", config.RuleSets.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase));
                return (null, null, $"Ruleset '{ruleSetName}' not found. Available: {available}.");
            }

            return (named, ruleSetName, null);
        }

        // Find the default entry using the original key name preserved in the dictionary
        foreach (var kvp in config.RuleSets)
        {
            if (kvp.Value.IsDefault)
            {
                return (kvp.Value, kvp.Key, null);
            }
        }

        return (null, null, "No default ruleset is defined.");
    }

    public static string? DiscoverPath(string? explicitPath)
    {
        if (explicitPath is not null)
        {
            return File.Exists(explicitPath) ? explicitPath : null;
        }

        var appData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SystemFitnessHelper",
            "rules.json");

        if (File.Exists(appData))
        {
            return appData;
        }

        var nextToExe = Path.Combine(AppContext.BaseDirectory, "rules.json");
        if (File.Exists(nextToExe))
        {
            return nextToExe;
        }

        return null;
    }

    private static void ValidateConfig(RuleSetsConfig config, ValidationResult validation)
    {
        if (config.RuleSets.Count == 0)
        {
            validation.AddError("No rulesets defined. The 'ruleSets' dictionary must contain at least one entry.");
            return;
        }

        var defaultCount = config.RuleSets.Values.Count(rs => rs.IsDefault);
        if (defaultCount == 0)
        {
            validation.AddError("No default ruleset is defined. Exactly one ruleset must have 'isDefault: true'.");
        }
        else if (defaultCount > 1)
        {
            validation.AddError($"Multiple default rulesets defined ({defaultCount}). Exactly one ruleset must have 'isDefault: true'.");
        }

        foreach (var kvp in config.RuleSets)
        {
            var name = kvp.Key;
            var ruleSet = kvp.Value;

            if (string.IsNullOrWhiteSpace(name))
            {
                validation.AddError("A ruleset has an empty or whitespace key name.");
                continue;
            }

            ValidateRuleSet(name, ruleSet, validation);
        }
    }

    private static void ValidateRuleSet(string ruleSetName, RuleSet ruleSet, ValidationResult validation)
    {
        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var knownOps = new HashSet<string> { "eq", "neq", "regex", "gt", "lt" };

        foreach (var rule in ruleSet.Rules)
        {
            if (string.IsNullOrWhiteSpace(rule.Id))
            {
                validation.AddError($"Ruleset '{ruleSetName}': a rule is missing a required 'id' field.");
                continue;
            }

            if (!seenIds.Add(rule.Id))
            {
                validation.AddError($"Ruleset '{ruleSetName}': Duplicate rule ID: '{rule.Id}'.");
            }

            if (rule.Conditions.Count == 0)
            {
                validation.AddWarning($"Ruleset '{ruleSetName}': rule '{rule.Id}' has no conditions and will never match.");
            }

            foreach (var condition in rule.Conditions)
            {
                if (string.IsNullOrWhiteSpace(condition.Field))
                {
                    validation.AddError($"Ruleset '{ruleSetName}': rule '{rule.Id}': a condition is missing the 'field' property.");
                }

                if (!knownOps.Contains(condition.Op.ToLowerInvariant()))
                {
                    validation.AddWarning($"Ruleset '{ruleSetName}': rule '{rule.Id}': unknown operator '{condition.Op}'.");
                }
            }

            if (rule.Action == ActionType.Kill)
            {
                var targetsServiceField = rule.Conditions.Any(c =>
                    c.Field.ToLowerInvariant() is "servicename" or "servicedisplayname");
                if (targetsServiceField)
                {
                    validation.AddWarning(
                        $"Ruleset '{ruleSetName}': rule '{rule.Id}' uses Kill on a service-targeted rule. Use Stop instead for services.");
                }
            }
        }
    }
}
