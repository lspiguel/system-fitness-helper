// <copyright file="SampleConfigSchemaTests.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using FluentAssertions;
using SystemFitnessHelper.Configuration;
using Xunit;

namespace SystemFitnessHelper.Core.Tests.Configuration;

/// <summary>
/// Guards the shipped sample configuration against schema drift.
/// </summary>
/// <remarks>
/// <c>build.ps1</c> turns <c>docs/sample-config1.json</c> into the <c>rules.sample.json</c> that
/// both installers seed onto a new machine. It was left in the pre-0.C single-ruleset shape
/// (a top-level <c>rules</c> array), so every fresh installation seeded a file that deserialised
/// to a config with no rulesets at all. Nothing caught it, because the file is data rather than
/// code and seeding only happens when no configuration exists yet.
/// </remarks>
public sealed class SampleConfigSchemaTests
{
    [Theory]
    [InlineData("sample-config0c.json")]
    [InlineData("sample-config1.json")]
    public void SampleConfig_UsesTheMultiRuleSetSchema(string fileName)
    {
        string path = Path.Combine(RepositoryRoot(), "docs", fileName);
        File.Exists(path).Should().BeTrue($"{fileName} is shipped as a seed or documented sample");

        (RuleSetsConfig? config, ValidationResult validation) = ConfigurationLoader.Load(path);

        config.Should().NotBeNull();
        validation.IsValid.Should().BeTrue(
            "a sample that fails validation would be seeded onto a real machine: {0}",
            string.Join("; ", validation.Errors));

        config!.RuleSets.Should().NotBeEmpty(
            "a top-level 'rules' array deserialises to zero rulesets without any parse error");
    }

    [Theory]
    [InlineData("sample-config0c.json")]
    [InlineData("sample-config1.json")]
    public void SampleConfig_HasExactlyOneDefaultRuleSet(string fileName)
    {
        string path = Path.Combine(RepositoryRoot(), "docs", fileName);

        (RuleSetsConfig? config, _) = ConfigurationLoader.Load(path);

        config!.RuleSets.Values.Count(rs => rs.IsDefault).Should().Be(
            1,
            "resolving the default ruleset fails when none or several are marked");
    }

    [Fact]
    public void SeedSample_HasRulesToSeed()
    {
        string path = Path.Combine(RepositoryRoot(), "docs", "sample-config1.json");

        (RuleSetsConfig? config, _) = ConfigurationLoader.Load(path);

        config!.RuleSets.Values
            .SelectMany(rs => rs.Rules)
            .Should().NotBeEmpty("this file is what a new installation is seeded from");
    }

    /// <summary>
    /// Walks up from the test binaries until the repository root is found, so the test does not
    /// depend on the build output nesting depth.
    /// </summary>
    private static string RepositoryRoot()
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);

        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "docs")))
            dir = dir.Parent;

        dir.Should().NotBeNull("the tests must be able to locate the repository root");
        return dir!.FullName;
    }
}
