using FluentAssertions;
using SystemFitnessHelper.Installer;
using Xunit;

namespace SystemFitnessHelper.Installer.Tests;

public sealed class InstallerOptionsTests
{
    [Theory]
    [InlineData("install", InstallerCommand.Install)]
    [InlineData("INSTALL", InstallerCommand.Install)]
    [InlineData("start", InstallerCommand.Start)]
    [InlineData("stop", InstallerCommand.Stop)]
    [InlineData("uninstall", InstallerCommand.Uninstall)]
    [InlineData("status", InstallerCommand.Status)]
    [InlineData("--help", InstallerCommand.Help)]
    public void Parse_RecognisesCommands(string verb, InstallerCommand expected) =>
        InstallerOptions.Parse([verb]).Command.Should().Be(expected);

    [Fact]
    public void Parse_NoArguments_ShowsHelp() =>
        InstallerOptions.Parse([]).Command.Should().Be(InstallerCommand.Help);

    [Fact]
    public void Parse_UnknownVerb_IsUnknown()
    {
        InstallerOptions options = InstallerOptions.Parse(["frobnicate"]);

        options.Command.Should().Be(InstallerCommand.Unknown);
        options.RawCommand.Should().Be("frobnicate");
    }

    [Fact]
    public void Parse_PrefixWithSpaces_IsCapturedWhole()
    {
        InstallerOptions options = InstallerOptions.Parse(
            ["install", "--prefix", @"C:\Program Files\Custom Location"]);

        options.Prefix.Should().Be(@"C:\Program Files\Custom Location");
        options.ParseError.Should().BeNull();
    }

    [Fact]
    public void Parse_PrefixWithoutValue_ReportsAnError() =>
        InstallerOptions.Parse(["install", "--prefix"]).ParseError.Should().NotBeNull();

    [Fact]
    public void Parse_ServiceNameOverride_IsCaptured() =>
        InstallerOptions.Parse(["install", "--service-name", "SfhTest"])
            .ServiceName.Should().Be("SfhTest");

    [Fact]
    public void Parse_Purge_ImpliesRemoveFiles()
    {
        InstallerOptions options = InstallerOptions.Parse(["uninstall", "--purge"]);

        options.Purge.Should().BeTrue();
        options.RemoveFiles.Should().BeTrue(
            "purging config while leaving the binaries in place is never what is wanted");
    }

    [Fact]
    public void Parse_UnknownOption_ReportsAnError() =>
        InstallerOptions.Parse(["install", "--wat"]).ParseError.Should().Contain("--wat");

    [Fact]
    public void Parse_Flags_AreRecognised()
    {
        InstallerOptions options = InstallerOptions.Parse(
            ["install", "--no-tray-autostart", "--elevated-child"]);

        options.NoTrayAutostart.Should().BeTrue();
        options.ElevatedChild.Should().BeTrue();
    }
}
