using FluentAssertions;
using Moq;
using SystemFitnessHelper.Cli.Commands;
using SystemFitnessHelper.Fingerprinting;
using SystemFitnessHelper.Matching;
using Xunit;

namespace SystemFitnessHelper.Cli.Tests.Commands;

public sealed class ListCommandTests
{
    [Fact]
    public async Task HandleAsync_NonExistentConfig_Returns2()
    {
        var scanner = new Mock<IProcessScanner>();
        var matcher = new Mock<IRuleMatcher>();

        var result = await ListCommand.HandleAsync(
            @"C:\nonexistent\sfh-test\rules.json",
            scanner.Object, matcher.Object);

        result.Should().Be(2);
        scanner.Verify(s => s.Scan(), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_ValidConfig_NoMatches_Returns0()
    {
        var path    = WriteTempConfig("""
            {
              "rules": [
                {
                  "id": "r1",
                  "enabled": true,
                  "conditions": [{ "field": "ProcessName", "op": "eq", "value": "nonexistent-process-xyz" }],
                  "action": "Kill"
                }
              ],
              "protected": []
            }
            """);
        var scanner = new Mock<IProcessScanner>();
        scanner.Setup(s => s.Scan()).Returns([]);
        var matcher = new Mock<IRuleMatcher>();
        matcher.Setup(m => m.Match(It.IsAny<IReadOnlyList<ProcessFingerprint>>(),
                                   It.IsAny<SystemFitnessHelper.Configuration.RuleSet>()))
               .Returns([]);

        var result = await ListCommand.HandleAsync(path, scanner.Object, matcher.Object);

        result.Should().Be(0);
    }

    private static string WriteTempConfig(string json)
    {
        var path = Path.ChangeExtension(Path.GetTempFileName(), ".json");
        File.WriteAllText(path, json);
        return path;
    }
}
