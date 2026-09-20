namespace SystemFitnessHelper.Service;

public sealed class ServiceConfig
{
    /// <summary>
    /// Absolute path to the rules file the service reads and writes.
    /// </summary>
    /// <remarks>
    /// Settable (not <c>init</c>) so that <see cref="Resolve"/> can run as a post-configure step
    /// after the options binder and the <c>SFH_CONFIG_PATH</c> override have had their say.
    /// </remarks>
    public string ConfigPath { get; set; } = DefaultConfigPath;

    public static string DefaultConfigPath { get; } =
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "SystemFitnessHelper",
            "rules.json");

    /// <summary>
    /// Expands environment variables and makes the path absolute.
    /// </summary>
    /// <remarks>
    /// Without this a configured value such as <c>%ProgramData%\SystemFitnessHelper\rules.json</c>
    /// is used verbatim, and because a Windows Service starts in <c>C:\Windows\System32</c> the
    /// service would create and write a directory literally named <c>%ProgramData%</c> there.
    /// </remarks>
    public void Resolve()
    {
        string path = Environment.ExpandEnvironmentVariables(this.ConfigPath);

        if (string.IsNullOrWhiteSpace(path))
            path = DefaultConfigPath;

        this.ConfigPath = Path.GetFullPath(path);
    }
}
