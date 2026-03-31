using FluentAssertions;
using Moq;
using SystemFitnessHelper.Actions;
using SystemFitnessHelper.Cli.Commands;
using SystemFitnessHelper.Configuration;
using SystemFitnessHelper.Fingerprinting;
using SystemFitnessHelper.Matching;
using SystemFitnessHelper.Safety;
using Xunit;

namespace SystemFitnessHelper.Cli.Tests.Commands;

public sealed class ExecuteCommandTests
{
    [Fact]
    public async Task HandleAsync_NonExistentConfig_Returns2()
    {
        var result = await ExecuteCommand.HandleAsync(
            @"C:\nonexistent\sfh-test\rules.json",
            skipPrompt: true,
            new Mock<IProcessScanner>().Object,
            new Mock<IRuleMatcher>().Object,
            new Mock<IActionExecutor>().Object);

        result.Should().Be(2);
    }

    [Fact]
    public async Task HandleAsync_NoMatches_Returns0()
    {
        var path    = WriteTempConfig();
        var scanner = new Mock<IProcessScanner>();
        scanner.Setup(s => s.Scan()).Returns([]);
        var matcher  = new Mock<IRuleMatcher>();
        matcher.Setup(m => m.Match(It.IsAny<IReadOnlyList<ProcessFingerprint>>(), It.IsAny<RuleSet>()))
               .Returns([]);
        var executor = new Mock<IActionExecutor>();

        var result = await ExecuteCommand.HandleAsync(
            path, skipPrompt: true, scanner.Object, matcher.Object, executor.Object);

        result.Should().Be(0);
        executor.Verify(e => e.Execute(It.IsAny<ActionPlan>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_SuccessfulAction_Returns0()
    {
        var path     = WriteTempConfig();
        var fp       = MakeProcessFp("notepad");
        var rule     = new Rule { Id = "r1", Enabled = true, Action = ActionType.Kill, Conditions = [] };
        var scanner  = new Mock<IProcessScanner>();
        scanner.Setup(s => s.Scan()).Returns([fp]);
        var matcher  = new Mock<IRuleMatcher>();
        matcher.Setup(m => m.Match(It.IsAny<IReadOnlyList<ProcessFingerprint>>(), It.IsAny<RuleSet>()))
               .Returns([new MatchResult(fp, rule)]);
        var executor = new Mock<IActionExecutor>();
        executor.Setup(e => e.Execute(It.IsAny<ActionPlan>())).Returns(ActionResult.Ok("done"));
        var guard    = new SafetyGuard();   // notepad is not protected

        var result = await ExecuteCommand.HandleAsync(
            path, skipPrompt: true, scanner.Object, matcher.Object, executor.Object, guard);

        result.Should().Be(0);
        executor.Verify(e => e.Execute(It.IsAny<ActionPlan>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_FailedAction_Returns1()
    {
        var path     = WriteTempConfig();
        var fp       = MakeProcessFp("notepad");
        var rule     = new Rule { Id = "r1", Enabled = true, Action = ActionType.Kill, Conditions = [] };
        var scanner  = new Mock<IProcessScanner>();
        scanner.Setup(s => s.Scan()).Returns([fp]);
        var matcher  = new Mock<IRuleMatcher>();
        matcher.Setup(m => m.Match(It.IsAny<IReadOnlyList<ProcessFingerprint>>(), It.IsAny<RuleSet>()))
               .Returns([new MatchResult(fp, rule)]);
        var executor = new Mock<IActionExecutor>();
        executor.Setup(e => e.Execute(It.IsAny<ActionPlan>())).Returns(ActionResult.Fail("process not found"));
        var guard    = new SafetyGuard();

        var result = await ExecuteCommand.HandleAsync(
            path, skipPrompt: true, scanner.Object, matcher.Object, executor.Object, guard);

        result.Should().Be(1);
    }

    [Fact]
    public async Task HandleAsync_BlockedByGuard_NotExecuted()
    {
        var path     = WriteTempConfig();
        var fp       = MakeProcessFp("svchost");   // svchost is hard-coded protected
        var rule     = new Rule { Id = "r1", Enabled = true, Action = ActionType.Kill, Conditions = [] };
        var scanner  = new Mock<IProcessScanner>();
        scanner.Setup(s => s.Scan()).Returns([fp]);
        var matcher  = new Mock<IRuleMatcher>();
        matcher.Setup(m => m.Match(It.IsAny<IReadOnlyList<ProcessFingerprint>>(), It.IsAny<RuleSet>()))
               .Returns([new MatchResult(fp, rule)]);
        var executor = new Mock<IActionExecutor>();
        var guard    = new SafetyGuard();

        await ExecuteCommand.HandleAsync(
            path, skipPrompt: true, scanner.Object, matcher.Object, executor.Object, guard);

        executor.Verify(e => e.Execute(It.IsAny<ActionPlan>()), Times.Never);
    }

    private static ProcessFingerprint MakeProcessFp(string name) =>
        new(1234, name, null, null, null, 0, null, false, null, null, null);

    private static string WriteTempConfig() => WriteTempConfig("""
        { "rules": [{ "id": "r1", "enabled": true, "conditions": [], "action": "None" }], "protected": [] }
        """);

    private static string WriteTempConfig(string json)
    {
        var path = Path.ChangeExtension(Path.GetTempFileName(), ".json");
        File.WriteAllText(path, json);
        return path;
    }
}
