using System.Text.Json;
using FluentAssertions;
using Moq;
using SystemFitnessHelper.Cli.Commands;
using SystemFitnessHelper.Configuration;
using SystemFitnessHelper.Fingerprinting;
using SystemFitnessHelper.Matching;
using Xunit;

namespace SystemFitnessHelper.Cli.Tests.Commands;

public sealed class ListCommandTests
{
    // -------------------------------------------------------------------------
    // console output (existing behaviour)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task HandleAsync_Console_NonExistentConfig_Returns2()
    {
        var scanner = new Mock<IProcessScanner>();
        scanner.Setup(s => s.Scan()).Returns([]);
        var matcher = new Mock<IRuleMatcher>();

        var result = await ListCommand.HandleAsync(
            @"C:\nonexistent\sfh-test\rules.json",
            "console",
            scanner.Object,
            matcher.Object);

        result.Should().Be(2);
        matcher.Verify(m => m.Match(It.IsAny<IReadOnlyList<ProcessFingerprint>>(),
                                    It.IsAny<RuleSet>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_Console_ValidConfig_NoMatches_Returns0()
    {
        var path = WriteTempConfig("""
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
                                   It.IsAny<RuleSet>()))
               .Returns([]);

        var result = await ListCommand.HandleAsync(path, "console", scanner.Object, matcher.Object);

        result.Should().Be(0);
    }

    // -------------------------------------------------------------------------
    // json output — general
    // -------------------------------------------------------------------------

    [Fact]
    public async Task HandleAsync_Json_NoConfigRequired_Returns0()
    {
        var scanner = new Mock<IProcessScanner>();
        scanner.Setup(s => s.Scan()).Returns([]);
        var matcher = new Mock<IRuleMatcher>();

        var result = await CaptureConsole(() =>
            ListCommand.HandleAsync(
                @"C:\nonexistent\sfh-test\rules.json",
                "json",
                scanner.Object,
                matcher.Object));

        result.ExitCode.Should().Be(0);
        matcher.Verify(m => m.Match(It.IsAny<IReadOnlyList<ProcessFingerprint>>(),
                                    It.IsAny<RuleSet>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_Json_EmptyFingerprints_OutputsEmptyRuleSet()
    {
        var scanner = new Mock<IProcessScanner>();
        scanner.Setup(s => s.Scan()).Returns([]);
        var matcher = new Mock<IRuleMatcher>();

        var (exitCode, json) = await CaptureConsole(() =>
            ListCommand.HandleAsync(null, "json", scanner.Object, matcher.Object));

        exitCode.Should().Be(0);
        var ruleSet = JsonSerializer.Deserialize<RuleSet>(json, JsonOptions);
        ruleSet.Should().NotBeNull();
        ruleSet!.Rules.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_Json_OutputIsValidJson()
    {
        var scanner = new Mock<IProcessScanner>();
        scanner.Setup(s => s.Scan()).Returns(
        [
            PlainProcess("notepad", @"C:\Windows\notepad.exe", "Microsoft", "explorer"),
        ]);
        var matcher = new Mock<IRuleMatcher>();

        var (_, json) = await CaptureConsole(() =>
            ListCommand.HandleAsync(null, "json", scanner.Object, matcher.Object));

        var act = () => JsonSerializer.Deserialize<RuleSet>(json, JsonOptions);
        act.Should().NotThrow();
    }

    // -------------------------------------------------------------------------
    // json output — plain process rules
    // -------------------------------------------------------------------------

    [Fact]
    public async Task HandleAsync_Json_PlainProcess_AllFieldsPresent_ProducesFourConditions()
    {
        var fp = PlainProcess("notepad", @"C:\Windows\notepad.exe", "Microsoft", "explorer");
        var scanner = new Mock<IProcessScanner>();
        scanner.Setup(s => s.Scan()).Returns([fp]);
        var matcher = new Mock<IRuleMatcher>();

        var (_, json) = await CaptureConsole(() =>
            ListCommand.HandleAsync(null, "json", scanner.Object, matcher.Object));

        var ruleSet = JsonSerializer.Deserialize<RuleSet>(json, JsonOptions)!;
        var rule = ruleSet.Rules.Single();

        rule.Enabled.Should().BeFalse();
        rule.ConditionLogic.Should().Be("And");
        rule.Action.Should().Be(ActionType.Kill);
        rule.Conditions.Should().HaveCount(4);
        rule.Conditions.Should().ContainSingle(c => c.Field == "ProcessName" && c.Value == "notepad");
        rule.Conditions.Should().ContainSingle(c => c.Field == "ExecutablePath" && c.Value == @"C:\Windows\notepad.exe");
        rule.Conditions.Should().ContainSingle(c => c.Field == "Publisher" && c.Value == "Microsoft");
        rule.Conditions.Should().ContainSingle(c => c.Field == "ParentProcessName" && c.Value == "explorer");
    }

    [Fact]
    public async Task HandleAsync_Json_PlainProcess_NullOptionalFields_OnlyProcessNameCondition()
    {
        var fp = PlainProcess("notepad", null, null, null);
        var scanner = new Mock<IProcessScanner>();
        scanner.Setup(s => s.Scan()).Returns([fp]);
        var matcher = new Mock<IRuleMatcher>();

        var (_, json) = await CaptureConsole(() =>
            ListCommand.HandleAsync(null, "json", scanner.Object, matcher.Object));

        var ruleSet = JsonSerializer.Deserialize<RuleSet>(json, JsonOptions)!;
        var rule = ruleSet.Rules.Single();

        rule.Conditions.Should().HaveCount(1);
        rule.Conditions.Single().Field.Should().Be("ProcessName");
    }

    [Fact]
    public async Task HandleAsync_Json_PlainProcess_AllConditionsUseEqOperator()
    {
        var fp = PlainProcess("notepad", @"C:\Windows\notepad.exe", "Microsoft", "explorer");
        var scanner = new Mock<IProcessScanner>();
        scanner.Setup(s => s.Scan()).Returns([fp]);
        var matcher = new Mock<IRuleMatcher>();

        var (_, json) = await CaptureConsole(() =>
            ListCommand.HandleAsync(null, "json", scanner.Object, matcher.Object));

        var ruleSet = JsonSerializer.Deserialize<RuleSet>(json, JsonOptions)!;
        ruleSet.Rules.Single().Conditions.Should().OnlyContain(c => c.Op == "eq");
    }

    // -------------------------------------------------------------------------
    // json output — service rules
    // -------------------------------------------------------------------------

    [Fact]
    public async Task HandleAsync_Json_Service_ProducesSingleServiceNameCondition()
    {
        var fp = ServiceFingerprint("SteamClientService", "Steam Client Service");
        var scanner = new Mock<IProcessScanner>();
        scanner.Setup(s => s.Scan()).Returns([fp]);
        var matcher = new Mock<IRuleMatcher>();

        var (_, json) = await CaptureConsole(() =>
            ListCommand.HandleAsync(null, "json", scanner.Object, matcher.Object));

        var ruleSet = JsonSerializer.Deserialize<RuleSet>(json, JsonOptions)!;
        var rule = ruleSet.Rules.Single();

        rule.Enabled.Should().BeFalse();
        rule.Action.Should().Be(ActionType.Stop);
        rule.Conditions.Should().HaveCount(1);
        rule.Conditions.Single().Field.Should().Be("ServiceName");
        rule.Conditions.Single().Op.Should().Be("eq");
        rule.Conditions.Single().Value.Should().Be("SteamClientService");
    }

    [Fact]
    public async Task HandleAsync_Json_Service_DescriptionUsesDisplayName()
    {
        var fp = ServiceFingerprint("SteamClientService", "Steam Client Service");
        var scanner = new Mock<IProcessScanner>();
        scanner.Setup(s => s.Scan()).Returns([fp]);
        var matcher = new Mock<IRuleMatcher>();

        var (_, json) = await CaptureConsole(() =>
            ListCommand.HandleAsync(null, "json", scanner.Object, matcher.Object));

        var ruleSet = JsonSerializer.Deserialize<RuleSet>(json, JsonOptions)!;
        ruleSet.Rules.Single().Description.Should().Be("Steam Client Service");
    }

    // -------------------------------------------------------------------------
    // json output — deduplication
    // -------------------------------------------------------------------------

    [Fact]
    public async Task HandleAsync_Json_DuplicateProcessFingerprints_ProducesOneRule()
    {
        var fp1 = PlainProcess("notepad", @"C:\Windows\notepad.exe", "Microsoft", "explorer");
        var fp2 = PlainProcess("notepad", @"C:\Windows\notepad.exe", "Microsoft", "explorer");
        var scanner = new Mock<IProcessScanner>();
        scanner.Setup(s => s.Scan()).Returns([fp1, fp2]);
        var matcher = new Mock<IRuleMatcher>();

        var (_, json) = await CaptureConsole(() =>
            ListCommand.HandleAsync(null, "json", scanner.Object, matcher.Object));

        var ruleSet = JsonSerializer.Deserialize<RuleSet>(json, JsonOptions)!;
        ruleSet.Rules.Should().HaveCount(1);
    }

    [Fact]
    public async Task HandleAsync_Json_ProcessesDifferingByPublisher_ProducesSeparateRules()
    {
        var fp1 = PlainProcess("host", @"C:\app\host.exe", "Vendor A", "svchost");
        var fp2 = PlainProcess("host", @"C:\app\host.exe", "Vendor B", "svchost");
        var scanner = new Mock<IProcessScanner>();
        scanner.Setup(s => s.Scan()).Returns([fp1, fp2]);
        var matcher = new Mock<IRuleMatcher>();

        var (_, json) = await CaptureConsole(() =>
            ListCommand.HandleAsync(null, "json", scanner.Object, matcher.Object));

        var ruleSet = JsonSerializer.Deserialize<RuleSet>(json, JsonOptions)!;
        ruleSet.Rules.Should().HaveCount(2);
    }

    [Fact]
    public async Task HandleAsync_Json_ServicesAreNotDeduplicated()
    {
        var fp1 = ServiceFingerprint("ServiceA", "Service A");
        var fp2 = ServiceFingerprint("ServiceB", "Service B");
        var scanner = new Mock<IProcessScanner>();
        scanner.Setup(s => s.Scan()).Returns([fp1, fp2]);
        var matcher = new Mock<IRuleMatcher>();

        var (_, json) = await CaptureConsole(() =>
            ListCommand.HandleAsync(null, "json", scanner.Object, matcher.Object));

        var ruleSet = JsonSerializer.Deserialize<RuleSet>(json, JsonOptions)!;
        ruleSet.Rules.Should().HaveCount(2);
    }

    // -------------------------------------------------------------------------
    // helpers
    // -------------------------------------------------------------------------

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
    };

    private static ProcessFingerprint PlainProcess(
        string name,
        string? executablePath,
        string? publisher,
        string? parentProcessName) =>
        new(
            ProcessId: 1234,
            ProcessName: name,
            ExecutablePath: executablePath,
            CommandLine: null,
            Publisher: publisher,
            WorkingSetBytes: 0,
            ParentProcessName: parentProcessName,
            IsService: false,
            ServiceName: null,
            ServiceDisplayName: null,
            ServiceStatus: null);

    private static ProcessFingerprint ServiceFingerprint(string serviceName, string displayName) =>
        new(
            ProcessId: 5678,
            ProcessName: "svchost",
            ExecutablePath: null,
            CommandLine: null,
            Publisher: null,
            WorkingSetBytes: 0,
            ParentProcessName: null,
            IsService: true,
            ServiceName: serviceName,
            ServiceDisplayName: displayName,
            ServiceStatus: null);

    private static async Task<(int ExitCode, string Output)> CaptureConsole(Func<Task<int>> action)
    {
        var writer = new StringWriter();
        var original = Console.Out;
        Console.SetOut(writer);
        try
        {
            var exitCode = await action();
            return (exitCode, writer.ToString().Trim());
        }
        finally
        {
            Console.SetOut(original);
        }
    }

    private static string WriteTempConfig(string json)
    {
        var path = Path.ChangeExtension(Path.GetTempFileName(), ".json");
        File.WriteAllText(path, json);
        return path;
    }
}
