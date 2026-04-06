using FluentAssertions;
using Moq;
using SystemFitnessHelper.Configuration;
using SystemFitnessHelper.Fingerprinting;
using SystemFitnessHelper.Matching;
using SystemFitnessHelper.Services;
using Xunit;

namespace SystemFitnessHelper.Tests.Services;

public sealed class ListServiceTests
{
    [Fact]
    public void GetProcessList_NonExistentConfig_Returns2WithErrorMessage()
    {
        var scanner = new Mock<IProcessScanner>();
        var matcher = new Mock<IRuleMatcher>();
        var sut = new ListService(scanner.Object, matcher.Object);

        var result = sut.GetProcessList(@"C:\nonexistent\sfh-test\rules.json", null);

        result.ExitCode.Should().Be(2);
        result.ErrorMessage.Should().NotBeNullOrEmpty();
        result.Fingerprints.Should().BeEmpty();
        result.Matches.Should().BeEmpty();
        result.ResolvedRuleSetName.Should().BeNull();
    }

    [Fact]
    public void GetProcessList_ValidConfig_NoMatches_Returns0WithResolvedName()
    {
        var path = WriteTempConfig();
        var scanner = new Mock<IProcessScanner>();
        scanner.Setup(s => s.Scan()).Returns([]);
        var matcher = new Mock<IRuleMatcher>();
        matcher.Setup(m => m.Match(It.IsAny<IReadOnlyList<ProcessFingerprint>>(), It.IsAny<RuleSet>()))
               .Returns([]);
        var sut = new ListService(scanner.Object, matcher.Object);

        var result = sut.GetProcessList(path, null);

        result.ExitCode.Should().Be(0);
        result.ErrorMessage.Should().BeNull();
        result.Fingerprints.Should().BeEmpty();
        result.Matches.Should().BeEmpty();
        result.ResolvedRuleSetName.Should().Be("default");
    }

    [Fact]
    public void GetProcessList_ValidConfig_WithMatches_Returns0WithMatches()
    {
        var path = WriteTempConfig();
        var fp = MakeProcessFp("notepad");
        var rule = new Rule { Id = "r1", Enabled = true, Action = ActionType.Kill, Conditions = [] };
        var scanner = new Mock<IProcessScanner>();
        scanner.Setup(s => s.Scan()).Returns([fp]);
        var matcher = new Mock<IRuleMatcher>();
        matcher.Setup(m => m.Match(It.IsAny<IReadOnlyList<ProcessFingerprint>>(), It.IsAny<RuleSet>()))
               .Returns([new MatchResult(fp, rule)]);
        var sut = new ListService(scanner.Object, matcher.Object);

        var result = sut.GetProcessList(path, null);

        result.ExitCode.Should().Be(0);
        result.Fingerprints.Should().HaveCount(1);
        result.Matches.Should().HaveCount(1);
    }

    [Fact]
    public void GetProcessList_UnknownRulesetName_Returns2WithError()
    {
        var path = WriteTempConfig();
        var scanner = new Mock<IProcessScanner>();
        var matcher = new Mock<IRuleMatcher>();
        var sut = new ListService(scanner.Object, matcher.Object);

        var result = sut.GetProcessList(path, "nonexistent");

        result.ExitCode.Should().Be(2);
        result.ErrorMessage.Should().Contain("nonexistent");
        result.ResolvedRuleSetName.Should().BeNull();
    }

    [Fact]
    public void GetProcessList_NamedRulesetExists_UsesNamedRuleset()
    {
        var path = WriteTempTwoRulesetConfig();
        var scanner = new Mock<IProcessScanner>();
        scanner.Setup(s => s.Scan()).Returns([]);
        var matcher = new Mock<IRuleMatcher>();
        matcher.Setup(m => m.Match(It.IsAny<IReadOnlyList<ProcessFingerprint>>(), It.IsAny<RuleSet>()))
               .Returns([]);
        var sut = new ListService(scanner.Object, matcher.Object);

        var result = sut.GetProcessList(path, "gaming");

        result.ExitCode.Should().Be(0);
        result.ResolvedRuleSetName.Should().Be("gaming");
    }

    [Fact]
    public void BuildTemplate_ProducesOneRulePerUniqueFingerprint()
    {
        var fp = MakeProcessFp("notepad");
        var scanner = new Mock<IProcessScanner>();
        scanner.Setup(s => s.Scan()).Returns([fp]);
        var matcher = new Mock<IRuleMatcher>();
        var sut = new ListService(scanner.Object, matcher.Object);

        var template = sut.BuildTemplate();

        template.Rules.Should().HaveCount(1);
        template.Rules[0].Enabled.Should().BeFalse();
    }

    private static ProcessFingerprint MakeProcessFp(string name) =>
        new(1234, name, null, null, null, 0, null, false, null, null, null);

    private static string WriteTempConfig() => WriteTempConfig("""
        {
          "ruleSets": {
            "default": {
              "isDefault": true,
              "rules": [{ "id": "r1", "enabled": true, "conditions": [], "action": "None" }],
              "protected": []
            }
          }
        }
        """);

    private static string WriteTempTwoRulesetConfig() => WriteTempConfig("""
        {
          "ruleSets": {
            "default": { "isDefault": true,  "rules": [], "protected": [] },
            "gaming":  { "isDefault": false, "rules": [], "protected": [] }
          }
        }
        """);

    private static string WriteTempConfig(string json)
    {
        var path = Path.ChangeExtension(Path.GetTempFileName(), ".json");
        File.WriteAllText(path, json);
        return path;
    }
}
