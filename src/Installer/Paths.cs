namespace SystemFitnessHelper.Installer;

/// <summary>
/// Every path the installer touches, derived from the parsed options. Pure: no I/O, so the
/// layout can be asserted in unit tests.
/// </summary>
public sealed class Paths
{
    public const string DefaultServiceName = "SystemFitnessHelper";
    public const string ProductName = "SystemFitnessHelper";
    public const string DisplayName = "System Fitness Helper";

    /// <summary>Component subdirectory names, shared by the build script and the installer.</summary>
    public const string ServiceComponent = "Service";
    public const string TrayAppComponent = "TrayApp";
    public const string UiComponent = "Ui";

    public const string SampleConfigFileName = "rules.sample.json";

    private Paths(string serviceName, string installRoot, string programData, string sourceRoot)
    {
        this.ServiceName = serviceName;
        this.InstallRoot = installRoot;
        this.ConfigDir = programData;
        this.SourceRoot = sourceRoot;
    }

    public string ServiceName { get; }

    /// <summary>Where the product is installed, e.g. C:\Program Files\SystemFitnessHelper.</summary>
    public string InstallRoot { get; }

    /// <summary>Where the installer reads its payload from (the directory sfhi.exe lives in).</summary>
    public string SourceRoot { get; }

    public string ServiceDir => Path.Combine(this.InstallRoot, ServiceComponent);

    public string TrayAppDir => Path.Combine(this.InstallRoot, TrayAppComponent);

    public string UiDir => Path.Combine(this.InstallRoot, UiComponent);

    public string ServiceExe => Path.Combine(this.ServiceDir, "SystemFitnessHelper.Service.exe");

    public string TrayAppExe => Path.Combine(this.TrayAppDir, "SystemFitnessHelper.TrayApp.exe");

    public string UiExe => Path.Combine(this.UiDir, "SystemFitnessHelper.Ui.exe");

    public string InstalledInstallerExe => Path.Combine(this.InstallRoot, "sfhi.exe");

    public string InstalledSampleConfig => Path.Combine(this.InstallRoot, SampleConfigFileName);

    public string SourceSampleConfig => Path.Combine(this.SourceRoot, SampleConfigFileName);

    /// <summary>C:\ProgramData\SystemFitnessHelper.</summary>
    public string ConfigDir { get; }

    public string ConfigPath => Path.Combine(this.ConfigDir, "rules.json");

    public string LogDir => Path.Combine(this.ConfigDir, "logs");

    public string StartMenuDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu),
        "Programs",
        DisplayName);

    public string UninstallRegistryKey =>
        $@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{this.ServiceName}";

    /// <summary>Value name under HKLM\...\CurrentVersion\Run for the tray app.</summary>
    public string RunValueName => this.ServiceName + "Tray";

    /// <summary>Source directory for one component within the publish layout.</summary>
    public string SourceComponentDir(string component) => Path.Combine(this.SourceRoot, component);

    public static Paths Create(InstallerOptions options, string sourceRoot)
    {
        string serviceName = string.IsNullOrWhiteSpace(options.ServiceName)
            ? DefaultServiceName
            : options.ServiceName;

        string installRoot = string.IsNullOrWhiteSpace(options.Prefix)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                ProductName)
            : Path.GetFullPath(options.Prefix);

        string configDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            ProductName);

        return new Paths(serviceName, installRoot, configDir, Path.GetFullPath(sourceRoot));
    }
}
