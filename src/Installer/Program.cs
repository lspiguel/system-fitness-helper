using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.ServiceProcess;

const string ServiceName = "SystemFitnessHelper";
const string ServiceDisplayName = "System Fitness Helper";
const string ServiceDescription = "Monitors and manages processes and services based on configurable rules.";

string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
string programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

string installDir = Path.Combine(programFiles, "SystemFitnessHelper", "Service");
string configDir = Path.Combine(programData, "SystemFitnessHelper");
string configPath = Path.Combine(configDir, "rules.json");
string serviceExe = Path.Combine(installDir, "SystemFitnessHelper.Service.exe");

if (args.Length == 0)
{
    PrintUsage();
    return 1;
}

return args[0].ToLowerInvariant() switch
{
    "install" => await InstallAsync(),
    "start" => StartService(),
    "stop" => StopService(),
    "uninstall" => await UninstallAsync(args.Contains("--remove-files")),
    "status" => PrintStatus(),
    _ => PrintUnknown(args[0]),
};

void PrintUsage()
{
    Console.WriteLine("System Fitness Helper Installer");
    Console.WriteLine();
    Console.WriteLine("Usage: sfhi <command> [options]");
    Console.WriteLine();
    Console.WriteLine("Commands:");
    Console.WriteLine("  install         Install and register the service");
    Console.WriteLine("  start           Start the service");
    Console.WriteLine("  stop            Stop the service");
    Console.WriteLine("  uninstall       Stop and remove the service");
    Console.WriteLine("    --remove-files  Also delete installed binaries");
    Console.WriteLine("  status          Print service status and config path");
}

int PrintUnknown(string cmd)
{
    Console.Error.WriteLine($"Unknown command: {cmd}");
    PrintUsage();
    return 1;
}

async Task<int> InstallAsync()
{
    if (!EnsureElevated())
        return 1;

    Console.WriteLine($"Installing service to: {installDir}");
    string sourceDir = AppContext.BaseDirectory;

    try
    {
        Directory.CreateDirectory(installDir);
        foreach (string file in Directory.GetFiles(sourceDir, "*", SearchOption.TopDirectoryOnly))
        {
            if (Path.GetFileName(file).StartsWith("sfhi", StringComparison.OrdinalIgnoreCase))
                continue; // Don't copy installer itself
            File.Copy(file, Path.Combine(installDir, Path.GetFileName(file)), overwrite: true);
        }
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Failed to copy files: {ex.Message}");
        return 1;
    }

    Directory.CreateDirectory(configDir);

    if (!File.Exists(configPath))
    {
        const string defaultConfig = """
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
        await File.WriteAllTextAsync(configPath, defaultConfig);
        Console.WriteLine($"Created default config at: {configPath}");
    }
    else
    {
        Console.WriteLine($"Config already exists at: {configPath} (not overwritten)");
    }

    int rc = RunSc($"create {ServiceName} binPath= \"{serviceExe}\" start= auto DisplayName= \"{ServiceDisplayName}\"");
    if (rc != 0)
    {
        Console.Error.WriteLine("sc create failed.");
        return rc;
    }

    RunSc($"description {ServiceName} \"{ServiceDescription}\"");

    Console.WriteLine($"Service '{ServiceName}' installed successfully.");
    Console.WriteLine($"Install path: {installDir}");
    Console.WriteLine($"Config path:  {configPath}");
    Console.WriteLine("Run 'sfhi start' to start the service.");
    return 0;
}

int StartService()
{
    try
    {
        using ServiceController sc = new(ServiceName);
        if (sc.Status == ServiceControllerStatus.Running)
        {
            Console.WriteLine("Service is already running.");
            return 0;
        }

        sc.Start();
        sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));
        Console.WriteLine("Service started.");
        return 0;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Failed to start service: {ex.Message}");
        return 1;
    }
}

int StopService()
{
    try
    {
        using ServiceController sc = new(ServiceName);
        if (sc.Status == ServiceControllerStatus.Stopped)
        {
            Console.WriteLine("Service is already stopped.");
            return 0;
        }

        sc.Stop();
        sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));
        Console.WriteLine("Service stopped.");
        return 0;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Failed to stop service: {ex.Message}");
        return 1;
    }
}

async Task<int> UninstallAsync(bool removeFiles)
{
    if (!EnsureElevated())
        return 1;

    StopService();

    int rc = RunSc($"delete {ServiceName}");
    if (rc != 0)
    {
        Console.Error.WriteLine("sc delete failed.");
        return rc;
    }

    Console.WriteLine($"Service '{ServiceName}' removed.");

    if (removeFiles && Directory.Exists(installDir))
    {
        Directory.Delete(installDir, recursive: true);
        Console.WriteLine($"Deleted install directory: {installDir}");
    }

    return 0;
}

int PrintStatus()
{
    string status;
    try
    {
        using ServiceController sc = new(ServiceName);
        status = sc.Status.ToString();
    }
    catch
    {
        status = "NotInstalled";
    }

    Console.WriteLine($"Service status: {status}");
    Console.WriteLine($"Config path:    {configPath}");
    Console.WriteLine($"Config exists:  {File.Exists(configPath)}");
    return 0;
}

bool EnsureElevated()
{
    using WindowsIdentity identity = WindowsIdentity.GetCurrent();
    WindowsPrincipal principal = new(identity);
    if (principal.IsInRole(WindowsBuiltInRole.Administrator))
        return true;

    // Re-launch elevated
    ProcessStartInfo psi = new()
    {
        FileName = Environment.ProcessPath ?? "sfhi.exe",
        Arguments = string.Join(' ', args),
        Verb = "runas",
        UseShellExecute = true,
    };
    try
    {
        Process.Start(psi);
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Failed to elevate: {ex.Message}");
    }

    return false;
}

int RunSc(string arguments)
{
    ProcessStartInfo psi = new("sc.exe", arguments)
    {
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
    };

    using Process process = Process.Start(psi)!;
    process.WaitForExit();
    if (process.ExitCode != 0)
        Console.Error.WriteLine(process.StandardError.ReadToEnd());
    return process.ExitCode;
}
