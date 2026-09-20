namespace SystemFitnessHelper.Installer;

public enum InstallerCommand
{
    Unknown,
    Install,
    Start,
    Stop,
    Uninstall,
    Status,
    Help,
}

/// <summary>
/// Parsed command line. Kept free of I/O so it can be unit tested.
/// </summary>
public sealed class InstallerOptions
{
    public InstallerCommand Command { get; init; } = InstallerCommand.Unknown;

    /// <summary>Raw verb as typed, for error messages.</summary>
    public string RawCommand { get; init; } = string.Empty;

    /// <summary>Install root override; null means %ProgramFiles%\SystemFitnessHelper.</summary>
    public string? Prefix { get; init; }

    /// <summary>SCM service name override; null means SystemFitnessHelper.</summary>
    public string? ServiceName { get; init; }

    /// <summary>Delete installed binaries on uninstall.</summary>
    public bool RemoveFiles { get; init; }

    /// <summary>Also delete %ProgramData%\SystemFitnessHelper (config and logs) on uninstall.</summary>
    public bool Purge { get; init; }

    /// <summary>Skip the machine-wide autostart entry for the tray app.</summary>
    public bool NoTrayAutostart { get; init; }

    /// <summary>Set on the elevated relaunch so the child keeps its console open to be read.</summary>
    public bool ElevatedChild { get; init; }

    public string? ParseError { get; init; }

    public static InstallerOptions Parse(string[] args)
    {
        if (args.Length == 0)
            return new InstallerOptions { Command = InstallerCommand.Help };

        string verb = args[0];
        InstallerCommand command = verb.ToLowerInvariant() switch
        {
            "install" => InstallerCommand.Install,
            "start" => InstallerCommand.Start,
            "stop" => InstallerCommand.Stop,
            "uninstall" => InstallerCommand.Uninstall,
            "status" => InstallerCommand.Status,
            "help" or "--help" or "-h" or "/?" => InstallerCommand.Help,
            _ => InstallerCommand.Unknown,
        };

        string? prefix = null;
        string? serviceName = null;
        bool removeFiles = false;
        bool purge = false;
        bool noTrayAutostart = false;
        bool elevatedChild = false;
        string? error = null;

        for (int i = 1; i < args.Length; i++)
        {
            string arg = args[i];
            switch (arg.ToLowerInvariant())
            {
                case "--remove-files":
                    removeFiles = true;
                    break;
                case "--purge":
                    purge = true;
                    removeFiles = true;
                    break;
                case "--no-tray-autostart":
                    noTrayAutostart = true;
                    break;
                case "--elevated-child":
                    elevatedChild = true;
                    break;
                case "--prefix":
                    if (i + 1 >= args.Length)
                        error ??= "--prefix requires a path.";
                    else
                        prefix = args[++i];
                    break;
                case "--service-name":
                    if (i + 1 >= args.Length)
                        error ??= "--service-name requires a name.";
                    else
                        serviceName = args[++i];
                    break;
                default:
                    error ??= $"Unknown option: {arg}";
                    break;
            }
        }

        return new InstallerOptions
        {
            Command = command,
            RawCommand = verb,
            Prefix = prefix,
            ServiceName = serviceName,
            RemoveFiles = removeFiles,
            Purge = purge,
            NoTrayAutostart = noTrayAutostart,
            ElevatedChild = elevatedChild,
            ParseError = error,
        };
    }
}
