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
        result.RuleSet.Should().BeNull();
    }

    [Fact]
    public void GetConfig_ValidConfig_Returns0WithRules()
    {
        var path = WriteTempConfig("""
            {
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
            """);

        var result = sut.GetConfig(path);

        result.ExitCode.Should().Be(0);
        result.ErrorMessage.Should().BeNull();
        result.RuleSet.Should().NotBeNull();
        result.RuleSet!.Rules.Should().HaveCount(1);
        result.Validation.IsValid.Should().BeTrue();
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
    public void GetConfig_DuplicateIds_Returns2WithValidationErrors()
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
