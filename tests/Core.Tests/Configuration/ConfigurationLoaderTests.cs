using FluentAssertions;
using SystemFitnessHelper.Configuration;
using Xunit;

namespace SystemFitnessHelper.Tests.Configuration;

public sealed class ConfigurationLoaderTests
{
    // -------------------------------------------------------------------------
    // Load — basic
    // -------------------------------------------------------------------------

    [Fact]
    public void Load_ValidTwoRulesetConfig_ReturnsConfigWithBothRulesets()
    {
        var path = WriteTempConfig("""
            {
              "ruleSets": {
                "work": {
                  "isDefault": true,
                  "rules": [
                    {
                      "id": "rule-1",
                      "description": "Test rule",
                      "enabled": true,
                      "conditions": [
                        { "field": "ProcessName", "op": "eq", "value": "notepad" }
                      ],
                      "action": "Kill"
                    }
                  ],
                  "protected": ["wuauserv"]
                },
                "gaming": {
                  "isDefault": false,
                  "rules": [],
                  "protected": []
                }
              }
            }
            """);

        var (config, validation) = ConfigurationLoader.Load(path);

        validation.IsValid.Should().BeTrue();
        config.Should().NotBeNull();
        config!.RuleSets.Should().HaveCount(2);
        config.RuleSets.Should().ContainKey("work");
        config.RuleSets.Should().ContainKey("gaming");
        config.RuleSets["work"].Rules.Should().HaveCount(1);
        config.RuleSets["work"].Rules[0].Id.Should().Be("rule-1");
        config.RuleSets["work"].Rules[0].Action.Should().Be(ActionType.Kill);
        config.RuleSets["work"].Protected.Should().ContainSingle().Which.Should().Be("wuauserv");
        config.RuleSets["work"].IsDefault.Should().BeTrue();
        config.RuleSets["gaming"].IsDefault.Should().BeFalse();
    }

    [Fact]
    public void Load_MissingFile_ReturnsError()
    {
        var (config, validation) = ConfigurationLoader.Load(@"C:\nonexistent\sfh-test\rules.json");

        validation.IsValid.Should().BeFalse();
        validation.Errors.Should().ContainSingle(e => e.Contains("not found"));
        config.Should().BeNull();
    }

    [Fact]
    public void Load_InvalidJson_ReturnsError()
    {
        var path = WriteTempConfig("{ invalid json }");

        var (_, validation) = ConfigurationLoader.Load(path);

        validation.IsValid.Should().BeFalse();
        validation.Errors.Should().ContainSingle(e => e.Contains("parse"));
    }

    // -------------------------------------------------------------------------
    // Load — multi-ruleset validation
    // -------------------------------------------------------------------------

    [Fact]
    public void Load_NoRulesets_ReturnsError()
    {
        var path = WriteTempConfig("""{ "ruleSets": {} }""");

        var (_, validation) = ConfigurationLoader.Load(path);

        validation.IsValid.Should().BeFalse();
        validation.Errors.Should().ContainSingle(e => e.Contains("No rulesets"));
    }

    [Fact]
    public void Load_NoDefaultRuleset_ReturnsError()
    {
        var path = WriteTempConfig("""
            {
              "ruleSets": {
                "work": { "isDefault": false, "rules": [], "protected": [] }
              }
            }
            """);

        var (_, validation) = ConfigurationLoader.Load(path);

        validation.IsValid.Should().BeFalse();
        validation.Errors.Should().ContainSingle(e => e.Contains("No default ruleset"));
    }

    [Fact]
    public void Load_MultipleDefaultRulesets_ReturnsError()
    {
        var path = WriteTempConfig("""
            {
              "ruleSets": {
                "work":   { "isDefault": true, "rules": [], "protected": [] },
                "gaming": { "isDefault": true, "rules": [], "protected": [] }
              }
            }
            """);

        var (_, validation) = ConfigurationLoader.Load(path);

        validation.IsValid.Should().BeFalse();
        validation.Errors.Should().ContainSingle(e => e.Contains("Multiple default"));
    }

    [Fact]
    public void Load_DuplicateRuleIdsWithinRuleset_ReturnsErrorPrefixedWithRulesetName()
    {
        var path = WriteTempConfig("""
            {
              "ruleSets": {
                "work": {
                  "isDefault": true,
                  "rules": [
                    { "id": "dup", "enabled": true, "conditions": [], "action": "None" },
                    { "id": "dup", "enabled": true, "conditions": [], "action": "None" }
                  ],
                  "protected": []
                }
              }
            }
            """);

        var (_, validation) = ConfigurationLoader.Load(path);

        validation.Errors.Should().ContainSingle(e => e.Contains("Duplicate") && e.Contains("work"));
    }

    [Fact]
    public void Load_MissingIdInRuleset_ReturnsErrorPrefixedWithRulesetName()
    {
        var path = WriteTempConfig("""
            {
              "ruleSets": {
                "work": {
                  "isDefault": true,
                  "rules": [
                    { "enabled": true, "conditions": [], "action": "None" }
                  ],
                  "protected": []
                }
              }
            }
            """);

        var (_, validation) = ConfigurationLoader.Load(path);

        validation.Errors.Should().ContainSingle(e => (e.Contains("missing") || e.Contains("id")) && e.Contains("work"));
    }

    [Fact]
    public void Load_KillOnServiceDisplayNameField_ReturnsWarningPrefixedWithRulesetName()
    {
        var path = WriteTempConfig("""
            {
              "ruleSets": {
                "work": {
                  "isDefault": true,
                  "rules": [
                    {
                      "id": "svc-kill",
                      "enabled": true,
                      "conditions": [{ "field": "ServiceDisplayName", "op": "eq", "value": "Steam Client Service" }],
                      "action": "Kill"
                    }
                  ],
                  "protected": []
                }
              }
            }
            """);

        var (_, validation) = ConfigurationLoader.Load(path);

        validation.IsValid.Should().BeTrue();
        validation.Warnings.Should().ContainSingle(w =>
            (w.Contains("Kill") || w.Contains("Stop")) && w.Contains("work"));
    }

    // -------------------------------------------------------------------------
    // ResolveRuleSet
    // -------------------------------------------------------------------------

    [Fact]
    public void ResolveRuleSet_NullName_ReturnsDefaultRuleset()
    {
        var config = MakeTwoRulesetConfig(defaultName: "work");

        var (ruleSet, resolvedName, error) = ConfigurationLoader.ResolveRuleSet(config, null);

        error.Should().BeNull();
        resolvedName.Should().Be("work");
        ruleSet.Should().NotBeNull();
        ruleSet!.IsDefault.Should().BeTrue();
    }

    [Fact]
    public void ResolveRuleSet_ExistingName_ReturnsNamedRuleset()
    {
        var config = MakeTwoRulesetConfig(defaultName: "work");

        var (ruleSet, resolvedName, error) = ConfigurationLoader.ResolveRuleSet(config, "gaming");

        error.Should().BeNull();
        resolvedName.Should().Be("gaming");
        ruleSet.Should().NotBeNull();
        ruleSet!.IsDefault.Should().BeFalse();
    }

    [Fact]
    public void ResolveRuleSet_ExistingNameCaseInsensitive_ReturnsRuleset()
    {
        var config = MakeTwoRulesetConfig(defaultName: "work");

        var (ruleSet, resolvedName, error) = ConfigurationLoader.ResolveRuleSet(config, "GAMING");

        error.Should().BeNull();
        ruleSet.Should().NotBeNull();
        resolvedName.Should().NotBeNull();
    }

    [Fact]
    public void ResolveRuleSet_UnknownName_ReturnsErrorWithAvailableList()
    {
        var config = MakeTwoRulesetConfig(defaultName: "work");

        var (ruleSet, resolvedName, error) = ConfigurationLoader.ResolveRuleSet(config, "nonexistent");

        ruleSet.Should().BeNull();
        resolvedName.Should().BeNull();
        error.Should().NotBeNullOrEmpty();
        error.Should().Contain("nonexistent");
        error.Should().Contain("work").And.Contain("gaming");
    }

    // -------------------------------------------------------------------------
    // DiscoverPath
    // -------------------------------------------------------------------------

    [Fact]
    public void DiscoverPath_ExplicitPathExists_ReturnsPath()
    {
        var path = WriteTempConfig("""{ "ruleSets": { "default": { "isDefault": true, "rules": [], "protected": [] } } }""");

        ConfigurationLoader.DiscoverPath(path).Should().Be(path);
    }

    [Fact]
    public void DiscoverPath_ExplicitPathMissing_ReturnsNull()
    {
        ConfigurationLoader.DiscoverPath(@"C:\nonexistent\sfh-test\rules.json").Should().BeNull();
    }

    // -------------------------------------------------------------------------
    // helpers
    // -------------------------------------------------------------------------

    private static RuleSetsConfig MakeTwoRulesetConfig(string defaultName)
    {
        return new RuleSetsConfig
        {
            RuleSets = new Dictionary<string, RuleSet>(StringComparer.OrdinalIgnoreCase)
            {
                [defaultName] = new RuleSet { IsDefault = true, Rules = [], Protected = [] },
                ["gaming"] = new RuleSet { IsDefault = false, Rules = [], Protected = [] },
            },
        };
    }

    private static string WriteTempConfig(string json)
    {
        var path = Path.ChangeExtension(Path.GetTempFileName(), ".json");
        File.WriteAllText(path, json);
        return path;
    }
}
