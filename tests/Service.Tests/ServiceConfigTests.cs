using FluentAssertions;
using Xunit;

namespace SystemFitnessHelper.Service.Tests;

/// <summary>
/// Regression tests for the configured rules path.
/// </summary>
/// <remarks>
/// appsettings.json used to carry the literal string <c>%ProgramData%\SystemFitnessHelper\rules.json</c>,
/// which the options binder stored verbatim. Because a Windows Service starts in
/// <c>C:\Windows\System32</c>, saving config from the UI created a directory literally named
/// <c>%ProgramData%</c> underneath it.
/// </remarks>
public sealed class ServiceConfigTests
{
    [Fact]
    public void Resolve_ExpandsEnvironmentVariables()
    {
        ServiceConfig config = new() { ConfigPath = @"%ProgramData%\SystemFitnessHelper\rules.json" };

        config.Resolve();

        config.ConfigPath.Should().NotContain("%");
        config.ConfigPath.Should().Be(
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "SystemFitnessHelper",
                "rules.json"));
    }

    [Fact]
    public void Resolve_MakesRelativePathsAbsolute()
    {
        ServiceConfig config = new() { ConfigPath = "rules.json" };

        config.Resolve();

        Path.IsPathRooted(config.ConfigPath).Should().BeTrue();
    }

    [Fact]
    public void Resolve_EmptyValue_FallsBackToTheDefault()
    {
        ServiceConfig config = new() { ConfigPath = "   " };

        config.Resolve();

        config.ConfigPath.Should().Be(ServiceConfig.DefaultConfigPath);
    }

    [Fact]
    public void Default_IsAnAbsoluteProgramDataPath()
    {
        ServiceConfig config = new();

        config.ConfigPath.Should().Be(ServiceConfig.DefaultConfigPath);
        Path.IsPathRooted(config.ConfigPath).Should().BeTrue();
        config.ConfigPath.Should().NotContain("%");
    }

    [Fact]
    public void Resolve_IsIdempotent()
    {
        ServiceConfig config = new() { ConfigPath = @"%ProgramData%\SystemFitnessHelper\rules.json" };

        config.Resolve();
        string once = config.ConfigPath;
        config.Resolve();

        config.ConfigPath.Should().Be(once);
    }
}
