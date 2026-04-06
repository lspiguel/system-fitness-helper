using FluentAssertions;
using Moq;
using SystemFitnessHelper.Configuration;
using SystemFitnessHelper.Fingerprinting;
using SystemFitnessHelper.Matching;
using SystemFitnessHelper.Services;
using Xunit;

namespace SystemFitnessHelper.Tests.Services;

public sealed class ActionsServiceTests
{
    [Fact]
    public void GetActions_NonExistentConfig_Returns2WithErrorMessage()
    {
        var scanner = new Mock<IProcessScanner>();
        var matcher = new Mock<IRuleMatcher>();
        var sut = new ActionsService(scanner.Object, matcher.Object);

        var result = sut.GetActions(@"C:\nonexistent\sfh-test\rules.json", null);

        result.ExitCode.Should().Be(2);
        result.ErrorMessage.Should().NotBeNullOrEmpty();
        result.Plans.Should().BeEmpty();
        result.ResolvedRuleSetName.Should().BeNull();
    }

    [Fact]
    public void GetActions_ValidConfig_NoMatches_Returns0WithEmptyPlans()
    {
        var path = WriteTempConfig();
        var scanner = new Mock<IProcessScanner>();
        scanner.Setup(s => s.Scan()).Returns([]);
        var matcher = new Mock<IRuleMatcher>();
        matcher.Setup(m => m.Match(It.IsAny<IReadOnlyList<ProcessFingerprint>>(), It.IsAny<RuleSet>()))
               .Returns([]);
        var sut = new ActionsService(scanner.Object, matcher.Object);

        var result = sut.GetActions(path, null);

        result.ExitCode.Should().Be(0);
        result.Plans.Should().BeEmpty();
        result.ResolvedRuleSetName.Should().Be("default");
    }

    [Fact]
    public void GetActions_AllowedPlan_IsNotBlocked()
    {
        var path = WriteTempConfig();
        var fp = MakeProcessFp("notepad");   // notepad is not hard-coded protected
        var rule = new Rule { Id = "r1", Enabled = true, Action = ActionType.Kill, Conditions = [] };
        var scanner = new Mock<IProcessScanner>();
        scanner.Setup(s => s.Scan()).Returns([fp]);
        var matcher = new Mock<IRuleMatcher>();
        matcher.Setup(m => m.Match(It.IsAny<IReadOnlyList<ProcessFingerprint>>(), It.IsAny<RuleSet>()))
               .Returns([new MatchResult(fp, rule)]);
        var sut = new ActionsService(scanner.Object, matcher.Object);

        var result = sut.GetActions(path, null);

        result.ExitCode.Should().Be(0);
        result.Plans.Should().HaveCount(1);
        result.Plans[0].Blocked.Should().BeFalse();
        result.Plans[0].BlockReason.Should().BeNull();
    }

    [Fact]
    public void GetActions_HardCodedProtectedProcess_IsBlocked()
    {
        var path = WriteTempConfig();
        var fp = MakeProcessFp("svchost");   // svchost is hard-coded protected
        var rule = new Rule { Id = "r1", Enabled = true, Action = ActionType.Kill, Conditions = [] };
        var scanner = new Mock<IProcessScanner>();
        scanner.Setup(s => s.Scan()).Returns([fp]);
        var matcher = new Mock<IRuleMatcher>();
        matcher.Setup(m => m.Match(It.IsAny<IReadOnlyList<ProcessFingerprint>>(), It.IsAny<RuleSet>()))
               .Returns([new MatchResult(fp, rule)]);
        var sut = new ActionsService(scanner.Object, matcher.Object);

        var result = sut.GetActions(path, null);

        result.ExitCode.Should().Be(0);   // actions never fails on blocked items
        result.Plans[0].Blocked.Should().BeTrue();
        result.Plans[0].BlockReason.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void GetActions_UserProtectedService_IsBlocked()
    {
        // Config declares "MyService" as protected in the default ruleset
        var path = WriteTempConfig("""
            {
              "ruleSets": {
                "default": {
                  "isDefault": true,
                  "rules": [{ "id": "r1", "enabled": true, "conditions": [], "action": "Stop" }],
                  "protected": ["MyService"]
                }
              }
            }
            """);
        var fp = MakeServiceFp("MyService");
        var rule = new Rule { Id = "r1", Enabled = true, Action = ActionType.Stop, Conditions = [] };
        var scanner = new Mock<IProcessScanner>();
        scanner.Setup(s => s.Scan()).Returns([fp]);
        var matcher = new Mock<IRuleMatcher>();
        matcher.Setup(m => m.Match(It.IsAny<IReadOnlyList<ProcessFingerprint>>(), It.IsAny<RuleSet>()))
               .Returns([new MatchResult(fp, rule)]);
        var sut = new ActionsService(scanner.Object, matcher.Object);

        var result = sut.GetActions(path, null);

        result.Plans[0].Blocked.Should().BeTrue();
        result.Plans[0].BlockReason.Should().Contain("user-defined");
    }

    [Fact]
    public void GetActions_UnknownRulesetName_Returns2WithError()
    {
        var path = WriteTempConfig();
        var scanner = new Mock<IProcessScanner>();
        var matcher = new Mock<IRuleMatcher>();
        var sut = new ActionsService(scanner.Object, matcher.Object);

        var result = sut.GetActions(path, "nonexistent");

        result.ExitCode.Should().Be(2);
        result.ErrorMessage.Should().Contain("nonexistent");
        result.ResolvedRuleSetName.Should().BeNull();
    }

    [Fact]
    public void GetActions_NamedRulesetExists_UsesNamedRulesetAndPopulatesResolvedName()
    {
        var path = WriteTempTwoRulesetConfig();
        var scanner = new Mock<IProcessScanner>();
        scanner.Setup(s => s.Scan()).Returns([]);
        var matcher = new Mock<IRuleMatcher>();
        matcher.Setup(m => m.Match(It.IsAny<IReadOnlyList<ProcessFingerprint>>(), It.IsAny<RuleSet>()))
               .Returns([]);
        var sut = new ActionsService(scanner.Object, matcher.Object);

        var result = sut.GetActions(path, "gaming");

        result.ExitCode.Should().Be(0);
        result.ResolvedRuleSetName.Should().Be("gaming");
    }

    private static ProcessFingerprint MakeProcessFp(string name) =>
        new(1234, name, null, null, null, 0, null, false, null, null, null);

    private static ProcessFingerprint MakeServiceFp(string serviceName) =>
        new(5678, "svchost", null, null, null, 0, null, true, serviceName, serviceName, null);

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
