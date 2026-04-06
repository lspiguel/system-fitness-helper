using FluentAssertions;
using SystemFitnessHelper.Services;
using Xunit;

namespace SystemFitnessHelper.Tests.Services;

public sealed class ConfigServiceTests
{
    private readonly ConfigService sut = new();

    [Fact]
    public void GetConfig_NonExistentPath_Returns2WithErrorMessage()
    {
        var result = sut.GetConfig(@"C:\nonexistent\sfh-test\rules.json");

        result.ExitCode.Should().Be(2);
        result.ErrorMessage.Should().NotBeNullOrEmpty();
        result.Config.Should().BeNull();
        result.AvailableRuleSetNames.Should().BeEmpty();
    }

    [Fact]
    public void GetConfig_ValidConfig_Returns0WithRuleSetsConfig()
    {
        var path = WriteTempConfig("""
            {
              "ruleSets": {
                "work": {
                  "isDefault": true,
                  "rules": [
                    {
                      "id": "r1",
                      "enabled": true,
                      "conditions": [{ "field": "ProcessName", "op": "eq", "value": "notepad" }],
                      "action": "Kill"
                    }
                  ],
                  "protected": []
                }
              }
            }
            """);

        var result = sut.GetConfig(path);

        result.ExitCode.Should().Be(0);
        result.ErrorMessage.Should().BeNull();
        result.Config.Should().NotBeNull();
        result.Config!.RuleSets.Should().ContainKey("work");
        result.Config.RuleSets["work"].Rules.Should().HaveCount(1);
        result.AvailableRuleSetNames.Should().ContainSingle("work");
        result.Validation.IsValid.Should().BeTrue();
    }

    [Fact]
    public void GetConfig_MultipleRulesets_AvailableRuleSetNamesSortedAlphabetically()
    {
        var path = WriteTempConfig("""
            {
              "ruleSets": {
                "zebra": { "isDefault": false, "rules": [], "protected": [] },
                "alpha": { "isDefault": true,  "rules": [], "protected": [] }
              }
            }
            """);

        var result = sut.GetConfig(path);

        result.ExitCode.Should().Be(0);
        result.AvailableRuleSetNames.Should().Equal("alpha", "zebra");
    }

    [Fact]
    public void GetConfig_InvalidJson_Returns2WithValidationErrors()
    {
        var path = WriteTempConfig("{ not valid json }");

        var result = sut.GetConfig(path);

        result.ExitCode.Should().Be(2);
        result.Validation.IsValid.Should().BeFalse();
        result.Validation.Errors.Should().NotBeEmpty();
    }

    [Fact]
    public void GetConfig_NoDefaultRuleset_Returns2WithValidationErrors()
    {
        var path = WriteTempConfig("""
            {
              "ruleSets": {
                "work": { "isDefault": false, "rules": [], "protected": [] }
              }
            }
            """);

        var result = sut.GetConfig(path);

        result.ExitCode.Should().Be(2);
        result.Validation.IsValid.Should().BeFalse();
    }

    private static string WriteTempConfig(string json)
    {
        var path = Path.ChangeExtension(Path.GetTempFileName(), ".json");
        File.WriteAllText(path, json);
        return path;
    }
}
