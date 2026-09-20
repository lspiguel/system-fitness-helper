using FluentAssertions;
using SystemFitnessHelper.Installer;
using Xunit;

namespace SystemFitnessHelper.Installer.Tests;

public sealed class PathsTests
{
    private const string SourceRoot = @"C:\src\publish";

    private static Paths Create(params string[] args) =>
        Paths.Create(InstallerOptions.Parse(["install", .. args]), SourceRoot);

    [Fact]
    public void Default_InstallsUnderProgramFiles()
    {
        Paths paths = Create();

        paths.InstallRoot.Should().Be(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "SystemFitnessHelper"));
        paths.ServiceName.Should().Be(Paths.DefaultServiceName);
    }

    [Fact]
    public void Prefix_OverridesTheInstallRoot() =>
        Create("--prefix", @"C:\Temp\sfh-test").InstallRoot.Should().Be(@"C:\Temp\sfh-test");

    [Fact]
    public void ServiceName_OverridesTheScmName()
    {
        Paths paths = Create("--service-name", "SfhTest");

        paths.ServiceName.Should().Be("SfhTest");
        paths.UninstallRegistryKey.Should().EndWith("SfhTest");
        paths.RunValueName.Should().Be("SfhTestTray");
    }

    [Fact]
    public void ComponentLayout_MatchesWhatUiLauncherExpects()
    {
        // TrayApp resolves the dashboard as ..\Ui\SystemFitnessHelper.Ui.exe relative to itself,
        // so the two component directories must be siblings under the install root.
        Paths paths = Create("--prefix", @"C:\sfh");

        paths.ServiceDir.Should().Be(@"C:\sfh\Service");
        paths.TrayAppDir.Should().Be(@"C:\sfh\TrayApp");
        paths.UiDir.Should().Be(@"C:\sfh\Ui");

        string resolvedFromTray = Path.GetFullPath(
            Path.Combine(paths.TrayAppDir, "..", "Ui", "SystemFitnessHelper.Ui.exe"));
        resolvedFromTray.Should().Be(paths.UiExe);
    }

    [Fact]
    public void ConfigPath_IsAlwaysUnderProgramData_EvenWithAPrefix()
    {
        // Config lives with the machine, not with the binaries, so the service and a
        // prefix-installed copy agree on where the rules are.
        Paths paths = Create("--prefix", @"C:\Temp\sfh-test");

        paths.ConfigPath.Should().Be(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "SystemFitnessHelper",
            "rules.json"));
        paths.LogDir.Should().EndWith(@"SystemFitnessHelper\logs");
    }

    [Fact]
    public void SourceComponentDir_ResolvesAgainstTheInstallerDirectory() =>
        Create().SourceComponentDir(Paths.ServiceComponent)
            .Should().Be(Path.Combine(SourceRoot, "Service"));

    [Fact]
    public void ExecutablePaths_LiveInsideTheirComponentDirectories()
    {
        Paths paths = Create("--prefix", @"C:\sfh");

        paths.ServiceExe.Should().Be(@"C:\sfh\Service\SystemFitnessHelper.Service.exe");
        paths.TrayAppExe.Should().Be(@"C:\sfh\TrayApp\SystemFitnessHelper.TrayApp.exe");
        paths.InstalledInstallerExe.Should().Be(@"C:\sfh\sfhi.exe");
    }
}
