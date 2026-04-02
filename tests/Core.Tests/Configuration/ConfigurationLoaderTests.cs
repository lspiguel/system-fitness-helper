using FluentAssertions;
using SystemFitnessHelper.Configuration;
using Xunit;

namespace SystemFitnessHelper.Tests.Configuration;

public sealed class ConfigurationLoaderTests
{
    [Fact]
    public void Load_ValidConfig_ReturnsRuleSet()
    {
        var path = WriteTempConfig("""
            {
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
            }
            """);

        var (ruleSet, validation) = ConfigurationLoader.Load(path);

        validation.IsValid.Should().BeTrue();
        ruleSet.Should().NotBeNull();
        ruleSet!.Rules.Should().HaveCount(1);
        ruleSet.Rules[0].Id.Should().Be("rule-1");
        ruleSet.Rules[0].Action.Should().Be(ActionType.Kill);
        ruleSet.Protected.Should().ContainSingle().Which.Should().Be("wuauserv");
    }

    [Fact]
    public void Load_MissingFile_ReturnsError()
    {
        var (ruleSet, validation) = ConfigurationLoader.Load(@"C:\nonexistent\sfh-test\rules.json");

        validation.IsValid.Should().BeFalse();
        validation.Errors.Should().ContainSingle(e => e.Contains("not found"));
        ruleSet.Should().BeNull();
    }

    [Fact]
    public void Load_DuplicateRuleIds_ReturnsError()
    {
        var path = WriteTempConfig("""
            {
              "rules": [
                { "id": "dup", "enabled": true, "conditions": [], "action": "None" },
                { "id": "dup", "enabled": true, "conditions": [], "action": "None" }
              ],
              "protected": []
            }
            """);

        var (_, validation) = ConfigurationLoader.Load(path);

        validation.Errors.Should().ContainSingle(e => e.Contains("Duplicate"));
    }

    [Fact]
    public void Load_MissingId_ReturnsError()
    {
        var path = WriteTempConfig("""
            {
              "rules": [
                { "enabled": true, "conditions": [], "action": "None" }
              ],
              "protected": []
            }
            """);

        var (_, validation) = ConfigurationLoader.Load(path);

        validation.Errors.Should().ContainSingle(e => e.Contains("missing") || e.Contains("id"));
    }

    [Fact]
    public void Load_KillOnServiceDisplayNameField_ReturnsWarning()
    {
        var path = WriteTempConfig("""
            {
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
            """);

        var (_, validation) = ConfigurationLoader.Load(path);

        validation.IsValid.Should().BeTrue();
        validation.Warnings.Should().ContainSingle(w => w.Contains("Kill") || w.Contains("Stop"));
    }

    [Fact]
    public void Load_InvalidJson_ReturnsError()
    {
        var path = WriteTempConfig("{ invalid json }");

        var (_, validation) = ConfigurationLoader.Load(path);

        validation.IsValid.Should().BeFalse();
        validation.Errors.Should().ContainSingle(e => e.Contains("parse"));
    }

    [Fact]
    public void DiscoverPath_ExplicitPathExists_ReturnsPath()
    {
        var path = WriteTempConfig("""{ "rules": [], "protected": [] }""");

        ConfigurationLoader.DiscoverPath(path).Should().Be(path);
    }

    [Fact]
    public void DiscoverPath_ExplicitPathMissing_ReturnsNull()
    {
        ConfigurationLoader.DiscoverPath(@"C:\nonexistent\sfh-test\rules.json").Should().BeNull();
    }

    private static string WriteTempConfig(string json)
    {
        var path = Path.ChangeExtension(Path.GetTempFileName(), ".json");
        File.WriteAllText(path, json);
        return path;
    }
}
