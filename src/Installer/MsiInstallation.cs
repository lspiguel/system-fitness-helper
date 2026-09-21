using Microsoft.Win32;

namespace SystemFitnessHelper.Installer;

/// <summary>
/// Detects an installation managed by the MSI package.
/// </summary>
/// <remarks>
/// Both installers can deploy the same product, but they track it differently: the MSI keeps its
/// own component state and Apps &amp; Features entry, while <c>sfhi</c> writes the SCM entry and
/// registry keys itself. Letting <c>sfhi install</c> or <c>sfhi uninstall</c> loose on an
/// MSI-managed installation desynchronises the two - the MSI would still believe it owns files
/// and a service that <c>sfhi</c> had replaced or deleted.
/// </remarks>
public static class MsiInstallation
{
    private const string MarkerKey = @"SOFTWARE\SystemFitnessHelper";

    /// <summary>
    /// True when the MSI package installed the product on this machine.
    /// </summary>
    public static bool IsPresent()
    {
        using RegistryKey? key = Registry.LocalMachine.OpenSubKey(MarkerKey);
        return key?.GetValue("InstalledBy") as string == "msi";
    }

    public static string? InstallRoot()
    {
        using RegistryKey? key = Registry.LocalMachine.OpenSubKey(MarkerKey);
        return key?.GetValue("InstallRoot") as string;
    }

    /// <summary>
    /// Refuses a mutating command when the MSI owns this installation.
    /// </summary>
    /// <remarks>
    /// An explicit <c>--service-name</c> means the caller is deliberately working on a separate,
    /// isolated installation, which cannot be the MSI's - it always registers
    /// <c>SystemFitnessHelper</c>. Those are allowed through.
    /// </remarks>
    public static bool BlocksCommand(InstallerOptions options, string verb)
    {
        if (options.ServiceName is not null || !IsPresent())
            return false;

        Console.Error.WriteLine($"This machine has an MSI-managed installation of {Paths.DisplayName}.");
        Console.Error.WriteLine($"  Install root: {InstallRoot() ?? "(unknown)"}");
        Console.Error.WriteLine();
        Console.Error.WriteLine($"Running 'sfhi {verb}' against it would leave Windows Installer's view");
        Console.Error.WriteLine("of the product out of step with what is actually on disk.");
        Console.Error.WriteLine();
        Console.Error.WriteLine(verb == "install"
            ? "Upgrade by running the newer SystemFitnessHelper.msi instead."
            : "Uninstall from Apps & Features, or: msiexec /x SystemFitnessHelper.msi");
        Console.Error.WriteLine();
        Console.Error.WriteLine("'sfhi status', 'start' and 'stop' work normally against an MSI installation.");
        Console.Error.WriteLine("To manage an isolated copy instead, pass --service-name and --prefix.");
        return true;
    }
}
