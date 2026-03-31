using FluentAssertions;
using SystemFitnessHelper.Cli.Commands;
using Xunit;

namespace SystemFitnessHelper.Cli.Tests.Commands;

public sealed class ConfigCommandTests
{
    [Fact]
    public async Task HandleAsync_NonExistentPath_Returns2()
    {
        var result = await ConfigCommand.HandleAsync(@"C:\nonexistent\sfh-test\rules.json");
        result.Should().Be(2);
    }

    [Fact]
    public async Task HandleAsync_ValidConfig_Returns0()
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

        var result = await ConfigCommand.HandleAsync(path);
        result.Should().Be(0);
    }

    [Fact]
    public async Task HandleAsync_InvalidJson_Returns2()
    {
        var path = WriteTempConfig("{ not valid json }");

        var result = await ConfigCommand.HandleAsync(path);
        result.Should().Be(2);
    }

    [Fact]
    public async Task HandleAsync_DuplicateIds_Returns2()
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

        var result = await ConfigCommand.HandleAsync(path);
        result.Should().Be(2);
    }

    private static string WriteTempConfig(string json)
    {
        var path = Path.ChangeExtension(Path.GetTempFileName(), ".json");
        File.WriteAllText(path, json);
        return path;
    }
}
