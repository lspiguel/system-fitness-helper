using FluentAssertions;
using System.ServiceProcess;
using SystemFitnessHelper.Configuration;
using SystemFitnessHelper.Fingerprinting;
using SystemFitnessHelper.Matching;
using Xunit;

namespace SystemFitnessHelper.Tests.Matching;

public sealed class RuleMatcherTests
{
    private static readonly RuleMatcher Matcher = new();

    private static ProcessFingerprint MakeProcess(
        string name                  = "test",
        string? serviceName          = null,
        string? serviceDisplayName   = null,
        bool isService               = false,
        long workingSetBytes         = 0,
        string? parentProcessName    = null) =>
        new(ProcessId:          1234,
            ProcessName:        name,
            ExecutablePath:     null,
            CommandLine:        null,
            Publisher:          null,
            WorkingSetBytes:    workingSetBytes,
            ParentProcessName:  parentProcessName,
            IsService:          isService,
            ServiceName:        serviceName,
            ServiceDisplayName: serviceDisplayName,
            ServiceStatus:      isService ? ServiceControllerStatus.Running : null);

    [Theory]
    [InlineData("eq",  "notepad", "notepad", true)]
    [InlineData("eq",  "notepad", "NOTEPAD", true)]   // case-insensitive
    [InlineData("eq",  "notepad", "chrome",  false)]
    [InlineData("neq", "notepad", "chrome",  true)]
    [InlineData("neq", "notepad", "notepad", false)]
    public void EqNeq_MatchCorrectly(string op, string processName, string conditionValue, bool shouldMatch)
    {
        var fp   = MakeProcess(name: processName);
        var rule = MakeRule(new FingerprintCondition { Field = "ProcessName", Op = op, Value = conditionValue });

        Matcher.Match([fp], MakeRuleSet(rule)).Should().HaveCount(shouldMatch ? 1 : 0);
    }

    [Fact]
    public void Regex_MatchesPattern()
    {
        var fp   = MakeProcess(name: "SteamClientService");
        var rule = MakeRule(new FingerprintCondition { Field = "ProcessName", Op = "regex", Value = "^Steam.*" });

        Matcher.Match([fp], MakeRuleSet(rule)).Should().HaveCount(1);
    }

    [Fact]
    public void Regex_NoMatch()
    {
        var fp   = MakeProcess(name: "chrome");
        var rule = MakeRule(new FingerprintCondition { Field = "ProcessName", Op = "regex", Value = "^Steam.*" });

        Matcher.Match([fp], MakeRuleSet(rule)).Should().BeEmpty();
    }

    [Fact]
    public void Gt_MatchesWhenAboveThreshold()
    {
        var fp   = MakeProcess(workingSetBytes: 2_000_000_000L);
        var rule = MakeRule(new FingerprintCondition { Field = "WorkingSetBytes", Op = "gt", Value = "1073741824" });

        Matcher.Match([fp], MakeRuleSet(rule)).Should().HaveCount(1);
    }

    [Fact]
    public void Lt_MatchesWhenBelowThreshold()
    {
        var fp   = MakeProcess(workingSetBytes: 500_000L);
        var rule = MakeRule(new FingerprintCondition { Field = "WorkingSetBytes", Op = "lt", Value = "1073741824" });

        Matcher.Match([fp], MakeRuleSet(rule)).Should().HaveCount(1);
    }

    [Fact]
    public void AndLogic_AllConditionsMustMatch()
    {
        var fp = MakeProcess(name: "DiscordCrashHandler", parentProcessName: "explorer");
        var rule = new Rule
        {
            Id = "r1", Enabled = true, Action = ActionType.Kill, ConditionLogic = "And",
            Conditions =
            [
                new FingerprintCondition { Field = "ProcessName",       Op = "eq",  Value = "DiscordCrashHandler" },
                new FingerprintCondition { Field = "ParentProcessName", Op = "neq", Value = "Discord" },
            ],
        };

        Matcher.Match([fp], MakeRuleSet(rule)).Should().HaveCount(1);
    }

    [Fact]
    public void AndLogic_OneConditionFails_NoMatch()
    {
        var fp = MakeProcess(name: "DiscordCrashHandler", parentProcessName: "Discord");
        var rule = new Rule
        {
            Id = "r1", Enabled = true, Action = ActionType.Kill, ConditionLogic = "And",
            Conditions =
            [
                new FingerprintCondition { Field = "ProcessName",       Op = "eq",  Value = "DiscordCrashHandler" },
                new FingerprintCondition { Field = "ParentProcessName", Op = "neq", Value = "Discord" },
            ],
        };

        Matcher.Match([fp], MakeRuleSet(rule)).Should().BeEmpty();
    }

    [Fact]
    public void OrLogic_AnyConditionSuffices()
    {
        var fp = MakeProcess(name: "chrome");
        var rule = new Rule
        {
            Id = "r1", Enabled = true, Action = ActionType.Kill, ConditionLogic = "Or",
            Conditions =
            [
                new FingerprintCondition { Field = "ProcessName", Op = "eq", Value = "chrome" },
                new FingerprintCondition { Field = "ProcessName", Op = "eq", Value = "firefox" },
            ],
        };

        Matcher.Match([fp], MakeRuleSet(rule)).Should().HaveCount(1);
    }

    [Fact]
    public void DisabledRule_IsSkipped()
    {
        var fp   = MakeProcess(name: "notepad");
        var rule = new Rule
        {
            Id = "r1", Enabled = false, Action = ActionType.Kill,
            Conditions = [new FingerprintCondition { Field = "ProcessName", Op = "eq", Value = "notepad" }],
        };

        Matcher.Match([fp], MakeRuleSet(rule)).Should().BeEmpty();
    }

    [Fact]
    public void EmptyConditions_NeverMatch()
    {
        var fp   = MakeProcess(name: "anything");
        var rule = new Rule { Id = "r1", Enabled = true, Action = ActionType.Kill, Conditions = [] };

        Matcher.Match([fp], MakeRuleSet(rule)).Should().BeEmpty();
    }

    [Fact]
    public void ServiceDisplayName_MatchedCaseInsensitively()
    {
        var fp   = MakeProcess(serviceDisplayName: "Steam Client Service", isService: true, serviceName: "SteamClientService");
        var rule = MakeRule(new FingerprintCondition { Field = "ServiceDisplayName", Op = "eq", Value = "steam client service" });

        Matcher.Match([fp], MakeRuleSet(rule)).Should().HaveCount(1);
    }

    private static Rule MakeRule(FingerprintCondition condition) => new()
    {
        Id = "r1", Enabled = true, Action = ActionType.Kill, Conditions = [condition],
    };

    private static RuleSet MakeRuleSet(Rule rule) => new() { Rules = [rule] };
}
