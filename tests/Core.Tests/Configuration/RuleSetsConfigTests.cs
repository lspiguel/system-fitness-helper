using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using SystemFitnessHelper.Configuration;
using Xunit;

namespace SystemFitnessHelper.Tests.Configuration;

public sealed class RuleSetsConfigTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    [Fact]
    public void Serialize_TwoEntryDictionary_RoundTripsCorrectly()
    {
        var config = new RuleSetsConfig
        {
            RuleSets = new Dictionary<string, RuleSet>(StringComparer.OrdinalIgnoreCase)
            {
                ["work"] = new RuleSet
                {
                    IsDefault = true,
                    Rules =
                    [
                        new Rule { Id = "r1", Enabled = true, Action = ActionType.Kill, Conditions = [] },
                    ],
                    Protected = ["svchost"],
                },
                ["gaming"] = new RuleSet
                {
                    IsDefault = false,
                    Rules = [],
                    Protected = [],
                },
            },
        };

        var json = JsonSerializer.Serialize(config, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<RuleSetsConfig>(json, JsonOptions);

        deserialized.Should().NotBeNull();
        deserialized!.RuleSets.Should().HaveCount(2);
        deserialized.RuleSets.Should().ContainKey("work");
        deserialized.RuleSets.Should().ContainKey("gaming");
        deserialized.RuleSets["work"].IsDefault.Should().BeTrue();
        deserialized.RuleSets["work"].Rules.Should().HaveCount(1);
        deserialized.RuleSets["work"].Rules[0].Id.Should().Be("r1");
        deserialized.RuleSets["work"].Protected.Should().ContainSingle("svchost");
        deserialized.RuleSets["gaming"].IsDefault.Should().BeFalse();
        deserialized.RuleSets["gaming"].Rules.Should().BeEmpty();
    }

    [Fact]
    public void Dictionary_KeyLookup_IsCaseInsensitive()
    {
        var config = new RuleSetsConfig
        {
            RuleSets = new Dictionary<string, RuleSet>(StringComparer.OrdinalIgnoreCase)
            {
                ["Gaming"] = new RuleSet { IsDefault = true, Rules = [], Protected = [] },
            },
        };

        config.RuleSets.Should().ContainKey("gaming");
        config.RuleSets.Should().ContainKey("GAMING");
        config.RuleSets.Should().ContainKey("Gaming");
    }

    [Fact]
    public void Deserialize_EmptyDictionary_DoesNotThrow()
    {
        var json = """{ "ruleSets": {} }""";

        var act = () => JsonSerializer.Deserialize<RuleSetsConfig>(json, JsonOptions);

        act.Should().NotThrow();
        var result = act();
        result.Should().NotBeNull();
        result!.RuleSets.Should().BeEmpty();
    }
}
