using FluentAssertions;
using Moq;
using SystemFitnessHelper.Actions;
using SystemFitnessHelper.Configuration;
using SystemFitnessHelper.Fingerprinting;
using SystemFitnessHelper.Matching;
using SystemFitnessHelper.Services;
using Xunit;

namespace SystemFitnessHelper.Tests.Services;

public sealed class ExecuteServiceTests
{
    [Fact]
    public void Execute_NonExistentConfig_Returns2WithErrorMessage()
    {
        var scanner = new Mock<IProcessScanner>();
        var matcher = new Mock<IRuleMatcher>();
        var executor = new Mock<IActionExecutor>();
        var sut = new ExecuteService(scanner.Object, matcher.Object, executor.Object);

        var result = sut.Execute(@"C:\nonexistent\sfh-test\rules.json", null);

        result.ExitCode.Should().Be(2);
        result.ErrorMessage.Should().NotBeNullOrEmpty();
        result.Results.Should().BeEmpty();
        result.ResolvedRuleSetName.Should().BeNull();
    }

    [Fact]
    public void Execute_BlockedPlan_RecordsFailureWithoutCallingExecutor()
    {
        var path = WriteTempConfig();
        var fp = MakeProcessFp("svchost");   // hard-coded protected
        var rule = new Rule { Id = "r1", Enabled = true, Action = ActionType.Kill, Conditions = [] };
        var scanner = new Mock<IProcessScanner>();
        scanner.Setup(s => s.Scan()).Returns([fp]);
        var matcher = new Mock<IRuleMatcher>();
        matcher.Setup(m => m.Match(It.IsAny<IReadOnlyList<ProcessFingerprint>>(), It.IsAny<RuleSet>()))
               .Returns([new MatchResult(fp, rule)]);
        var executor = new Mock<IActionExecutor>();
        var sut = new ExecuteService(scanner.Object, matcher.Object, executor.Object);

        var result = sut.Execute(path, null);

        executor.Verify(e => e.Execute(It.IsAny<ActionPlan>()), Times.Never);
        result.Results[0].Success.Should().BeFalse();
        result.Results[0].Message.Should().StartWith("Blocked:");
        result.AnyFailed.Should().BeTrue();
        result.ExitCode.Should().Be(1);
    }

    [Fact]
    public void Execute_SuccessfulAction_Returns0WithResolvedName()
    {
        var path = WriteTempConfig();
        var fp = MakeProcessFp("notepad");
        var rule = new Rule { Id = "r1", Enabled = true, Action = ActionType.Kill, Conditions = [] };
        var scanner = new Mock<IProcessScanner>();
        scanner.Setup(s => s.Scan()).Returns([fp]);
        var matcher = new Mock<IRuleMatcher>();
        matcher.Setup(m => m.Match(It.IsAny<IReadOnlyList<ProcessFingerprint>>(), It.IsAny<RuleSet>()))
               .Returns([new MatchResult(fp, rule)]);
        var executor = new Mock<IActionExecutor>();
        executor.Setup(e => e.Execute(It.IsAny<ActionPlan>())).Returns(ActionResult.Ok("done"));
        var sut = new ExecuteService(scanner.Object, matcher.Object, executor.Object);

        var result = sut.Execute(path, null);

        result.ExitCode.Should().Be(0);
        result.AnyFailed.Should().BeFalse();
        result.Results[0].Success.Should().BeTrue();
        result.ResolvedRuleSetName.Should().Be("default");
        executor.Verify(e => e.Execute(It.IsAny<ActionPlan>()), Times.Once);
    }

    [Fact]
    public void Execute_FailedAction_Returns1WithAnyFailed()
    {
        var path = WriteTempConfig();
        var fp = MakeProcessFp("notepad");
        var rule = new Rule { Id = "r1", Enabled = true, Action = ActionType.Kill, Conditions = [] };
        var scanner = new Mock<IProcessScanner>();
        scanner.Setup(s => s.Scan()).Returns([fp]);
        var matcher = new Mock<IRuleMatcher>();
        matcher.Setup(m => m.Match(It.IsAny<IReadOnlyList<ProcessFingerprint>>(), It.IsAny<RuleSet>()))
               .Returns([new MatchResult(fp, rule)]);
        var executor = new Mock<IActionExecutor>();
        executor.Setup(e => e.Execute(It.IsAny<ActionPlan>())).Returns(ActionResult.Fail("process not found"));
        var sut = new ExecuteService(scanner.Object, matcher.Object, executor.Object);

        var result = sut.Execute(path, null);

        result.ExitCode.Should().Be(1);
        result.AnyFailed.Should().BeTrue();
        result.Results[0].Success.Should().BeFalse();
    }

    [Fact]
    public void Execute_UnknownRulesetName_Returns2WithError()
    {
        var path = WriteTempConfig();
        var scanner = new Mock<IProcessScanner>();
        var matcher = new Mock<IRuleMatcher>();
        var executor = new Mock<IActionExecutor>();
        var sut = new ExecuteService(scanner.Object, matcher.Object, executor.Object);

        var result = sut.Execute(path, "nonexistent");

        result.ExitCode.Should().Be(2);
        result.ErrorMessage.Should().Contain("nonexistent");
        result.ResolvedRuleSetName.Should().BeNull();
    }

    [Fact]
    public void Execute_NamedRulesetExists_PopulatesResolvedName()
    {
        var path = WriteTempTwoRulesetConfig();
        var scanner = new Mock<IProcessScanner>();
        scanner.Setup(s => s.Scan()).Returns([]);
        var matcher = new Mock<IRuleMatcher>();
        matcher.Setup(m => m.Match(It.IsAny<IReadOnlyList<ProcessFingerprint>>(), It.IsAny<RuleSet>()))
               .Returns([]);
        var executor = new Mock<IActionExecutor>();
        var sut = new ExecuteService(scanner.Object, matcher.Object, executor.Object);

        var result = sut.Execute(path, "gaming");

        result.ExitCode.Should().Be(0);
        result.ResolvedRuleSetName.Should().Be("gaming");
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
