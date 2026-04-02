using FluentAssertions;
using Moq;
using System.ServiceProcess;
using SystemFitnessHelper.Actions;
using SystemFitnessHelper.Cli.Commands;
using SystemFitnessHelper.Configuration;
using SystemFitnessHelper.Fingerprinting;
using SystemFitnessHelper.Matching;
using SystemFitnessHelper.Safety;
using Xunit;

namespace SystemFitnessHelper.Cli.Tests.Commands;

public sealed class ActionsCommandTests
{
    [Fact]
    public async Task HandleAsync_NonExistentConfig_Returns2()
    {
        var scanner = new Mock<IProcessScanner>();
        var matcher = new Mock<IRuleMatcher>();

        var result = await ActionsCommand.HandleAsync(
            @"C:\nonexistent\sfh-test\rules.json",
            scanner.Object, matcher.Object);

        result.Should().Be(2);
    }

    [Fact]
    public async Task HandleAsync_ValidConfig_NoMatches_Returns0()
    {
        var path    = WriteTempConfig();
        var scanner = new Mock<IProcessScanner>();
        scanner.Setup(s => s.Scan()).Returns([]);
        var matcher = new Mock<IRuleMatcher>();
        matcher.Setup(m => m.Match(It.IsAny<IReadOnlyList<ProcessFingerprint>>(), It.IsAny<RuleSet>()))
               .Returns([]);

        var result = await ActionsCommand.HandleAsync(path, scanner.Object, matcher.Object);

        result.Should().Be(0);
    }

    [Fact]
    public async Task HandleAsync_WithGuardInjected_BlockedItemsDisplayed()
    {
        var path    = WriteTempConfig();
        var fp      = MakeProcessFp("notepad");
        var rule    = new Rule { Id = "r1", Enabled = true, Action = ActionType.Kill, Conditions = [] };
        var scanner = new Mock<IProcessScanner>();
        scanner.Setup(s => s.Scan()).Returns([fp]);
        var matcher = new Mock<IRuleMatcher>();
        matcher.Setup(m => m.Match(It.IsAny<IReadOnlyList<ProcessFingerprint>>(), It.IsAny<RuleSet>()))
               .Returns([new MatchResult(fp, rule)]);
        var guard = new SafetyGuard();   // hard-coded list only

        var result = await ActionsCommand.HandleAsync(path, scanner.Object, matcher.Object, guard);

        result.Should().Be(0);  // actions command never fails on blocked items, it just shows them
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
