using System.Reflection;
using System.ServiceProcess;
using System.Text.Json;
using SystemFitnessHelper.Installer;
using SystemFitnessHelper.Ipc.Messages;
using SystemFitnessHelper.Ipc.Pipes;

InstallerOptions options = InstallerOptions.Parse(args);

if (options.ParseError is not null)
{
    Console.Error.WriteLine(options.ParseError);
    Usage.Print();
    Elevation.PauseIfElevatedChild(options);
    return 1;
}

Paths paths = Paths.Create(options, AppContext.BaseDirectory);
ServiceControl service = new(paths.ServiceName);

int exitCode;
try
{
    exitCode = options.Command switch
    {
        InstallerCommand.Install => await Commands.InstallAsync(options, paths, service, args),
        InstallerCommand.Start => Commands.Start(service),
        InstallerCommand.Stop => Commands.Stop(service),
        InstallerCommand.Uninstall => Commands.Uninstall(options, paths, service, args),
        InstallerCommand.Status => await Commands.StatusAsync(paths, service),
        InstallerCommand.Help => Usage.Print(),
        _ => Usage.Unknown(options.RawCommand),
    };
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Unexpected failure: {ex.Message}");
    exitCode = 1;
}

Elevation.PauseIfElevatedChild(options);
return exitCode;

internal static class Usage
{
    public static int Print()
    {
        Console.WriteLine("System Fitness Helper Installer");
        Console.WriteLine();
        Console.WriteLine("Usage: sfhi <command> [options]");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  install      Install the service, tray app and dashboard, then register the service");
        Console.WriteLine("  start        Start the service");
        Console.WriteLine("  stop         Stop the service");
        Console.WriteLine("  uninstall    Stop and remove the service and its shortcuts");
        Console.WriteLine("  status       Print service status, paths and a live health probe");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --prefix <path>        Install root (default: %ProgramFiles%\\SystemFitnessHelper)");
        Console.WriteLine("  --service-name <name>  SCM service name (default: SystemFitnessHelper)");
        Console.WriteLine("  --no-tray-autostart    Do not start the tray app at sign-in (install)");
        Console.WriteLine("  --remove-files         Delete installed binaries (uninstall)");
        Console.WriteLine("  --purge                Also delete config and logs in %ProgramData% (uninstall)");
        return 0;
    }

    public static int Unknown(string command)
    {
        Console.Error.WriteLine($"Unknown command: {command}");
        Print();
        return 1;
    }
}

internal static class Commands
{
    private const string ServiceDescription =
        "Monitors and manages processes and services based on configurable rules.";

    private static string Version =>
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";

    public static async Task<int> InstallAsync(
        InstallerOptions options, Paths paths, ServiceControl service, string[] args)
    {
        if (!Elevation.IsElevated())
            return Elevation.RelaunchElevated(args);

        // Validate before touching anything, so a wrong working directory cannot half-install.
        if (!Layout.ValidateSource(paths, out string? layoutError))
        {
            Console.Error.WriteLine(layoutError);
            return 1;
        }

        Console.WriteLine($"Installing System Fitness Helper to: {paths.InstallRoot}");

        // A running service holds its binaries open; stop before copying over them.
        if (service.Exists() && service.TryGetStatus() != ServiceControllerStatus.Stopped)
        {
            Console.WriteLine("Stopping the running service before upgrading...");
            if (!service.Stop(quiet: true))
            {
                Console.Error.WriteLine("Could not stop the existing service; aborting.");
                return 1;
            }
        }

        try
        {
            // Copies the components, the installer and its dependencies in one pass.
            Layout.CopyPayload(paths);
            Console.WriteLine($"  Copied {string.Join(", ", Layout.Components)} and the installer");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to copy files: {ex.Message}");
            return 1;
        }

        Directory.CreateDirectory(paths.ConfigDir);
        Directory.CreateDirectory(paths.LogDir);
        SeedConfig(paths);

        if (!service.CreateOrUpdate(paths.ServiceExe, Paths.DisplayName, ServiceDescription))
            return 1;

        Shortcuts.CreateStartMenuShortcuts(paths);

        if (options.NoTrayAutostart)
            Shortcuts.DisableTrayAutostart(paths);
        else
            Shortcuts.EnableTrayAutostart(paths);

        Shortcuts.RegisterUninstallEntry(paths, Version);

        Console.WriteLine();
        Console.WriteLine($"Service '{paths.ServiceName}' installed successfully.");
        Console.WriteLine($"  Install path: {paths.InstallRoot}");
        Console.WriteLine($"  Config path:  {paths.ConfigPath}");
        Console.WriteLine($"  Dashboard:    {paths.UiExe}");
        Console.WriteLine();
        Console.WriteLine("Next: sfhi start");
        return 0;
    }

    public static int Start(ServiceControl service) => service.Start() ? 0 : 1;

    public static int Stop(ServiceControl service) => service.Stop() ? 0 : 1;

    public static int Uninstall(
        InstallerOptions options, Paths paths, ServiceControl service, string[] args)
    {
        if (!Elevation.IsElevated())
            return Elevation.RelaunchElevated(args);

        if (!service.Exists())
        {
            Console.WriteLine($"Service '{paths.ServiceName}' is not installed; cleaning up anything left behind.");
        }
        else
        {
            service.Stop(quiet: true);
            if (!service.Delete())
                return 1;

            Console.WriteLine($"Service '{paths.ServiceName}' removed.");
        }

        Shortcuts.RemoveStartMenuShortcuts(paths);
        Shortcuts.DisableTrayAutostart(paths);
        Shortcuts.RemoveUninstallEntry(paths);

        if (options.RemoveFiles && Directory.Exists(paths.InstallRoot))
        {
            try
            {
                Directory.Delete(paths.InstallRoot, recursive: true);
                Console.WriteLine($"Deleted install directory: {paths.InstallRoot}");
            }
            catch (Exception ex)
            {
                // The running sfhi.exe may be the copy inside InstallRoot.
                Console.Error.WriteLine($"Warning: could not fully delete {paths.InstallRoot}: {ex.Message}");
            }
        }

        if (options.Purge && Directory.Exists(paths.ConfigDir))
        {
            try
            {
                Directory.Delete(paths.ConfigDir, recursive: true);
                Console.WriteLine($"Deleted configuration and logs: {paths.ConfigDir}");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Warning: could not delete {paths.ConfigDir}: {ex.Message}");
            }
        }
        else if (Directory.Exists(paths.ConfigDir))
        {
            Console.WriteLine($"Kept configuration and logs: {paths.ConfigDir} (use --purge to remove)");
        }

        return 0;
    }

    public static async Task<int> StatusAsync(Paths paths, ServiceControl service)
    {
        ServiceControllerStatus? status = service.TryGetStatus();

        Console.WriteLine($"Service name:   {paths.ServiceName}");
        Console.WriteLine($"Service status: {status?.ToString() ?? "NotInstalled"}");
        Console.WriteLine($"Image path:     {service.GetImagePath() ?? "(not registered)"}");
        Console.WriteLine($"Install root:   {paths.InstallRoot} (exists: {Directory.Exists(paths.InstallRoot)})");
        Console.WriteLine($"Config path:    {paths.ConfigPath} (exists: {File.Exists(paths.ConfigPath)})");
        Console.WriteLine($"Log directory:  {paths.LogDir}");

        if (status != ServiceControllerStatus.Running)
            return 0;

        // Ask the service itself, so a config path mismatch shows up here rather than in the log.
        try
        {
            CommandPipeClient client = new();
            PingResult ping = await client.SendAsync<PingResult>(
                Methods.Ping, null, default, TimeSpan.FromSeconds(5));

            Console.WriteLine();
            Console.WriteLine("Health probe (sfh.ping):");
            Console.WriteLine($"  Responding:  {ping.Ok}");
            Console.WriteLine($"  Version:     {ping.Version}");
            Console.WriteLine($"  Rules file:  {ping.ConfigPath} (exists: {ping.ConfigExists})");

            if (!string.Equals(
                    Path.GetFullPath(ping.ConfigPath),
                    Path.GetFullPath(paths.ConfigPath),
                    StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("  WARNING: the service is using a different rules file than the installer expects.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine();
            Console.Error.WriteLine($"Health probe failed: {ex.Message}");
            return 1;
        }

        return 0;
    }

    /// <summary>
    /// Writes the initial rules file, never overwriting an existing one.
    /// </summary>
    private static void SeedConfig(Paths paths)
    {
        if (File.Exists(paths.ConfigPath))
        {
            Console.WriteLine($"Config already exists at: {paths.ConfigPath} (not overwritten)");
            return;
        }

        string content = File.Exists(paths.InstalledSampleConfig)
            ? DisableAllRules(File.ReadAllText(paths.InstalledSampleConfig))
            : """
              {
                "ruleSets": {
                  "default": {
                    "isDefault": true,
                    "rules": [],
                    "protected": []
                  }
                }
              }
              """;

        File.WriteAllText(paths.ConfigPath, content);
        Console.WriteLine($"Created default config at: {paths.ConfigPath}");
    }

    /// <summary>
    /// Forces every seeded rule off, so a fresh install never stops anything until the user has
    /// reviewed the rules and enabled them deliberately.
    /// </summary>
    private static string DisableAllRules(string json)
    {
        try
        {
            using JsonDocument doc = JsonDocument.Parse(json);
            JsonSerializerOptions options = new() { WriteIndented = true };

            using MemoryStream stream = new();
            using (Utf8JsonWriter writer = new(stream, new JsonWriterOptions { Indented = true }))
            {
                WriteDisabled(doc.RootElement, writer, insideRule: false);
            }

            return System.Text.Encoding.UTF8.GetString(stream.ToArray());
        }
        catch (JsonException)
        {
            return json; // Malformed sample; copy it through and let validation report it.
        }
    }

    private static void WriteDisabled(JsonElement element, Utf8JsonWriter writer, bool insideRule)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                bool isRule = element.TryGetProperty("id", out _) && element.TryGetProperty("action", out _);
                foreach (JsonProperty prop in element.EnumerateObject())
                {
                    if (isRule && prop.NameEquals("enabled"))
                    {
                        writer.WriteBoolean("enabled", false);
                        continue;
                    }

                    writer.WritePropertyName(prop.Name);
                    WriteDisabled(prop.Value, writer, isRule);
                }

                writer.WriteEndObject();
                break;

            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (JsonElement item in element.EnumerateArray())
                    WriteDisabled(item, writer, insideRule);
                writer.WriteEndArray();
                break;

            default:
                element.WriteTo(writer);
                break;
        }
    }
}
