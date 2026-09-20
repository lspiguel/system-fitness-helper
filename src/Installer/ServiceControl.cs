using System.Diagnostics;
using System.ServiceProcess;
using System.Text;

namespace SystemFitnessHelper.Installer;

/// <summary>
/// SCM operations, driven through sc.exe.
/// </summary>
public sealed class ServiceControl
{
    private static readonly TimeSpan StatusTimeout = TimeSpan.FromSeconds(30);

    private readonly string _serviceName;

    public ServiceControl(string serviceName) => this._serviceName = serviceName;

    public bool Exists()
    {
        try
        {
            using ServiceController sc = new(this._serviceName);
            _ = sc.Status;
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    public ServiceControllerStatus? TryGetStatus()
    {
        try
        {
            using ServiceController sc = new(this._serviceName);
            return sc.Status;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>Reads the registered ImagePath so callers can verify it is quoted.</summary>
    public string? GetImagePath()
    {
        using Microsoft.Win32.RegistryKey? key = Microsoft.Win32.Registry.LocalMachine
            .OpenSubKey($@"SYSTEM\CurrentControlSet\Services\{this._serviceName}");
        return key?.GetValue("ImagePath") as string;
    }

    public bool Start()
    {
        try
        {
            using ServiceController sc = new(this._serviceName);
            if (sc.Status == ServiceControllerStatus.Running)
            {
                Console.WriteLine("Service is already running.");
                return true;
            }

            sc.Start();
            sc.WaitForStatus(ServiceControllerStatus.Running, StatusTimeout);
            Console.WriteLine("Service started.");
            return true;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to start service: {ex.Message}");
            Console.Error.WriteLine(
                "Check the log at %ProgramData%\\SystemFitnessHelper\\logs for a startup exception.");
            return false;
        }
    }

    public bool Stop(bool quiet = false)
    {
        try
        {
            using ServiceController sc = new(this._serviceName);
            if (sc.Status == ServiceControllerStatus.Stopped)
            {
                if (!quiet)
                    Console.WriteLine("Service is already stopped.");
                return true;
            }

            sc.Stop();
            sc.WaitForStatus(ServiceControllerStatus.Stopped, StatusTimeout);
            if (!quiet)
                Console.WriteLine("Service stopped.");
            return true;
        }
        catch (InvalidOperationException) when (quiet)
        {
            return true; // not installed
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to stop service: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Registers the service, or reconfigures it when it already exists.
    /// </summary>
    /// <remarks>
    /// The binPath is double-quoted inside the sc.exe argument. sc.exe stores the value verbatim
    /// in the registry, so passing it bare leaves an unquoted ImagePath containing spaces — the
    /// classic unquoted-service-path weakness, where Windows would try C:\Program.exe first.
    /// </remarks>
    public bool CreateOrUpdate(string exePath, string displayName, string description)
    {
        string quotedExe = "\"\\\"" + exePath + "\\\"\"";

        if (this.Exists())
        {
            Console.WriteLine($"Service '{this._serviceName}' already exists; updating its configuration.");
            if (!this.RunSc($"config {this._serviceName} binPath= {quotedExe} start= auto obj= LocalSystem"))
                return false;
        }
        else
        {
            if (!this.RunSc(
                $"create {this._serviceName} binPath= {quotedExe} start= auto obj= LocalSystem " +
                $"DisplayName= \"{displayName}\""))
                return false;
        }

        // Non-fatal niceties.
        this.RunSc($"description {this._serviceName} \"{description}\"", warnOnly: true);
        this.RunSc($"failure {this._serviceName} reset= 60 actions= restart/5000/restart/10000//0", warnOnly: true);
        return true;
    }

    /// <summary>
    /// Deletes the service, retrying past error 1072.
    /// </summary>
    /// <remarks>
    /// "Marked for deletion" happens whenever something still holds a handle to the service —
    /// most often an open services.msc. The deletion completes once the handle closes, so a
    /// short retry usually turns a confusing failure into a success.
    /// </remarks>
    public bool Delete()
    {
        const int maxAttempts = 5;

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            if (this.RunSc($"delete {this._serviceName}", warnOnly: attempt < maxAttempts))
                return true;

            if (!this.Exists())
                return true; // deletion landed despite the reported failure

            if (attempt < maxAttempts)
            {
                Console.WriteLine($"  Retrying delete ({attempt}/{maxAttempts - 1})...");
                Thread.Sleep(2000);
            }
        }

        return false;
    }

    /// <summary>
    /// Runs sc.exe, surfacing everything it said.
    /// </summary>
    /// <remarks>
    /// sc.exe reports its failures on stdout, not stderr. A previous version echoed only stderr,
    /// so a failed create printed nothing at all and the user was left with "sc create failed."
    /// Both streams are drained on background threads because reading one to the end after
    /// WaitForExit can deadlock when the other fills its buffer.
    /// </remarks>
    private bool RunSc(string arguments, bool warnOnly = false)
    {
        ProcessStartInfo psi = new("sc.exe", arguments)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        StringBuilder output = new();
        using Process process = new() { StartInfo = psi };
        process.OutputDataReceived += (_, e) => { if (e.Data is not null) output.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) output.AppendLine(e.Data); };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        process.WaitForExit();

        if (process.ExitCode == 0)
            return true;

        string text = output.ToString().Trim();
        string prefix = warnOnly ? "Warning" : "Error";
        Console.Error.WriteLine($"{prefix}: sc.exe {arguments.Split(' ')[0]} failed with exit code {process.ExitCode}.");
        if (text.Length > 0)
            Console.Error.WriteLine(text);

        string? hint = Explain(process.ExitCode);
        if (hint is not null)
            Console.Error.WriteLine($"  {hint}");

        return false;
    }

    private static string? Explain(int exitCode) => exitCode switch
    {
        5 => "Access denied - run this command from an elevated prompt.",
        1060 => "The service is not installed.",
        1072 => "The service is marked for deletion. Close services.msc and try again.",
        1073 => "The service already exists.",
        _ => null,
    };
}
