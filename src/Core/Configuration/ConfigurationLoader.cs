using System.Text.Json;
using System.Text.Json.Serialization;

namespace SystemFitnessHelper.Configuration;

/// <summary>
/// Loads and validates a <see cref="RuleSet"/> from a JSON file.
/// Discovery order when no explicit path is given: <c>%APPDATA%\SystemFitnessHelper\rules.json</c>,
/// then <c>rules.json</c> next to the executable.
/// </summary>
public static class ConfigurationLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static (RuleSet? RuleSet, ValidationResult Validation) Load(string path)
    {
        var validation = new ValidationResult();

        if (!File.Exists(path))
        {
            validation.AddError($"Config file not found: {path}");
            return (null, validation);
        }

        RuleSet? ruleSet;
        try
        {
            var json = File.ReadAllText(path);
            ruleSet = JsonSerializer.Deserialize<RuleSet>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            validation.AddError($"Failed to parse config file: {ex.Message}");
            return (null, validation);
        }

        if (ruleSet is null)
        {
            validation.AddError("Config file is empty or null.");
            return (null, validation);
        }

        Validate(ruleSet, validation);
        return (ruleSet, validation);
    }

    public static string? DiscoverPath(string? explicitPath)
    {
        if (explicitPath is not null)
            return File.Exists(explicitPath) ? explicitPath : null;

        var appData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SystemFitnessHelper", "rules.json");
        if (File.Exists(appData))
            return appData;

        var nextToExe = Path.Combine(AppContext.BaseDirectory, "rules.json");
        if (File.Exists(nextToExe))
            return nextToExe;

        return null;
    }

    private static void Validate(RuleSet ruleSet, ValidationResult validation)
    {
        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var knownOps = new HashSet<string> { "eq", "neq", "regex", "gt", "lt" };

        foreach (var rule in ruleSet.Rules)
        {
            if (string.IsNullOrWhiteSpace(rule.Id))
            {
                validation.AddError("A rule is missing a required 'id' field.");
                continue;
            }

            if (!seenIds.Add(rule.Id))
                validation.AddError($"Duplicate rule ID: '{rule.Id}'.");

            if (rule.Conditions.Count == 0)
                validation.AddWarning($"Rule '{rule.Id}' has no conditions and will never match.");

            foreach (var condition in rule.Conditions)
            {
                if (string.IsNullOrWhiteSpace(condition.Field))
                    validation.AddError($"Rule '{rule.Id}': a condition is missing the 'field' property.");

                if (!knownOps.Contains(condition.Op.ToLowerInvariant()))
                    validation.AddWarning($"Rule '{rule.Id}': unknown operator '{condition.Op}'.");
            }

            if (rule.Action == ActionType.Kill)
            {
                var targetsServiceField = rule.Conditions.Any(c =>
                    c.Field.ToLowerInvariant() is "servicename" or "servicedisplayname");
                if (targetsServiceField)
                    validation.AddWarning(
                        $"Rule '{rule.Id}' uses Kill on a service-targeted rule. Use Stop instead for services.");
            }
        }
    }
}
